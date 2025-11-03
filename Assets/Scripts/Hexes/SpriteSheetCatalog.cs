// Assets/Scripts/Adventure/Backdrop/SpriteSheetCatalog.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpriteSheetCatalog", menuName = "Robinson/Backdrop/Sprite Sheet Catalog")]
public class SpriteSheetCatalog : ScriptableObject
{
    [Header("Registered Sets")]
    public List<SpriteSheetSet> sets = new();  // Все комплекты, доступные для выбора в редакторе

    [Header("Defaults (used if cell has no set)")]
    public SpriteSheetSet defaultUnrevealed;   // Дефолт для закрытых гексов
    public SpriteSheetSet defaultBlocked;      // Дефолт для Blocked
    public SpriteSheetSet defaultRevealed;     // Дефолт для открытых (Empty/Event/Exit)
}
