using System.Collections.Generic;                     // Списки
using UnityEngine;                                    // MonoBehaviour, Transform, Sprite
using UnityEngine.UI;                                 // Image
using TMPro;                                          // TextMeshProUGUI

// Привязчик ассета EnemySO к UI врага внутри Fighting_block
public class EnemyView : MonoBehaviour
{
    [Header("UI refs")]                                // Ссылки на элементы EnemyPrefab
    public TextMeshProUGUI nameText;                   // Name
    public Image pictureImage;                         // Picture
    public TextMeshProUGUI attackText;                 // Strange
    public TextMeshProUGUI defenseText;                // Defense
    public Transform lifeContainer;                    // Life (контейнер сердец)
    public Image heartPrefab;                          // Hearth (один элемент)
    public Sprite heartFull;                           // Спрайт «жизнь есть»
    public Sprite heartLost;                           // Спрайт «жизнь потеряна»
    public Transform resourcesContainer;               // Resourses (контейнер для res_1)
    public RewardItemUI resItemPrefab;                 // res_1 (UI награды)
    public Button traitsButton;                        // Traits (кнопка «свойства»)
    public GameObject traitsPanel;                     // Panel (описание свойств) — сворачивается/разворачивается
    public TextMeshProUGUI descriptionText;            // Description (текст свойств)
    public GameObject deadOverlay;                     // Картинка «dead» поверх врага (по умолчанию выключена)

    [Header("Loot visuals")]
    [Range(0.3f, 1f)] public float lootItemScale = 0.7f;   // По ТЗ — 0.7

    [Header("Runtime")]
    public EnemySO data;                               // Ассет врага
    public int currentHP;                              // Текущее ХП в бою

    [Header("Tags (icons)")]
    public Transform tagsIconContainer; // Контейнер для иконок тегов (например, "Tags")
    public Image tagIconPrefab;         // Префаб маленькой иконки (Image) для одного тега

    private void Awake()                                            // На инициализации компонента
    {
        EnsureLocalRefs();                                          // Приводим ссылки к сценовым
    }

    public void Bind(EnemySO so)                       // Привязываем ассет врага
    {
        data = so;                                     // Сохраняем ссылку
        currentHP = (data != null) ? data.maxHP : 0;   // Обнуляем HP на старт
        // Имя / числа
        if (nameText) nameText.text = data ? data.displayName : "";
        if (attackText) attackText.text = data ? data.attack.ToString() : "1";
        if (defenseText) defenseText.text = data ? data.armor.ToString() : "0";
        // Спрайт
        if (pictureImage) pictureImage.sprite = data ? data.sprite : null;

        EnsureLocalRefs();

        // Сердечки
        RebuildHearts();                               // Показать maxHP штук
        // Награды-ресурсы
        RebuildLoot();                                 // Спавним res_1 для каждого Entry
        // Traits
        RebuildTagIcons();
        if (traitsButton)                              // Кнопка видна только если есть свойства
            traitsButton.gameObject.SetActive(data != null && data.traits != null && data.traits.Count > 0);
        if (traitsPanel) traitsPanel.SetActive(false); // Панель свёрнута
        if (deadOverlay) deadOverlay.SetActive(false); // «Dead» скрыт
    }

    public void ToggleTraitsPanel()
    {
        if (!traitsPanel) return;

        // Переключаем видимость панели
        traitsPanel.SetActive(!traitsPanel.activeSelf);

        // Если панель открыта и есть куда писать — собираем текст
        if (traitsPanel.activeSelf && descriptionText)
        {
            var sb = new System.Text.StringBuilder();

            // 1) TRAITS (EffectDef) — просто выводим имена (displayName необязателен)
            if (data != null && data.traits != null && data.traits.Count > 0)
            {
                for (int i = 0; i < data.traits.Count; i++)
                {
                    var tr = data.traits[i];
                    if (!tr) continue;
                    sb.AppendLine(tr.name); // коротко и без рефлексии
                }
            }

            // 2) TAGS (TagDef) — выводим только те, где есть осмысленное описание
            if (data != null && data.tags != null && data.tags.Count > 0)
            {
                for (int i = 0; i < data.tags.Count; i++)
                {
                    var tag = data.tags[i];
                    if (!tag) continue;

                    // Показываем «Имя: Описание», если описание заполнено
                    if (!string.IsNullOrWhiteSpace(tag.description))
                    {
                        // Имя тега можно взять из id (если оно у тебя «человечное») либо из имени ассета
                        string tagTitle = !string.IsNullOrEmpty(tag.id) ? tag.id : tag.name;
                        sb.AppendLine($"{tagTitle}: {tag.description}");
                    }
                }
            }

            // Применяем собранный текст к полю описания
            descriptionText.text = sb.ToString();
        }
    }

