using UnityEngine; // Базовые типы Unity (MonoBehaviour, Color, SpriteRenderer и т.д.)

// Тип клетки: пустая / событие / непроходимая
public enum HexType { Empty, Event, Blocked } // Перечисление для типа гекса

// Тип «подсказки» на закрытом гексе
public enum HexHintType { None, Enemy, Info, Food, GoldStar, SilverStar } // Варианты иконок-подсказок

public class HexTile : MonoBehaviour // Скрипт логики одного гекса
{
    public int x;                                        // Координата X (столбец)
    public int y;                                        // Координата Y (строка)
    public HexType type = HexType.Empty;                 // Текущий тип гекса
    public bool isRevealed = false;                      // Открыт ли гекс (видно ли детали события)
    public bool isPassable = true;                       // Проходим ли гекс (может меняться по ходу партии)
    public HexHintType hintType = HexHintType.None;      // Тип подсказки для закрытого гекса (если есть)

    [Header("Visuals")]
    public SpriteRenderer baseRenderer;                  // Основной спрайт гекса (фон/контур)
    //public SpriteRenderer iconRenderer;                  // Иконка типа/события (видна обычно на открытых)
    public SpriteRenderer hintRenderer;                  // Иконка-подсказка (видна на закрытых некоторых)

    private Color defaultColor = Color.white;           // Базовый цвет гекса (для сброса подсветки)
    private Color hoverColor = new Color(1f, 1f, 0.6f); // Цвет при наведении
    private Color blockedColor = new Color(0.6f, 0.6f, 0.6f); // Цвет непроходимого гекса

    [Header("Данные события")]
    public EventSO eventData;  // Данные события (SO)

    [Header("Event badge (world-space UI)")]
    public Transform badgeAnchor;              // Укажи пустышку на префабе гекса (где рисовать бейдж)
    public HexEventBadgeUI badgePrefab;        // Префаб бейджа
    private HexEventBadgeUI badge;             // Живая ссылка

    [Header("Backdrop (persists on hex)")]
    public SpriteRenderer backdropUnrevealed;         // Рендер подложки для состояния 1) !isRevealed
    public SpriteRenderer backdropBlocked;            // Рендер подложки для состояния 2) !isPassable (если уже открыт)
    public SpriteRenderer backdropRevealedPassable;   // Рендер подложки для состояния 3) isRevealed && isPassable

    [Tooltip("Варианты спрайтов для каждого состояния. Можно оставить пустым.")]
    public Sprite[] backUnrevealedSprites;            // Набор вариантов для !isRevealed
    public Sprite[] backBlockedSprites;               // Набор вариантов для !isPassable
    public Sprite[] backRevealedSprites;              // Набор вариантов для isRevealed && isPassable

    [Tooltip("Фиксированные индексы спрайтов. -1 = выбрать случайно один раз при создании.")]
    public int backUnrevealedIndex = -1;              // Какой вариант взять для !isRevealed
    public int backBlockedIndex = -1;                 // Какой вариант взять для !isPassable
    public int backRevealedIndex = -1;                // Какой вариант взять для isRevealed && isPassable

    // Внутренний флаг, чтобы не перекидывать варианты каждый кадр
    private bool backdropChosen = false;              // Выбраны ли уже спрайты для подложек


    public void Init(int xCoord, int yCoord) // Инициализация координат
    {
        x = xCoord;                              // Сохраняем X
        y = yCoord;                              // Сохраняем Y
        gameObject.name = $"Hex_{x}_{y}";        // Для удобной отладки — имя по координатам

        //  один раз выбираем спрайты подложек и настраиваем сортинг
        EnsureBackdropConfigured();              // Подберём случайные (или фиксированные) варианты и выставим сортинг


        UpdateVisual();                          // Обновляем внешний вид (на случай предзаданных полей)
    }

