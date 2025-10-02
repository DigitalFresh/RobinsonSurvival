// RewardPickupAnimator.cs
using System;                                            // Для Action коллбеков
using System.Collections;                                 // ДЛЯ IEnumerator (исправляет вашу ошибку про IEnumerator<T>)
using System.Collections.Generic;                         // Для List<T>
using TMPro;                                              // На случай, если в res_1 есть TMP-текст
using UnityEngine;                                        // Базовые Unity-типы
using UnityEngine.UI;                                     // Для Image

public class RewardPickupAnimator : MonoBehaviour
{
    public static RewardPickupAnimator Instance;          // Синглтон экземпляр для удобного вызова из UI-окон

    [Header("Canvas & Parents")]
    public Canvas rootCanvas;                             // Ссылка на верхний UI Canvas (Screen Space: Overlay/Camera)
    public RectTransform fxParent;                        // Контейнер для «летящих» иконок (дочерний к rootCanvas)

    [Header("Prefab")]
    public RewardItemUI res1Prefab;                       // Префаб иконки ресурса (ВАШ res_1, содержит RewardItemUI)

    [Header("Stat icons (reward)")]
    public Sprite[] rewardStatSprites = new Sprite[4];    // Индексация: 0=Hunger, 1=Thirst, 2=Energy, 3=Health (как в EventSO.PlayerStat/StatType)

    [Header("Stat icons (penalty)")]
    public Sprite[] penaltyStatSprites = new Sprite[4];   // Индексация та же: 0=Hunger, 1=Thirst, 2=Energy, 3=Health

    // === Настройки анимации «карт» ===
    [Header("Cards draw")]
    public RectTransform deckAnchor;                 // Якорь стопки колоды в UI (отсюда взлетают)
    public RectTransform handRightAnchor;            // Якорь правого края панели руки (сюда садятся)
    public GameObject cardIconPrefab;                // Префаб визуала карты для полёта (укажите UICard.prefab)


    // === Пул объектов UI ===
    [Header("Pooling")]
    [SerializeField] private int resIconPrewarm = 8;   // предсоздание иконок ресурсов
    [SerializeField] private int cardIconPrewarm = 4;  // предсоздание «иконок карт»
    private readonly Queue<RewardItemUI> _poolResIcons = new Queue<RewardItemUI>(64); // пул ресурсов
    private readonly Queue<UnityEngine.GameObject> _poolCardIcons = new Queue<UnityEngine.GameObject>(16); // пул карт
    [Header("Timing")]
    public float phase1Time = 1.35f;                      // Длительность фазы 1: старт → центр
    public float phase2Time = 1.30f;                      // Длительность фазы 2: центр → правая полка
    public float phase3Time = 1.35f;                      // Длительность фазы 3: правая полка → слот инвентаря

    // Длительность «фазы B» в быстром режиме (центр → слот инвентаря),
    // отдельная от phase2Time, чтобы можно было сделать перелёт более заметным
    public float phase2TimeDirect = 0.45f;         // 0.45–0.60 обычно смотрится хорошо

    public AnimationCurve moveCurve =                     // Кривая перемещения (можно настроить в инспекторе)
        AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve scaleCurve =                    // Кривая масштабирования (можно настроить в инспекторе)
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Anchors")]
    public RectTransform rightSideAnchor;                 // Якорь «правая часть экрана» (fall-back точка)
    public RectTransform playerStatsAnchor;               // Якорь зоны параметров игрока (левый верх, для будущих анимаций)

    [Header("Stagger")]
    public bool useStagger = true;                   // Включать ли наложенный старт (следующая иконка стартует раньше завершения предыдущей)
    [Min(0f)] public float cardsStagger = 0.6f;     // Пауза между стартами ПОЛЁТОВ КАРТ (сек)
    [Min(0f)] public float statsStagger = 0.3f;     // Пауза между стартами ПОЛЁТОВ СТАТОВ (сек)

    [Header("Stagger (resources from enemy)")]          // Группа настроек stagger
    [Range(0.0f, 0.5f)] public float resourceStagger = 0.15f; // 90 мс по умолчанию, выглядит «живенько»
                                                              // --- ADDED END ---

    [Header("Links")]
    public InventoryInMissionUI inventoryUI;              // Ссылка на панель инвентаря в миссии
    // Очередь заспавненных летящих иконок, чтобы точно сопоставлять 1:1 «что спавнили» → «что сажаем в инвентарь»
    private readonly Queue<RectTransform> _flyingQueue = new Queue<RectTransform>(); // FIFO очередь RectTransform

    // Режим: три фазы (как было) или быстрый (без «парковки» у правого края)
    public bool useThreePhase = false;              // false = летим сразу в инвентарь (рекомендуется)

    // Внутренний флаг: ресурсы уже начислены в инвентарь (слоты готовы)
    // нужен для быстрого режима, чтобы вызвать onBeforeInventoryApply ровно один раз
    private bool _inventoryAppliedForThisBatch = false;

    private Camera _uiCam;                                // Кеш: камера Canvas'а (если не Overlay)

