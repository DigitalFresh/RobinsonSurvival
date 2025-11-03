using UnityEngine;                   // Базовые Unity-типы (MonoBehaviour, Sprite)
using UnityEngine.UI;                // Компоненты UI (Image, Button)
using TMPro;                         // Текстовые компоненты TextMeshPro
using System.Collections;            // ДЛЯ IEnumerator  // --- ADDED START ---
using System.Collections.Generic;    // для List

// Окно события: показывает данные EventSO и позволяет подтвердить/отменить
public class EventWindowUI : MonoBehaviour
{
    public static EventWindowUI Instance;        // Синглтон-ссылка для удобного доступа из других скриптов
    public static EventWindowUI Get() => Instance ?? (Instance = FindFirstObjectByType<EventWindowUI>(FindObjectsInactive.Include));

    [Header("Refs")]
    public CanvasGroup canvasGroup;              // CanvasGroup, чтобы быстро включать/выключать окно и блокировать клики сквозь него
    public Image iconImage;                      // UI-изображение для иконки события
    public TextMeshProUGUI titleText;            // Текст заголовка события
    public TextMeshProUGUI descriptionText;      // Текст описания события

    [Header("Main cost block")]
    public Image hexBack;                  // Image "Hex" (смена цвета по типу)
    public Sprite[] hexBackByCostType;     // [✋=0, 👊=1, 👁=2]
    public Image iconHex;                  // Image "icon_Hex" (рука/кулак/глаз)
    public Sprite[] iconHexByCostType;     // [✋,👊,👁]
    public TextMeshProUGUI amountText;     // Text "amount"

    [Header("Cost text colors (main amount)")]
    public Color costTextColorHands = Color.white;                         // ✋ зелёный → белый текст
    public Color costTextColorFists = new Color(0.90f, 0.15f, 0.15f, 1f);  // 👊 красный → красный текст
    public Color costTextColorEye = new Color(0.20f, 0.50f, 1.00f, 1f);  // 👁 синий  → синий текст

    [Header("Additional costs")]
    public Image[] adCostIcons;            // массив из 3 Image "ad_cost" (по индексу 0..2) public Image[] adCostIcons; 
    public Sprite[] adCostSprites;         // [Brain, Power, Speed]

    [Header("Penalties (Req_back)")]
    public GameObject reqBackPanel;        // сам бэк (чтобы включать/выключать)
    public Image[] penaltyIcons;           // Cost_1..Cost_4
    public Sprite[] penaltySprites;        // [Hunger,Thirst,Energy,Health]

    [Header("Rewards panel")]
    public RewardItemUI[] rewardItems;     // res_1..res_4

    [Header("Alternative rewards (two-choice)")]
    public GameObject altRewardsRoot;   // общий контейнер альтернативного режима (2 слота + разделитель)
    public RewardItemUI altRewardA;     // левый слот
    public RewardItemUI altRewardB;     // правый слот
    public Image altDivider;            // картинка-разделитель (полоса между слотами)

    private int selectedAltIndex = 0;   // по умолчанию выбираем первую награду (левую)

    public Button confirmButton;                 // Кнопка подтверждения (разыграть/принять)
    public Button cancelButton;                  // Кнопка отмены/закрытия

    // Текущий контент, который окно показывает
    private EventSO currentEvent;                // Текущее событие (данные из ScriptableObject)
    private HexTile sourceTile;                  // Тайл, с которого открыто окно (чтобы знать, что изменять при подтверждении)

    [Header("Play Area")]                        // Зона розыгрыша
    public EventWindowDropZone dropZone;         // Ссылка на зону, куда кладут карты
    public TextMeshProUGUI hintText;       // динамическая подсказка

    // ================== BARRIERS: UI в окне события ==================
    [Header("Barriers (optional)")]
    [SerializeField] private GameObject barriersPanel;                     // панель в окне
    [SerializeField] private UnityEngine.UI.Image[] barrierSlots = new UnityEngine.UI.Image[3];
    [SerializeField] private Sprite bar1Sprite;
    [SerializeField] private Sprite bar3Sprite;

    [Header("Amount font sizes")]
    [SerializeField] private int amountFontSmall = 40;   // когда значение >= 10
    [SerializeField] private int amountFontLarge = 62;   // когда значение < 10
    [SerializeField] private int amountSwitchThreshold = 10;


