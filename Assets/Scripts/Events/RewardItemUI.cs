using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardItemUI : MonoBehaviour
{
    public Image icon;                 // картинка ресурса
    public TextMeshProUGUI amountText; // количество (для ресурса и энергии/карт)
    public Image gateIcon;             // маленькая иконка Brain/Power/Speed (можно null)
    public Image frame;                // рамка (красная, если не выполнены условия)

    // спрайты для gateIcon по тегу (0=Brain,1=Power,2=Speed)
    public Sprite[] gateSprites;

    [Header("Sprites for RestoreStat (optional)")]
    public Sprite energySprite;        // иконка энергии (цифра показывается в amountText)
    public TextMeshProUGUI energy_nummer;      // текст «+N» энергии (amountText при RestoreStat всегда скрыт)
    public Sprite[] hungerSprites = new Sprite[5]; // 1..5
    public Sprite[] thirstSprites = new Sprite[5]; // 1..5
    public Sprite[] healthSprites = new Sprite[5]; // 1..5

    [Header("Sprites for NewCard (fallback)")]
    public Sprite newCardKnownSprite;      // если r.icon пустой и knownPreview == true
    public Sprite newCardUnknownSprite;    // если r.icon пустой и knownPreview == false

    [Header("Optional: click/select")]
    public Button selectButton; // можно назначить на корневой объект слота (если добавите туда Button)

    //private EventSO.Reward bound;          // текущие данные (на случай повторной подсветки)

    public void Bind(EventSO.Reward r)
    {
        // Сброс по умолчанию
        if (icon) icon.sprite = null;
        if (amountText) { amountText.text = ""; amountText.gameObject.SetActive(true); } // включим по умолчанию
        if (energy_nummer) { energy_nummer.text = ""; energy_nummer.gameObject.SetActive(false); }

        if (r == null)
        {
            if (gateIcon) gateIcon.enabled = false;
            SetGateState(true);
            return;
        }

        if (r != null && r.type == EventSO.RewardType.FreeReward)
        {
            if (icon) { icon.enabled = (r.freeReward != null && r.freeReward.icon != null); icon.sprite = r.freeReward?.icon; }
            if (amountText) amountText.text = "";                // цифры не нужны
                                                                 // gating-иконка оставляем как у вас (используется общий блок gateIcon/SetGateState)
        }

        //bound = r;
        //gameObject.SetActive(r != null);
        //if (r == null) return;

        //// По умолчанию очистим текст
        //if (amountText) amountText.text = "";

        switch (r.type)
        {
            case EventSO.RewardType.Resource:
                DrawAsResource(r);
                break;

            case EventSO.RewardType.RestoreStat:
                DrawAsRestore(r);
                break;

            case EventSO.RewardType.NewCard:
                DrawAsNewCard(r);
                break;

            default:
                // На всякий случай — просто покажем то, что прислано в icon/amount
                if (icon) icon.sprite = r.icon;
                if (amountText) amountText.text = r.amount.ToString();
                break;
        }

        if (gateIcon)
        {
            if (r != null && r.gatedByAdditional)
            {
                gateIcon.enabled = true;
                if (gateSprites != null && gateSprites.Length >= 3)
                    gateIcon.sprite = gateSprites[(int)r.requiredTag];
            }
            else gateIcon.enabled = false;
        }
        SetGateState(true); // по умолчанию «выполнено», затем EventWindowUI обновит по факту
    }

    public void SetGateState(bool ok)
    {
        if (frame) frame.color = ok ? Color.white : Color.red;
    }

    // Явная подсветка «выбранной» альтернативы (белая рамка = выбран, красная = не выбран)
    public void SetAltSelection(bool isSelected)
    {
        if (frame) frame.color = isSelected ? Color.white : Color.red;
    }

    private void DrawAsResource(EventSO.Reward r)
    {
        // РЕСУРС: иконка из ResourceDef, количество показываем в amountText
        if (icon) icon.sprite = r.resource ? r.resource.icon : null;      // //// берём из SO
        if (amountText)
        {
            amountText.gameObject.SetActive(true);                        // для ресурсов — показываем
            amountText.text = r.amount.ToString();
        }
    }

    // визуал восстановления статов
    private void DrawAsRestore(EventSO.Reward r)
    {
        // Энергия: фиксированная иконка + показываем число восстановления в amountText
        if (r.stat == EventSO.PlayerStat.Energy)
        {
            if (icon) icon.sprite = energySprite;
            {
                int val = Mathf.Max(1, r.restoreAmount);
                energy_nummer.text = val.ToString();
                energy_nummer.gameObject.SetActive(true);
            }
            return;
        }

        // Остальные параметры (Голод/Жажда/Жизнь):
        // выбираем спрайт из соответствующего массива по количеству (1..5)
        int idx = Mathf.Clamp(r.restoreAmount, 1, 5) - 1;
        Sprite s = null;

        switch (r.stat)
        {
            case EventSO.PlayerStat.Hunger: s = GetFrom(hungerSprites, idx); break;
            case EventSO.PlayerStat.Thirst: s = GetFrom(thirstSprites, idx); break;
            case EventSO.PlayerStat.Health: s = GetFrom(healthSprites, idx); break;
            default: break;
        }

        if (icon) icon.sprite = s;
        // Для Голод/Жажда/Жизнь цифру не показываем — она «зашита» в спрайте
        if (amountText) amountText.text = "";
    }

    // Безопасное получение элемента массива
    private Sprite GetFrom(Sprite[] arr, int idx)
    {
        if (arr == null || idx < 0 || idx >= arr.Length) return null;
        return arr[idx];
    }

    private void DrawAsNewCard(EventSO.Reward r)
    {
        // Если в самом Reward задан icon — используем его.
        // Иначе покажем дефолт (известная/неизвестная).
        if (icon)
        {
            if (r.icon != null) icon.sprite = r.icon;
            else icon.sprite = r.knownPreview ? newCardKnownSprite : newCardUnknownSprite;
        }
        if (amountText) amountText.text = ""; //= $"x{Mathf.Max(1, r.cardCount)}";
    }

    private Sprite PickByAmount(Sprite[] arr, int amount, Sprite fallback)
    {
        if (arr != null && arr.Length > 0)
        {
            int idx = Mathf.Clamp(amount, 1, 5) - 1;
            if (idx >= arr.Length) idx = arr.Length - 1;
            if (arr[idx] != null) return arr[idx];
        }
        return fallback; // если не назначены спрайты — остаётся то, что указано в Reward.icon
    }
}

