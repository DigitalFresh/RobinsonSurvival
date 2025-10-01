using UnityEngine;
using System.Collections.Generic;

// ��������� �������: ������������ ��������/����� + ������ �������� (EffectDef),
// ������� ��������������� ����������� ��� ������������� �������.
[CreateAssetMenu(menuName = "Robinson/Rewards/FreeReward", fileName = "FreeReward")]
public class FreeRewardDef : ScriptableObject
{
    [Header("UI")]
    public string resourceId;            // ���������� ID (��� ����������/������)
    public string title;                 // ��������� � �������
    [TextArea] public string description; // �������� �������
    public Sprite icon;                  // ������� ������ �������

    [Header("Effects (executed in order)")]
    public List<EffectDef> effects = new(); // �������, ����������� ������ ����
}