    private void Awake()                         // Инициализация при создании объекта
    {
        Instance = this;                         // Сохраняем ссылку на себя (простой синглтон)
        HideImmediate();                         // Прячем окно сразу при старте (без анимации)
        // Подписываем кнопки на методы-обработчики
        confirmButton.onClick.AddListener(OnConfirmClicked); // Подтверждение события
        cancelButton.onClick.AddListener(OnCancelClicked);   // Закрытие окна без выполнения
        if (dropZone) dropZone.OnZoneChanged += OnZoneChanged;
    }
    private void OnDestroy()
    {
        if (dropZone) dropZone.OnZoneChanged -= OnZoneChanged;
    }

    // Публичный метод: показать окно для конкретного события и тайла
    public void Show(EventSO ev, HexTile tile)
    {
        currentEvent = ev;                       // Запоминаем событие
        sourceTile = tile;                       // Запоминаем тайл-источник

        // Заполняем визуальные поля данными
        titleText.text = ev != null ? ev.eventName : "Событие";  // Заголовок — имя события
        descriptionText.text = ev != null ? ev.description : ""; // Описание — из SO
        iconImage.sprite = ev != null ? ev.icon : null;           // Ставим иконку (если есть)

        // главная стоимость
        int effective = GetEffectiveMainCost();
        if (amountText)
        {
            amountText.text = effective.ToString();
            ApplyAmountFont(amountText, effective);     // <-- добавь эту строчку
        }

        int costIdx = ev ? (int)ev.mainCostType : 0;
        if (hexBack && hexBackByCostType != null && hexBackByCostType.Length >= 3)
            hexBack.sprite = hexBackByCostType[costIdx];
        if (iconHex && iconHexByCostType != null && iconHexByCostType.Length >= 3)
            iconHex.sprite = iconHexByCostType[costIdx];
        //  цвет текста amount по типу стоимости ---
        if (amountText && ev != null)                                        // Если есть текст и валидное событие
            amountText.color = GetCostTextColor(ev.mainCostType);            // Поставить цвет

        if (dropZone && ev != null)
        {
            // ✋/👊/👁 + требуемое количество = ЭФФЕКТИВНАЯ стоимость
            dropZone.SetupRequirementTyped(ev.mainCostType, effective);
        }

        DrawBarriers(tile != null ? tile.Barriers : null);

        // доп.стоимости – покажем до 3 значков
        for (int i = 0; i < adCostIcons.Length; i++)
        {
            if (!adCostIcons[i]) continue;
            if (ev != null && i < ev.additionalCosts.Count)
            {
                var a = ev.additionalCosts[i];
                adCostIcons[i].gameObject.SetActive(true); //enabled = true;
                if (adCostSprites != null && adCostSprites.Length >= 3)
                    adCostIcons[i].sprite = adCostSprites[(int)a.tag];
            }
            else adCostIcons[i].gameObject.SetActive(false);// = false;
        }

        // штрафы
        bool hasPenalties = (ev != null && ev.penalties != null && ev.penalties.Count > 0);
        if (reqBackPanel) reqBackPanel.SetActive(hasPenalties);
        for (int i = 0; i < penaltyIcons.Length; i++)
        {
            if (!penaltyIcons[i]) continue;
            if (hasPenalties && i < ev.penalties.Count)
            {
                var p = ev.penalties[i];
                penaltyIcons[i].gameObject.SetActive(true); //enabled = true;
                if (penaltySprites != null && penaltySprites.Length >= 4)
                    penaltyIcons[i].sprite = penaltySprites[(int)p.stat];
            }
            else penaltyIcons[i].gameObject.SetActive(false); //enabled = false;
        }

        // --- НАГРАДЫ ---
        bool useAlt = (ev != null && ev.rewardsAreAlternative);

        // Обычные 4 слота (как раньше)
        if (!useAlt)
        {
            if (altRewardsRoot) altRewardsRoot.SetActive(false); // скрываем режим альтернатив
            for (int i = 0; i < rewardItems.Length; i++)
            {
                var item = rewardItems[i];
                if (!item) continue;
                if (ev != null && i < ev.rewards.Count) { item.gameObject.SetActive(true); item.Bind(ev.rewards[i]); }
                else item.gameObject.SetActive(false);
            }
        }
        else
        {
            // Режим «2 альтернативы»
            if (altRewardsRoot) altRewardsRoot.SetActive(true);
            // Скрываем/не используем обычные 4 слота
            for (int i = 0; i < rewardItems.Length; i++)
                if (rewardItems[i]) rewardItems[i].gameObject.SetActive(false);

            // биндим левую/правую альтернативу (если есть)
            if (altRewardA)
            {
                if (ev.alternativeRewards != null && ev.alternativeRewards.Count > 0)
                { altRewardA.gameObject.SetActive(true); altRewardA.Bind(ev.alternativeRewards[0]); }
                else altRewardA.gameObject.SetActive(false);

                // обработчик клика (если назначен selectButton)
                if (altRewardA.selectButton)
                {
                    altRewardA.selectButton.onClick.RemoveAllListeners();
                    altRewardA.selectButton.onClick.AddListener(() => { selectedAltIndex = 0; UpdateAltSelectionFrames(); UpdateConfirmInteractable(); });
                }
            }

            if (altRewardB)
            {
                if (ev.alternativeRewards != null && ev.alternativeRewards.Count > 1)
                { altRewardB.gameObject.SetActive(true); altRewardB.Bind(ev.alternativeRewards[1]); }
                else altRewardB.gameObject.SetActive(false);

                if (altRewardB.selectButton)
                {
                    altRewardB.selectButton.onClick.RemoveAllListeners();
                    altRewardB.selectButton.onClick.AddListener(() => { selectedAltIndex = 1; UpdateAltSelectionFrames(); UpdateConfirmInteractable(); });
                }
            }


            // выставим стартовую подсветку выбора (по умолчанию — левая)
            selectedAltIndex = 0;
            UpdateAltSelectionFrames();
        }

        //// награды

        UpdateConfirmInteractable();                              // Обновим доступность кнопки

        // Делаем окно видимым и интерактивным
        canvasGroup.alpha = 1f;                                   // Полная видимость
        canvasGroup.blocksRaycasts = true;                        // Блокируем клики сквозь окно
        canvasGroup.interactable = true;                          // Разрешаем взаимодействие с UI
        gameObject.SetActive(true);                               // Активируем объект

        ModalGate.Acquire(this); // <— включили
    }