    // Маленький рефлекшн-хелпер, чтобы не зависеть от точных имён полей SO
    private static string TryGetString(ScriptableObject so, string field)
    {
        if (!so) return null;
        var f = so.GetType().GetField(field);
        if (f != null && f.FieldType == typeof(string))
            return (string)f.GetValue(so);
        var p = so.GetType().GetProperty(field);
        if (p != null && p.PropertyType == typeof(string))
            return (string)p.GetValue(so, null);
        return null;
    }

    public void RebuildHearts()                        // Пересобираем визуал жизней
    {
        if (!lifeContainer || !heartPrefab) return;    // Без ссылок — нельзя
        if (!IsSceneObject(lifeContainer)) return;
        // Чистим детей
        for (int i = lifeContainer.childCount - 1; i >= 0; i--)
            Destroy(lifeContainer.GetChild(i).gameObject);
        // Строим maxHP сердец
        int max = (data != null) ? data.maxHP : 0;     // Сколько штук
        for (int i = 0; i < max; i++)                  // Цикл по сердцам
        {
            var img = Instantiate(heartPrefab, lifeContainer); // Клонируем иконку сердца
            // Установим состояние: первые currentHP — полные, остальные — потеряны
            bool alive = (i < currentHP);             // Жизнь есть?
            //Debug.Log(currentHP);
            img.sprite = alive ? heartFull : heartLost; // Выбор спрайта
            var rt = img.rectTransform;                        // Берём RectTransform
            rt.localScale = Vector3.one;                       // Масштаб по умолчанию = 1
        }
        RecollectHeartImages();                                // Перечитать ссылки на иконки сердец
        StopDamagePreview(false);                              // На всякий случай гасим мигание (без полного rebuild, он уже сделан)
    }

    public void SetAllHeartsLostVisual()               // Показываем «все жизни потеряны» (для анимации смерти)
    {
        if (!lifeContainer) return;                    // Защита
        for (int i = 0; i < lifeContainer.childCount; i++)
        {
            var img = lifeContainer.GetChild(i).GetComponent<Image>(); // Берём Image сердца
            if (img) img.sprite = heartLost;           // Ставим потерянный спрайт
        }
    }

    public void ShowDeadOverlay(bool on)               // Вкл/выкл картинку «dead»
    {
        if (deadOverlay) deadOverlay.SetActive(on);    // Меняем активность
    }

    private bool IsSceneObject(Transform t)                         // Хелпер «это сценовый объект?»
    {
        return t != null && t.gameObject.scene.IsValid();           // true, если ссылка валидна и в сцене
    }

    // Найти дочерний узел по относительному пути у текущего инстанса
    private Transform FindLocalChild(string path)                   // Хелпер «найти ребёнка»
    {
        var tr = transform.Find(path);                              // Ищем по пути относительно EnemyView
        return tr;                                                  // Вернём (может быть null — это ок)
    }

    // Привести ссылки lifeContainer/resourcesContainer к сценовым
    private void EnsureLocalRefs()                                  // Основная «починка» ссылок
    {
        // Если контейнер жизней не назначен или назначен на prefab asset — найдём локальный «Life»
        if (!IsSceneObject(lifeContainer))                          // Проверяем сценовость
            lifeContainer = FindLocalChild("Life");                 // Ищем дочерний объект «Life»

        // Если контейнер ресурсов не назначен или назначен на prefab asset — найдём локальный «Resourses»
        if (!IsSceneObject(resourcesContainer))                     // Аналогично для зоны лута
            resourcesContainer = FindLocalChild("Resourses");       // Ищем дочерний объект «Resourses»
    }

