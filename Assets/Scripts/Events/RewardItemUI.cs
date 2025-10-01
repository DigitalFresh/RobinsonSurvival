using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardItemUI : MonoBehaviour
{
    public Image icon;                 // �������� �������
    public TextMeshProUGUI amountText; // ���������� (��� ������� � �������/����)
    public Image gateIcon;             // ��������� ������ Brain/Power/Speed (����� null)
    public Image frame;                // ����� (�������, ���� �� ��������� �������)

    // ������� ��� gateIcon �� ���� (0=Brain,1=Power,2=Speed)
    public Sprite[] gateSprites;

    [Header("Sprites for RestoreStat (optional)")]
    public Sprite energySprite;        // ������ ������� (����� ������������ � amountText)
    public TextMeshProUGUI energy_nummer;      // ����� �+N� ������� (amountText ��� RestoreStat ������ �����)
    public Sprite[] hungerSprites = new Sprite[5]; // 1..5
    public Sprite[] thirstSprites = new Sprite[5]; // 1..5
    public Sprite[] healthSprites = new Sprite[5]; // 1..5

    [Header("Sprites for NewCard (fallback)")]
    public Sprite newCardKnownSprite;      // ���� r.icon ������ � knownPreview == true
    public Sprite newCardUnknownSprite;    // ���� r.icon ������ � knownPreview == false

    [Header("Optional: click/select")]
    public Button selectButton; // ����� ��������� �� �������� ������ ����� (���� �������� ���� Button)

    //private EventSO.Reward bound;          // ������� ������ (�� ������ ��������� ���������)

    public void Bind(EventSO.Reward r)
    {
        // ����� �� ���������
        if (icon) icon.sprite = null;
        if (amountText) { amountText.text = ""; amountText.gameObject.SetActive(true); } // ������� �� ���������
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
            if (amountText) amountText.text = "";                // ����� �� �����
                                                                 // gating-������ ��������� ��� � ��� (������������ ����� ���� gateIcon/SetGateState)
        }

        //bound = r;
        //gameObject.SetActive(r != null);
        //if (r == null) return;

        //// �� ��������� ������� �����
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
                // �� ������ ������ � ������ ������� ��, ��� �������� � icon/amount
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
        SetGateState(true); // �� ��������� ����������, ����� EventWindowUI ������� �� �����
    }

    public void SetGateState(bool ok)
    {
        if (frame) frame.color = ok ? Color.white : Color.red;
    }

    // ����� ��������� ���������� ������������ (����� ����� = ������, ������� = �� ������)
    public void SetAltSelection(bool isSelected)
    {
        if (frame) frame.color = isSelected ? Color.white : Color.red;
    }

    private void DrawAsResource(EventSO.Reward r)
    {
        // ������: ������ �� ResourceDef, ���������� ���������� � amountText
        if (icon) icon.sprite = r.resource ? r.resource.icon : null;      // //// ���� �� SO
        if (amountText)
        {
            amountText.gameObject.SetActive(true);                        // ��� �������� � ����������
            amountText.text = r.amount.ToString();
        }
    }

    // ������ �������������� ������
    private void DrawAsRestore(EventSO.Reward r)
    {
        // �������: ������������� ������ + ���������� ����� �������������� � amountText
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

        // ��������� ��������� (�����/�����/�����):
        // �������� ������ �� ���������������� ������� �� ���������� (1..5)
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
        // ��� �����/�����/����� ����� �� ���������� � ��� ������� � �������
        if (amountText) amountText.text = "";
    }

    // ���������� ��������� �������� �������
    private Sprite GetFrom(Sprite[] arr, int idx)
    {
        if (arr == null || idx < 0 || idx >= arr.Length) return null;
        return arr[idx];
    }

    private void DrawAsNewCard(EventSO.Reward r)
    {
        // ���� � ����� Reward ����� icon � ���������� ���.
        // ����� ������� ������ (���������/�����������).
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
        return fallback; // ���� �� ��������� ������� � ������� ��, ��� ������� � Reward.icon
    }
}