    private void OnZoneChanged()
    {
        UpdateConfirmInteractable();
    }

    // === Проверка условий, подсказки и подсветки наград ===
    public void UpdateConfirmInteractable()
    {
        if (confirmButton == null || dropZone == null) return;
        if (currentEvent == null) { confirmButton.interactable = false; return; }

        // 1) главная стоимость
        bool mainOK = false;
        int have = 0;
        switch (currentEvent.mainCostType)
        {
            case CostType.Hands: have = dropZone.currentHands; break;
            case CostType.Fists: have = dropZone.currentFists; break;
            case CostType.Eye: have = dropZone.currentEye; break;
        }
        mainOK = (have >= GetEffectiveMainCost());

        // 2) обязательные доп.стоимости (если additionalMandatory)
        bool addOK = true;
        if (currentEvent.additionalMandatory)
        {
            foreach (var a in currentEvent.additionalCosts)
            {
                int val = a.tag switch
                {
                    AddTag.Brain => dropZone.currentBrain,
                    AddTag.Power => dropZone.currentPower,
                    AddTag.Speed => dropZone.currentSpeed,
                    _ => 0
                };
                if (val < a.amount) { addOK = false; break; }
            }
        }

        bool canConfirm = mainOK && addOK;
        confirmButton.interactable = canConfirm;

        // 3) подсказка
        if (hintText)
        {
            bool useAlt = (currentEvent != null && currentEvent.rewardsAreAlternative);
            if (!mainOK)
                hintText.text = "Добавьте в эту зону карты с параметрами достаточными для выполнения выбранного действия.";
            else if (!addOK)
                hintText.text = "Требуются дополнительные параметры (мозг/сила/скорость).";
            else
                hintText.text = useAlt
                    ? "Выберите одну из двух наград и нажмите «ОК», чтобы получить выбранную."
                    : "Нажмите «ОК», чтобы выполнить действие.";
        }

        // 4) подсветка наград (гейт по дополнительному тегу) — ТОЛЬКО для обычного режима
        if (!(currentEvent != null && currentEvent.rewardsAreAlternative))
        {
            for (int i = 0; i < rewardItems.Length; i++)
            {
                var item = rewardItems[i];
                if (!item) continue;
                bool ok = true;
                if (currentEvent != null && i < currentEvent.rewards.Count)
                {
                    var r = currentEvent.rewards[i];
                    if (r.gatedByAdditional)
                    {
                        int haveTag = r.requiredTag switch
                        {
                            AddTag.Brain => dropZone.currentBrain,
                            AddTag.Power => dropZone.currentPower,
                            AddTag.Speed => dropZone.currentSpeed,
                            _ => 0
                        };
                        ok = (haveTag >= r.requiredAmount);
                    }
                }
                item.SetGateState(ok);
            }
        }
    }

