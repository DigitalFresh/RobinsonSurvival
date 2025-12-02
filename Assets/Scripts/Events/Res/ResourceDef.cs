using UnityEngine;                           // ScriptableObject, Sprite

// Тип ресурса (можете расширять по мере необходимости)
public enum ResourceType { Food, Materials, Charge, Other }

// Данные ресурса, живущие в ассете .asset
[CreateAssetMenu(fileName = "ResourceDef", menuName = "Robinson/Resource")]
public class ResourceDef : ScriptableObject
{
    [Header("ID и отображение")]
    public string resourceId;                // Уникальный ID (для сохранений/поиска)
    public string displayName;               // Отображаемое имя ресурса
    public Sprite icon;                      // Иконка ресурса (для UI)

    [Header("Классификация")]
    public ResourceType type = ResourceType.Other;  // Тип (еда/строймат/заряд/…)

    [Header("Порча / срок годности")]
    public int spoilDays = 0;                // За сколько «портится» (0 — не портится)
}
