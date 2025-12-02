using UnityEngine; // Базовые типы Unity (MonoBehaviour, Color, SpriteRenderer и т.д.)
using static AdventureAsset; // сверху файла (или пишите AdventureAsset.SpritePickRule)

// Тип клетки: пустая / событие / непроходимая
public enum HexType { Empty, Event, Blocked, Exit } // Перечисление для типа гекса

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

    //private Color defaultColor = Color.white;           // Базовый цвет гекса (для сброса подсветки)
    //private Color hoverColor = new Color(1f, 1f, 0.6f); // Цвет при наведении
    //private Color blockedColor = new Color(0.6f, 0.6f, 0.6f); // Цвет непроходимого гекса

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

    // --- Move Hint через SpriteRenderer (альтернатива UI) ---
    [Header("Move Hint (SpriteRenderers)")]
    public SpriteRenderer goSprite;             // Спрайт «можно идти»
    public SpriteRenderer xSprite;              // Спрайт «нельзя»
    public TMPro.TextMeshPro moveCostText3D;    // 3D-вариант текста (не UGUI), если хотите число поверх

    [Header("Hover selection (frame)")]
    [SerializeField] private GameObject isSelected;                 // сюда перетащи GO "isSelected" с рамкой
    [SerializeField] private SpriteRenderer isSelectedRenderer;     // (необязательно) сам рендерер рамки для сортинга
    [SerializeField] private Color hoverFrameColorPassable = Color.white;                 // цвет для проходимых
    [SerializeField] private Color hoverFrameColorBlocked = new Color(1f, 0.25f, 0.25f); // цвет для непроходимых

    [Header("Barriers (optional)")]
    [SerializeField] private System.Collections.Generic.List<int> barriers = new System.Collections.Generic.List<int>();
    // значения 1 (синяя) или 3 (оранжевая); максимум 3 шт

    [Header("Exit visuals")]
    public SpriteRenderer exitRenderer;   // ← повесь рендерер «иконки выхода» на префаб гекса

    [Header("Hint icon set")]
    [SerializeField] private HexHintIconSet hintIconSet;   // ← перетащи сюда ваш asset с иконками

    [Header("Backdrop Renderer")]
    [SerializeField] private SpriteRenderer backdropRenderer;       // Сюда ставим выбранный кадр

    // Сюда билдер положит уже выбранные кадры (по правилам из ассета и наборов)
    private Sprite _chosenUnrevealed;                                // Спрайт для закрытого
    private Sprite _chosenBlocked;                                   // Спрайт для Blocked
    private Sprite _chosenRevealed;                                  // Спрайт для открытого

    // Ваши поля состояния
    //[SerializeField] private bool isRevealed;                        // Флаг «открыт»
    //[SerializeField] private HexType type;                           // Empty/Event/Blocked/Exit ...


    /// <summary>Только чтение наружу — актуальный набор фишек на гексе.</summary>
    public System.Collections.Generic.IReadOnlyList<int> Barriers => barriers;

    // Внутренний флаг, чтобы не перекидывать варианты каждый кадр
    private bool backdropChosen = false;              // Выбраны ли уже спрайты для подложек


    public void Init(int xCoord, int yCoord) // Инициализация координат
    {
        x = xCoord;                              // Сохраняем X
        y = yCoord;                              // Сохраняем Y
        gameObject.name = $"Hex_{x}_{y}";        // Для удобной отладки — имя по координатам

        //  один раз выбираем спрайты подложек и настраиваем сортинг
        EnsureBackdropConfigured();              // Подберём случайные (или фиксированные) варианты и выставим сортинг

        // рамка изначально выключена
        if (isSelected) isSelected.SetActive(false);
        // при желании — подтянуть сортинг рамки над фоном гекса
        if (isSelectedRenderer) ApplySortingLike(baseRenderer, isSelectedRenderer, +25);
        else if (isSelected) isSelectedRenderer = isSelected.GetComponentInChildren<SpriteRenderer>(true);

        UpdateVisual();                          // Обновляем внешний вид (на случай предзаданных полей)
    }

    public void BindEvent(EventSO data)                  // Привязать событие
    {
        eventData = data;                                // Сохраняем SO
        if (eventData != null)                           // Если задано
        {
           // type = eventData.hexType;                    // Тип тайла берём из события (обычно Event)
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
        // Подсветку рисуем только если гекс открыт и курсор над ним
        if (!(isHover && isRevealed))
        {
            if (isSelected) isSelected.SetActive(false);
            return;
        }

        // Разрешаем подсветку только для соседей клетки, где стоит игрок
        bool isNeighbor = false;
        var map = HexMapController.Instance;                             // контроллер карты (синглтон)
        if (map != null && map.playerPawn != null)                       // фишка игрока есть?
        {
            var neigh = map.GetNeighbors(map.playerPawn.x, map.playerPawn.y); // соседи клетки игрока
            if (neigh != null) isNeighbor = neigh.Contains(this);        // этот гекс — в списке соседей?
        }

        // Условия показа рамки:
        // - Если гекс ПРОХОДИМЫЙ → подсвечиваем только если на нём есть событие (eventData != null)
        // - Если гекс НЕПРОХОДИМЫЙ → подсвечиваем всегда
        bool show = isNeighbor && (isPassable ? (eventData != null) : true);

        if (isSelected) isSelected.SetActive(show);

        // Цвет рамки: белый (или ваш) для проходимых, красный — для непроходимых
        if (show && isSelectedRenderer)
            isSelectedRenderer.color = isPassable ? hoverFrameColorPassable : hoverFrameColorBlocked;
    }

    public void UpdateVisual() // Централизованное обновление внешнего вида
    {
        if (baseRenderer == null) return;        // Если не назначен — выходим (лучше назначить в префабе)

        // Цвет фона в зависимости от проходимости
       //baseRenderer.color = isPassable ? defaultColor : blockedColor; // Серый для блокированных

        if (badge) badge.SetVisible(isRevealed && eventData != null);

        if (hintRenderer != null)
        {
            bool showHint = !isRevealed && hintType != HexHintType.None;
            hintRenderer.enabled = showHint;

            if (showHint)
            {
                // Берём спрайт из набора (по enum), назначаем и гарантируем слой чуть выше фона гекса
                var s = hintIconSet ? hintIconSet.Get(hintType) : null;
                hintRenderer.sprite = s;

                // слой/порядок поверх baseRenderer и под бейджем (бейдж всё равно показывается только на открытых)
                ApplySortingLike(baseRenderer, hintRenderer, +12);

                // подогнать под размер гекса (чуть внутрь рамки)
                FitSpriteToHex(hintRenderer, inset: 0.35f);
            }
        }

        // актуализируем видимость трёх подложек
        UpdateBackdropVisibility();

        // 5) СПЕЦИАЛЬНО ДЛЯ EXIT: всегда показываем иконку «выхода»
        if (exitRenderer)
        {
            // виден только на открытом гексе типа Exit
            bool on = (type == HexType.Exit && isRevealed);
            exitRenderer.enabled = on;

            if (on)
            {
                ApplySortingLike(baseRenderer, exitRenderer, +2); // поверх фона гекса
                //FitSpriteToHex(exitRenderer, inset: 1f);          // подгон по размеру
            }
        }

        // при любом пересчёте визуала рамку снимаем (покажется заново при ховере)
        if (isSelected) isSelected.SetActive(false);

        UpdateBackdropVisual();

    }


    // Когда тайл становится пустым/событие удаляется:
    public void ClearEvent()
    {
        eventData = null;
        type = HexType.Empty;
        UpdateVisual();
        if (badge) badge.SetVisible(false);
    }

    /// Настроить сортинг подсказок (спрайты/3D-текст) «над» базовым гексом
    private void EnsureHintSorting()
    {
        // Если нет базового рендерера — дальше делать нечего
        if (!baseRenderer) return;

        // Для картинок «go»/«X»: тот же слой, порядок = базовый + 20
        ApplySortingLike(baseRenderer, goSprite, +20); // гарантируем, что не «скатится» к 10
        ApplySortingLike(baseRenderer, xSprite, +20);

        // Для 3D-текста (TMP) — настроим Renderer
        if (moveCostText3D)
        {
            var r = moveCostText3D.GetComponent<Renderer>();
            if (r != null)
            {
                r.sortingLayerID = baseRenderer.sortingLayerID; // тот же слой
                r.sortingOrder = baseRenderer.sortingOrder + 21; // на 1 выше иконок
            }
        }
    }

    /// Показать подсказку хода: can = можно ли пройти; cost = стоимость в картах (если can)
    public void ShowMoveHint(bool can, int cost = 0)
    {
        EnsureHintSorting();                          // каждый раз убеждаемся в корректном слое/порядке

        // SpriteRenderer-ветка: иконки «go»/«X»
        if (goSprite) goSprite.enabled = can;         // «go» только если можем пройти
        if (xSprite) xSprite.enabled = !can;        // «X» если не можем пройти

        // 3D-текст стоимости
        if (moveCostText3D)
        {
            bool showCost = can && cost > 0;          // цифру показываем только при валидном ходе
            moveCostText3D.gameObject.SetActive(showCost);
            if (showCost) moveCostText3D.text = cost.ToString();
        }
    }

    /// Скрыть подсказку
    public void HideMoveHint()
    {
        if (goSprite) goSprite.enabled = false;       // всегда выключаем иконки
        if (xSprite) xSprite.enabled = false;
        if (moveCostText3D) moveCostText3D.gameObject.SetActive(false); // и цифру
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


    public void ApplyBackdropPicks(
        SpritePickRule unrev,
        SpritePickRule blocked,
        SpritePickRule revealed)
    {
        int ChooseIndex(SpritePickRule rule, int variantsCount)
        {
            if (rule == null) return -1;
            if (rule.fixedIndex >= 0) return rule.fixedIndex;

            // Пул → фильтруем валидные значения и берём случайный
            if (rule.pool != null && rule.pool.Count > 0)
            {
                var valid = new System.Collections.Generic.List<int>();
                for (int i = 0; i < rule.pool.Count; i++)
                {
                    int idx = rule.pool[i];
                    if (idx >= 0 && idx < variantsCount) valid.Add(idx);
                }
                if (valid.Count > 0) return valid[UnityEngine.Random.Range(0, valid.Count)];
            }
            // иначе — оставляем -1 (значит, HexTile сам выберет случайный из всего набора)
            return -1;
        }

        // Проставляем индексы для трёх состояний; HexTile затем их использует в EnsureBackdropConfigured()
        backUnrevealedIndex = ChooseIndex(unrev, backUnrevealedSprites != null ? backUnrevealedSprites.Length : 0);
        backBlockedIndex = ChooseIndex(blocked, backBlockedSprites != null ? backBlockedSprites.Length : 0);
        backRevealedIndex = ChooseIndex(revealed, backRevealedSprites != null ? backRevealedSprites.Length : 0);

        // Сбрасываем флаг, чтобы пересобрать подложки под новые индексы
        // (если у вас есть поле backdropChosen)
        // backdropChosen = false; // ← если оно у вас приватное — поставьте true/false согласно вашей логике
        EnsureBackdropConfigured();
        UpdateVisual();
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

    // Сумма фишек — это модификатор Main Cost для simple-событий этого гекса.
    public int BarrierTotal
    {
        get
        {
            int s = 0;
            if (barriers != null) for (int i = 0; i < barriers.Count; i++) s += (barriers[i] >= 3 ? 3 : 1);
            return s;
        }
    }

    //Полностью заменить набор фишек (вызывается из билдера/редактора).
    public void SetBarriers(System.Collections.Generic.IEnumerable<int> values)
    {
        barriers = values != null ? new System.Collections.Generic.List<int>(values)
                                  : new System.Collections.Generic.List<int>();
        ClampBarriers();
        PushBarriersToBadge();
    }

    //Добавить одну фишку (1/3). Игнорируем, если уже 3 шт.
    public void AddBarrier(int value)
    {
        if (barriers == null) barriers = new System.Collections.Generic.List<int>();
        if (barriers.Count >= 3) return;
        barriers.Add(value >= 3 ? 3 : 1);
        PushBarriersToBadge();
    }

    //Снять самую «первую» фишку (по ТЗ) — возвращает true, если сняли.
    public bool RemoveFirstBarrier()
    {
        if (barriers == null || barriers.Count == 0) return false;
        barriers.RemoveAt(0);
        PushBarriersToBadge();
        return true;
    }

    private void ClampBarriers()
    {
        if (barriers == null) return;
        for (int i = 0; i < barriers.Count; i++) barriers[i] = (barriers[i] >= 3 ? 3 : 1); // нормализуем 1/3
        if (barriers.Count > 3) barriers.RemoveRange(3, barriers.Count - 3);              // максимум 3
    }

    // Протолкнуть состояние фишек в UI бейджа.
    private void PushBarriersToBadge()
    {
        if (badge != null) badge.SetBarriers(Barriers); // badge: HexEventBadgeUI на этом тайле
    }

    // (Не обязательно, но удобно) — при спавне гекса синхронизируем UI:
    private void Start()
    {
        PushBarriersToBadge();
    }

    // Вызывается AdventureBuilder'ом сразу после Instantiate/Init
    public void ApplyChosenBackdropSprites(Sprite unrev, Sprite blocked, Sprite revealed)
    {
        _chosenUnrevealed = unrev;                                   // Запоминаем спрайт закрытого
        _chosenBlocked = blocked;                                 // Запоминаем спрайт Blocked
        _chosenRevealed = revealed;                                // Запоминаем спрайт открытого
        UpdateBackdropVisual();                                      // Переключаем визуал под текущее состояние
    }

    // Вызывай из ваших SetType()/Reveal()/UpdateVisual()
    private void UpdateBackdropVisual()
    {
        if (!backdropRenderer) return;                               // Если не назначен рендер — выходим
        Sprite s = null;                                             // Сюда выберем итоговый спрайт

        if (!isRevealed)                                             // Если гекс закрыт
            s = _chosenUnrevealed ? _chosenUnrevealed : _chosenRevealed; // Падаем на открытую, если нет закрытой
        else if (type == HexType.Blocked)                            // Если Blocked
            s = _chosenBlocked ? _chosenBlocked : _chosenRevealed;   // Падаем на открытую, если нет блокированной
        else                                                         // Иначе — обычный открытый
            s = _chosenRevealed;

        backdropRenderer.sprite = s;                                 // Ставим выбранный кадр

        SetSpriteLikeHex(backdropRenderer, s, sortingOffset: +1, inset: 1f);
    }

}