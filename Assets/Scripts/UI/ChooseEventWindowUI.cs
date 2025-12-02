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

    private bool _tileClearedByCombat;   // если true — в конце пайплайна не чистим тайл повторно

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
        //ReturnCardsFromDropZoneToHand();

        //if (dropZone && currentEvent != null && currentEvent.choices != null && currentEvent.choices.Count > 0)
        //{
        //    int clamped = Mathf.Clamp(selectedIndex, 0, currentEvent.choices.Count - 1); // Защита индекса
        //    var opt = currentEvent.choices[clamped];                                      // Текущая опция
        //    dropZone.SetupRequirementTyped(opt.mainCostType,                              // Тип ✋/👊/👁
        //                                   Mathf.Max(0, opt.mainCostAmount));            // Сколько нужно
        //    //dropZone.ClearZone();                                                        // Очистить карты в зоне
        //}

        UpdateConfirmInteractable();
    }

    // Проверка условий выбранной опции, подсказка, включение Confirm
    public void UpdateConfirmInteractable()
    {
        Debug.Log(dropZone.currentEye);
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

        //Debug.Log(dropZone.currentEye);
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

        // Находим выбранную опцию
        int idx = Mathf.Clamp(selectedIndex, 0, currentEvent.choices.Count - 1);
        var opt = currentEvent.choices[idx];

        // Собираем награды выбранной опции (порядок сохранён)
        var rewardsToProcess = new List<EventSO.Reward>();
        if (opt != null && opt.rewards != null)
        {
            for (int i = 0; i < opt.rewards.Count; i++)
            {
                var r = opt.rewards[i];
                if (r != null) rewardsToProcess.Add(r);
            }
        }

        // ПЕНАЛЬТИ: копим для VFX и сразу применяем к модели статов
        var stats = FindFirstObjectByType<PlayerStatsSimple>();
        var statPenaltiesToAnimate = new List<(StatType stat, int amount)>();
        if (opt != null && opt.penalties != null)
        {
            for (int i = 0; i < opt.penalties.Count; i++)
            {
                var p = opt.penalties[i];
                if (p == null || p.amount <= 0) continue;

                int val = Mathf.Max(1, p.amount);
                statPenaltiesToAnimate.Add((p.stat, val));

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
        }

        // Всё, что лежит в зоне события, отправляем в сброс ДО анимаций
        MovePlacedCardsToDiscard();

        // Прячем окно и запускаем общий «оркестратор» (как в EventWindowUI)
        Hide();
        StartCoroutine(ProcessRewardsSequentially(rewardsToProcess, statPenaltiesToAnimate));
    }


    /// Полная последовательность: пенальти → ресторы → ресурсы → модалка новых карт → анимация карт → free-reward бой (pre/post) → продолжение → финал
    private IEnumerator ProcessRewardsSequentially(
        List<EventSO.Reward> rewards,
        List<(StatType stat, int amount)> statPenaltiesToAnimate
    )
    {
        var inv = InventoryController.Instance;
        var deck = FindFirstObjectByType<DeckController>();
        var hand = HandController.Instance;
        var map = HexMapController.Instance ?? FindFirstObjectByType<HexMapController>(FindObjectsInactive.Include);

        // (1) Пенальти: одной пачкой (модель уже обновили в OnConfirm)
        if (statPenaltiesToAnimate != null && statPenaltiesToAnimate.Count > 0)
        {
            bool penaltyDone = false;
            RewardPickupAnimator.Instance?.PlayStatPenaltyBatch(
                sourceTile,
                statPenaltiesToAnimate,
                onDone: () => penaltyDone = true
            );
            while (!penaltyDone) yield return null;
        }

        // (2) Награды по очереди (строго одна за другой)
        if (rewards != null)
            for (int i = 0; i < rewards.Count; i++)
            {
                var r = rewards[i];
                if (r == null) continue;

                // Доп. гейт награды (если включён)
                if (r.gatedByAdditional)
                {
                    int haveTag = r.requiredTag switch
                    {
                        AddTag.Brain => dropZone.currentBrain,
                        AddTag.Power => dropZone.currentPower,
                        AddTag.Speed => dropZone.currentSpeed,
                        _ => 0
                    };
                    if (haveTag < r.requiredAmount) continue;
                }

                // 2.1) Ресторы статов: модель сразу, VFX после
                if (r.type == EventSO.RewardType.RestoreStat && r.restoreAmount > 0)
                {
                    var stats = FindFirstObjectByType<PlayerStatsSimple>();
                    int val = Mathf.Max(1, r.restoreAmount);

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

                    bool restDone = false;
                    RewardPickupAnimator.Instance?.PlayStatRestoreBatch(
                        sourceTile,
                        new List<(EventSO.PlayerStat stat, int amount)> { (r.stat, val) },
                        onDone: () => restDone = true
                    );
                    while (!restDone) yield return null;
                    continue;
                }

                // 2.2) Ресурсы: начисляем в onBefore, затем VFX (тайл→центр→инвентарь)
                if (r.type == EventSO.RewardType.Resource && r.resource != null && r.amount > 0)
                {
                    bool resDone = false;
                    RewardPickupAnimator.Instance?.PlayForRewards(
                        sourceTile,
                        new List<EventSO.Reward> { r },
                        onBeforeInventoryApply: () =>
                        {
                            inv?.AddResource(r.resource, Mathf.Max(1, r.amount));
                        },
                        onAfterDone: () => resDone = true
                    );
                    while (!resDone) yield return null;
                    continue;
                }

                // 2.3) Новые карты: СНАЧАЛА модалка «получены карты», потом анимация выдачи
                if (r.type == EventSO.RewardType.NewCard && r.cardDef != null && r.cardCount > 0)
                {
                    // модалка «получены карты»
                    yield return StartCoroutine(ShowCardsModalAndWait(
                        CreateCardDefList(r.cardDef, Mathf.Max(1, r.cardCount))
                    ));

                    // выдача
                    for (int k = 0; k < r.cardCount; k++)
                    {
                        var inst = new CardInstance(r.cardDef);

                        int inHandNow = hand ? hand.HandCount : 0;
                        int maxHand = hand ? hand.maxHand : 7;

                        if (inHandNow < maxHand)
                        {
                            bool cardsDone = false;
                            RewardPickupAnimator.Instance?.PlayCardsToHandFromDeck(
                                new List<CardInstance> { inst },
                                onDone: () => cardsDone = true
                            );
                            while (!cardsDone) yield return null;

                            if (hand != null)
                            {
                                hand.AddCardToHand(inst);
                                hand.RaisePilesChanged();
                            }
                        }
                        else
                        {
                            deck?.AddToTop(inst); // перебор — на верх колоды без анимации
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

                // 2.4) Free-reward, который запускает бой (pre/post модалки, ожидание боя)
                StartAdHocCombatEffectDef combatEff = null;
                if (r.type == EventSO.RewardType.FreeReward && r.freeReward != null && r.freeReward.effects != null)
                {
                    foreach (var eff in r.freeReward.effects)
                    {
                        if (eff is StartAdHocCombatEffectDef sc) { combatEff = sc; break; }
                    }
                }

                if (combatEff != null)
                {
                    // pre-модалка боя (если задана)
                    if (!string.IsNullOrEmpty(combatEff.preFightCatalogKey))
                        yield return StartCoroutine(ShowFreeModalAndWait(combatEff.preFightCatalogKey));

                    // запуск боя и ожидание результата
                    if (map != null && sourceTile != null && combatEff.enemies != null && combatEff.enemies.Count > 0)
                    {
                        var cc1 = HexMapController.Instance ?? FindFirstObjectByType<HexMapController>(FindObjectsInactive.Include);
                        if (cc1) cc1.suppressMapCleanupOnce = true;

                        bool finished = false;
                        bool playerWon = false;

                        ModalGate.Acquire(this);
                        map.StartAdHocCombat(sourceTile, combatEff.enemies, won => { finished = true; playerWon = won; });
                        while (!finished) yield return null;
                        ModalGate.Release(this);

                        if (!playerWon) yield break;   // смерть/поражение — выходим из последовательности
                    }

                    // post-модалка боя (если задана)
                    if (!string.IsNullOrEmpty(combatEff.postFightCatalogKey))
                        yield return StartCoroutine(ShowFreeModalAndWait(combatEff.postFightCatalogKey));

                    continue; // к следующей награде
                }

                // 2.5) Прочий «кастомный» эффект награды
                //if (r.rewardEffect != null)
                //{
                //    r.rewardEffect.Execute(new EffectContext());
                //    yield return null;
                //}
            }

        // (3) Финал: если бой этого не сделал — очищаем тайл и переносим фишку
        if (sourceTile != null)
        {
            sourceTile.SetType(HexType.Empty);
            sourceTile.eventData = null;
            sourceTile.Reveal();
            sourceTile.UpdateVisual();
            var map2 = HexMapController.Instance ?? FindFirstObjectByType<HexMapController>(FindObjectsInactive.Include);
            if (map2 && map2.playerPawn) map2.playerPawn.MoveTo(sourceTile);
        }

        sourceTile = null;
        gameObject.SetActive(false);
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

    // Показать большую free-reward модалку из каталога и дождаться ОК
    private IEnumerator ShowFreeModalAndWait(string catalogKey)
    {
        string title = null, body = null;
        Sprite picture = null;

        var provider = ModalContentProvider.Instance;
        if (provider != null)
        {
            var rc = provider.Resolve(catalogKey); // rc: title, description, image
            title = rc.title;
            body = rc.description;
            picture = rc.image;
        }

        var req = new ModalRequest
        {
            kind = ModalKind.FreeReward,
            size = ModalSize.Medium,
            title = title,
            message = body,
            picture = picture
        };

        bool closed = false;
        ModalManager.Instance?.Show(req, onClose: _ => closed = true);
        while (!closed) yield return null;
    }

    // Сервис: N повторов одного CardDef
    private List<CardDef> CreateCardDefList(CardDef def, int count)
    {
        var l = new List<CardDef>(count);
        for (int i = 0; i < count; i++) l.Add(def);
        return l;
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