    // Спрятать окно (мягко)
    public void Hide()
    {
        canvasGroup.alpha = 0f;                                    // Делаем невидимым
        canvasGroup.blocksRaycasts = false;                         // Не блокируем клики
        canvasGroup.interactable = false;                           // Отключаем интерактив
        //gameObject.SetActive(false);                                // Отключаем объект
        currentEvent = null;                                        // Сбрасываем ссылку на событие
        //sourceTile = null;                                          // Сбрасываем ссылку на тайл

        ModalGate.Release(this); // <— выключил

        // после закрытия окна сообщаем руке, что окружение могло разблокироваться
        HandController.Instance?.RaisePilesChanged();
    }

    // Спрятать окно (в инициализации)
    private void HideImmediate()
    {
        canvasGroup.alpha = 0f;                                    // Невидимое
        canvasGroup.blocksRaycasts = false;                         // Без блокировок
        canvasGroup.interactable = false;                           // Не интерактивно
        gameObject.SetActive(false);                                // Объект выключен
    }

    private Color GetCostTextColor(CostType t)                             // Хелпер выбора цвета
    {
        switch (t)
        {
            case CostType.Fists: return costTextColorFists;                // 👊
            case CostType.Eye: return costTextColorEye;                  // 👁
            case CostType.Hands:
            default: return costTextColorHands;                            // ✋
        }
    }


