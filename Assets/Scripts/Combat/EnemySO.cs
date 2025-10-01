using System.Collections.Generic;                     // Для списков
using UnityEngine;                                    // ScriptableObject, Sprite

// Описание врага (ассет)
[CreateAssetMenu(menuName = "Robinson/Enemy", fileName = "EnemySO")]
public class EnemySO : ScriptableObject
{
    [Header("Visuals")]                               // Визуальные данные
    public string displayName;                        // Имя для UI
    public Sprite sprite;                             // Картинка врага

    [Header("Stats")]                                 // Боевые параметры
    public int attack = 1;                            // Сила атаки (Strange)
    public int armor = 0;                             // Броня (Defense)
    public int maxHP = 3;                             // Максимум жизней (Hearth)

    [Header("Tags")]                                  // Теги (просто строки/флаги на будущее)
    public string[] tags;                             // Например: "aggressive", "animal", "bird", "aquatic"

    [System.Serializable]                             // Запись «ресурс + кол-во»
    public class LootEntry
    {
        public ResourceDef resource;                  // Какой ресурс выпадет
        public int amount = 1;                        // Сколько
    }

    [Header("Loot (resources)")]                      // Награды за убийство
    public List<LootEntry> loot = new();              // Список наград-ресурсов

    [Header("Traits (effects)")]                      // Свойства (эффекты боя)
    public List<EffectDef> traits = new();            // Эффекты, срабатывающие в ходу (до/после, см. CombatMoment)
}
