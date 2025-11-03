using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;             // ДЛЯ IEnumerator
using System.Collections.Generic;

// Окно события-выбора. Похоже на EventWindowUI, но рисует 2-3 блока Option и даёт выбрать один.
public class ChooseEventWindowUI : MonoBehaviour
{
    public static ChooseEventWindowUI Instance;
    public static ChooseEventWindowUI Get() => Instance ?? (Instance = FindFirstObjectByType<ChooseEventWindowUI>(FindObjectsInactive.Include));

    [Header("Refs: header")]
    public CanvasGroup canvasGroup;          // показать/скрыть окно
    public Image iconImage;                  // Icon
    public TextMeshProUGUI titleText;        // Title
    public TextMeshProUGUI descriptionText;  // Description

    [Header("Choice arrows")]
    public Image arrows;                     // Ch_Arrows
    public Sprite[] arrowsFor2;              // [выбрано верхнее, выбрано нижнее]
    public Sprite[] arrowsFor3;              // [выбрано верхнее, выбрано среднее, выбрано нижнее]

    [Header("Options (2..3)")]
    public OptionUI[] options;               // массив из 3 слотов Option (третий можно отключить)

    [Header("Play Area & hint")]
    public EventWindowDropZone dropZone;     // та же зона, что и в простом окне
    public TextMeshProUGUI hintText;         // Area_description

    [Header("Footer")]
    public Button confirmButton;
    public Button cancelButton;

    [Header("Hand anchors")]
    public Transform handPanelRoot;                                // Корневой контейнер руки (указать в инспекторе)


    // Рантайм: что сейчас показываем
    private EventSO currentEvent;
    private HexTile sourceTile;
    private int selectedIndex = 0;           // по умолчанию выбран первый вариант