    public void BindEvent(EventSO data)                  // Привязать событие
    {
        eventData = data;                                // Сохраняем SO
        if (eventData != null)                           // Если задано
        {
            type = eventData.hexType;                    // Тип тайла берём из события (обычно Event)
            if (!isRevealed && hintRenderer != null)     // Если тайл закрыт — показываем подсказку (если есть)
            {
                // Тут можно назначить спрайт подсказки по eventData.defaultHint (когда появятся иконки)
                hintType = eventData.defaultHint;        // Пока просто копируем тип подсказки
            }
        }

        // Создаём/показываем бейдж
        EnsureBadge();
        if (badge) { badge.Bind(eventData); badge.SetVisible(isRevealed); }

        UpdateVisual();                                  // Обновить отрисовку
    }

    public void Reveal() // Открыть гекс (делает видимыми детали события)
    {
        isRevealed = true;                       // Помечаем как открытый
        UpdateVisual();                          // Обновляем визуал под новое состояние
    }

    public void SetPassable(bool passable) // Изменить проходимость (по ходу партии)
    {
        isPassable = passable;                   // Сохраняем флаг
        UpdateVisual();                          // Обновляем цвет/индикаторы
    }

    public void SetType(HexType newType) // Изменить тип гекса
    {
        type = newType;                          // Сохраняем тип
        UpdateVisual();                          // Обновляем визуал
    }

    public void SetHover(bool isHover)                   // Включить/выключить подсветку наведения
    {
        if (baseRenderer == null) return;                // Защита
        if (isHover)                                     // Если навели
            baseRenderer.color = hoverColor;             // Красим в цвет наведения
        else                                             // Если убрали курсор
            baseRenderer.color = isPassable ? defaultColor : blockedColor; // Возврат базового/блокированного цвета
    }

    public void UpdateVisual() // Централизованное обновление внешнего вида
    {
        if (baseRenderer == null) return;        // Если не назначен — выходим (лучше назначить в префабе)

        // Цвет фона в зависимости от проходимости
        baseRenderer.color = isPassable ? defaultColor : blockedColor; // Серый для блокированных

        if (badge) badge.SetVisible(isRevealed && eventData != null);

        if (hintRenderer != null)                        // Иконка подсказки
        {
            bool showHint = !isRevealed && hintType != HexHintType.None; // Только на закрытом и если подсказка задана
            hintRenderer.enabled = showHint;             // Вкл/выкл
            //hintRenderer.sprite = ... // Когда подключим спрайты подсказок — назначим по hintType
        }

        // актуализируем видимость трёх подложек
        UpdateBackdropVisibility();
    }


    // Когда тайл становится пустым/событие удаляется:
    public void ClearEvent()
    {
        eventData = null;
        type = HexType.Empty;
        UpdateVisual();
        if (badge) badge.SetVisible(false);
    }

    private void EnsureBadge()
    {
        if (badge != null) return;
        if (badgePrefab == null || badgeAnchor == null) return;

        badge = Instantiate(badgePrefab, badgeAnchor);

        // нормализуем local TRS
        var rt = (RectTransform) badge.transform;
        rt.localPosition = Vector3.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        // Поставим сортинг выше спрайта гекса и подгоним размер
        badge.ConfigureSortingLike(baseRenderer, orderOffset: 10);
        badge.FitToHex(baseRenderer);
    }

    // Выбрать (один раз) конкретные спрайты для трёх состояний и настроить их сортинг
    private void EnsureBackdropConfigured()
    {
        if (backdropChosen) return; // уже делали

        // 1) Выбираем спрайты
        var sUnrev = PickSprite(backUnrevealedSprites, backUnrevealedIndex);
        var sBlocked = PickSprite(backBlockedSprites, backBlockedIndex);
        var sRevPass = PickSprite(backRevealedSprites, backRevealedIndex);

        // 2) Назначаем + сортинг + автоподгон под размер гекса
        //    inset = 1f — ровно во всю «шапку» гекса; поставь 0.98f, если хочется чуть «внутрь» рамки
        SetSpriteLikeHex(backdropUnrevealed, sUnrev, sortingOffset: +1, inset: 1f);
        SetSpriteLikeHex(backdropBlocked, sBlocked, sortingOffset: +1, inset: 1f);
        SetSpriteLikeHex(backdropRevealedPassable, sRevPass, sortingOffset: +1, inset: 1f);


        // Сортинг: подложка должна быть выше baseRenderer, но ниже бейджа события
        // Выставим тот же слой и order чуть больше baseRenderer, чтобы гарантированно оказаться над ним.
        ApplySortingLike(baseRenderer, backdropUnrevealed, +1);       // +1 к порядку
        ApplySortingLike(baseRenderer, backdropBlocked, +1);
        ApplySortingLike(baseRenderer, backdropRevealedPassable, +1);

        backdropChosen = true; // больше не пересчитывать
    }