    // Нажата кнопка подтверждения (разыграть/принять)
    private void OnConfirmClicked()
    {
        if (currentEvent == null) { Hide(); return; }
        //Debug.Log(sourceTile);
        // 1) применить награды (учитывая гейты)
        var stats = FindFirstObjectByType<PlayerStatsSimple>();
        var deck = FindFirstObjectByType<DeckController>();   // нужна ссылка на колоду
        var inv = InventoryController.Instance; //  инвентарь для ресурсов
        // Ставим сюда список ПОЛУЧЕННЫХ карт (CardDef), чтобы потом показать их полноценными UICard
        var awardedCardDefs = new List<CardDef>();
        var resourceRewardsToAnimate = new List<EventSO.Reward>(); // Соберём ресурсные награды для полёта
        var statRestoresToAnimate = new List<(EventSO.PlayerStat stat, int amount)>(); // ресторы статов для анимации
        var statPenaltiesToAnimate = new List<(StatType stat, int amount)>();          // штрафы статов для анимации
                                                                                       // Флаги: что нужно показывать после вычисления наград/штрафов
        bool needAwardedCardsModal = false; // нужна ли модалка «получены карты» (awardedCardDefs != null && awardedCardDefs.Count > 0);
        bool needChooseFinalModal = false; // при необходимости поставьте true там, где это уместно

        var cardsToAnimateToHand = new List<CardInstance>();     // Эти карты полетят в руку (есть место)
        var cardsOverflowToDeckTop = new List<CardInstance>();   // Эти карты уйдут на верх колоды (места в руке нет)

        if (stats != null)
        {
            bool useAlt = currentEvent.rewardsAreAlternative;

            // helper-локалка: применить одну награду (с учётом gating)
            System.Action<EventSO.Reward, int> ApplyOneReward = (r, slotIndex) =>
            {
                if (r == null) return;

                bool grant = true;
                if (r.gatedByAdditional)
                {
                    int haveTag = r.requiredTag switch
                    {
                        AddTag.Brain => dropZone.currentBrain,
                        AddTag.Power => dropZone.currentPower,
                        AddTag.Speed => dropZone.currentSpeed,
                        _ => 0
                    };
                    grant = (haveTag >= r.requiredAmount);
                }

                // В обычном режиме, если не выполнили гейт — красним рамку слота
                if (!useAlt && !grant)
                {
                    if (slotIndex >= 0 && slotIndex < rewardItems.Length && rewardItems[slotIndex])
                        rewardItems[slotIndex].SetGateState(false);
                    return; // просто не выдаём эту награду
                }

                if (!grant) return; // в альтернативном режиме тоже просто не выдаём

                switch (r.type)
                {
                    case EventSO.RewardType.Resource:
                        {
                            resourceRewardsToAnimate.Add(r);             // НЕ добавляем в инвентарь сейчас, копим для анимации
                            break;
                        }

                    case EventSO.RewardType.RestoreStat:
                        {
                            int val = Mathf.Max(1, r.restoreAmount);
                            statRestoresToAnimate.Add((r.stat, val));
                            switch (r.stat)
                            {
                                case EventSO.PlayerStat.Hunger: stats.Eat(val); break;
                                case EventSO.PlayerStat.Thirst: stats.Drink(val); break;
                                case EventSO.PlayerStat.Energy: stats.GainEnergy(val); break;
                                case EventSO.PlayerStat.Health: stats.Heal(val); break;
                            }
                            break;
                        }

                    case EventSO.RewardType.NewCard:
                        {
                            if (r.cardDef == null || deck == null) break;

                            int count = Mathf.Max(1, r.cardCount);
                            for (int k = 0; k < count; k++)
                            {
                                var inst = new CardInstance(r.cardDef);

                                int handCountProjected = HandController.Instance ? HandController.Instance.HandCount + cardsToAnimateToHand.Count : cardsToAnimateToHand.Count; // Проекция
                                int maxHand = HandController.Instance ? HandController.Instance.maxHand : 7; // Fallback 10
                                if (handCountProjected < maxHand)                                 // Если ещё есть место
                                    cardsToAnimateToHand.Add(inst);                               // Планируем анимацию в руку
                                else
                                    cardsOverflowToDeckTop.Add(inst);                             // Перебор — пойдёт на верх колоды после анимации
                                                                                                  // Также собирайте список для модалки «получены карты» (как раньше)
                                awardedCardDefs.Add(r.cardDef);                                   // Для InfoModal (визуальное уведомление)
                                needAwardedCardsModal = true;
                            }
                            break;
                        }
                }
            };

            if (useAlt)
            {
                // выдаём только выбранную альтернативу
                var arr = currentEvent.alternativeRewards;
                var chosen = (arr != null && arr.Count > selectedAltIndex) ? arr[selectedAltIndex] : null;
                ApplyOneReward(chosen, -1);
            }
            else
            {
                // обычный режим: обрабатываем все слоты res_1..res_4
                for (int i = 0; i < currentEvent.rewards.Count; i++)
                    ApplyOneReward(currentEvent.rewards[i], i);
            }

            // ШТРАФЫ — как было
            foreach (var p in currentEvent.penalties)
            {
                statPenaltiesToAnimate.Add((p.stat, Mathf.Max(1, p.amount)));

                switch (p.stat)
                {
                    case StatType.Hunger: stats.ConsumeHunger(p.amount); break;
                    case StatType.Thirst: stats.ConsumeThirst(p.amount); break;
                    case StatType.Energy: stats.SpendEnergy(p.amount); break;
                    case StatType.Health: stats.TakeDamage(p.amount); break;
                }
            }

        }

        // 3) переместить карты из PlayArea в сброс
        MovePlacedCardsToDiscard();

        // Закрываем окно ПЕРЕД синематиком (по ТЗ) и запускаем анимацию ресурсов:
        Hide();                                              // Прячем окно событий

        StartCoroutine(ShowModalsThenRunAnimations_AndMove(
            needChooseFinalModal,                 // показывать ли chooseFinalModalUI (подключите в корутине при необходимости)
            needAwardedCardsModal,                // показывать ли awardedCardModalUI (используем ваш InfoModalUI)
            awardedCardDefs,                      // данные для «получены карты»
            sourceTile,                           // гекс-источник (для анимаций)
            resourceRewardsToAnimate,             // ресурсы для анимации
            statPenaltiesToAnimate,               // пенальти статов
            statRestoresToAnimate,                 // ресторы статов
            cardsToAnimateToHand,
            cardsOverflowToDeckTop
        ));

    }