    private void RebuildLoot()                         // Построить список наград (res_1)
    {
        if (!resourcesContainer || !resItemPrefab) return; // Проверка
        if (!IsSceneObject(resourcesContainer)) return;
        // Чистим детей
        for (int i = resourcesContainer.childCount - 1; i >= 0; i--)
            Destroy(resourcesContainer.GetChild(i).gameObject);
        // Создаём по одному RewardItemUI на LootEntry
        if (data == null || data.loot == null) return; // Если нет — выходим
        foreach (var le in data.loot)                  // Перебор лута
        {
            if (le == null || le.resource == null) continue; // Пропуск
            var ui = Instantiate(resItemPrefab, resourcesContainer); // Клонируем префаб
            var uiRT = ui.transform as RectTransform;               // Берём RectTransform инстанса
            if (uiRT)                                               // Если он есть (в UI всегда есть)
                uiRT.localScale = new Vector3(lootItemScale,        // Устанавливаем масштаб по X
                                              lootItemScale,        // и по Y
                                              1f);                  // Z = 1 (для UI не важен)
            var r = new EventSO.Reward                    // Создаём временный Reward под UI
            {
                type = EventSO.RewardType.Resource,       // Тип: ресурс
                resource = le.resource,                    // Сам ресурс
                amount = Mathf.Max(1, le.amount),         // Сколько
                gatedByAdditional = false                  // Без гейта
            };
            ui.Bind(r);                                    // Привязка данных
            ui.SetGateState(true);                         // Рамка «ок»
        }
    }

    // --- ADDED: превью урона по сердцам (мигание) ---
    // Скорость мигания «кандидатов на потерю»
    [Header("Hearts blink preview")]                       // Группа настроек
    public float heartBlinkInterval = 0.4f;               // Интервал тумблера (сек)
    [Range(1.0f, 1.2f)]
    public float heartBlinkScale = 1.5f;                      // Во сколько раз «вздутие» сердец при мигании

    // Рантайм-состояние мигания
    private Coroutine _blinkCo;                            // Идущая корутина мигания (если есть)
    private readonly List<Image> _heartImages = new();     // Кэш ссылок на инстансы «сердечек» (по индексам Life)
    private int _lastPreviewDamage = 0;                    // Запомненный размер последнего превью (чтобы лишний раз не перезапускать)
    private int _previewStart = -1;                            // Начальный индекс диапазона «мигающих»
    private int _previewEnd = -1;                            // Конечный индекс диапазона «мигающих»

