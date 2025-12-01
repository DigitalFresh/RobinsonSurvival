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

  //  private int selectedAltIndex = 0;   // по умолчанию выбираем первую награду (левую)

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
                //else altRewardA.gameObject.SetActive(false);

                //// обработчик клика (если назначен selectButton)
                //if (altRewardA.selectButton)
                //{
                //    altRewardA.selectButton.onClick.RemoveAllListeners();
                //    altRewardA.selectButton.onClick.AddListener(() => { selectedAltIndex = 0; UpdateAltSelectionFrames(); UpdateConfirmInteractable(); });
                //}
            }

            if (altRewardB)
            {
                if (ev.alternativeRewards != null && ev.alternativeRewards.Count > 1)
                { altRewardB.gameObject.SetActive(true); altRewardB.Bind(ev.alternativeRewards[1]); }
                //else altRewardB.gameObject.SetActive(false);

                //if (altRewardB.selectButton)
                //{
                //    altRewardB.selectButton.onClick.RemoveAllListeners();
                //    altRewardB.selectButton.onClick.AddListener(() => { selectedAltIndex = 1; UpdateAltSelectionFrames(); UpdateConfirmInteractable(); });
                //}
            }


            // выставим стартовую подсветку выбора (по умолчанию — левая)
            //selectedAltIndex = 0;
            //UpdateAltSelectionFrames();
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
        Debug.Log(dropZone.currentEye);
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
                    ? "Нажмите «ОК», чтобы выбрать одну из двух наград."
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

    private void OnConfirmClicked()
    {
        // Если события нет — просто закрыть
        if (currentEvent == null) { Hide(); return; }

        // Стартуем корутину, где уже можно yield'ить
        StartCoroutine(OnConfirmRoutine());
    }


    // Нажата кнопка подтверждения (разыграть/принять)
    private System.Collections.IEnumerator OnConfirmRoutine()
    {
        // 1) Определяем, какие именно награды разыгрываем: или альтернативную выбранную, или весь набор из 4 слотов
        var rewardsToProcess = new List<EventSO.Reward>();
        if (currentEvent.rewardsAreAlternative)
        {
            // Берём альтернативы ИЗ СОБЫТИЯ (а не из "altRewards", которой нет в контексте)
            var alternatives = currentEvent.alternativeRewards;
            if (alternatives == null || alternatives.Count < 2)
            {
                Debug.LogWarning("[EventWindowUI] Alternative mode: need exactly 2 rewards.");
                yield break;
            }

            // Показываем НОВУЮ модалку выбора альтернативы
            int chosen = -1;
            bool closed = false;
            var req = new ModalRequest
            {
                kind = ModalKind.AltRewardChoice,
                size = ModalSize.Medium,
                title = "Выберите награду",
                altRewards = new List<EventSO.Reward> { alternatives[0], alternatives[1] },
                onAltChosen = idx => { chosen = idx; }
            };
            ModalManager.Instance?.Show(req, _ => closed = true);
            while (!closed) yield return null;

            // Если закрыли без выбора — остаёмся в окне
            if (chosen != 0 && chosen != 1) yield break;

            // передаём выбранную альтернативу «стандартным образом»
            rewardsToProcess.Add(alternatives[chosen]);
        }
        else
        {
            // обычный режим — все заполненные слоты, в ТОМ ПОРЯДКЕ, в каком они указаны в EventSO
            for (int i = 0; i < currentEvent.rewards.Count; i++)
            {
                var r = currentEvent.rewards[i];
                if (r != null) rewardsToProcess.Add(r);
            }
        }

        // --- 2) ПЕНАЛЬТИ: собрать пары (stat, amount) и СРАЗУ применить к модели статов ---
        var statPenaltiesToAnimate = CollectPenaltiesForAnimationAndApply();
        //var statPenaltiesToAnimate = new List<(StatType stat, int amount)>();  // для анимации
        //var stats = FindFirstObjectByType<PlayerStatsSimple>();
        //if (currentEvent.penalties != null && currentEvent.penalties.Count > 0)
        //{
        //    for (int i = 0; i < currentEvent.penalties.Count; i++)
        //    {
        //        var p = currentEvent.penalties[i];
        //        if (p == null || p.amount <= 0) continue;

        //        int val = Mathf.Max(1, p.amount);             // защита от «0»
        //        statPenaltiesToAnimate.Add((p.stat, val));     // сохраним для VFX

        //        // Немедленно применяем к модели (чтобы HUD показал актуальные значения)
        //        if (stats != null)
        //        {
        //            switch (p.stat)
        //            {
        //                case StatType.Hunger: stats.ConsumeHunger(val); break;
        //                case StatType.Thirst: stats.ConsumeThirst(val); break;
        //                case StatType.Energy: stats.SpendEnergy(val); break;
        //                case StatType.Health: stats.TakeDamage(val); break;
        //            }
        //        }
        //    }
        //}

        // --- 3) Стоимость: карты из PlayArea отправляем в сброс, как было ---
        MovePlacedCardsToDiscard();

        // --- 4) Прячем окно и запускаем оркестратор ПООЧЕРЁДНОЙ выдачи всего ---
        Hide();
        yield return StartCoroutine(ProcessRewardsSequentially(rewardsToProcess, statPenaltiesToAnimate));
    }

    private List<(StatType stat, int amount)> CollectPenaltiesForAnimationAndApply()
    {
        var list = new List<(StatType stat, int amount)>();
        if (currentEvent == null || currentEvent.penalties == null || currentEvent.penalties.Count == 0)
            return list;

        var stats = FindFirstObjectByType<PlayerStatsSimple>();
        for (int i = 0; i < currentEvent.penalties.Count; i++)
        {
            var p = currentEvent.penalties[i];
            if (p == null || p.amount <= 0) continue;

            int val = Mathf.Max(1, p.amount);
            list.Add((p.stat, val));                          // для анимации

            // — немедленно применяем к модели, как раньше —
            if (stats != null)
            {
                switch (p.stat)
                {
                    case StatType.Hunger: stats.ConsumeHunger(val); break;
                    case StatType.Thirst: stats.ConsumeThirst(val); break;
                    case StatType.Energy: stats.SpendEnergy(val); break;
                    case StatType.Health: stats.TakeDamage(val); break;
                }
            }
        }
        return list;
    }


    /// Последовательно отрабатывает список наград:
    /// - для обычных (ресурсы/ресторы/карты) показывает модалку при необходимости + проигрывает FX и ждёт onDone;
    /// - для free-reward «запуск боя» — показывает pre-модалку (если задана), запускает бой и ждёт завершения, затем post-модалку;
    /// - по окончании ВСЕГО — если гекс ещё не очищён боем, очищает событие и переносит фишку на тайл.
    /// Строгая последовательность:
    /// (1) пенальти (HUD→центр→тайл),
    /// (2) по каждой награде: рестор статов → ресурсы → МОДАЛКА НОВЫХ КАРТ → анимация карт → free-reward бой (если есть),
    /// (3) финал: очистка тайла и перенос фишки (если бой не сделал это).
    private IEnumerator ProcessRewardsSequentially(List<EventSO.Reward> rewards,
    List<(StatType stat, int amount)> statPenaltiesToAnimate)
    {

        // Понадобятся ссылки
        var map = HexMapController.Instance ?? FindFirstObjectByType<HexMapController>(FindObjectsInactive.Include);
        var inv = InventoryController.Instance;
        var deck = FindFirstObjectByType<DeckController>();
        var hand = HandController.Instance;

        // ---------- (1) Пенальти: одной пачкой, ДО других VFX ----------
        if (statPenaltiesToAnimate != null && statPenaltiesToAnimate.Count > 0)
        {
            bool penaltyDone = false;                                               // флаг завершения анимации
            RewardPickupAnimator.Instance?.PlayStatPenaltyBatch(
                sourceTile,                                                         // из какого тайла играем VFX
                statPenaltiesToAnimate,                                             // список (stat, amount)
                onDone: () => penaltyDone = true                                    // колбэк по завершению
            );
            while (!penaltyDone) yield return null;                                 // ждём окончания
        }

        // ---------- (2) Награды: строго по очереди, КАЖДАЯ полностью ----------
        if (rewards != null)
            for (int i = 0; i < rewards.Count; i++)
            {
                var r = rewards[i];
                if (r == null) continue;

                // 2.1) Ресторы статов: сначала применяем модель, затем VFX (тайл→центр→HUD)
                if (r.type == EventSO.RewardType.RestoreStat && r.restoreAmount > 0)
                {
                    var stats = FindFirstObjectByType<PlayerStatsSimple>();
                    int val = Mathf.Max(1, r.restoreAmount);                            // защита от «0»
                    if (stats != null)
                    {
                        switch (r.stat)
                        {
                            case EventSO.PlayerStat.Hunger: stats.Eat(val); break;
                            case EventSO.PlayerStat.Thirst: stats.Drink(val); break;
                            case EventSO.PlayerStat.Energy: stats.GainEnergy(val); break;
                            case EventSO.PlayerStat.Health: stats.Heal(val); break;
                        }
                    }

                    bool restDone = false;                                              // ждём VFX
                    RewardPickupAnimator.Instance?.PlayStatRestoreBatch(
                        sourceTile,
                        new List<(EventSO.PlayerStat stat, int amount)> { (r.stat, val) },
                        onDone: () => restDone = true
                    );
                    while (!restDone) yield return null;
                    continue;                                                           // к следующему reward
                }

                // 2.2) Ресурсы: onBefore — начисляем в инвентарь; затем VFX (тайл→центр→инвентарь)
                if (r.type == EventSO.RewardType.Resource && r.resource != null && r.amount > 0)
                {
                    bool resDone = false;                                               // ждём VFX
                    RewardPickupAnimator.Instance?.PlayForRewards(
                        sourceTile,
                        new List<EventSO.Reward> { r },                                 // в обёртке из одного
                        onBeforeInventoryApply: () =>
                        {
                            if (inv != null) inv.AddResource(r.resource, Mathf.Max(1, r.amount));
                        },
                        onAfterDone: () => resDone = true
                    );
                    while (!resDone) yield return null;
                    continue;
                }

                // 2.3) Новые карты: СНАЧАЛА модалка «получены карты», потом анимация выдачи
                if (r.type == EventSO.RewardType.NewCard && r.cardDef != null && r.cardCount > 0)
                {
                    // (а) модалка «получены карты» — это то, чего не хватало
                    yield return StartCoroutine(ShowCardsModalAndWait(
                        // список карточек для показа (столько, сколько выдаём в этой награде)
                        CreateCardDefList(r.cardDef, Mathf.Max(1, r.cardCount))
                    ));

                    // (б) выдача: что в руку (с VFX), что на верх колоды
                    for (int k = 0; k < r.cardCount; k++)
                    {
                        var inst = new CardInstance(r.cardDef);

                        int inHandNow = hand ? hand.HandCount : 0;
                        int maxHand = hand ? hand.maxHand : 7;

                        if (inHandNow < maxHand)
                        {
                            bool cardsDone = false;
                            RewardPickupAnimator.Instance?.PlayCardsToHandFromDeck(
                                new List<CardInstance> { inst },                         // анимируем одну
                                onDone: () => cardsDone = true
                            );
                            while (!cardsDone) yield return null;

                            if (hand != null)                                           // реально кладём в руку
                            {
                                hand.AddCardToHand(inst);
                                hand.RaisePilesChanged();
                            }
                        }
                        else
                        {
                            if (deck != null) deck.AddToTop(inst);                      // перебор — без VFX на верх колоды
                        }
                    }
                    continue;
                }

                // ====== FREE-REWARD: вступительная модалка (если включена во FreeRewardDef) ======
                if (r.type == EventSO.RewardType.FreeReward && r.freeReward != null && r.freeReward.showModalBeforeEffects)
                {
                    // Если задан ключ — показываем модалку и ждём ОК
                    if (!string.IsNullOrEmpty(r.freeReward.modalCatalogKey))
                        yield return StartCoroutine(ShowFreeModalAndWait(r.freeReward.modalCatalogKey));
                }


                // 2.4) Free-reward, который запускает бой: pre-модалка → бой → post-модалка
                StartAdHocCombatEffectDef combatEff = null;
                if (r.type == EventSO.RewardType.FreeReward && r.freeReward != null && r.freeReward.effects != null)
                {
                    foreach (var eff in r.freeReward.effects)
                    {
                        combatEff = eff as StartAdHocCombatEffectDef;
                        if (combatEff != null) break;
                    }
                }

                if (combatEff != null)
                {
                    // pre-модалка (если задан ключ каталога)
                    if (!string.IsNullOrEmpty(combatEff.preFightCatalogKey))
                        yield return StartCoroutine(ShowFreeModalAndWait(combatEff.preFightCatalogKey));

                    var cc1 = HexMapController.Instance ?? FindFirstObjectByType<HexMapController>(FindObjectsInactive.Include);
                    if (cc1) cc1.suppressMapCleanupOnce = true;

                    // запуск боя и ожидание
                    if (map != null && sourceTile != null && combatEff.enemies != null && combatEff.enemies.Count > 0)
                    {
                        bool finished = false;
                        bool playerWon = false;
                        map.StartAdHocCombat(sourceTile, combatEff.enemies, won => { finished = true; playerWon = won; });
                        while (!finished) yield return null;                             // ждём завершения боя
                        if (!playerWon) yield break;                                     // смерть/поражение — прерываем цепочку
                    }

                    // post-модалка (если задан ключ каталога)
                    if (!string.IsNullOrEmpty(combatEff.postFightCatalogKey))
                        yield return StartCoroutine(ShowFreeModalAndWait(combatEff.postFightCatalogKey));

                    continue;
                }

                //// 2.5) Прочий кастомный EffectDef — исполняем синхронно (если он есть)
                //if (r.rewardEffect != null)
                //{
                //    r.rewardEffect.Execute(new EffectContext());
                //    yield return null;                                                  // кадрик между шагами
                //}
            }

        // ---------- (3) Финал: очистить событие и перенести фишку, если это НЕ сделал бой ----------
        if (sourceTile != null)
        {
            sourceTile.SetType(HexType.Empty);                                      // гекс становится пустым
            sourceTile.eventData = null;                                            // отвязываем событие
            sourceTile.Reveal();                                                    // он остаётся открытым
            sourceTile.UpdateVisual();                                              // обновляем визуал

            var map2 = HexMapController.Instance ?? FindFirstObjectByType<HexMapController>(FindObjectsInactive.Include);
            if (map2) map2.PopOneBarrierOnNeighbors(sourceTile);                    // «распыление» барьеров
            if (map2 && map2.playerPawn) map2.playerPawn.MoveTo(sourceTile);        // перенос фишки игрока
        }

        sourceTile = null;                                                          // контекст больше не нужен
        gameObject.SetActive(false);                                                // окно уже скрыто
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

    // Аккуратный способ принудительно обновить визуал тайла без зависимости от внутренних методов HexTile
    private void ForceTileVisualRefresh(HexTile tile)
    {
        if (tile == null) return;

        // Если в твоём HexTile есть публичный метод RefreshVisuals() — можно раскомментировать строку ниже
        tile.UpdateVisual();
    }

    // Подсветка выбора альтернатив: белая рамка у выбранной, красная — у невыбранной
    //private void UpdateAltSelectionFrames()
    //{
    //    if (altRewardA) altRewardA.SetAltSelection(selectedAltIndex == 0);
    //    if (altRewardB) altRewardB.SetAltSelection(selectedAltIndex == 1);
    //}

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

    // Показать модалку «получены карты» и дождаться ОК
    private IEnumerator ShowCardsModalAndWait(List<CardDef> defs)
    {
        if (defs == null || defs.Count == 0) yield break;

        var req = new ModalRequest
        {
            kind = ModalKind.Info,
            size = ModalSize.Medium,
            title = (defs.Count == 1) ? "Получена новая карта"
                                      : $"Получены новые карты ×{defs.Count}",
            cards = defs
        };

        bool closed = false;
        ModalManager.Instance?.Show(req, onClose: _ => closed = true);
        while (!closed) yield return null;
    }


    // Сервисная утилита: сделать список N повторов CardDef (для модалки по конкретной награде)
    private List<CardDef> CreateCardDefList(CardDef def, int count)
    {
        var l = new List<CardDef>(count);
        for (int i = 0; i < count; i++) l.Add(def);
        return l;
    }

    // Показать модалку по каталожному ключу и дождаться ОК
    private IEnumerator ShowFreeModalAndWait(string catalogKey)
    {
        // подтягиваем контент из провайдера
        string title = null, body = null;
        Sprite picture = null;

        var provider = ModalContentProvider.Instance;
        if (provider != null)
        {
            var rc = provider.Resolve(catalogKey);   // <- у rc есть title, description, image
            title = rc.title;                      // заголовок
            body = rc.description;                // ТЕКСТ — используем description (а не message)
            picture = rc.image;                      // картинка
        }

        var req = new ModalRequest
        {
            kind = ModalKind.FreeReward,
            size = ModalSize.Medium,
            title = title,
            message = body,        // <- сюда кладём description
            picture = picture
        };

        bool closed = false;
        ModalManager.Instance?.Show(req, onClose: _ => closed = true);
        while (!closed) yield return null;
    }


    private IEnumerator WaitCombatEnd()
    {
        var cc = CombatController.Instance;
        if (cc == null) yield break;

        bool finished = false;
        void Handler(bool _) => finished = true;

        cc.CombatEnded += Handler;
        while (cc.IsRunning && !finished) yield return null;   // крутим пока бой не кончился
        cc.CombatEnded -= Handler;
    }

}
