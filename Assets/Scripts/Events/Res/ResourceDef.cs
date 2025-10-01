using UnityEngine;                           // ScriptableObject, Sprite

// ��� ������� (������ ��������� �� ���� �������������)
public enum ResourceType { Food, Materials, Charge, Other }

// ������ �������, ������� � ������ .asset
[CreateAssetMenu(fileName = "ResourceDef", menuName = "Robinson/Resource")]
public class ResourceDef : ScriptableObject
{
    [Header("ID � �����������")]
    public string resourceId;                // ���������� ID (��� ����������/������)
    public string displayName;               // ������������ ��� �������
    public Sprite icon;                      // ������ ������� (��� UI)

    [Header("�������������")]
    public ResourceType type = ResourceType.Other;  // ��� (���/��������/�����/�)

    [Header("����� / ���� ��������")]
    public int spoilDays = 0;                // �� ������� ���������� (0 � �� ��������)
}