    private void Awake()                                  // Инициализация
    {
        if (Instance != null && Instance != this)         // Если уже есть — удалим дубль
        {
            Destroy(gameObject);                          // Уничтожаем этот объект
            return;                                       // Выходим
        }
        Instance = this;                                  // Сохраняем синглтон

        if (!rootCanvas)                                  // Если Canvas не задан из инспектора
            rootCanvas = GetComponentInParent<Canvas>();  // Попробуем найти родительский Canvas

        // Если Canvas не Screen Space Overlay — нужна камера
        _uiCam = (rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? rootCanvas.worldCamera
            : null;                                       // Для Overlay камера null
        PrewarmPools(); // предсоздадим объекты пула PrewarmPools();
    }

    // --- Хелперы пула ---
    private RewardItemUI GetPooledResIcon()
    {
        var ui = _poolResIcons.Count > 0 ? _poolResIcons.Dequeue() : Instantiate(res1Prefab, fxParent);
        if (ui) { ui.transform.SetParent(fxParent, false); ui.gameObject.SetActive(true); }
        return ui;
    }
    private void ReleaseResIcon(RewardItemUI ui)
    {
        if (!ui) return;
        if (ui.gameObject.activeSelf) ui.gameObject.SetActive(false);
        ui.transform.SetParent(fxParent, false);
        _poolResIcons.Enqueue(ui);
    }
    private void ReleaseResIconByRT(RectTransform rt)
    {
        if (!rt) return;
        var ui = rt.GetComponent<RewardItemUI>() ?? rt.GetComponentInParent<RewardItemUI>();
        if (ui) ReleaseResIcon(ui);
    }
    private UnityEngine.GameObject GetPooledCardIcon()
    {
        var go = _poolCardIcons.Count > 0 ? _poolCardIcons.Dequeue() : Instantiate(cardIconPrefab, fxParent);
        if (go) { go.transform.SetParent(fxParent, false); go.SetActive(true); }
        return go;
    }
    private void ReleaseCardIcon(UnityEngine.GameObject go)
    {
        if (!go) return;
        go.SetActive(false);
        go.transform.SetParent(fxParent, false);
        _poolCardIcons.Enqueue(go);
    }
    private void PrewarmPools()
    {
        for (int i = 0; i < resIconPrewarm; i++)
        {
            var ui = GetPooledResIcon();
            ui.gameObject.SetActive(false);
            _poolResIcons.Enqueue(ui);
        }
        for (int i = 0; i < cardIconPrewarm; i++)
        {
            var go = GetPooledCardIcon();
            go.SetActive(false);
            _poolCardIcons.Enqueue(go);
        }
    }




    /// Публичная точка входа: проиграть анимацию для списка ресурсных наград
    public void PlayForRewards(HexTile tile, List<EventSO.Reward> rewards, Action onBeforeInventoryApply, Action onAfterDone)
    {
        // Фильтруем только ресурсы, чтобы не путать с очками/картами
        var batch = new List<EventSO.Reward>();           // Локальный список для полётов
        if (rewards != null)                              // Если список наград не пуст
        {
            foreach (var r in rewards)                    // Перебираем все награды
            {
                if (r != null && r.type == EventSO.RewardType.Resource && r.resource) // Только ресурсные
                    batch.Add(r);                         // Добавляем в очередь полётов
            }
        }

        if (batch.Count == 0)                             // Если нечего анимировать
        {
            onBeforeInventoryApply?.Invoke();             // Всё равно даём шанс применить инвентарь
            onAfterDone?.Invoke();                        // И завершить цепочку
            return;                                       // Выходим
        }

        _inventoryAppliedForThisBatch = false;          // сбрасываем флаг перед стартом партии

        // Блокируем ввод на время «синематика»
        ModalGate.Acquire(this);                          // Подняли «шлагбаум»

        // Стартуем корутину последовательного полёта
        StartCoroutine(PlayQueueRoutine(tile, batch, onBeforeInventoryApply, () =>
        {
            ModalGate.Release(this);                      // Опускаем «шлагбаум» после завершения
            onAfterDone?.Invoke();                        // Вызываем финальный коллбек
        }));
    }

    /// Последовательно проиграть полёты всех ресурсов
    private IEnumerator PlayQueueRoutine(HexTile tile, List<EventSO.Reward> batch, Action onBeforeInventoryApply, Action onAllDone)
    {

        if (!_inventoryAppliedForThisBatch)                     // Если ещё не применяли начисления
        {
            onBeforeInventoryApply?.Invoke();                   // Начисляем ресурсы (UI создаст слоты — новые скрыты)
            _inventoryAppliedForThisBatch = true;               // Больше не повторяем
        }

        for (int i = 0; i < batch.Count; i++)                   // Поочерёдно для каждой награды
        {
            var r = batch[i];                                   // Текущая награда
            // Получаем якорь — видимый или скрытый слот (если новый — он скрыт, но уже есть)
            var targetAnchor = inventoryUI                       // Если UI задан
                ? inventoryUI.GetSlotAnchorForResource(r.resource) // Вернём RectTransform слота/плейсхолдера
                : null;                                          // Иначе якоря нет

            // Прямой полёт: гекс → центр → (якори слота)
            yield return PlaySingleResourceDirectToInventory(tile, r.resource.icon, r.resource, targetAnchor); // Летим

            // Раскрываем слот, если он был скрытым (только для новых ресурсов)
            inventoryUI?.RevealHiddenSlot(r.resource);          // Делает CanvasGroup.alpha=1 и переводит в видимые

            // Лёгкий пинг для акцента (и для существующих, и для новых)
            var anchorAfter = inventoryUI?.GetSlotAnchorForResource(r.resource); // Повторно берём якорь (уже видимый)
            if (anchorAfter) inventoryUI.PingSlot(anchorAfter);  // Подсветка
        }

        onAllDone?.Invoke();                                     // Завершили партию
        yield break;                                             // Выходим
    }

    /// Внутренний твинар: позиция + масштаб
    private IEnumerator Tween(RectTransform rt, Vector2 from, Vector2 to, float sFrom, float sTo, float time)
    {

        // Если иконку уже уничтожили — сразу прерываем твин
        if (!rt) yield break;                                  // Проверка «юнити-нуля» (MissingReference безопасно)
                                                               // --- ADDED END ---

        float t = 0f;                                     // Временная переменная
        while (t < time)                                  // Пока не истекла длительность
        {
            if (!rt) yield break;                              // На каждом кадре убеждаемся, что объект ещё жив

            t += Time.deltaTime;                          // Наращиваем прошедшее время
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, time)); // Нормализуем в 0..1
            float m = moveCurve.Evaluate(k);              // По кривой движения
            float sc = Mathf.LerpUnclamped(sFrom, sTo, scaleCurve.Evaluate(k)); // По кривой масштаба
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, m); // Интерполируем позицию
            rt.localScale = new Vector3(sc, sc, 1f);      // Интерполируем масштаб
            yield return null;                            // Ждём кадр
        }

        if (!rt) yield break;                              // Финальная проверка перед записью в уничтоженный объект

        rt.anchoredPosition = to;                         // Фиксируем конечную позицию
        rt.localScale = new Vector3(sTo, sTo, 1f);        // Фиксируем конечный масштаб
    }

    /// Конвертер «мир → Canvas»
    private Vector2 WorldToCanvas(Vector3 worldPos, bool directWorldPos = false)
    {
        Vector2 screen;                                              // Буфер для экранных координат

        if (!directWorldPos)                                         // Если это МИРОВОЙ объект (гекс и т.п.)
        {
            // Безопасно берём главный мирокамер
            Camera worldCam = Camera.main;                           // Главная камера мира
            Vector3 sp = worldCam ? worldCam.WorldToScreenPoint(worldPos)
                                  : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f); // Fallback в центр
            screen = new Vector2(sp.x, sp.y);                        // Переносим в 2D
        }
        else                                                         // Если это UI-элемент (RectTransform под Canvas)
        {
            // Для Overlay — КАМЕРА ДОЛЖНА БЫТЬ null; для Screen Space - Camera — используем _uiCam
            Camera camForUI = (rootCanvas && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                ? null                                               // Overlay: обязательно null
                : (_uiCam ? _uiCam : Camera.main);                   // Иначе — камера Canvas, либо fallback на main

            // RectTransformUtility корректно превращает UI world-pos → screen-pos с нужной камерой
            screen = RectTransformUtility.WorldToScreenPoint(camForUI, worldPos);
        }

        // Теперь общая конвертация экран → локальные координаты Canvas
        return ScreenToCanvas(screen);                               // Возвращаем точку в пространстве Canvas
    }

    /// Конвертер «экран → Canvas»
    private Vector2 ScreenToCanvas(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.transform as RectTransform,        // Корневой RectTransform Canvas
            screen,                                       // Экранные координаты
            _uiCam,                                       // Камера Canvas (или null для Overlay)
            out var local);                               // Выход: локальная точка
        return local;                                     // Возвращаем локальные координаты
    }

    /// Центр бейджа события/гекса
    private Vector3 FindBadgeWorldCenter(HexTile tile)
    {
        if (tile && tile.badgeAnchor)                     // Если есть якорь бейджа
            return tile.badgeAnchor.position;             // Возвращаем его позицию
        return tile ? tile.transform.position : Vector3.zero; // Иначе — позицию тайла
    }

    private IEnumerator PlaySingleResourceDirectToInventory(HexTile tile, Sprite icon, ResourceDef res, RectTransform preparedAnchor)
    {
        if (!tile || !icon || !res1Prefab || !fxParent) yield break;               // Защита от null

        var ui = GetPooledResIcon();                                 // Создаём летящую иконку
        var rt = ui.transform as RectTransform;                                     // Берём RectTransform
        ui.gameObject.SetActive(true);                                              // Включаем

        var temp = new EventSO.Reward                                               // Заглушка для бинда
        {
            type = EventSO.RewardType.Resource,                                     // Тип — ресурс
            resource = ScriptableObject.CreateInstance<ResourceDef>(),              // Временный SO
            amount = 1                                                              // Количество не важно
        };
        temp.resource.icon = icon;                                                  // Иконка ресурса
        ui.Bind(temp);                                                              // Биндим на RewardItemUI
        ui.SetGateState(true);                                                      // Рамка ок
        if (ui.amountText) ui.amountText.gameObject.SetActive(false);               // Убираем текст

        Vector2 start = WorldToCanvas(FindBadgeWorldCenter(tile));                  // Старт — центр бейджа гекса
        rt.anchoredPosition = start;                                                // Ставим позицию
        rt.localScale = Vector3.one;                                                // Масштаб 1

        Vector2 center = ScreenToCanvas(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)); // Центр экрана

        RectTransform invSlot = preparedAnchor;                                     // Готовый якорь слота (скрытый/видимый)
        Vector2 invPos = invSlot                                                    // Куда садимся
            ? WorldToCanvas(invSlot.position, true)                                  // UI world → Canvas (true = UI)
            : (rightSideAnchor ? rightSideAnchor.anchoredPosition                   // Иначе — правая «полка»
                               : ScreenToCanvas(new Vector2(Screen.width * 0.83f, Screen.height * 0.5f)));

        yield return Tween(rt, start, center, 1f, 1.5f, phase1Time);                // Фаза A: до центра (увеличение)
        float tPhaseB = (phase2TimeDirect > 0f) ? phase2TimeDirect : 0.45f;         // Длительность B (наглядная)
        yield return Tween(rt, center, invPos, 1.5f, 1.0f, tPhaseB);                // Фаза B: до слота (уменьшение)

        if (rt) ReleaseResIconByRT(rt);                                             // Убираем летящую иконку
    }
    // Партия «восстановлений» (награда): из тайла → центр → левый верх (playerStatsAnchor)
    public void PlayStatRestoreBatch(HexTile tile,
                                     List<(EventSO.PlayerStat stat, int amount)> restores,
                                     Action onDone)
    {
        // Если нет данных — сразу завершить
        if (restores == null || restores.Count == 0) { onDone?.Invoke(); return; }

        // Блокируем ввод на время анимации (ModalGate поддерживает множественных владельцев)
        ModalGate.Acquire(this);

        // Стартуем корутину последовательного прогона
        StartCoroutine(PlayStatRestoreRoutine(tile, restores, () =>
        {
            ModalGate.Release(this);    // Снимаем блок после завершения
            onDone?.Invoke();           // Вызываем коллбек
        }));
    }

    // Корутин последовательной отрисовки всех «единиц» восстановления
    private IEnumerator PlayStatRestoreRoutine(HexTile tile,
        List<(EventSO.PlayerStat stat, int amount)> restores,
        Action onDone)
    {
        Vector2 startBase = WorldToCanvas(FindBadgeWorldCenter(tile));              // Базовая стартовая точка (центр бейджа тайла)
        Vector2 mid = ScreenToCanvas(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)); // Центр экрана (Canvas)
        Vector2 end = playerStatsAnchor ? WorldToCanvas(playerStatsAnchor.position, true)      // Левый верх (HUD) из UI→Canvas
                                        : mid;                                                 // Если якоря нет — fallback в центр

        int total = 0;                                                                // Общее количество "единиц" полётов
        foreach (var e in restores) total += Mathf.Max(1, e.amount);                  // Суммируем по всем типам статов
        int completed = 0;                                                            // Счётчик завершённых перелётов

        foreach (var entry in restores)                                              // Перебираем типы статов
        {
            int cnt = Mathf.Max(1, entry.amount);                                    // Сколько иконок этого типа
            Sprite icon = GetStatSpriteReward(entry.stat);                            // Какой спрайт использовать

            for (int i = 0; i < cnt; i++)                                            // Стартуем КАЖДУЮ иконку
            {
                Vector2 start = startBase;                                           // Для читаемости: локальная копия старта

                // Запускаем ПАРАЛЛЕЛЬНУЮ корутину одного полёта иконки
                StartCoroutine(FlyStatIconOnce(                                      // Отдельная корутина одного перелёта
                    start,                                                           // Старт из тайла
                    mid,                                                             // Через центр
                    end,                                                             // К левому верху
                    icon,                                                            // Какой спрайт
                    () => { completed++; }                                           // По завершении инкремент счётчика
                ));

                // Stagger между СТАРТАМИ (если включен)
                if (useStagger && statsStagger > 0f)                                  // Проверяем флажок и величину
                    yield return new WaitForSeconds(statsStagger);                    // Короткая пауза между стартам
            }
        }

        // Ждём, пока все параллельные перелёты завершатся
        while (completed < total)                                                     // Пока не прилетели все
            yield return null;                                                        // Ждём кадр

        onDone?.Invoke();                                                             // Докладываем о завершении партии

    }

    // Обёртка: один полёт стат-иконки с коллбеком по завершении
    private System.Collections.IEnumerator FlyStatIconOnce(                   // Новая корутина-обёртка
        Vector2 from,                                                         // Старт (Canvas)
        Vector2 mid,                                                          // Центр (Canvas)
        Vector2 to,                                                           // Финиш (Canvas)
        Sprite icon,                                                          // Спрайт
        System.Action onFinish                                                // Коллбек
    )
    {
        yield return FlyStatIcon(from, mid, to, icon);                        // Используем уже существующий полёт
        onFinish?.Invoke();                                                   // Отмечаем завершение
    }

    // Партия «штрафов» (пенальти): из левого верха → центр → тайл
    public void PlayStatPenaltyBatch(HexTile tile,
                                     List<(StatType stat, int amount)> penalties,
                                     Action onDone)
    {
        if (penalties == null || penalties.Count == 0) { onDone?.Invoke(); return; }
        ModalGate.Acquire(this);
        StartCoroutine(PlayStatPenaltyRoutine(tile, penalties, () =>
        {
            ModalGate.Release(this);
            onDone?.Invoke();
        }));
    }

    private IEnumerator PlayStatPenaltyRoutine(HexTile tile,
        List<(StatType stat, int amount)> penalties,
        Action onDone)
    {
        // Вычислим конечную точку в Canvas (центр тайла)
        Vector2 tileCenter = WorldToCanvas(FindBadgeWorldCenter(tile));
        // Для каждой записи (параметр + количество)
        foreach (var entry in penalties)
        {
            int cnt = Mathf.Max(1, entry.amount);                                // единицы штрафа
            Sprite icon = GetStatSpritePenalty(entry.stat);                       // спрайт штрафа
            Vector2 start = playerStatsAnchor                                     // старт — левый верх
                ? WorldToCanvas(playerStatsAnchor.position, true)                 // UI позиция → Canvas
                : ScreenToCanvas(new Vector2(Screen.width * 0.15f, Screen.height * 0.85f)); // fallback

            Vector2 center = ScreenToCanvas(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)); // центр
            Vector2 end = tileCenter;                                             // к тайлу

            for (int i = 0; i < cnt; i++)                                         // «по одной» единице
                yield return FlyStatIcon(start, center, end, icon);               // полет
        }
        onDone?.Invoke(); // конец партии
    }

    // Один «полет» параметра: A → центр (scale 1→1.5) → B (scale 1.5→1.0)
    private IEnumerator FlyStatIcon(Vector2 from, Vector2 mid, Vector2 to, Sprite icon)
    {
        // Используем тот же визуальный префаб, что и ресурсы (res_1) — он содержит Image
        if (!res1Prefab || !fxParent || icon == null) yield break;

        // Создаём временный экземпляр
        var ui = GetPooledResIcon();           // инстанс RewardItemUI
        var rt = ui.transform as RectTransform;               // его RectTransform
        ui.gameObject.SetActive(true);                        // включаем

        // Биндим иконку в RewardItemUI (через временную награду-ресурс, просто как способ показать картинку)
        var temp = new EventSO.Reward
        {
            type = EventSO.RewardType.Resource,               // тип тут неважен, используем ради icon-поля
            resource = ScriptableObject.CreateInstance<ResourceDef>(),
            amount = 1
        };
        temp.resource.icon = icon;                            // подставляем нужный спрайт параметра
        ui.Bind(temp);                                        // отрисовка
        ui.SetGateState(true);                                // рамка «ок»
        if (ui.amountText) ui.amountText.gameObject.SetActive(false); // текст не нужен

        // Стартовая позиция и масштаб
        rt.anchoredPosition = from;                           // ставим в точку A
        rt.localScale = Vector3.one;                          // масштаб 1

        // Фаза A: A → центр (scale 1→1.5)
        yield return Tween(rt, from, mid, 1f, 1.5f, phase1Time);

        // Фаза B: центр → B (scale 1.5→1.0), берём «наглядную» длительность прямой фазы
        float tPhaseB = (phase2TimeDirect > 0f) ? phase2TimeDirect : phase2Time;
        yield return Tween(rt, mid, to, 1.5f, 1.0f, tPhaseB);

        // Удаляем временную иконку
        if (rt) ReleaseResIconByRT(rt);
    }

    // Вспомогалки выбора спрайта (по единому индексу 0..3)
    private Sprite GetStatSpriteReward(EventSO.PlayerStat st)
    {
        int idx = st switch
        {
            EventSO.PlayerStat.Hunger => 0,
            EventSO.PlayerStat.Thirst => 1,
            EventSO.PlayerStat.Energy => 2,
            EventSO.PlayerStat.Health => 3,
            _ => 0
        };
        return (rewardStatSprites != null && idx >= 0 && idx < rewardStatSprites.Length) ? rewardStatSprites[idx] : null;
    }

    private Sprite GetStatSpritePenalty(StatType st)
    {
        int idx = st switch
        {
            StatType.Hunger => 0,
            StatType.Thirst => 1,
            StatType.Energy => 2,
            StatType.Health => 3,
            _ => 0
        };
        return (penaltyStatSprites != null && idx >= 0 && idx < penaltyStatSprites.Length) ? penaltyStatSprites[idx] : null;
    }

    // Запуск партии анимаций «карт из колоды → правая часть руки».
    // cards — это список CardInstance, соответствующих действительно выдаваемым картам в руку.
    // onDone — коллбек, который вызовется ПОСЛЕ того, как все иконки долетят (там вы добавите карты в руку фактически).
    public void PlayCardsToHandFromDeck(
        List<CardInstance> cards,                    // Список карт для анимации (1 иконка = 1 карта)
        System.Action onDone                         // Коллбек завершения партии
    )
    {
        // Если нет данных — сразу завершить
        if (cards == null || cards.Count == 0) { onDone?.Invoke(); return; }             // Нет карт — ничего не анимируем

        // Блокируем ввод на время всей партии полётов
        ModalGate.Acquire(this);                                                          // Подняли «шлагбаум»

        // Запускаем корутину последовательного проигрыша
        StartCoroutine(PlayCardsRoutine(cards, () =>                                     // Старт корутины
        {
            ModalGate.Release(this);                                                      // Опускаем «шлагбаум»
            onDone?.Invoke();                                                             // Сообщаем, что можно добавлять карты в руку
        }));
    }

    // Внутренняя корутина: для каждой карты создаёт временный UI-объект и проигрывает полёт «колода → центр → рука».
    private System.Collections.IEnumerator PlayCardsRoutine(
        List<CardInstance> cards,                                                         // Список карт для полёта
        System.Action allDone                                                             // Коллбек окончания
    )
    {
        // Предрасчёт ключевых точек Canvas
        Vector2 center = ScreenToCanvas(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)); // Центр экрана

        int total = (cards != null) ? cards.Count : 0;                             // Общее число полётов                  // комм: сколько карт запускаем
        int completed = 0;                                                         // Счётчик завершённых полётов          // комм: будем инкрементить по мере прилёта

        for (int i = 0; i < total; i++)                                            // Идём по картам
        {
            var inst = cards[i];                                                   // Текущая карта
            if (inst == null) { completed++; continue; }                           // Нет экземпляра — считаем "завершён" и идём дальше

            Vector2 start = deckAnchor ? WorldToCanvas(deckAnchor.position, true)  // Старт — якорь колоды (UI→Canvas)
                                       : center;                                   // Если якорь не задан — стартуем из центра

            Vector2 end = handRightAnchor ? WorldToCanvas(handRightAnchor.position, true) // Финиш — правая часть руки (UI→Canvas)
                                          : center;                                   // Если якорь не задан — финиш в центр

            // Запускаем ПАРАЛЛЕЛЬНУЮ корутину одного перелёта карты
            StartCoroutine(FlyCardOnce(                                             // Стартуем отдельную корутину
                inst,                                                               // Какая карта визуализируется
                start,                                                              // Откуда летит
                center,                                                             // Через центр
                end,                                                                // Куда садится
                () => { completed++; }                                              // Когда долетит — инкремент счётчика завершений
            ));

            // Если включён stagger — ждём чуть-чуть перед СЛЕДУЮЩИМ стартом
            if (useStagger && cardsStagger > 0f && i < total - 1)                   // Проверяем флажок и что это не последняя карта
                yield return new WaitForSeconds(cardsStagger);                      // Делаем короткую задержку между стартами
        }

        // Ждём, пока все параллельные полёты завершатся
        while (completed < total)                                                   // Пока не прилетели все
            yield return null;                                                      // Ждём кадр

        allDone?.Invoke();
    }

    // Один полёт карты: "колода → центр → правая часть руки"
    private System.Collections.IEnumerator FlyCardOnce(                             // Новая внутренняя корутина одного перелёта
        CardInstance inst,                                                          // Карта для визуализации
        Vector2 start,                                                              // Стартовая точка (Canvas)
        Vector2 mid,                                                                // Средняя точка (Canvas) — центр
        Vector2 end,                                                                // Конечная точка (Canvas) — правая часть руки
        System.Action onFinish                                                      // Коллбек по завершении полёта
    )
    {
        var go = GetPooledCardIcon();                             // Инстанциируем визуал карты (например, UICard.prefab)
        var rt = go.transform as RectTransform;                                     // Берём RectTransform
        go.SetActive(true);                                                         // Включаем объект
        rt.anchoredPosition = start;                                                // Ставим в стартовую позицию
        rt.localScale = Vector3.one;                                                // Масштаб 1

        var view = go.GetComponent<CardView>();                                     // Ищем CardView (если есть на префабе)
        if (view != null)                                                           // Если нашли
        {
            view.Bind(inst);                                                        // Привязываем CardInstance, чтобы показать арт/статы
                                                                                    // view.SetRaycastsEnabled(false);                                         // На время полёта отключим лучи (если такой метод есть)
                                                                                    // view.SetDraggable(false);                                               // И перетаскивание (если такой метод есть)
        }

        yield return Tween(rt, start, mid, 1f, 1.5f, phase1Time);                   // Фаза A: до центра, масштаб 1→1.5
        float tPhaseB = (phase2TimeDirect > 0f) ? phase2TimeDirect : phase2Time;    // Выбираем длительность второй фазы (наглядную)
        yield return Tween(rt, mid, end, 1.5f, 1.0f, tPhaseB);                       // Фаза B: до правого края, масштаб 1.5→1.0

        if (rt) ReleaseResIconByRT(rt);                                             // Удаляем временный визуал
        ReleaseCardIcon(go);      // ← всегда вернуть в пул, НЕ парентить в руку
        onFinish?.Invoke();                                                         // Сообщаем "этот перелёт завершён"
    }

    // разворот наград в единичные ресурсы ---
    private static List<EventSO.Reward> ExpandToUnitRewards(List<EventSO.Reward> src)   // Метод вернёт список, где каждая запись = 1 шт.
    {
        var units = new List<EventSO.Reward>();                                         // Итоговый список «единичек»
        if (src == null) return units;                                                  // Пусто → пустой список

        foreach (var r in src)                                                          // Перебираем исходные награды
        {
            if (r == null) continue;                                                    // Защита от null
            if (r.type != EventSO.RewardType.Resource || r.resource == null) continue;  // Нас интересуют только ресурсы
            int count = Mathf.Max(1, r.amount);                                         // Сколько единиц летит (минимум 1)
            for (int i = 0; i < count; i++)                                             // Добавим поштучно
            {
                units.Add(new EventSO.Reward                                             // Создаём «единичный» элемент
                {
                    type = EventSO.RewardType.Resource,                                  // Тип — ресурс
                    resource = r.resource,                                               // Тот же ресурс
                    amount = 1,                                                          // Всегда 1
                    gatedByAdditional = r.gatedByAdditional                              // Сохраним флаг, если важен
                });
            }
        }
        return units;                                                                    // Вернули развёрнутый список
    }

    public void PlayRewardsFromUIAnchor(                             // Публичный метод запуска партии полётов
    RectTransform startAnchor,                                   // Якорь-старт (верхняя точка зоны лута врага, UI)
    List<EventSO.Reward> loot,                                   // Список наград (фильтровать по ресурсам будем внутри)
    Action onBeforeInventoryApply,                               // Коллбек «до посадки» — начислить ресурсы (создать слоты)
    Action onAfterDone                                           // Коллбек «после завершения» — продолжать пайплайн
)
    {
        if (loot == null || loot.Count == 0)                         // Если нечего анимировать
        {
            onBeforeInventoryApply?.Invoke();                        // Всё равно начислим (на случай логики выше)
            onAfterDone?.Invoke();                                   // И завершим
            return;                                                  // Выходим
        }

        _inventoryAppliedForThisBatch = false;                       // Сбрасываем флаг «начислено в этой партии»

        ModalGate.Acquire(this);                                     // Блокируем ввод на время «синематика»

        StartCoroutine(PlayQueueFromUIAnchorRoutine(                 // Запускаем корутину партии
            startAnchor,                                             // Стартовый якорь врага (UI)
            loot,                                                    // Полный список наград
            onBeforeInventoryApply,                                  // Коллбек «до»
            () =>                                                    // Коллбек «после»
            {
                ModalGate.Release(this);                             // Снимаем блок ввода
                onAfterDone?.Invoke();                               // Сообщаем о завершении партии
            }
        ));
    }
    // полёт одного ресурса с post-логикой раскрытия слота ---
    private IEnumerator FlyAndRevealRoutine(                          // Обёртка поверх «одного полёта»
    RectTransform startAnchor,                                    // Стартовая UI-точка (якорь у врага)
    Sprite icon,                                                  // Спрайт ресурса
    ResourceDef res,                                              // Тип ресурса (для слота инвентаря)
    RectTransform preparedAnchor,                                 // Якорь слота в инвентаре (может быть скрыт)
    Action onOneDone                                              // Коллбек: «этот ресурс долетел»
)
    {
        // 1) Летим: якорь врага → центр экрана → слот инвентаря
        yield return PlaySingleResourceFromUIAnchorToInventory(       // Дождёмся завершения полёта
            startAnchor,                                              // Откуда летим (UI)
            icon,                                                     // Какой спрайт
            res,                                                      // Какой ресурс
            preparedAnchor                                            // Куда летим (якорь слота)
        );

        // 2) Раскрыть скрытый слот (если это новый ресурс)
        if (inventoryUI)                                              // Если UI инвентаря доступен
            inventoryUI.RevealHiddenSlot(res);                        // Делает слот видимым именно в момент прибытия

        // 3) Лёгкий «пинг» слота для акцента
        var anchorAfter = inventoryUI ?                               // Повторно получим актуальный якорь
            inventoryUI.GetSlotAnchorForResource(res) : null;         // (уже видимый)
        if (anchorAfter)                                              // Если якорь существует
            inventoryUI.PingSlot(anchorAfter);                        // Лёгкая подсветка

        // 4) Сообщить наружу, что один ресурс завершил свою анимацию
        onOneDone?.Invoke();                                          // Снижаем счётчик активных полётов
    }


    // --- CHANGED START: партия полётов с «гусеницей» (стартуем с задержкой, ждём, когда все долетят) ---
    private IEnumerator PlayQueueFromUIAnchorRoutine(                 // Корутина: проиграть всю пачку
     RectTransform startAnchor,                                    // Стартовый UI-якорь (лут врага)
     List<EventSO.Reward> loot,                                    // Сырые награды
     Action onBeforeInventoryApply,                                // Коллбек до старта (начислить)
     Action onAllDone                                              // Коллбек после завершения всех полётов
 )
    {
        // A) Разворачиваем в «единичные» ресурсы
        var units = ExpandToUnitRewards(loot);                         // Получили список unit-наград
        if (units.Count == 0)                                          // Если нечего летать
        {
            onBeforeInventoryApply?.Invoke();                          // Всё равно дадим применить начисление
            onAllDone?.Invoke();                                       // И завершим
            yield break;                                               // Выходим
        }

        // Б) Единожды применяем начисление — UI создаст слоты; новые будут скрыты
        if (!_inventoryAppliedForThisBatch)                            // Если ещё не делали
        {
            onBeforeInventoryApply?.Invoke();                          // Начисляем в модель
            _inventoryAppliedForThisBatch = true;                      // Помечаем
        }

        // В) Запускаем полёты «гусеницей»: следующий стартует с задержкой
        int running = 0;                                               // Счётчик активных полётов
        for (int i = 0; i < units.Count; i++)                          // Идём по всем «единицам»
        {
            var r = units[i];                                          // Текущая unit-награда (ровно 1 шт.)
            var targetAnchor = inventoryUI                              // Если доступен UI инвентаря
                ? inventoryUI.GetSlotAnchorForResource(r.resource)     // Берём якорь слота/плейсхолдера
                : null;                                                // Иначе будет запасной якорь в полёте

            running++;                                                 // Увеличим число активных полётов

            StartCoroutine(FlyAndRevealRoutine(                        // Запустим единичный полёт
                startAnchor,                                           // Откуда (UI якорь врага)
                r.resource.icon,                                       // Какой спрайт
                r.resource,                                            // Какой ресурс
                targetAnchor,                                          // Куда (якорь слота)
                () => running--                                        // По завершении — декремент счётчика
            ));

            if (resourceStagger > 0f)                                  // Если нужна «гусеница»
                yield return new WaitForSeconds(resourceStagger);      // Подождём между СТАРТАМИ
        }

        // Г) Ждём завершения всех параллельных полётов
        while (running > 0)                                            // Пока есть активные
            yield return null;                                         // Ждём кадр

        // Д) Партия завершена
        onAllDone?.Invoke();                                           // Сообщаем наружу
        yield break;                                                   // Готово
    }

    // единичный полёт иконки ресурса (без числа) ---
    private IEnumerator PlaySingleResourceFromUIAnchorToInventory(    // Корутина: один полёт
        RectTransform startAnchor,                                    // Старт UI
        Sprite icon,                                                  // Спрайт
        ResourceDef res,                                              // Ресурс (для слота/раскрытия)
        RectTransform preparedAnchor                                  // Якорь инвентаря (может быть скрыт)
    )
    {
        if (!startAnchor || !icon || !res1Prefab || !fxParent)        // Проверка ссылок
            yield break;                                              // Выходим, если чего-то нет

        var ui = GetPooledResIcon();                   // Создаём «летящую» иконку (RewardItemUI)
        var rt = ui.transform as RectTransform;                       // Берём RectTransform
        ui.gameObject.SetActive(true);                                // Включаем объект

        var temp = new EventSO.Reward                                  // Временная единичная награда для Bind
        {
            type = EventSO.RewardType.Resource,                        // Тип — ресурс
            resource = res,                                            // ВАЖНО: используем исходный ресурс (иконка оттуда)
            amount = 1,                                                // Единичка
            gatedByAdditional = false                                  // Без гейта
        };
        ui.Bind(temp);                                                 // Биндим визуал иконки
        ui.SetGateState(true);                                         // Рамка «ок»
        if (ui.amountText) ui.amountText.gameObject.SetActive(false);  // Скрываем цифру КАТЕГОРИЧЕСКИ (всегда без числа)

        Vector2 start = WorldToCanvas(startAnchor.position, true);     // Переводим позицию якоря → Canvas
        rt.anchoredPosition = start;                                   // Ставим стартовую позицию
        rt.localScale = Vector3.one;                                   // Масштаб 1 в начале

        Vector2 center = ScreenToCanvas(                               // Центр экрана в координатах Canvas
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
        );

        RectTransform invSlot = preparedAnchor;                        // Целевой якорь (если есть)
        Vector2 invPos = invSlot                                      // Вычисляем позицию назначения
            ? WorldToCanvas(invSlot.position, true)                    // Из позиции якоря слота
            : (rightSideAnchor                                         // Иначе — запасная точка справа
                ? rightSideAnchor.anchoredPosition
                : ScreenToCanvas(new Vector2(Screen.width * 0.83f, Screen.height * 0.5f)));

        yield return Tween(rt, start, center, 1f, 1.5f, phase1Time);   // Фаза A: старт → центр (scale 1 → 1.5)
        float tPhaseB = (phase2TimeDirect > 0f) ? phase2TimeDirect : phase2Time; // Длительность В-фазы
        yield return Tween(rt, center, invPos, 1.5f, 1.0f, tPhaseB);   // Фаза B: центр → слот (scale 1.5 → 1)

        if (rt) ReleaseResIconByRT(rt);                                // Удаляем «летящую» иконку
        yield break;                                                   // Готово
    }
}