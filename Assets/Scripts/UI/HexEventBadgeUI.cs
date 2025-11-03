using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEditor.PackageManager.Requests;
using UnityEngine.VFX;

/// Бейдж события на гексе (world-space UI): НЕ ловит клики, только рендерит данные EventSO.
public class HexEventBadgeUI : MonoBehaviour
{
    [Header("Infra / visibility")]
    public Canvas canvas;                       // World Space Canvas бейджа (для SortingLayer/Order)


    public CanvasGroup canvasGroup;             // Быстрый контроль видимости
    public bool startHidden = false;            // Удобно, если хотим включать по Reveal
    public bool faceCamera = false;             // Если нужно повернуть к камере (2D обычно не надо)
    public CanvasScaler scaler;           // (не обяз.) если есть — используем для масштабирования
    private Camera cam;

    [Header("Fit to hex")]
    public float padding = 1f;         // чуть ужать, чтобы не вылезало за края
    public float fallbackDesignWidth = 417f; // «дизайн-ширина» префаба в пикселях/единицах, на случай если не получится прочитать из RectTransform

    [Header("Cost text colors (Amount)")]
    public Color costTextColorHands = Color.white;                         // ✋ зелёный фон → белый текст
    public Color costTextColorFists = new Color(0.90f, 0.15f, 0.15f, 1f);  // 👊 красный фон → красный текст
    public Color costTextColorEye = new Color(0.20f, 0.50f, 1.00f, 1f);  // 👁 синий фон  → синий текст

    // ================== BARRIERS: UI на бейдже ==================
    [Header("Barriers (optional)")]
    [SerializeField] private GameObject barriersPanel;                     // корневая панель
    [SerializeField] private UnityEngine.UI.Image[] barrierSlots = new UnityEngine.UI.Image[3]; // 3 слота
    [SerializeField] private Sprite bar1Sprite;                            // спрайт синей фишки (+1)
    [SerializeField] private Sprite bar3Sprite;                            // спрайт оранжевой (+3)

    [Header("Amount font sizes")]
    [SerializeField] private int amountFontSmall = 40;   // для значений ≥ 10
    [SerializeField] private int amountFontLarge = 62;   // для значений < 10
    [SerializeField] private int amountSwitchThreshold = 10;

    // Текущая суммарная «стоимость» барьеров на этом гексе, полученная через SetBarriers(...)
    private int _barrierTotal = 0;

    // Корневой RectTransform бейджа, по которому меряем фактическую ширину
    [SerializeField, HideInInspector] private RectTransform _measureRT;
    // Базовый локальный масштаб корня (на момент инициализации)
    [SerializeField, HideInInspector] private Vector3 _initialLocalScale = Vector3.one;
    // Мировая ширина бейджа при базовом масштабе (калибровка)
    [SerializeField, HideInInspector] private float _designWorldWidth = 0f;

    float _designWidth;                   // закешируем исходную ширину префаба


    // 2) hex_choose_txt_darker — тёмная подложка под текст выбора (только для isChoice)
    public Image hex_choose_txt_darker;

    // 3) hex_picture_mask (маска) и picture (арт события)
    //public Mask hex_picture_mask;               // можно и RectMask2D, если используете UGUI маску
    public Image picture;

    // 6) hex_req2_back — видна у простого события, только когда есть И доп.стоимость, И штрафы
    // 7) hex_req1_back — видна у простого события, когда есть (доп.стоимость И штрафы) ИЛИ (есть что-то одно)
    public Image hex_req2_back;
    public Image hex_req1_back;

    // 8) hex_color — подложка цвета/типа стоимости (3 спрайта по CostType: ✋,👊,👁)
    public Image hex_color;
    public Sprite[] hex_color_byCostType;       // [0=Hands,1=Fists,2=Eye] — назначьте в инспекторе

    // 9) Amount — число стоимости (только простое событие)
    public TextMeshProUGUI Amount;

    // 10) hex_choose — картинка выбора (2 спрайта: обычное выборное/неотменяемое)
    public Image hex_choose;
    public Sprite chooseSprite_Normal;          // показывать когда isChoice && choiceCancelable == true
    public Sprite chooseSprite_NoCancel;        // показывать когда isChoice && choiceCancelable == false

