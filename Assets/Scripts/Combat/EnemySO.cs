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

    [Header("Tags")]
    public List<TagDef> tags = new();           // Теги для логики (Timid, Beast, …)

    [System.Serializable]                        // Пара «тег + переопределение описания»
    public class TagNote
    {
        public TagDef tag;                       // Ссылка на TagDef
        [TextArea] public string note;           // Кастомное описание для этого врага (опционально)
    }
    public List<TagNote> tagNotes = new();       // Кастомные описания тегов (если нужно)

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

    // Дать описание для конкретного тега: кастомная заметка > описание из TagDef > пусто
    public string GetTagDescription(TagDef tag)
    {
        if (!tag) return "";
        // 1) ищем переопределение
        for (int i = 0; i < tagNotes.Count; i++)
        {
            var tn = tagNotes[i];
            if (tn != null && tn.tag == tag && !string.IsNullOrEmpty(tn.note))
                return tn.note;
        }
        // 2) fallback — описание из самого TagDef (если поле есть)
        var desc = TryGetString(tag, "description");
        return string.IsNullOrEmpty(desc) ? "" : desc;
    }

    // Небольшой рефлекшн-хелпер, чтобы не зависеть от жёстких имён полей у ScriptableObject
    private static string TryGetString(ScriptableObject so, string field)
    {
        if (!so) return null;
        var f = so.GetType().GetField(field);
        if (f != null && f.FieldType == typeof(string))
            return (string)f.GetValue(so);
        var p = so.GetType().GetProperty(field);
        if (p != null && p.PropertyType == typeof(string))
            return (string)p.GetValue(so, null);
        return null;
    }
}
