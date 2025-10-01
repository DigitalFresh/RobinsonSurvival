using UnityEngine;                 // ������� Unity-����
using TMPro;                       // ��� ����������� � UI (���� ������)

public class PlayerStatsSimple : MonoBehaviour
{
    // --- ������������ ��������(����� ������ � ����������) ---
    [Header("Max values")]
    public int maxHealth = 6;              // �������� ������
    public int maxEnergy = 25;             // �������� �������
    public int maxThirst = 6;              // �������� ������� (������� ����)
    public int maxHunger = 6;              // �������� ��������

    // --- ������� �������� ---
    [Header("Current values (runtime)")]
    [SerializeField] private int health;   // ������� ��������
    [SerializeField] private int energy;   // ������� �������
    [SerializeField] private int thirst;   // ������� ������� ����
    [SerializeField] private int hunger;   // ������� �������

    // ������ (��������� ������������� � ������� ���������� ������)
    [SerializeField] private int xp;       // ���� (���������)

    // �������: ���-�� �������� � ������� HUD
    public System.Action OnStatsChanged;   // �������� ��� ����� ����������

    private void Awake()                   // ������������� ��������
    {
        health = Mathf.Clamp(health <= 0 ? maxHealth : health, 0, maxHealth); // ���� �� ������ � ����� = ��������
        energy = Mathf.Clamp(energy <= 0 ? maxEnergy : energy, 0, maxEnergy); // ��������� �������� �������
        thirst = Mathf.Clamp(thirst <= 0 ? maxThirst : thirst, 0, maxThirst); // ����� ������
        hunger = Mathf.Clamp(hunger <= 0 ? maxHunger : hunger, 0, maxHunger); // ����� ��������
        RaiseChanged();                   // ����� ������� HUD
    }

    // --- ��������� ��������� (���� ���-�� �����) ---
    public int Health => health;           // ������� ��������
    public int Energy => energy;           // ������� �������
    public int Thirst => thirst;           // ������� �����
    public int Hunger => hunger;           // ������� �������


    // --- ������� �������� � ����������� ---
    public void TakeDamage(int dmg)        // �������� ����
    {
        health = Mathf.Clamp(health - Mathf.Max(0, dmg), 0, maxHealth); // ��������� ��������
        RaiseChanged();                                                  // �������� �����������
    }

    public void Heal(int amount)           // ����������
    {
        health = Mathf.Clamp(health + Mathf.Max(0, amount), 0, maxHealth); // ����������� ��������
        RaiseChanged();                                                      // �������� HUD
    }

    public bool SpendEnergy(int amount)    // ��������� ������� (����� true, ���� �������)
    {
        amount = Mathf.Max(0, amount);                              // ������
        if (energy < amount) return false;                          // �� ������� � �������
        energy -= amount;                                           // ������
        RaiseChanged();                                             // ������� HUD
        return true;                                                // ��
    }

    public void GainEnergy(int amount)     // �������� �������
    {
        energy = Mathf.Clamp(energy + Mathf.Max(0, amount), 0, maxEnergy); // ���������
        RaiseChanged();                                                     // ������� HUD
    }

    public void Drink(int amount)          // ������� �����
    {
        thirst = Mathf.Clamp(thirst + Mathf.Max(0, amount), 0, maxThirst);  // ��������� �����
        RaiseChanged();                                                     // ������� HUD
    }

    public void Eat(int amount)            // ������ (�������)
    {
        hunger = Mathf.Clamp(hunger + Mathf.Max(0, amount), 0, maxHunger);  // ��������� ���������
        RaiseChanged();                                                     // ������� HUD
    }

    public void ConsumeThirst(int amount)  // ������ ���� (��� ����/��������� ������ � �.�.)
    {
        thirst = Mathf.Clamp(thirst - Mathf.Max(0, amount), 0, maxThirst);  // ��������� ����
        RaiseChanged();                                                     // ������� HUD
    }

    public void ConsumeHunger(int amount)  // ������ �������
    {
        hunger = Mathf.Clamp(hunger - Mathf.Max(0, amount), 0, maxHunger);  // ��������� �������
        RaiseChanged();                                                     // ������� HUD
    }

    // --- ������������� � ������� �������� ������ (�� EventWindowUI) ---
    public void AddFood(int amount)        // ������� ����: ����� ��������� �������� �������
    {
        if (amount <= 0) return;           // ���� ���� � ������
        Eat(amount);                       // ���������� ���� ������ �������
    }

    public void AddWater(int amount)       // ������� �����: �������� �����
    {
        if (amount <= 0) return;           // ���� � ������
        Drink(amount);                     // ���������� ���� ������ �����
    }

    public void AddXP(int amount)          // ������� ����� (������ ����� �����)
    {
        xp = Mathf.Max(0, xp + amount);    // ����������� XP
        // ���� HUD ���� �� ���������� � ��� RaiseChanged()
    }

    // ���������������: ��������� �����������
    private void RaiseChanged()            // ������� �������
    {
        OnStatsChanged?.Invoke();          // ���� ���-�� �������� � ��������
    }
}