    // 11) hex_enemy — отображать только если isCombat (спрайт 0 — обычный, 1 — агрессивный)
    public Image hex_enemy;
    public Sprite combatSprite_Normal;
    public Sprite combatSprite_Aggressive;
    public GameObject enemy_ark;

    [Header("Combat title colors")]
    [SerializeField] private Color titleColorAggressive = Color.white;                 // агрессивный бой → белый
    [SerializeField] private Color titleColorNonAggressive = new(0.90f, 0.15f, 0.15f); // НЕагрессивный (false) → красный
    [SerializeField] private Color titleColorTimid = new(1.00f, 0.55f, 0.75f);

    // 12) Attack — цифра/иконка атаки врага (для боя, на будущее)
    // 13) Lifes — цифра/иконка жизней врага (для боя, на будущее)
    public GameObject Attack;                   // держим как GameObject, т.к. пока логика боя не введена
    public GameObject Lifes;

    // 14) shield — значок «броня» (только если есть броня)
    // 15) Armor — цифра «броня»
    public GameObject shield;
    public TextMeshProUGUI Armor;

    // 17) request — родитель для иконок AdditionalCosts (Brain/Power/Speed)
    //     Требование: менять PosX = -81.2 если есть penalties, иначе = -75
    public GameObject request; // контейнер для дополнительных требований
    public float requestX_WithPenalties = -81.2f;
    public float requestX_NoPenalties = -75f;
    public Image[] adCostIcons;            // массив из 3 Image "ad_cost" (по индексу 0..2) public Image[] adCostIcons;
    public Sprite[] adCostSprites;         // [Brain, Power, Speed]

    // 18) penalty — родитель для иконок первых трех штрафов
    public GameObject penaltyContainer;          // сюда инстансим 0..3 первых штрафа
    public Image[] penaltyIcons;           // Cost_1..Cost_4
    public Sprite[] penaltySprites;        // [Hunger,Thirst,Energy,Health]

    // 19) Title — название события
    public TextMeshProUGUI Title;

    // 20) res_Panel — панель наград обычного простого события
    //     Показываем как в EventWindowUI (иконки/кол-во). Берём ТОЖЕ RewardItemUI.
    public GameObject res_Panel; // контейнер для четырех обычных наград
    public RewardItemUI[] rewardItems;     // res_1..res_4

    // 21) AltResPanel — панель альтернативных наград (как в EventWindowUI)
    public GameObject AltResPanel;   // общий контейнер альтернативного режима (2 слота + разделитель)
    public RewardItemUI altRewardA;     // левый слот
    public RewardItemUI altRewardB;     // правый слот

    // 22) Choose_description — описание выбора (для isChoice)
    public GameObject Choose_description_parent;
    public TextMeshProUGUI Choose_description;

    // Текущее событие (для возможных ре-рендеров)
    private EventSO current;

    void Awake()
    {
        cam = Camera.main;
        // делаем UI «прозрачным» для кликов
        var gr = GetComponentInChildren<GraphicRaycaster>(true);
        if (gr) gr.enabled = false;
        if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (!canvas) canvas = GetComponentInChildren<Canvas>(true);
        if (!scaler) scaler = canvas ? canvas.GetComponent<CanvasScaler>() : null;

        //// Берём корневой RectTransform для измерения габаритов (предпочтительно Canvas)
        //_measureRT = canvas ? canvas.GetComponent<RectTransform>()
        //                    : transform as RectTransform;

        //// Запоминаем исходный масштаб корня бейджа
        //_initialLocalScale = transform.localScale;

        //// Калибруем «дизайн‑ширину» в МИРОВЫХ юнитах — это устранит зависимость от пикселей/скейлера
        //_designWorldWidth = ComputeWorldWidth(_measureRT);
        //if (_designWorldWidth <= 0.0001f)
        //    _designWorldWidth = fallbackDesignWidth; // последняя страховка

        // попробуем взять фактическую ширину корневого RectTransform (в единицах префаба)
        var rt = transform as RectTransform;
        _designWidth = (rt && rt.rect.width > 0f) ? rt.rect.width : fallbackDesignWidth;

        SetVisible(!startHidden);
    }

    void LateUpdate()
    {
        if (!faceCamera || cam == null) return;
        // В 2D обычно не нужен billboard, оставим заглушку на будущее
        // transform.LookAt(cam.transform); // если бы было 3D
    }

