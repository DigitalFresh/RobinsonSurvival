using System.Collections.Generic;                     // ��� �������
using UnityEngine;                                    // ScriptableObject, Sprite

// �������� ����� (�����)
[CreateAssetMenu(menuName = "Robinson/Enemy", fileName = "EnemySO")]
public class EnemySO : ScriptableObject
{
    [Header("Visuals")]                               // ���������� ������
    public string displayName;                        // ��� ��� UI
    public Sprite sprite;                             // �������� �����

    [Header("Stats")]                                 // ������ ���������
    public int attack = 1;                            // ���� ����� (Strange)
    public int armor = 0;                             // ����� (Defense)
    public int maxHP = 3;                             // �������� ������ (Hearth)

    [Header("Tags")]                                  // ���� (������ ������/����� �� �������)
    public string[] tags;                             // ��������: "aggressive", "animal", "bird", "aquatic"

    [System.Serializable]                             // ������ ������� + ���-��
    public class LootEntry
    {
        public ResourceDef resource;                  // ����� ������ �������
        public int amount = 1;                        // �������
    }

    [Header("Loot (resources)")]                      // ������� �� ��������
    public List<LootEntry> loot = new();              // ������ ������-��������

    [Header("Traits (effects)")]                      // �������� (������� ���)
    public List<EffectDef> traits = new();            // �������, ������������� � ���� (��/�����, ��. CombatMoment)
}