    // Подобрать спрайт по индексу (или случайно, если index < 0). Возвращает null, если вариантов нет.
    private Sprite PickSprite(Sprite[] variants, int index)
    {
        if (variants == null || variants.Length == 0) return null;
        int i = index >= 0 && index < variants.Length ? index : Random.Range(0, variants.Length);
        return variants[i];
    }

    // Назначить слой/порядок рисования как у базового спрайта, но со смещением по order
    private void ApplySortingLike(SpriteRenderer reference, SpriteRenderer target, int orderOffset)
    {
        if (reference == null || target == null) return;
        target.sortingLayerID = reference.sortingLayerID; // тот же слой
        target.sortingOrder = reference.sortingOrder + orderOffset; // чуть выше фона гекса
    }

    // Включить видимый вариант подложки согласно приоритетам состояния
    private void UpdateBackdropVisibility()
    {
        // Правило приоритета: если одновременно применимы (1) и (2), используем (1) — !isRevealed.
        bool showUnrevealed = !isRevealed;
        bool showBlocked = isRevealed && !isPassable;              // только если уже открыт, но непроходим
        bool showRevealed = isRevealed && isPassable;              // открыт и проходим

        if (backdropUnrevealed) backdropUnrevealed.enabled = showUnrevealed;
        if (backdropBlocked) backdropBlocked.enabled = !showUnrevealed && showBlocked; // (1) имеет приоритет
        if (backdropRevealedPassable) backdropRevealedPassable.enabled = !showUnrevealed && !showBlocked && showRevealed;
    }

    // Подогнать спрайт target под размер базового гекса (baseRenderer)
    private void FitSpriteToHex(SpriteRenderer target, float inset = 1f)
    {
        if (!target || !target.sprite || !baseRenderer || !baseRenderer.sprite) return;

        // Локальные размеры спрайтов при scale = 1
        Vector2 baseSize = baseRenderer.sprite.bounds.size; // ширина/высота гекса в «юнитах-спрайта»
        Vector2 targetSize = target.sprite.bounds.size;

        if (targetSize.x <= 0.0001f || targetSize.y <= 0.0001f) return;

        // Текущие мировые коэффициенты масштаба
        Vector3 baseLossy = baseRenderer.transform.lossyScale;
        Vector3 targetLossy = target.transform.lossyScale;

        // Мировые размеры «как сейчас»
        float baseWorldW = baseSize.x * baseLossy.x;
        float baseWorldH = baseSize.y * baseLossy.y;
        float targetWorldW = targetSize.x * targetLossy.x;
        float targetWorldH = targetSize.y * targetLossy.y;

        // Во сколько раз надо домножить локальный scale target, чтобы совпасть с базой
        float mulX = (baseWorldW / Mathf.Max(0.0001f, targetWorldW)) * inset;
        float mulY = (baseWorldH / Mathf.Max(0.0001f, targetWorldH)) * inset;

        // Применяем, сохраняя текущую локальную ориентацию/масштаб по Z
        var ls = target.transform.localScale;
        target.transform.localScale = new Vector3(ls.x * mulX, ls.y * mulY, ls.z);
    }

    // Удобный хелпер: присвоить спрайт и сразу подогнать масштаб/слой
    private void SetSpriteLikeHex(SpriteRenderer r, Sprite s, int sortingOffset = +1, float inset = 1f)
    {
        if (!r) return;
        r.sprite = s;
        // сортировка поверх базового (или под ним — зависит от offset)
        if (baseRenderer)
        {
            r.sortingLayerID = baseRenderer.sortingLayerID;
            r.sortingOrder = baseRenderer.sortingOrder + sortingOffset;
        }
        FitSpriteToHex(r, inset); // ← ключевая строчка
    }

}