    /// Публично: показать/скрыть бейдж
    public void SetVisible(bool v)
    {
        if (canvasGroup)
        {
            canvasGroup.alpha = v ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(v);
    }

    /// Главный метод: привязать данные EventSO к элементам UI бейджа.
    public void Bind(EventSO ev)
    {
        current = ev;
        if (ev == null)
        {
            SetVisible(false);
            return;
        }

        // Подложка под текст выбора только для isChoice
        SafeEnable(hex_choose_txt_darker, ev.isChoice);

        // Картинка события
        if (picture)
        {
            picture.sprite = ev.icon;
            picture.enabled = (ev.icon != null);
        }

        // Название
        if (Title) Title.text = ev.eventName ?? "";

        // --- ПРОСТОЕ СОБЫТИЕ vs ВЫБОР ---
        bool isSimple = !ev.isChoice;
        bool hasAdCosts = (ev.additionalCosts != null && ev.additionalCosts.Count > 0);
        bool hasPenalties = (ev.penalties != null && ev.penalties.Count > 0);

        // hex_req2_back: только у простого, и только если есть И ад.косты, И штрафы
        SafeEnable(hex_req2_back, isSimple && hasAdCosts && hasPenalties);

        // hex_req1_back: только у простого, и если есть (И) или есть любое одно
        bool showReq1 = isSimple && ((hasAdCosts && hasPenalties) || (hasAdCosts ^ hasPenalties));
        SafeEnable(hex_req1_back, showReq1);

        // hex_color и Amount — только для простого события
        if (hex_color)
        {
            if (isSimple && hex_color_byCostType != null && hex_color_byCostType.Length >= 3)
            {
                int idx = (int)ev.mainCostType; // 0,1,2
                hex_color.sprite = hex_color_byCostType[idx];
                hex_color.enabled = true;
            }
            else hex_color.enabled = false;
        }
        if (Amount)
        {
            Amount.text = (isSimple ? ev.mainCostAmount.ToString() : "");
            Amount.gameObject.SetActive(isSimple);
        }
        // цвет текста Amount по типу стоимости ---
        if (isSimple && Amount)                                              // Если простое событие и есть текст
        {
            Amount.color = GetCostTextColor(ev.mainCostType);                // Выставить цвет
            if (isSimple) ApplyEffectiveCost();
        }
        // hex_choose — только для события с выбором
        if (hex_choose)
        {
            if (ev.isChoice)
            {
                bool cancelable = true; // по умолчанию — отменяемое
                // Если вы уже добавили в EventSO поле choiceCancelable — используем его:
                // cancelable = ev.choiceCancelable;
                hex_choose.sprite = cancelable ? chooseSprite_Normal : chooseSprite_NoCancel;
                hex_choose.enabled = true;
            }
            else hex_choose.enabled = false;
        }

        // hex_enemy — только для боя; спрайт по «агрессивности»
        if (hex_enemy)
        {
            if (ev.isCombat)                                                // Если событие — бой
            {
                bool aggressive = ev.isAggressiveCombat;                    // Берём флаг агрессии из EventSO
                hex_enemy.sprite = aggressive ?                             // Выбираем спрайт
                    combatSprite_Aggressive :
                    combatSprite_Normal;
                hex_enemy.enabled = (hex_enemy.sprite != null);             // Включаем, если есть спрайт
            }
            else hex_enemy.enabled = false;                                 // Не бой — прячем
        }

        // Превью противника (картинка + статы)
        if (ev.isCombat && ev.HasCombatEnemies())                            // Если у боя есть враги
        {
            // 1) Превьюшный враг
            var e = ev.GetPreviewEnemy();                                    // Берём EnemySO по индексу previewEnemyIndex

            // 1.1) Картинка — используем спрайт врага вместо icon события
            if (picture)
            {
                picture.sprite = (ev.icon != null) ? ev.icon : e.sprite;       // Если у врага нет спрайта — фоллбэк на icon события
                picture.enabled = (picture.sprite != null);                  // Включаем, если есть что рисовать
            }

            // 1.2) Атака — ищем TMP внутри контейнера Attack и пишем число
            if (Attack)
            {
                var tmp = Attack.GetComponentInChildren<TextMeshProUGUI>(true); // Любой TMP в детях
                if (tmp) tmp.text = (e != null ? e.attack.ToString() : "");  // Пишем атаку
                Attack.SetActive(e != null);                                  // Контейнер включаем/выключаем
            }

            // 1.3) Жизни (maxHP) — аналогично
            if (Lifes)
            {
                var tmp = Lifes.GetComponentInChildren<TextMeshProUGUI>(true);   // TMP в детях
                if (tmp) tmp.text = (e != null ? e.maxHP.ToString() : "");       // Пишем жизни
                Lifes.SetActive(e != null);                                       // Контейнер включаем/выключаем
            }

            // 1.4) Броня — показываем щит и число только если armor > 0
            int armor = (e != null ? e.armor : 0);                              // Броня врага
            if (shield) shield.SetActive(armor > 0);                             // Иконка щита — только при >0
            if (Armor)
            {
                Armor.text = armor > 0 ? armor.ToString() : "";                  // Текст брони
                Armor.gameObject.SetActive(armor > 0);                           // Прячем текст, если 0
            }

            // 2) enemy_ark — включаем 1..3 маленьких индикатора количества врагов
            if (enemy_ark)
            {
                int count = Mathf.Clamp(ev.combatEnemies != null ? ev.combatEnemies.Count : 0, 0, 3); // Сколько врагов (0..3)
                enemy_ark.SetActive(count > 0);                              // Показываем контейнер, если есть враги
                                                                             // Проходим по детям контейнера и включаем первые count, остальные выключаем
                for (int i = 0; i < enemy_ark.transform.childCount; i++)     // Для каждого ребёнка
                {
                    var child = enemy_ark.transform.GetChild(i);             // Ребёнок по индексу
                    if (child) child.gameObject.SetActive(i < count);        // Первые count — ON, остальные — OFF
                }
            }
        }
        else
        {
            // Не бой — прячем боевые элементы
            SafeEnable(Attack, false);                                       // Прячем атаку
            SafeEnable(Lifes, false);                                        // Прячем жизни
            SafeEnable(shield, false);                                       // Прячем щит
            if (Armor) { Armor.text = ""; Armor.gameObject.SetActive(false); } // Прячем текст брони
            SafeEnable(enemy_ark, false);                                    // Прячем счётчик врагов
        }

        if (ev.isCombat && Title)
        {
            bool timidByPreview = IsTimidPreview(ev);      // см. хелпер ниже
            if (timidByPreview) Title.color = titleColorTimid;
            else if (!ev.isAggressiveCombat) Title.color = titleColorNonAggressive;
            else Title.color = titleColorAggressive;
        }
        else
        {
            if (Title) Title.color = Color.white; // для не боя — по умолчанию белый
        }


        // request (доп.требования): показываем ТОЛЬКО в простом событии
        if (request)
        {
            if (isSimple && hasPenalties && hasAdCosts)
            {
                var ap = request.transform.position;
                ap.x += 0.1f;
                //Debug.Log(ap.x);
                request.transform.position = ap;
            }
            SafeEnable(request, isSimple && hasAdCosts);

            if (isSimple && hasAdCosts)
            {
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
            }
        }

        // penalty (первые до 3-х штрафов): только простое событие
        SafeEnable(penaltyContainer, isSimple && hasPenalties);
        if (isSimple && hasPenalties)
        {
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
        }

        // res_Panel — обычные награды (если НЕ альтернативные)
        bool isAlternativeRewards = (ev.rewardsAreAlternative); // поле вы уже добавляли для простых событий с альтернативной наградой
        SafeEnable(res_Panel, isSimple && !isAlternativeRewards);
        if (isSimple && !isAlternativeRewards)
        {
            for (int i = 0; i < rewardItems.Length; i++)
            {
                var item = rewardItems[i];
                if (!item) continue;
                if (ev != null && i < ev.rewards.Count) { item.gameObject.SetActive(true); item.Bind(ev.rewards[i]); }
                else item.gameObject.SetActive(false);
            }
        }
        // AltResPanel — альтернативные награды (2 панели)
        SafeEnable(AltResPanel, isSimple && isAlternativeRewards);
        if (isSimple && isAlternativeRewards && ev.alternativeRewards != null && ev.alternativeRewards.Count > 0)
        {
            altRewardA.gameObject.SetActive(true); altRewardA.Bind(ev.alternativeRewards[0]);
            altRewardB.gameObject.SetActive(true); altRewardB.Bind(ev.alternativeRewards[1]);

        }

        // Choose_description — только у события с выбором
        if (Choose_description)
        {
            Choose_description_parent.gameObject.SetActive(ev.isChoice);
            if (ev.isChoice) Choose_description.text = ev.description ?? "";
        }

        if (ev.isCombat)
        {
            if (res_Panel) res_Panel.SetActive(true);                       // включаем панель
            var uniq = CollectCombatLootResources(ev, 4);                    // забираем ресурсы из loot
            for (int i = 0; i < rewardItems.Length; i++)
            {
                var slot = rewardItems[i];
                if (!slot) continue;

                if (i < uniq.Count && uniq[i] != null)
                {
                    slot.gameObject.SetActive(true);
                    slot.Bind(uniq[i]);                                      // обычный биндинг
                    if (slot.amountText) slot.amountText.gameObject.SetActive(false); // ЦИФРЫ НЕ ПОКАЗЫВАЕМ
                }
                else slot.gameObject.SetActive(false);
            }
        }

        // и наконец — показать
        SetVisible(true);

    }

    private Color GetCostTextColor(CostType t)                             // Хелпер выбора цвета
    {
        switch (t)                                                         // По типу стоимости
        {
            case CostType.Fists: return costTextColorFists;                // 👊 → красный
            case CostType.Eye: return costTextColorEye;                  // 👁 → синий
            case CostType.Hands: // падение вниз
            default: return costTextColorHands;                            // ✋ → белый
        }
    }

    /// <summary>Обновить отрисовку фишек на бейдже (вызывается из HexTile).</summary>
    public void SetBarriers(System.Collections.Generic.IReadOnlyList<int> values)
    {
        if (barriersPanel == null || barrierSlots == null) return;

        bool hasAny = values != null && values.Count > 0;
        barriersPanel.SetActive(hasAny);

        _barrierTotal = 0; // заново считаем сумму

        for (int i = 0; i < barrierSlots.Length; i++)
        {
            var img = barrierSlots[i];
            if (!img) continue;

            if (hasAny && i < values.Count)
            {
                int v = values[i] >= 3 ? 3 : 1;   // нормализуем в 1 или 3
                _barrierTotal += v;               // суммируем для цены
                img.enabled = true;
                img.sprite = (v == 3) ? bar3Sprite : bar1Sprite;
            }
            else
            {
                img.enabled = false;
            }
        }

        // Если бейдж уже привязан к simple-событию — сразу обновим Amount
        ApplyEffectiveCost();
    }


    // =============== helpers ===============

    private void SafeEnable(Behaviour b, bool enabled)
    {
        if (!b) return;
        b.enabled = enabled;
        // Если это Mask/RectMask2D — достаточно enabled
    }

    private void SafeEnable(GameObject go, bool enabled)
    {
        if (!go) return;
        go.SetActive(enabled);
    }
    /// Позволяет гарантированно рисовать поверх спрайта гекса
    public void ConfigureSortingLike(SpriteRenderer refRenderer, int orderOffset = 10)
    {
        if (!canvas || !refRenderer) return;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingLayerID = refRenderer.sortingLayerID;
        canvas.sortingLayerName = refRenderer.sortingLayerName;
        canvas.sortingOrder = refRenderer.sortingOrder + orderOffset;
    }

    // Перерисовать Amount с учётом барьеров, если это простое событие
    private void ApplyEffectiveCost()
    {
        if (current == null || current.isChoice || current.isCombat) return; // только simple
        int val = current.mainCostAmount + _barrierTotal;
        if (Amount)
        {
            Amount.text = val.ToString();
            ApplyAmountFontForValue(val);   // <-- размер шрифта по значению
        }
    }

    // timid только у preview-врага (других НЕ проверяем)
    private bool IsTimidPreview(EventSO ev)
    {
        var enemy = ev ? ev.GetPreviewEnemy() : null;                 // берём врага по previewEnemyIndex
        if (!enemy || enemy.tags == null) return false;               // нет врага/тегов

        for (int i = 0; i < enemy.tags.Count; i++)                    // проверяем TagDef.id
        {
            var t = enemy.tags[i];
            if (t && !string.IsNullOrEmpty(t.id) &&
                string.Equals(t.id, "Timid", System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Собираем уникальные ресурсы из EnemySO.loot по всем врагам (макс. maxKinds)
    private System.Collections.Generic.List<EventSO.Reward>
        CollectCombatLootResources(EventSO ev, int maxKinds)
    {
        var result = new System.Collections.Generic.List<EventSO.Reward>(maxKinds);
        var seen = new System.Collections.Generic.HashSet<string>();       // ключ – resourceId/имя

        if (!ev || ev.combatEnemies == null) return result;

        for (int i = 0; i < ev.combatEnemies.Count && result.Count < maxKinds; i++)
        {
            var enemy = ev.combatEnemies[i];
            if (!enemy || enemy.loot == null) continue;

            // EnemySO.loot — List<LootEntry> { ResourceDef resource; int amount; }  :contentReference[oaicite:5]{index=5}
            for (int j = 0; j < enemy.loot.Count && result.Count < maxKinds; j++)
            {
                var e = enemy.loot[j];
                if (e == null || e.resource == null) continue;

                // ключ уникальности: resourceId если есть, иначе имя SO
                string key = !string.IsNullOrEmpty(e.resource.resourceId)
                            ? e.resource.resourceId
                            : e.resource.name;
                if (seen.Contains(key)) continue;

                // приводим к EventSO.Reward (тип Resource); amount нам НЕ нужен (иконку показываем без цифры)
                var r = new EventSO.Reward
                {
                    type = EventSO.RewardType.Resource,
                    resource = e.resource,
                    amount = e.amount,      // можно оставить фактическое; цифру всё равно скрываем
                    icon = e.resource.icon  // на всякий случай
                };

                result.Add(r);
                seen.Add(key);
            }
        }
        return result;
    }

    /// Подгоняем размер бейджа под размеры гекса (ширину его спрайта)
    public void FitToHex(SpriteRenderer hexRenderer)
    {
        if (!hexRenderer) return;

        // мировая ширина гекса
        float hexWorldWidth = hexRenderer.bounds.size.x * padding;

        // коэффициент: сколько «наших единиц префаба» приходится на мировую ширину гекса
        float k = (_designWidth > 0f) ? (hexWorldWidth / _designWidth) : 1f;

        // два варианта: через CanvasScaler или просто масштабом трансформа
        if (scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // скейлим весь Canvas
            scaler.scaleFactor = k;
        }
        else
        {
            transform.localScale = Vector3.one * k; // простой и надёжный путь
        }
    }
//    {
//    if (!hexRenderer || !_measureRT) return;

//    // Мировая ширина гекса (по его спрайту); padding немного «ужимает» бейдж внутрь гекса
//    float hexWorldWidth = hexRenderer.bounds.size.x * padding;

//    // Если калибровки нет, пересчитаем на лету
//    if (_designWorldWidth <= 0.0001f)
//        _designWorldWidth = ComputeWorldWidth(_measureRT);

//    // Коэффициент, который делает мировую ширину бейджа равной мировой ширине гекса
//    float k = (_designWorldWidth > 0f) ? (hexWorldWidth / _designWorldWidth) : 1f;

//    // ВНИМАНИЕ: не трогаем CanvasScaler — скейлим сам корень бейджа,
//    // чтобы не «размаживать» UI и не влиять на другие элементы.
//    transform.localScale = _initialLocalScale* k;
//}

private static float ComputeWorldWidth(RectTransform rt)
    {
        if (!rt) return 0f;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        // 0 = LB, 1 = LT, 2 = RT, 3 = RB (для стандартной ориентации)
        return Vector3.Distance(corners[0], corners[3]); // ширина по нижнему ребру
    }

    private void ApplyAmountFontForValue(int value)
    {
        if (!Amount) return;
        Amount.fontSize = (value >= amountSwitchThreshold) ? amountFontSmall : amountFontLarge;
    }

#if UNITY_EDITOR
    // Чтобы при настройке префаба в Editor Scene ширина пересчитывалась без Play
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (!canvas) canvas = GetComponentInChildren<Canvas>(true);
            _measureRT = canvas ? canvas.GetComponent<RectTransform>()
                                : transform as RectTransform;
            _initialLocalScale = Vector3.one;
            _designWorldWidth = ComputeWorldWidth(_measureRT);
        }
    }
#endif
}
