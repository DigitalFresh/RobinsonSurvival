using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// UI-биндер для одного варианта выбора (узел "Option" в ChooseEventWindow)
public class OptionUI : MonoBehaviour
{
    [Header("Root & selection")]
    public Button selectButton;                 // кнопка на всём Option (чтобы выбрать этот вариант)
    public GameObject selectedTxt;              // узел "Selected_txt" (виден только у выбранного)

    // фон опции и спрайты для состояний ---
    public Image rootBg;                        // Image на самом Option (или его фоне), где меняем спрайт
    public Sprite bgSelectedSprite;             // спрайт, когда вариант выбран
    public Sprite bgUnselectedSprite;           // спрайт, когда вариант НЕ выбран

    //затемнитель невыбранной опции ---
    public GameObject darker;                   // объект-плашка, включаем только когда вариант НЕ выбран

    [Header("Description")]
    public TextMeshProUGUI descriptionText;     // "Description"

    [Header("Main cost")]
    public Image hexBack;                  // Image "Hex" (смена цвета по типу)
    public Sprite[] hexBackByCostType;     // [✋=0, 👊=1, 👁=2]
    public Image iconHex;                  // Image "icon_Hex" (рука/кулак/глаз)
    public Sprite[] iconHexByCostType;     // [✋,👊,👁]
    public TextMeshProUGUI amountText;     // Text "amount"

    [Header("Cost text colors (option amount)")]
    public Color costTextColorHands = Color.white;                         // ✋
    public Color costTextColorFists = new Color(0.90f, 0.15f, 0.15f, 1f);  // 👊
    public Color costTextColorEye = new Color(0.20f, 0.50f, 1.00f, 1f);  // 👁

    [Header("Additional costs")]
    public Image[] adCostIcons;                 // "AD_costs/ad_cost_1..3"
    public Sprite[] adCostSprites;              // [Brain,Power,Speed]

    [Header("Penalties (Req_back)")]
    public GameObject reqBackPanel;             // бэк панель
    public Image[] penaltyIcons;                // Cost_1..Cost_4
    public Sprite[] penaltySprites;             // [Hunger,Thirst,Energy,Health]

    [Header("Rewards (visible)")]
    public Transform rewardsPanel;              // "res_Panel"
    public RewardItemUI[] rewardSlots;          // три-четыре "res_1" со скриптом RewardItemUI

    [Header("Hidden outcomes (if used)")]
    public GameObject hiddenContainer;          // тот же "res_Panel", но включаем/выключаем конкретные узлы
    public Image[] hiddenIcons;                 // массив ссылок на иконки "hidden_res" внутри слотов
    public TextMeshProUGUI[] hiddenTooltips;    // если есть TMP для подписи (можно опционально)

    // Рантайм-ссылка на данные
    private EventSO.ChoiceOption bound;
    public System.Action<OptionUI> OnSelectedRequest; // Вызовем у родителя ChooseEventWindowUI

    public void Bind(EventSO.ChoiceOption option)
    {
        bound = option;

        // Описание
        if (descriptionText) descriptionText.text = bound != null ? bound.description : "";

        // Главная стоимость (иконка + цифра)
        if (bound != null)
        {
            if (hexBack && hexBackByCostType != null && hexBackByCostType.Length >= 3)
                hexBack.sprite = hexBackByCostType[(int)bound.mainCostType];
            if (iconHex && iconHexByCostType != null && iconHexByCostType.Length >= 3)
                iconHex.sprite = iconHexByCostType[(int)bound.mainCostType];
            if (amountText) amountText.text = bound.mainCostAmount.ToString();
        }

        // цвет текста amount по типу стоимости для опции ---
        if (amountText && bound != null)                                      // Если есть текст
            amountText.color = GetCostTextColor(bound.mainCostType);          // Поставить цвет

        // Доп.стоимости — обязательные по условию задачи
        for (int i = 0; i < adCostIcons.Length; i++)
        {
            if (!adCostIcons[i]) continue;
            if (bound != null && i < bound.additionalCosts.Count)
            {
                var a = bound.additionalCosts[i];
                adCostIcons[i].gameObject.SetActive(true);
                if (adCostSprites != null && adCostSprites.Length >= 3)
                    adCostIcons[i].sprite = adCostSprites[(int)a.tag];
            }
            else adCostIcons[i].gameObject.SetActive(false);
        }

        // Потери параметров
        bool hasPen = (bound != null && bound.penalties != null && bound.penalties.Count > 0);
        if (reqBackPanel) reqBackPanel.SetActive(hasPen);
        for (int i = 0; i < penaltyIcons.Length; i++)
        {
            if (!penaltyIcons[i]) continue;
            if (hasPen && i < bound.penalties.Count)
            {
                var p = bound.penalties[i];
                penaltyIcons[i].gameObject.SetActive(true);
                if (penaltySprites != null && penaltySprites.Length >= 4)
                    penaltyIcons[i].sprite = penaltySprites[(int)p.stat];
            }
            else penaltyIcons[i].gameObject.SetActive(false);
        }

        // Награды ИЛИ скрытые исходы
        bool showReal = bound != null && bound.showRewards;
        bool showHidden = bound != null && bound.showHiddenOutcomes;

        // Реальные награды
        if (rewardsPanel) rewardsPanel.gameObject.SetActive(showReal);
        if (showReal && rewardSlots != null)
        {
            for (int i = 0; i < rewardSlots.Length; i++)
            {
                var slot = rewardSlots[i];
                if (!slot) continue;
                if (i < bound.rewards.Count)
                {
                    slot.gameObject.SetActive(true);
                    slot.Bind(bound.rewards[i]);    // используем ваш текущий RewardItemUI
                }
                else slot.gameObject.SetActive(false);
            }
        }

        // Скрытые исходы (иконки)
        if (hiddenContainer) hiddenContainer.SetActive(showHidden);
        if (showHidden && hiddenIcons != null)
        {
            for (int i = 0; i < hiddenIcons.Length; i++)
            {
                if (!hiddenIcons[i]) continue;
                if (i < bound.hiddenOutcomes.Count)
                {
                    var ho = bound.hiddenOutcomes[i];
                    hiddenIcons[i].gameObject.SetActive(true);
                    hiddenIcons[i].sprite = ho != null ? ho.icon : null;
                    if (hiddenTooltips != null && i < hiddenTooltips.Length && hiddenTooltips[i])
                        hiddenTooltips[i].text = ho != null ? ho.tooltip : "";
                }
                else hiddenIcons[i].gameObject.SetActive(false);
            }
        }

        // изначально «не выбран»
        SetSelected(false);
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

    // Внешний вызов из окна — подсветить/снять подсветку выбранного варианта
    public void SetSelected(bool value)
    {
        if (selectedTxt)
            selectedTxt.SetActive(value);                   // подпись "Selected" видно только у выбранной

        // === переключаем спрайт фона опции ===
        // Если в инспекторе назначен rootBg и один/оба спрайта — меняем картинку.
        if (rootBg)
        {
            // Выбираем нужный спрайт. Если не назначен — не падаем (оставим текущий).
            Sprite target = value ? bgSelectedSprite : bgUnselectedSprite;
            if (target) rootBg.sprite = target;

            // На всякий случай включим Image (если был выключен).
            if (!rootBg.enabled) rootBg.enabled = true;
        }

        // === затемнитель для невыбранной опции ===
        // Когда опция НЕ выбрана — показываем overlay "darker".
        if (darker)
            darker.SetActive(!value);

    }

    public EventSO.ChoiceOption GetBound() => bound;
}