    /// Показать модалки (по очереди), затем запустить все анимации и движение фишки
    private IEnumerator ShowModalsThenRunAnimations_AndMove(
        bool needChooseFinalModal,                                // нужно ли показывать chooseFinalModalUI
        bool needAwardedCardsModal,                               // нужно ли показывать awardedCardModalUI (InfoModalUI)
        List<CardDef> awardedCardDefs,                            // список новых карт для модалки с картами
        HexTile tile,                                             // гекс-источник для анимаций
        List<EventSO.Reward> resourceRewards,                     // ресурсные награды
        List<(StatType stat, int amount)> statPenalties,          // штрафы статов
        List<(EventSO.PlayerStat stat, int amount)> statRestores,  // ресторы статов
        List<CardInstance> cardsToAnimateToHand,     // Эти карты полетят в руку (есть место)
        List<CardInstance> cardsOverflowToDeckTop // Эти карты уйдут на верх колоды (места в руке нет)
    )
    {
        // 1) МОДАЛКИ — ПОСЛЕДОВАТЕЛЬНО
        // 1.1) chooseFinalModalUI (если используется у вас; оставляю место подключения)
        if (needChooseFinalModal)
        {
            // Если у вас есть отдельный компонент chooseFinalModalUI — откройте здесь.
            // Пример шаблона (адаптируйте к вашему API):
            // ModalGate.Acquire(this);
            // chooseFinalModalUI.Show(onClose: () => ModalGate.Release(this));
            // while (chooseFinalModalUI.isActiveAndEnabled) yield return null;
        }

        // 1.2) awardedCardModalUI — в вашем проекте это InfoModalUI.ShowNewCards(...)
        if (needAwardedCardsModal && awardedCardDefs != null && awardedCardDefs.Count > 0)
        {
            // Находим модалку «новые карты»
            var cardsModal = FindFirstObjectByType<InfoModalUI>(FindObjectsInactive.Include); // используем ваш InfoModalUI
            if (cardsModal != null)                                                           // если нашли
            {
                Debug.Log(needAwardedCardsModal);
                ModalGate.Acquire(this);                                                      // блокируем ввод
                                                                                              // Показываем модалку (текст можно менять по вкусу)
                string msg = (awardedCardDefs.Count == 1) ? "Получена новая карта" : $"Получены новые карты ×{awardedCardDefs.Count}";
                cardsModal.ShowNewCards(msg, awardedCardDefs);                                // показать список карт
                yield return null;                                                            // кадр на отрисовку
                                                                                              // Ждём пока модалка закроется (если нет коллбека onClose — поллим активность)
                while (cardsModal.isActiveAndEnabled)                                         // пока открыта
                    yield return null;                                                        // ждём кадр
                ModalGate.Release(this);                                                      // снимаем блок
            }
        }

        // 2) АНИМАЦИИ — ПО ПОРЯДКУ: пенальти → ресторы → ресурсы
        // 2.1) Пенальти: левый верх → центр → тайл (по одной иконке за единицу)
        bool penaltyDone = false;                                                             // флаг завершения
        RewardPickupAnimator.Instance?.PlayStatPenaltyBatch(                                  // запускаем партию
            tile,                                                                             // гекс-цель
            statPenalties,                                                                    // список штрафов
            onDone: () => { penaltyDone = true; }                                             // коллбек завершения
        );
        while (!penaltyDone) yield return null;                                               // ждём завершения пенальти

        // 2.2) Ресторы: тайл → центр → левый верх (по одной иконке за единицу)
        bool restoreDone = false;                                                             // флаг завершения
        RewardPickupAnimator.Instance?.PlayStatRestoreBatch(                                  // запускаем партию
            tile,                                                                             // гекс-источник
            statRestores,                                                                     // список ресторов
            onDone: () => { restoreDone = true; }                                             // коллбек завершения
        );
        while (!restoreDone) yield return null;                                               // ждём завершения ресторов

        // 2.3) Ресурсы: тайл → центр → слот инвентаря (быстрый режим без «полки»)
        bool resourcesDone = false;                                                           // флаг завершения
        RewardPickupAnimator.Instance?.PlayForRewards(                                        // запускаем партию ресурсов
            tile,                                                                             // гекс-источник
            resourceRewards,                                                                  // список ресурсных наград
            onBeforeInventoryApply: () =>                                                     // перед посадкой — начисляем ресурсы в модель
            {
                var invCtrl = InventoryController.Instance;                                   // берём инвентарь
                if (invCtrl != null)                                                          // если есть
                {
                    foreach (var rr in resourceRewards)                                       // для каждой награды
                        invCtrl.AddResource(rr.resource, Mathf.Max(1, rr.amount));            // начисляем (новые слоты будут скрыты)
                }
            },
            onAfterDone: () => { resourcesDone = true; }                                      // ресурсы долетели
        );
        while (!resourcesDone) yield return null;                                             // ждём завершения ресурсов

        // 2.4) Карты из события: «колода → центр → правая часть руки», ПОТОМ фактическое добавление в руку
        bool cardsDrawn = false;                                                             // Флаг завершения полёта
        var deck = FindFirstObjectByType<DeckController>();   // нужна ссылка на колоду
                                                              // Сначала подкладываем «перебор» на колоду (без анимации), чтобы игрок не видел «ложный» перелёт
        if (cardsOverflowToDeckTop.Count > 0 && deck != null)                                // Если есть излишки
        {
            foreach (var inst in cardsOverflowToDeckTop) deck.AddToTop(inst);                // Сразу кладём на верх колоды
        }
        // Теперь — полёт только для тех, кто реально попадёт в руку
        RewardPickupAnimator.Instance?.PlayCardsToHandFromDeck(
            cardsToAnimateToHand,                                                            // Эти карты анимируем
            onDone: () => { cardsDrawn = true; }                                             // Готово — ставим флаг
        );
        while (!cardsDrawn) yield return null;                                               // Ждём завершения анимации
                                                                                             // И ТОЛЬКО СЕЙЧАС фактически добавляем их в руку (спавн UI и т.п.)
        if (HandController.Instance != null)                                                 // Если есть контроллер руки
        {
            foreach (var inst in cardsToAnimateToHand) HandController.Instance.AddCardToHand(inst); // Добавляем
            HandController.Instance.RaisePilesChanged();                                     // Сообщаем UI о переменах
        }

        // 3) ТОЛЬКО ТЕПЕРЬ — очищаем тайл и двигаем фишку
        if (tile != null)                                                                      // если гекс валиден
        {
            tile.SetType(HexType.Empty);                                                       // делаем пустым
            tile.eventData = null;                                                             // снимаем данные события
            tile.Reveal();                                                                     // оставляем открытым
            tile.UpdateVisual();                                                               // обновляем визуал
        }
        //Debug.Log(sourceTile);
        var map = HexMapController.Instance
                  ?? FindFirstObjectByType<HexMapController>(FindObjectsInactive.Include);
        if (tile != null)
        {
            if (map) map.PopOneBarrierOnNeighbors(tile);
        }
        if (map != null && map.playerPawn != null)
        {
            map.playerPawn.MoveTo(sourceTile);
        }
        ForceTileVisualRefresh(sourceTile);
        sourceTile = null;
        gameObject.SetActive(false);                          // плавно двигаем фишку

        // Готово
        yield break;                                                                           // завершаем корутину
    }


