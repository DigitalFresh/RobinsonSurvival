using System.Collections.Generic;                         // Для списков (на будущее)
using UnityEngine;                                        // Базовые типы Unity (MonoBehaviour и т.п.)
using UnityEngine.UI;                                     // UI-компоненты (Image, Button)
using TMPro;                                              // Текстовые компоненты TextMeshPro
using UnityEngine.EventSystems;                           // Интерфейсы для Drag & Drop

using CColor = CardDef.CardColor;

// Отображение одной UI-карты: новая версия под расширенный префаб.
// Важное: мы больше НЕ меняем размер/шрифт при перемещении в EventWindow — только включаем/выключаем нужные элементы.
public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // ==== ДАННЫЕ КАРТЫ ====
    [Header("Data")]
    public CardDef data;                                   // Дефиниция карты (имя, цвет, artwork, руки/кулаки/глаз, спец-поля)
    public CardInstance instance;                          // Рантайм-экземпляр (лежит в колоде/руке/сбросе)

    // ==== БАЗОВЫЕ UI ССЫЛКИ ====
    [Header("UI Refs (base)")]
    public RectTransform rect;                             // Кэш собственного RectTransform (для Drag-позиции)
    public Image artImage;                                 // Главное изображение карты (арт)
    public TextMeshProUGUI nameText;                       // Название карты
    public TextMeshProUGUI handsText;                      // Число «Ладошек»
    public TextMeshProUGUI fistsText;                      // Число «Кулаков»
    public TextMeshProUGUI eyeText;                        // Число «Глаз» (если хотите показывать)
    public CanvasGroup canvasGroup;                        // Для прозрачности/блокировки Raycast во время Drag

    // ==== МАСКА ДЛЯ ОТРЕЗАНИЯ НИЗА АРТА ====
    [Header("Art crop (RectMask2D)")]
    public RectTransform artMask;                          // Контейнер с RectMask2D, ВНУТРИ которого лежит artImage
    public float handHeight = 309f;                        // Референс высоты карты «в руке» (если стартовая высота 0)
    public float eventCroppedHeight = 86f;                // Высота карты в EventWindow (уменьшенная кликабельная)


    // ==== НОВЫЕ ВИЗУАЛЬНЫЕ СЛОИ ПРЕФАБА ====
    [Header("Top overlays / backgrounds")]
    public Image cardColorImage;                           // Полоса цвета карты (Card_color)
    public Sprite[] cardColorSprites;                      // Спрайты цвета: [0]=Green, [1]=Red, [2]=Blue
    public Image topBlackGradient;                         // Верхний черный градиент (виден только в руке/drag)
    public Image underBlackLine;                           // Тонкая линия под градиентом (видна только в руке/drag)
    public Image eventBackImage;                           // Фон EventWindow (виден только в EventWindow)

    [Header("Bases")]
    public Image handBaseImage;                            // Зона Hand_base (верх, под «руки»)
    public Image fistBaseImage;                            // Зона Fist_base (верх справа)
    public Sprite[] fistBaseSprites;                       // [0] без глаза, [1] есть глаз (выбор по data.eye>0)
    public Image blackFistBaseImage;                       // Чёрная подложка Fist_base (показывать в EventWindow)

    [Header("Icons / numbers")]
    public Image fistIconImage;                            // Иконка кулака (доп. поверх чисел)
    public Sprite[] fistIconSprites;                       // [0] для красн/зелён (fists>1), [1] для синих карт
    public Image shieldImage;                              // Щит — показывать только если зелёная карта и fists == 1

    [Header("Specials icons")]
    public Image brainIcon;                                // Показать, если data.brain > 0
    public Image powerIcon;                                // Показать, если data.power > 0
    public Image speedIcon;                                // Показать, если data.speed > 0

    // ==== КНОПКА «ГЛАЗ» ====
    [Header("Eye button (Blue ability)")]
    public Button eyeDrawButton;                           // Маленькая кнопка «Добрать»
    //public TextMeshProUGUI eyeOnButtonText;                // Подпись на кнопке (например, «Добрать x2»)

    // ==== DRAG ЛОГИКА И СВЯЗЬ С ЗОНАМИ ====
    [Header("Drag & Zones")]
    public EventWindowDropZone ownerZone;                  // Если карта лежит в зоне EventWindow — здесь будет ссылка
    public Canvas dragCanvas;                              // Root Canvas, куда поднимаем карту во время drag
    private Transform originalParent;                      // Исходный родитель (HandPanel), чтобы вернуть, если не положили в зону
    private RectTransform dragCanvasRect;                  // RectTransform Canvas
    private Camera uiCamera;                               // Камера UI (null для Overlay)
    private Vector3 pointerWorldOffset;                    // Смещение «карта-курсор» при захвате

    [Header("Runtime links")]
    public PlayerStatsSimple stats;                        // Для логики кнопки «Глаз» (место в руке/энергия)
    public HandController hand;                            // Чтобы узнать HandCount/лимит

    // Удобные свойства: где карта сейчас
    private bool IsDragging => (dragCanvas != null && transform.parent == dragCanvas.transform); // Сейчас тащим под Canvas?
    private bool IsInPlayArea => (ownerZone != null);     // Находится в зоне EventWindow?
    private bool IsInHand => !IsInPlayArea && !IsDragging;// В руке = не в зоне и не в перетаскивании

    // Внутренние сохранённые размеры для корректного возврата в «руку»
    private Vector2 _cardFullSize;                         // Полный sizeDelta карты «в руке»
    private float _maskFullHeight;                       // Полная высота маски (чтобы восстановить при возврате)
    public float zoneScale = 1f;

    private static bool _lastConfirmOpen;                                  // Кэш последнего состояния модалки (глобально для CardView)


    // === ИНИЦИАЛИЗАЦИЯ ===
    private void Awake()                                   // Стартовые ссылки и настройки
    {
        // Кэш Rect/CanvasGroup
        if (rect == null) rect = GetComponent<RectTransform>();     // Берём свой RectTransform
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>(); // Берём CanvasGroup (если есть)

        // Найдём Canvas (root), камеру UI и его RectTransform
        dragCanvas = GetComponentInParent<Canvas>();                 // Ближайший Canvas
        dragCanvas = dragCanvas != null ? dragCanvas.rootCanvas : dragCanvas; // Корневой Canvas
        dragCanvasRect = dragCanvas != null ? dragCanvas.transform as RectTransform : null; // RectTransform Canvas
        uiCamera = (dragCanvas != null &&
                   (dragCanvas.renderMode == RenderMode.ScreenSpaceCamera || dragCanvas.renderMode == RenderMode.WorldSpace))
                   ? dragCanvas.worldCamera : null;                  // Камера UI (null для Overlay)

        // Арт-картинки — общие параметры
        if (artImage == null) artImage = GetComponent<Image>();      // Если арт на корне — берём этот Image
        if (artImage != null)
        {
            artImage.preserveAspect = true;                          // Не искажать спрайт
            artImage.raycastTarget = true;                           // Карта ловит Raycast (для Drag)
            artImage.color = Color.white;                            // На всякий случай открасим в белый
        }

        // Если маску не указали — попробуем найти RectMask2D среди детей
        if (artMask == null)
        {
            var mask = GetComponentInChildren<RectMask2D>(true);
            if (mask != null) artMask = mask.transform as RectTransform;
        }

        // Зафиксируем исходные размеры для «руки»
        _cardFullSize = rect.sizeDelta;                              // Что сейчас в префабе
        if (_cardFullSize.y <= 1f) _cardFullSize.y = handHeight;     // Подстрахуемся, если 0
        if (artMask != null)
        {
            _maskFullHeight = artMask.rect.height;                   // Текущая высота маски
            if (_maskFullHeight <= 1f) _maskFullHeight = handHeight; // Подстраховка
            // ВАЖНО: якоря маски — к ВЕРХУ, чтобы "резалось снизу"
            artMask.anchorMin = new Vector2(0f, 1f);
            artMask.anchorMax = new Vector2(1f, 1f);
            artMask.pivot = new Vector2(0.5f, 1f);
        }

        // Кнопка «Глаз» — подпишем обработчик
        if (eyeDrawButton != null)
            eyeDrawButton.onClick.AddListener(OnEyeButtonClicked);

        // Ссылки на статы/руку (если не назначены вручную)
        if (stats == null) stats = FindFirstObjectByType<PlayerStatsSimple>(); // Поищем PlayerStatsSimple
        if (hand == null) hand = HandController.Instance;                      // Возьмём синглтон руки
    }

    private void OnEnable()                                // Подписки при активации
    {
        // Когда меняются «кучи»/рука — пересчитываем видимость кнопки «Глаз»
        if (hand != null) hand.OnPilesChanged += UpdateEyeButtonVisibility;
        if (stats != null) stats.OnStatsChanged += UpdateEyeButtonVisibility;
    }

    private void OnDisable()                               // Снятие подписок
    {
        if (hand != null) hand.OnPilesChanged -= UpdateEyeButtonVisibility;
        if (stats != null) stats.OnStatsChanged -= UpdateEyeButtonVisibility;
    }

    private void Update()                                   // Блокировка кнопки «Глаз» поверх модалок/окон
    {
        if (eyeDrawButton == null) return;                                 // Нет кнопки — выходим
        bool now = ConfirmModalUI.IsOpen;                                  // Считываем текущее состояние модалки подтверждения
        if (now != _lastConfirmOpen)                                       // Было ли изменение состояния с прошлого кадра?
        {
            _lastConfirmOpen = now;                                        // Обновляем кэш
            if (now)                                                       // Если модалка открылась
            {
                if (eyeDrawButton.gameObject.activeSelf)                   // Если кнопка сейчас видна
                    eyeDrawButton.gameObject.SetActive(false);             // Прячем кнопку «Глаз» на время модалки
                return;                                                    // Больше ничего не делаем
            }
            // Модалка закрылась — можно пересчитать видимость кнопки
            UpdateEyeButtonVisibility();                                   // Локальные правила показа «Глаза»
            return;                                                        // И выходим
        }
    }


    // ====== ПРИВЯЗКА ДАННЫХ (CardInstance → UI) ======
    public void Bind(CardInstance inst)                    // Вызывайте при создании UI-карты
    {
        instance = inst;                                   // Запоминаем рантайм-экземпляр
        data = inst != null ? inst.def : null;             // Берём дефиницию (удобно)

        // Тексты
        if (nameText != null) nameText.text = data != null ? data.displayName : "";
        if (handsText != null) handsText.text = data != null ? $"{data.hands}" : "";
        if (fistsText != null) fistsText.text = (data != null && ShouldShowFistsNumber(data)) ? $"{data.fists}" : "";

        if (eyeText != null)                                       // Число «глаз» показывайте по желанию
        {
            if (data != null && data.eye > 0) { eyeText.gameObject.SetActive(true); eyeText.text = $"{data.eye}"; }
            else { eyeText.text = ""; eyeText.gameObject.SetActive(false); }
        }

        // Арт
        if (artImage != null) artImage.sprite = data != null ? data.artwork : null;

        // Статика: полоса цвета, базы, иконки-спецполя
        RefreshStaticVisuals();                        // Отрисуем элементы, зависящие от параметров карты

        // Динамика: рука/drag/EventWindow — какие слои видны сейчас
        RefreshLocationVisuals();                      // Отрисуем состояния видимости по месту нахождения

        // Кнопка «Глаз»
        //if (eyeOnButtonText != null) eyeOnButtonText.text = (data != null && data.eye > 0) ? $"Добрать x{data.eye}" : "";
        UpdateEyeButtonVisibility();                   // Пересчитаем видимость кнопки
    }

    // Проверяем, лежит ли карта в зоне боя (ищем CombatDropZone среди родителей)
    private bool IsInCombatZone()                                         // Метод: true, если родительская иерархия содержит CombatDropZone
    {
        // Если сейчас тащим под Canvas — это «промежуточное» состояние, считаем что НЕ в зоне
        if (dragCanvas != null && transform.parent == dragCanvas.transform) // При drag parent = rootCanvas
            return false;                                                   // Значит, не считаем «в бою»

        // Идём вверх по иерархии и ищем компонент CombatDropZone
        Transform t = transform.parent;                                     // Начинаем с родителя
        while (t != null)                                                   // Пока не дошли до корня
        {
            // Пытаемся взять компонент зоны боя
            var dz = t.GetComponent<CombatDropZone>();                      // Есть ли CombatDropZone на этом уровне?
            if (dz != null) return true;                                    // Нашли — значит, карта в боевой зоне
            t = t.parent;                                                   // Поднимаемся выше
        }
        return false;                                                       // Не нашли — не в боевой зоне
    }

    // === Правило: показывать число кулаков только если
    // - красная карта; ИЛИ
    // - зелёная карта и fists > 1; иначе скрыть
    private bool ShouldShowFistsNumber(CardDef d)
    {
        if (d == null) return false;                   // Нет данных
        if (d.color == CColor.Red) return true;     // Красная — всегда
        if (d.color == CColor.Green && d.fists > 1) return true; // Зелёная и >1
        return false;                                  // Иначе — не показывать
    }

    // === Отрисовать элементы, зависящие только от статических атрибутов карты (цвет/параметры) ===
    private void RefreshStaticVisuals()
    {
        // Полоса цвета
        if (cardColorImage != null)
        {
            cardColorImage.enabled = (data != null);   // Показываем, если есть данные
            if (data != null && cardColorSprites != null && cardColorSprites.Length >= 3)
            {
                int idx = data.color == CColor.Green ? 0 : (data.color == CColor.Red ? 1 : 2); // 0/1/2
                cardColorImage.sprite = cardColorSprites[idx]; // Выбираем спрайт
            }
        }

        // Fist_base: спрайт зависит от того, есть ли «глаз» (use [0]=noEye, [1]=hasEye)
        if (fistBaseImage != null)
        {
            fistBaseImage.enabled = (data != null);
            if (data != null && fistBaseSprites != null && fistBaseSprites.Length >= 2)
            {
                fistBaseImage.sprite = fistBaseSprites[data.eye > 0 ? 1 : 0]; // Глаз? → вторая картинка
            }
        }

        // Hand_base просто включаем, если есть (всегда есть зона рук)
        if (handBaseImage != null) handBaseImage.enabled = (data != null);

        // Число кулаков (fistsText) — логика показа/скрытия
        if (fistsText != null)
        {
            bool showFistsNum = (data != null && ShouldShowFistsNumber(data));
            fistsText.gameObject.SetActive(showFistsNum);
            if (showFistsNum) fistsText.text = $"{data.fists}";
        }

        // Иконка «fist»:
        // - показывать, если (красная) ИЛИ (зелёная и fists>1) → sprite[0]
        // - показывать, если (синяя) → sprite[1]
        // - иначе скрыть
        if (fistIconImage != null)
        {
            bool show =
                (data != null) &&
                (
                    (data.color == CColor.Red) ||
                    (data.color == CColor.Green && data.fists > 1) ||
                    (data.color == CColor.Blue)
                );

            fistIconImage.enabled = show;
            if (show && fistIconSprites != null && fistIconSprites.Length >= 2)
            {
                Sprite s =
                    (data.color == CColor.Blue) ? fistIconSprites[1] : fistIconSprites[0];
                fistIconImage.sprite = s;
            }
        }

        // Щит — только если зелёная карта и fists == 1
        if (shieldImage != null)
        {
            bool showShield = (data != null && data.color == CColor.Green && data.fists == 1);
            shieldImage.enabled = showShield;
        }

        //Спец - иконки(мозг / сила / скорость) — показываем, если соответствующее значение > 0
        if (brainIcon != null) brainIcon.enabled = (data != null && data.brain > 0);
        if (powerIcon != null) powerIcon.enabled = (data != null && data.power > 0);
        if (speedIcon != null) speedIcon.enabled = (data != null && data.speed > 0);
    }

    // === Отрисовать элементы, зависящие от МЕСТОПОЛОЖЕНИЯ карты (рука/drag/EventWindow) ===
    public void RefreshLocationVisuals()
    {
        // В руке ИЛИ во время перетаскивания: показываем верхний градиент и линию
        bool showHandOverlays = IsInHand || IsDragging;
        bool inCombatZone = IsInCombatZone();                                   // Проверяем, лежит ли карта в зоне боя

        if (topBlackGradient != null) topBlackGradient.enabled = showHandOverlays;
        if (underBlackLine != null) underBlackLine.enabled = showHandOverlays;

        // В EventWindow: показываем Event_back и чёрную подложку Fist_base
        if (eventBackImage != null) eventBackImage.enabled = IsInPlayArea || inCombatZone;
        if (blackFistBaseImage != null)
        {
            bool showBlackBase = IsInPlayArea || inCombatZone;                      // Показывать и в событии, и в бою
            blackFistBaseImage.enabled = showBlackBase;                             // Включаем/выключаем отображение

            if (showBlackBase)                                                     // Если подложку показываем
            {
                var rt = blackFistBaseImage.rectTransform;                          // Берём её RectTransform (UI-трансформ)
                Vector2 ap = rt.anchoredPosition;                                   // Текущая анкерная позиция
                bool shiftForEvent = false;                              // Флаг «сдвигать в событии?»
                // если карта лежит в зоне события и зона требует Fists/Eye — решаем, нужен ли сдвиг 
                if (IsInPlayArea && ownerZone != null)                   // Если карта действительно в зоне события
                {
                    shiftForEvent = ownerZone.ShouldShiftBlackBaseFor(this); // Узнаём у зоны, требует ли сдвиг
                }
                bool needShift = inCombatZone || shiftForEvent;          // Сдвигаем либо в бою, либо при требовании зоны
                ap.x = needShift ? -62f : 0f;                                    // В бою смещение -62 по X, в событии 0
                rt.anchoredPosition = ap;                                           // Применяем новое смещение
                                                                                    // Примечание: anchoredPosition корректно смещает элемент в UI-пространстве Canvas.
            }
        }

        if (IsInPlayArea || inCombatZone)                                       // Если карта в PlayArea события ИЛИ в боевой зоне
            SetToEventCroppedMode();                                          // Делаем «урезанную» высоту (арт обрезан снизу)
        else                                                                    // Иначе (рука или drag)
            SetToHandSize();                                                    // Полная высота карты (как в руке)
    }

    // === РЕЖИМЫ ВЫСОТЫ И ОБРЕЗКИ ===

    // Восстановить полную высоту «руки» и полную высоту маски (арт виден целиком)
    public void SetToHandSize()
    {
        // Размер кликабельной карты
        rect.sizeDelta = new Vector2(_cardFullSize.x, _cardFullSize.y);
        rect.localScale = Vector3.one * zoneScale;

        // Маска арта — на полную высоту
        if (artMask != null)
        {
            var sz = artMask.sizeDelta;
            sz.y = _maskFullHeight;
            artMask.sizeDelta = sz;
            artMask.anchoredPosition = new Vector2(0f, 0f); // при верхних якорях 0 — у верхней кромки
        }
    }

    // Включить «урезанный» режим для EventWindow: карта ниже, арт обрезан снизу
    public void SetToEventCroppedMode()
    {
        // Уменьшаем кликабельную высоту карты
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, eventCroppedHeight);

        // Сужаем высоту маски — "отрезаем низ" арта
        if (artMask != null)
        {
            var sz = artMask.sizeDelta;
            sz.y = eventCroppedHeight;       // маска такой же высоты
            artMask.sizeDelta = sz;
            // якоря/пивот уже заданы к верху в Awake()
            artMask.anchoredPosition = new Vector2(0f, 0f); // при верхних якорях 0 — у верхней кромки
        }
    }

    // === Видимость кнопки «Глаз» ===
    
    private void UpdateEyeButtonVisibility()
    {
        if (eyeDrawButton == null) return;                  // Нет кнопки — выходим

        // Карта вообще имеет «Глаз»?
        bool hasEye = (data != null && data.eye > 0);

        // Где находится карта сейчас:
        bool isDraggingNow = (rect != null && dragCanvas != null && transform.parent == dragCanvas.transform);
        bool inPlayArea = (ownerZone != null);           // если карта лежит в зоне EventWindow
        bool inCombat = IsInCombatZone();                                   // Находится в зоне боя (любой CombatDropZone)?


        // «В руке» теперь считаем так: НЕ в зоне события, НЕ в боевой зоне и НЕ тащим
        bool inHand = !inPlayArea && !inCombat && !isDraggingNow;    // считаем «в руке», если не в зоне и не тащим

        // Если карта в зоне события или тащится — кнопку не показываем в любом случае
        if (!hasEye || !inHand)
        {
            if (eyeDrawButton.gameObject.activeSelf)                            // Если кнопка видна
                eyeDrawButton.gameObject.SetActive(false);                      // Скрываем
            return;
        }

        // Лимиты руки
        int maxHand = HandController.Instance != null ? HandController.Instance.maxHand : 7; // Лимит карт в руке
        int handCount = HandController.Instance != null ? HandController.Instance.HandCount : 0; // Сколько сейчас в руке

        // Сколько карт лежит в PlayArea событий (если окно открыто)
        int playCount = 0;                                                       // Инициализируем
        var ew = EventWindowUI.Get();                                            // Пытаемся взять активное окно
        if (ew != null && ew.dropZone != null && ew.dropZone.placedCards != null) // Если есть зона и список карт
            playCount = ew.dropZone.placedCards.Count;                           // Берём количество карт в PlayArea

        // --- NEW: учитываем карты, лежащие в боевых зонах, если Combat_screen открыт ---
        int combatCount = CombatController.Instance ? CombatController.Instance.CardsInZones : 0; // Быстро: берём счётчик из контроллера
        //int combatCount = 0;
        // Счетчик карт в боевых зонах
        //var zones = Object.FindObjectsByType<CombatDropZone>(                     // ✅ НОВОЕ: современный API
        //    FindObjectsInactive.Include,                                          //    Включаем неактивные объекты
        //    FindObjectsSortMode.None                                              //    Без сортировки (быстрее)
        //    );                   // Ищем все боевые зоны (в т.ч. неактивные)
        //foreach (var z in zones)                                                 // Перебираем найденные зоны
        //{
        //    if (z == null || !z.gameObject.activeInHierarchy) continue;          // Пропускаем неактивные/пустые
        //    combatCount += z.GetComponentsInChildren<CardView>(false).Length;    // Считаем CardView как «1 карта»
        //}

        // Есть ли вместимость с учётом карт «на столе» (событие + бой)
        bool hasCapacityConsideringBoard = (handCount + playCount + combatCount) < maxHand;

        // Итог: в руке, есть «Глаз», ConfirmModal не открыт (фильтр в Update), есть вместимость с учётом PlayArea
        bool visible = hasEye && hasCapacityConsideringBoard;

        // Применяем
        if (eyeDrawButton.gameObject.activeSelf != visible)
            eyeDrawButton.gameObject.SetActive(visible);
    }

    // === Клик по кнопке «Глаз» (теперь через AbilityRunner) ===
    private void OnEyeButtonClicked()
    {
        if (instance == null || data == null || data.eye <= 0) return;       // Без данных — ничего не делаем
        // Запускаем первую способность с триггером ManualActivate (по вашей схеме 83–84)
        AbilityRunner.RunManualAbility(instance);           // Стоимости, затем эффекты
        // Внутри эффектов: DiscardSelf → DrawCards_Eye и т.д.
    }

    // === DRAG & DROP ===
    public void OnBeginDrag(PointerEventData eventData)     // Начало перетаскивания
    {
        // Если стартуем ИЗ зоны — вернуть по умолчанию нужно НЕ в зону, а в руку:
        if (ownerZone != null)
        {
            // 1) сообщаем зоне, что карта покидает её (список/счётчик/кнопка "Разыграть")
            var z = ownerZone;
            ownerZone = null;                        // карта больше не в зоне (визуалы тоже переключатся)
            z.RemoveCard(this);                      // зона сама уменьшит currentHands и обновит UI

            // 2) fallback-родитель на случай, если никуда не уронят — это HandPanel
            originalParent = (HandController.Instance != null && HandController.Instance.handPanel != null)
                ? HandController.Instance.handPanel
                : transform.parent;

            // 3) в руке карта отображается полной высоты — сразу вернуть полный режим
            SetToHandSize();
            rect.localScale = Vector3.one * zoneScale;                                    // Сразу возвращаем нормальный масштаб (после 0.7 в зоне боя)
        }
        else
        {
            var combatZone = transform.parent ? transform.parent.GetComponent<CombatDropZone>() : null; // Пытаемся взять зону боя у родителя
            if (combatZone != null)                                            // Если карта лежала в боевой зоне
            {
                if (CombatController.Instance != null)                                // Если есть контроллер боя
                    CombatController.Instance.NotifyCardLeftZone(this);               // Сообщаем: карта покинула боевую зону

                // Сообщаем блоку стычки, что карта покидает эту зону (чтобы пересчитать суммы)
                if (combatZone.block != null)                                   // Если есть владелец
                    combatZone.block.OnCardRemovedFromZone(this, combatZone.zoneType); // Обновим Fist/Shield/Wounds

                // Для «fallback» при отмене дропа — возвращать в руку
                originalParent = (HandController.Instance != null && HandController.Instance.handPanel != null)
                    ? HandController.Instance.handPanel                           // Возвращать в HandPanel
                    : transform.parent;                                           // Или в текущего родителя (на всякий случай)

                // В руке карта должна быть «полной высоты»
                SetToHandSize();                                                  // Сразу вернём высоту «как в руке»
            }
            else
            {
                // Иначе — обычный старт из руки: просто запомним исходного родителя
                originalParent = transform.parent;                                // Запоминаем родителя
            }
        }

        // Переводим экранные координаты курсора в мировые координаты плоскости Canvas
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            dragCanvasRect, eventData.position, uiCamera, out var pointerWorld);

        pointerWorldOffset = rect.position - pointerWorld;      // Смещение «карта-курсор»
        transform.SetParent(dragCanvas.transform, true);        // Поднять под Canvas
        rect.SetAsLastSibling();                                // Нарисовать поверх
        rect.position = pointerWorld + pointerWorldOffset;      // Поставить под курсор

        if (canvasGroup != null)                                // Пропуск лучей и подкраска
        {
            canvasGroup.blocksRaycasts = false;
            //canvasGroup.alpha = 0.9f;
        }

        if (eyeDrawButton != null) eyeDrawButton.gameObject.SetActive(false); // Скрыть кнопку на время drag

        RefreshLocationVisuals();                                   // Слои: рука → drag
        UpdateEyeButtonVisibility();                                 // Кнопка могла измениться
    }

    public void OnDrag(PointerEventData eventData)          // Во время перетаскивания
    {
        if (rect == null) return;                           // Защита
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            dragCanvasRect, eventData.position, uiCamera, out var pointerWorld); // Мировая точка под курсором
        rect.position = pointerWorld + pointerWorldOffset;  // Двигаем карту
    }

    public void OnEndDrag(PointerEventData eventData)       // Конец перетаскивания
    {
        if (canvasGroup != null)                            // Возврат интерактивности
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }

        // Если зону не поймали — возвращаем в руку (к исходному родителю)
        if (transform.parent == dragCanvas.transform)
        {
            transform.SetParent(originalParent, false);     // Возвращаем под HandPanel
            // ownerZone не меняем — он остался null (в руке)
            rect.localScale = Vector3.one;                  // Масштаб единичный в руке (на всякий случай)
        }

        RefreshLocationVisuals();                           // Перерисуем слои (рука/зона)
        UpdateEyeButtonVisibility();                        // Кнопка «Глаз» могла снова стать доступной
    }

    // === Важное: когда карту перекладывают программно (например, DropZone.SetParent), мы хотим пересчитать визуалы ===
    private void OnTransformParentChanged()
    {
        // Это срабатывает, когда EventWindowDropZone меняет parent на контейнер зоны
        // или мы возвращаем карту в HandPanel. ownerZone выставляется самой зоной.
        RefreshLocationVisuals();                           // Обновим слои (градиенты/фон/чёрную базу)
        UpdateEyeButtonVisibility();                        // Кнопка «Глаз» тоже может поменяться
    }
}