    // Пересобрать кэш «сердечек» из lifeContainer
    private void RecollectHeartImages()                    // Обновляем список _heartImages после RebuildHearts
    {
        _heartImages.Clear();                              // Чистим старые ссылки
        if (!lifeContainer) return;                        // Если контейнер не назначен — выходим
        for (int i = 0; i < lifeContainer.childCount; i++) // Идём по детям
        {
            var img = lifeContainer.GetChild(i)            // Берём ребёнка по индексу
                                  .GetComponent<Image>();  // Пытаемся взять Image
            if (img) _heartImages.Add(img);                // Если есть — складываем в кэш
        }
    }
    // Остановить превью-мигание (опционально сразу восстановить обычный вид сердец)
    public void StopDamagePreview(bool rebuildVisual = false)
    {
        if (_blinkCo != null)                              // Если корутина мигания существует
        {
            StopCoroutine(_blinkCo);                       // Останавливаем
            _blinkCo = null;                               // Сбрасываем ссылку
        }
        RestoreLastPreviewRangeToBase();                       // Полные спрайты + scale=1 в прежнем диапазоне
        _lastPreviewDamage = 0;                            // Сбрасываем сохранённый «размер превью»
        if (rebuildVisual) RebuildHearts();                // При необходимости — полностью перерисовать сердца
    }
    // Запустить превью урона: заставить мигать «пострадавшие» сердечки
    public void PreviewIncomingDamage(int damage)
    {
        if (damage <= 0 || currentHP <= 0)                 // Нет урона или враг уже мёртв
        {
            StopDamagePreview(true);                       // Гасим мигание и восстанавливаем обычный вид
            return;                                        // Выходим
        }
        if (_blinkCo != null && _lastPreviewDamage == damage) // Если уже мигаем с тем же значением
            return;                                        // Ничего не меняем

        StopDamagePreview(false);                          // На всякий — остановим предыдущее мигание (без rebuild)
        _blinkCo = StartCoroutine(BlinkHeartsRoutine(damage)); // Запускаем новую корутину мигания
        _lastPreviewDamage = damage;                       // Запомним «сколько урона визуализируем»
    }
    // Собственно «мигалка» по серцам, которые будут потеряны при таком уроне
    private System.Collections.IEnumerator BlinkHeartsRoutine(int damage)
    {
        // Индексы сердец, которые «потеряем»: от (currentHP - damage) включительно до (currentHP - 1)
        int start = Mathf.Max(0, currentHP - damage);      // Первое пострадавшее сердце (не меньше 0)
        int end = Mathf.Max(0, currentHP) - 1;           // Последнее живое сейчас сердце

        if (start > end)                                   // Если диапазон пуст
        {
            yield break;                                   // Нечего мигать — выходим
        }

        // Убедимся, что у нас есть актуальные ссылки на инстансы сердец
        RecollectHeartImages();                            // Пересобрать _heartImages после последнего RebuildHearts
        if (_heartImages.Count == 0) yield break;          // На всякий — если нет картинок, выходим

        _previewStart = start;                                 // Сохраняем начало диапазона
        _previewEnd = end;                                   // Сохраняем конец диапазона

        // Сразу зафиксируем пустой спрайт у всех «кандидатов на потерю» (чтобы не мигать спрайтом)
        for (int i = start; i <= end; i++)                             // Для каждого индекса в диапазоне
        {
            if (i < 0 || i >= _heartImages.Count) continue;            // Защита от выхода за границы
            var img = _heartImages[i];                                 // Берём Image
            if (!img) continue;                                        // Может быть уничтожен
            img.sprite = heartLost;                                    // Всегда «пустое» сердце на превью
            img.rectTransform.localScale = Vector3.one;                // Стартовый масштаб = 1
        }

        bool grow = true;                                              // Фаза пульса: «расти» / «сжиматься»
        var wait = new WaitForSeconds(heartBlinkInterval);             // Интервал переключения фазы

        while (true)                                                   // Бесконечный пульс, пока превью активно
        {
            float scale = grow ? heartBlinkScale : 1f;                 // Вычислим текущий масштаб (пульс)
            for (int i = start; i <= end; i++)                         // Применим ко всем «кандидатам»
            {
                if (i < 0 || i >= _heartImages.Count) continue;        // Защита
                var img = _heartImages[i];                             // Берём Image
                if (!img) continue;                                    // Может быть уничтожен
                img.rectTransform.localScale = new Vector3(scale, scale, 1f); // Применим масштаб по X/Y
            }
            grow = !grow;                                              // Смена фазы
            yield return wait;                                         // Пауза до следующей фазы
        }
    }

    private void RebuildTagIcons()
    {
        if (!tagsIconContainer || !tagIconPrefab) return;
        // очистка
        for (int i = tagsIconContainer.childCount - 1; i >= 0; i--)
            Destroy(tagsIconContainer.GetChild(i).gameObject);

        if (data == null || data.tags == null) return;

        for (int i = 0; i < data.tags.Count; i++)
        {
            var tag = data.tags[i];
            if (!tag || !tag.uiIcon) continue;
            var img = Instantiate(tagIconPrefab, tagsIconContainer);
            img.sprite = tag.uiIcon;      // собственно иконка тега
            var tt = img.GetComponent<TooltipTrigger>();
            if (!tt) tt = img.gameObject.AddComponent<TooltipTrigger>(); // повесим компонент, если его нет
            tt.tagDef = tag;                 // текст возьмём из tag.description
            tt.customText = null;            // лишний текст не нужен — приоритет у tagDef.description
            tt.delay = 0.45f;                // (по желанию) задержка перед показом
            img.preserveAspect = true;
            img.rectTransform.localScale = Vector3.one;
        }
    }

    private void RestoreLastPreviewRangeToBase()               // Вернуть сердцам обычный вид
    {
        if (_previewStart < 0 || _previewEnd < 0) return;      // Если диапазона нет — выходим
        if (_heartImages.Count == 0) return;                    // Нет картинок — нечего делать
        for (int i = _previewStart; i <= _previewEnd; i++)      // Идём по диапазону
        {
            if (i < 0 || i >= _heartImages.Count) continue;     // Защита от выхода за границы
            var img = _heartImages[i];                          // Берём Image
            if (!img) continue;                                  // Мог быть уничтожен
                                                                 // Если это «живые» сердца (по currentHP), базовый спрайт — full, иначе lost
            img.sprite = (i < currentHP) ? heartFull : heartLost; // Восстановить спрайт
            img.rectTransform.localScale = Vector3.one;         // Сбросить пульс масштаба
        }
        _previewStart = _previewEnd = -1;                       // Сброс диапазона
    }

}