    // Нажата кнопка отмены (закрыть)
    private void OnCancelClicked()
    {
        ReturnCardsFromDropZoneToHand();                           // Вернём все положенные карты обратно в руку (и размер 347)
        Hide();
        sourceTile = null;
        gameObject.SetActive(false); // Просто спрятать окно без изменений тайла
    }

    private void ReturnCardsFromDropZoneToHand()                   // Вернуть все карты из зоны обратно в руку
    {
        if (dropZone == null || HandController.Instance == null) return; // Защита
        foreach (var cv in dropZone.placedCards)                   // Перебираем все положенные карты
        {
            // Возвращаем в панель руки
            cv.transform.SetParent(HandController.Instance.handPanel, worldPositionStays: false);
            cv.SetToHandSize();       // вернуть полную высоту и полную маску арта
            cv.ownerZone = null;
            cv.RefreshLocationVisuals();                          // карта больше не принадлежит зоне
        }
        dropZone.ClearZone();                                      // Чистим список/счётчик
        UpdateConfirmInteractable();                               // Выключим кнопку
    }

    private void MovePlacedCardsToDiscard()                        // Отправить карты из зоны в сброс
    {
        if (dropZone == null || HandController.Instance == null) return; // Защита
        // Скопируем список, чтобы не ловить модификацию коллекции
        List<CardView> used = new List<CardView>(dropZone.placedCards); // Делаем копию ссылок
        dropZone.ClearZone();                                      // Чистим зону
        HandController.Instance.DiscardCards(used);                // Передаём в контроллер руки — он удалит UI и запишет в discard
    }

