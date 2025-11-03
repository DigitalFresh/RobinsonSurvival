using System;
using System.Collections.Generic;
using UnityEngine;

/// Единый каталог «контента модалок» (заголовок, описание, картинка) c вариантами для разных языков.
/// Хранится как ScriptableObject в проекте (например: Assets/Game/ModalContentCatalog.asset).
/// </summary>
[CreateAssetMenu(fileName = "ModalContentCatalog", menuName = "UI/Modal Content Catalog")]
public class ModalContentCatalog : ScriptableObject
{
    [Serializable]
    public class Localized
    {
        public SystemLanguage language;     // язык варианта (например, Russian)
        [TextArea(1, 3)] public string title;       // заголовок
        [TextArea(2, 6)] public string description; // описание
        public Sprite imageOverride;        // опционально: картинка для этого языка (если null — берем общую)
    }

    [Serializable]
    public class Entry
    {
        public string key;                  // ключ (например: "death")
        public Sprite defaultImage;         // общая картинка, если для языка не задана
        public List<Localized> variants = new List<Localized>(); // набор языковых вариантов
    }

    public List<Entry> entries = new List<Entry>();   // все записи каталога

    /// <summary>Найти запись по ключу (без учёта языка).</summary>
    public Entry Find(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return entries.Find(e => string.Equals(e.key, key, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Результат разрешения (готовые поля для показа).
/// </summary>
public struct ResolvedModalContent
{
    public string title;      // итоговый заголовок
    public string description;// итоговое описание
    public Sprite image;      // итоговая картинка
}
