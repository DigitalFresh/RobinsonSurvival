using UnityEngine;                       // MonoBehaviour, Mathf
using UnityEngine.UI;                    // Image (��� �������)
using TMPro;                             // TextMeshProUGUI

// HUD ���������� ������ (������ + ����� + �������)
public class StatsHUD : MonoBehaviour
{
    // ��������� ��������� � ���� ������ HUD (������, �����, �������)
    [System.Serializable]                // ����� ���� ����� � ����������
    public class StatRow
    {
        public Image icon;               // �������� ����� (������/������/�����/������)
        public TextMeshProUGUI value;    // ����� �����
        public Image barFill;            // ������� (Image Type = Filled, Horizontal, Origin Left)
    }

    [Header("Rows")]
    public StatRow healthRow;            // ������ �������
    public StatRow energyRow;            // ������ ���������
    public StatRow thirstRow;            // ������ ������
    public StatRow hungerRow;            // ������ ���������

    [Header("Source")]
    public PlayerStatsSimple stats;      // ������ ����� ��������

    private void Awake()                 // ������������� ������
    {
        if (stats == null)               // ���� �� ��������� � ����������
            stats = FindFirstObjectByType<PlayerStatsSimple>(); // ���� �� �����
        RefreshAll();                    // ����� �������� ��������� ��������
    }

    private void OnEnable()              // �������� �� �������
    {
        if (stats != null)               // ���� ���� ��������
            stats.OnStatsChanged += RefreshAll; // ������������� �� ����������
    }

    private void OnDisable()             // �������
    {
        if (stats != null)               // ���� �������� �����
            stats.OnStatsChanged -= RefreshAll; // ������� ��������
    }

    private void RefreshAll()            // �������� ��� 4 ������ HUD
    {
        if (stats == null) return;       // ������

        // �����
        if (healthRow != null && healthRow.value != null && healthRow.barFill != null)
        {
            healthRow.value.text = stats.Health.ToString();                          // �����
            healthRow.barFill.fillAmount = Safe01((float)stats.Health / stats.maxHealth); // ���� �������
        }

        // �������
        if (energyRow != null && energyRow.value != null && energyRow.barFill != null)
        {
            energyRow.value.text = stats.Energy.ToString();                          // �����
            energyRow.barFill.fillAmount = Safe01((float)stats.Energy / stats.maxEnergy); // ���� �������
        }

        // ����� (������� ����)
        if (thirstRow != null && thirstRow.value != null && thirstRow.barFill != null)
        {
            thirstRow.value.text = stats.Thirst.ToString();                          // �����
            thirstRow.barFill.fillAmount = Safe01((float)stats.Thirst / stats.maxThirst); // ���� �������
        }

        // �������
        if (hungerRow != null && hungerRow.value != null && hungerRow.barFill != null)
        {
            hungerRow.value.text = stats.Hunger.ToString();                          // �����
            hungerRow.barFill.fillAmount = Safe01((float)stats.Hunger / stats.maxHunger); // ���� �������
        }
    }

    private float Safe01(float v)        // ��������: ��������� ������ � [0..1]
    {
        return Mathf.Clamp01(v);         // ������������
    }
}