    //// Превращаем тайл в «пустой», форсируем его обновление и перемещаем на него игрока
    //private void ResolveTileAndMovePlayer()
    //{
    //    if (sourceTile == null) return;                            // Если тайл не задан — выходим

    //    // По DD: после успешного розыгрыша событие удаляется, игрок перемещается на этот гекс
    //    sourceTile.SetType(HexType.Empty);                         // Меняем тип на пустой
    //    sourceTile.eventData = null;                               // Отвяжем данные события (больше нет события)
    //    sourceTile.Reveal();                                       // Оставляем открытым (по логике DD)

    //    // ВАЖНО: сразу обновим визуал тайла, чтобы иконка события исчезла сразу после подтверждения
    //    ForceTileVisualRefresh(sourceTile);

    //    // Перемещаем игрока (используем уже существующую логику)
    //    var map = HexMapController.Instance;
    //    if (map != null && map.playerPawn != null)
    //    {
    //        map.playerPawn.MoveTo(sourceTile);                     // Двигаем фишку на тайл
    //        map.RevealNeighbors(sourceTile.x, sourceTile.y);       // Открываем соседей новой позиции
    //    }
    //}

    // Аккуратный способ принудительно обновить визуал тайла без зависимости от внутренних методов HexTile
    private void ForceTileVisualRefresh(HexTile tile)
    {
        if (tile == null) return;

        // Если в твоём HexTile есть публичный метод RefreshVisuals() — можно раскомментировать строку ниже
        tile.UpdateVisual();
    }

    // Подсветка выбора альтернатив: белая рамка у выбранной, красная — у невыбранной
    private void UpdateAltSelectionFrames()
    {
        if (altRewardA) altRewardA.SetAltSelection(selectedAltIndex == 0);
        if (altRewardB) altRewardB.SetAltSelection(selectedAltIndex == 1);
    }

    private void DrawBarriers(System.Collections.Generic.IReadOnlyList<int> values)
    {
        if (barriersPanel == null || barrierSlots == null) return;

        bool hasAny = values != null && values.Count > 0;
        barriersPanel.SetActive(hasAny);

        for (int i = 0; i < barrierSlots.Length; i++)
        {
            var img = barrierSlots[i];
            if (!img) continue;

            if (hasAny && i < values.Count)
            {
                int v = values[i];
                img.enabled = true;
                img.sprite = (v >= 3) ? bar3Sprite : bar1Sprite;
            }
            else
            {
                img.enabled = false;
            }
        }
    }

    // Итоговая стоимость для текущего события с учётом барьеров на тайле.
    // Для simple: mainCostAmount + sum(barriers); для choice/combat — без модификаторов.
    private int GetEffectiveMainCost()
    {
        if (currentEvent == null) return 0;
        if (currentEvent.isChoice || currentEvent.isCombat) return currentEvent.mainCostAmount;
        int barrier = (sourceTile != null) ? sourceTile.BarrierTotal : 0;
        return Mathf.Max(0, currentEvent.mainCostAmount + barrier);
    }

    private void ApplyAmountFont(TMPro.TextMeshProUGUI tmp, int value)
    {
        if (!tmp) return;
        // ≥10 — меньше шрифт, <10 — больше шрифт
        tmp.fontSize = (value >= amountSwitchThreshold) ? amountFontSmall : amountFontLarge;
    }

}