    private void Awake()
    {
        Instance = this;
        HideImmediate();

        if (dropZone) dropZone.OnZoneChanged += UpdateConfirmInteractable;
        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton) cancelButton.onClick.AddListener(OnCancel);
    }

    private void OnDestroy()
    {
        if (dropZone) dropZone.OnZoneChanged -= UpdateConfirmInteractable;
    }

    // Показать окно для события-выбора
    public void Show(EventSO ev, HexTile tile)
    {
        currentEvent = ev;
        sourceTile = tile;
        selectedIndex = 0; // по умолчанию — первый

        // Заголовок / иконка / описание
        if (titleText) titleText.text = ev ? ev.eventName : "Событие";
        if (descriptionText) descriptionText.text = ev ? ev.description : "";
        if (iconImage) iconImage.sprite = ev ? ev.icon : null;

        // Активируем нужное число опций (2 или 3) и забиндим каждую
        int count = Mathf.Clamp(ev != null ? ev.choices.Count : 0, 0, options.Length);
        for (int i = 0; i < options.Length; i++)
        {
            bool active = (i < count);
            if (options[i]) options[i].gameObject.SetActive(active);
            if (active)
            {
                var data = ev.choices[i];
                options[i].Bind(data);

                // подписываем кнопку выбора на переключение selectedIndex
                if (options[i].selectButton)
                {
                    int captured = i;
                    options[i].selectButton.onClick.RemoveAllListeners();
                    options[i].selectButton.onClick.AddListener(() =>
                    {
                        SelectOption(captured);
                    });
                }
            }
        }

        // применим выделение и стрелочки
        ApplySelectionVisuals();

        // интерфейс
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        gameObject.SetActive(true);

        UpdateConfirmInteractable(); // посчитать доступность Confirm по выбранной опции

        ModalGate.Acquire(this); // <— включили
    }

    private void ApplySelectionVisuals()
    {
        // Подсветка на Option
        for (int i = 0; i < options.Length; i++)
            if (options[i] && options[i].gameObject.activeSelf)
                options[i].SetSelected(i == selectedIndex);

        // Стрелочки
        int count = 0;
        foreach (var o in options) if (o && o.gameObject.activeSelf) count++;
        if (!arrows) return;
        if (count == 2 && arrowsFor2 != null && arrowsFor2.Length >= 2)
            arrows.sprite = arrowsFor2[Mathf.Clamp(selectedIndex, 0, 1)];
        else if (count == 3 && arrowsFor3 != null && arrowsFor3.Length >= 3)
            arrows.sprite = arrowsFor3[Mathf.Clamp(selectedIndex, 0, 2)];
    }

    private void SelectOption(int idx)
    {
        selectedIndex = idx;
        ApplySelectionVisuals();
        ReturnCardsFromDropZoneToHand();

        if (dropZone && currentEvent != null && currentEvent.choices != null && currentEvent.choices.Count > 0)
        {
            int clamped = Mathf.Clamp(selectedIndex, 0, currentEvent.choices.Count - 1); // Защита индекса
            var opt = currentEvent.choices[clamped];                                      // Текущая опция
            dropZone.SetupRequirementTyped(opt.mainCostType,                              // Тип ✋/👊/👁
                                           Mathf.Max(0, opt.mainCostAmount));            // Сколько нужно
            //dropZone.ClearZone();                                                        // Очистить карты в зоне
        }

        UpdateConfirmInteractable();
    }

    // Проверка условий выбранной опции, подсказка, включение Confirm
    public void UpdateConfirmInteractable()
    {
        if (!confirmButton || !dropZone || currentEvent == null) return;

        // если нет опций — ничего не подтверждаем
        if (currentEvent.choices == null || currentEvent.choices.Count == 0)
        {
            confirmButton.interactable = false;
            if (hintText) hintText.text = "Нет вариантов выбора.";
            return;
        }

        int idx = Mathf.Clamp(selectedIndex, 0, currentEvent.choices.Count - 1);
        var opt = currentEvent.choices[idx];

        // 1) Главная стоимость (по выбранной опции)
        int have = 0;
        switch (opt.mainCostType)
        {
            case CostType.Hands: have = dropZone.currentHands; break;
            case CostType.Fists: have = dropZone.currentFists; break;
            case CostType.Eye: have = dropZone.currentEye; break;
        }
        bool mainOK = (have >= opt.mainCostAmount);

        // 2) Доп.стоимости — ВСЕ обязательные в выборе
        bool addOK = true;
        foreach (var a in opt.additionalCosts)
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

        bool canConfirm = mainOK && addOK;
        confirmButton.interactable = canConfirm;

        // Подсказка Area_description: добавляем напоминание о выборе
        if (hintText)
        {
            if (!mainOK)
                hintText.text = "Добавьте в PlayArea карты с нужными параметрами для выбранного варианта.";
            else if (!addOK)
                hintText.text = "Этому выбору требуются дополнительные теги (мозг/сила/скорость).";
            else
                hintText.text = "Выберите вариант и нажмите «ОК», чтобы выполнить действие.";
        }
    }

    // Подтверждение: применяем ПЕНАЛЬТИ и НАГРАДЫ выбранного варианта; карты из зоны — в сброс; тайл — разруливаем
    private void OnConfirm()
    {
        if (currentEvent == null) { Hide(); return; }
        int idx = Mathf.Clamp(selectedIndex, 0, currentEvent.choices.Count - 1);
        var opt = currentEvent.choices[idx];
        var freeRewardsToShow = new List<FreeRewardDef>(); // модалка по свободным наградам (может быть несколько)

        var resourceRewardsToAnimate = new List<EventSO.Reward>(); // Копим ресурсы для полёта
        var statRestoresToAnimate = new List<(EventSO.PlayerStat stat, int amount)>();
        var statPenaltiesToAnimate = new List<(StatType stat, int amount)>();
        var awardedCardDefs = new List<CardDef>(); // соберём, чтобы показать InfoModal (если есть новые карты)

        bool needAwardedCardsModal = false;    // нужна ли модалка «получены карты»
        bool needChooseFinalModal = false;     // поставьте true, если у вас есть финальная модалка

        var cardsToAnimateToHand = new List<CardInstance>();     // Эти карты полетят в руку (есть место)
        var cardsOverflowToDeckTop = new List<CardInstance>();   // Эти карты уйдут на верх колоды (места в руке нет)


        // 1) применить награды с учётом gating (как в простом окне)
        var stats = FindFirstObjectByType<PlayerStatsSimple>();
        if (stats != null)
        {
            // выдаём «реальные» награды из opt.rewards

            foreach (var r in opt.rewards)
            {
                // проверка «гейта» только если r.gatedByAdditional == true
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
                if (!grant) continue;

                switch (r.type)
                {
                    case EventSO.RewardType.Resource:
                        resourceRewardsToAnimate.Add(r); // НЕ начисляем сейчас
                        break;

                    case EventSO.RewardType.RestoreStat:
                        int val = Mathf.Max(1, r.restoreAmount);             // нормализация
                        statRestoresToAnimate.Add((r.stat, val));
                        ApplyRestore(stats, r);
                        break;

                    case EventSO.RewardType.NewCard:

                        if (r.cardDef == null) return;
                        var deck = FindFirstObjectByType<DeckController>();
                        var hand = HandController.Instance;
                        //var awardedCardDefs = new List<CardDef>();
                        if (deck == null || hand == null) return;

                        for (int i = 0; i < Mathf.Max(1, r.cardCount); i++)
                        {
                            var inst = new CardInstance(r.cardDef);

                            int handCountProjected = HandController.Instance ? HandController.Instance.HandCount + cardsToAnimateToHand.Count : cardsToAnimateToHand.Count; // Проекция
                            int maxHand = HandController.Instance ? HandController.Instance.maxHand : 7; // Fallback 10
                            if (handCountProjected < maxHand)                                 // Если ещё есть место
                                cardsToAnimateToHand.Add(inst);                               // Планируем анимацию в руку
                            else
                                cardsOverflowToDeckTop.Add(inst);                             // Перебор — пойдёт на верх колоды после анимации

                            awardedCardDefs?.Add(r.cardDef); // для InfoModal
                        }
                        //GiveNewCards(r, awardedCards);
                        needAwardedCardsModal = true;
                        // Debug.Log(awardedCards);
                        break;
                    case EventSO.RewardType.FreeReward:
                        if (r.freeReward != null)
                        {
                            // 1) Исполняем её эффекты
                            var ctx = new EffectContext();             // Источник нам не нужен; возьмём hand/deck/stats внутри
                            foreach (var eff in r.freeReward.effects)
                                if (eff) eff.Execute(ctx);

                            // 2) Готовим к показу модалки (покажем после всех наград/штрафов)
                            if (freeRewardsToShow == null) freeRewardsToShow = new List<FreeRewardDef>();
                            freeRewardsToShow.Add(r.freeReward);
                        }
                        break;
                }
            }

            // 2) применить штрафы (opt.penalties)
            foreach (var p in opt.penalties)
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

        if (freeRewardsToShow.Count > 0)
        {
            needChooseFinalModal = true;
            //var rewardModal = FreeRewardModalUI.Get();
            //rewardModal?.ShowMany(freeRewardsToShow); // покажем очередь модалок (если их несколько)
        }

        // 3) карты из зоны в сброс
        MovePlacedCardsToDiscard();

        if (sourceTile != null)
        {
            var map = HexMapController.Instance
                  ?? FindFirstObjectByType<HexMapController>(FindObjectsInactive.Include);
            if (map) map.PopOneBarrierOnNeighbors(sourceTile);
        }

        Hide();                                        // Прячем окно выбора

        StartCoroutine(ShowModalsThenRunAnimations_AndMove(
            needChooseFinalModal,                 // chooseFinalModalUI (подключите при необходимости)
            freeRewardsToShow,
            needAwardedCardsModal,                // awardedCardModalUI (InfoModalUI)
            awardedCardDefs,                      // новые карты
            sourceTile,                           // гекс-источник
            resourceRewardsToAnimate,             // ресурсы
            statPenaltiesToAnimate,               // пенальти статов
            statRestoresToAnimate,                 // ресторы статов
            cardsToAnimateToHand,
            cardsOverflowToDeckTop
        ));
    }


    /// Показать модалки (по очереди), затем запустить все анимации и движение фишки
    private IEnumerator ShowModalsThenRunAnimations_AndMove(
        bool needChooseFinalModal,                                // нужно ли показывать chooseFinalModalUI
        List<FreeRewardDef> freeRewardsToShow, // модалка по свободным наградам (может быть несколько)
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
        if (needChooseFinalModal && freeRewardsToShow != null && freeRewardsToShow.Count > 0)
        {
            bool done = false;
            ModalManager.Instance?.Show(new ModalRequest
            {
                kind = ModalKind.FreeReward,
                size = ModalSize.Medium,
                freeRewards = freeRewardsToShow    // <— теперь поле существует
            }, _ => done = true);

            while (!done) yield return null;
        }
        //if (needChooseFinalModal)
        //{
        //    ModalGate.Acquire(this);
        //    var rewardModal = FreeRewardModalUI.Get();
        //    rewardModal?.ShowMany(freeRewardsToShow); // покажем очередь модалок (если их несколько)

        //    while (rewardModal.isActiveAndEnabled)
        //        yield return null;
        //    ModalGate.Release(this);
        //}
        //Debug.Log(needAwardedCardsModal);
        //Debug.Log(awardedCardDefs);

        // 1.2) awardedCardModalUI — в вашем проекте это InfoModalUI.ShowNewCards(...)
        if (needAwardedCardsModal && awardedCardDefs != null && awardedCardDefs.Count > 0)
        {
            bool doneCards = false;
            ModalManager.Instance?.Show(new ModalRequest
            {
                kind = ModalKind.Info,
                size = ModalSize.Large,
                title = (awardedCardDefs.Count == 1) ? "Получена новая карта" : $"Получены новые карты ×{awardedCardDefs.Count}",
                cards = awardedCardDefs
            }, _ => doneCards = true);

            while (!doneCards) yield return null;
        }


        //if (needAwardedCardsModal && awardedCardDefs != null && awardedCardDefs.Count > 0)
        //{
        //    // Находим модалку «новые карты»
        //    var cardsModal = FindFirstObjectByType<InfoModalUI>(FindObjectsInactive.Include); // используем ваш InfoModalUI
        //    if (cardsModal != null)                                                           // если нашли
        //    {
        //        //Debug.Log(needAwardedCardsModal);
        //        ModalGate.Acquire(this);                                                      // блокируем ввод
        //                                                                                      // Показываем модалку (текст можно менять по вкусу)
        //        string msg = (awardedCardDefs.Count == 1) ? "Получена новая карта" : $"Получены новые карты ×{awardedCardDefs.Count}";
        //        cardsModal.ShowNewCards(msg, awardedCardDefs);                                // показать список карт
        //        yield return null;                                                            // кадр на отрисовку
        //                                                                                      // Ждём пока модалка закроется (если нет коллбека onClose — поллим активность)
        //        while (cardsModal.isActiveAndEnabled)                                         // пока открыта
        //            yield return null;                                                        // ждём кадр
        //        ModalGate.Release(this);                                                      // снимаем блок
        //    }
        //}

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
        var map = HexMapController.Instance;
        if (map != null && map.playerPawn != null)
        {
            map.playerPawn.MoveTo(sourceTile);
        }
        //ForceTileVisualRefresh(sourceTile);
        sourceTile = null;
        gameObject.SetActive(false);                          // плавно двигаем фишку

        // Готово
        yield break;                                                                           // завершаем корутину
    }

    private void ApplyRestore(PlayerStatsSimple stats, EventSO.Reward r)
    {
        int val = Mathf.Max(1, r.restoreAmount);
        switch (r.stat)
        {
            case EventSO.PlayerStat.Hunger: stats.Eat(val); break;
            case EventSO.PlayerStat.Thirst: stats.Drink(val); break;
            case EventSO.PlayerStat.Energy: stats.GainEnergy(val); break;
            case EventSO.PlayerStat.Health: stats.Heal(val); break;
        }
    }

    private void OnCancel()
    {
        ReturnCardsFromDropZoneToHand();
        Hide();
    }

    // ==== утилиты (копии логики из вашего EventWindowUI) ====

    private void ReturnCardsFromDropZoneToHand()
    {
        if (dropZone == null || HandController.Instance == null) return;
        foreach (var cv in dropZone.placedCards)
        {
            cv.transform.SetParent(HandController.Instance.handPanel, false);
            cv.SetToHandSize();
            cv.ownerZone = null;
            cv.RefreshLocationVisuals();
        }
        dropZone.ClearZone();
        UpdateConfirmInteractable();
    }

    private void MovePlacedCardsToDiscard()
    {
        if (dropZone == null || HandController.Instance == null) return;
        var used = new List<CardView>(dropZone.placedCards);
        dropZone.ClearZone();
        HandController.Instance.DiscardCards(used);
    }

    public void Hide()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        // gameObject.SetActive(false);
        currentEvent = null;
        // sourceTile = null;

        ModalGate.Release(this); // <— выключил

        HandController.Instance?.RaisePilesChanged();
    }

    private void HideImmediate()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        gameObject.SetActive(false);
    }
}
