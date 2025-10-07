using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Ассет приключения: хранит размеры сетки и список ячеек с типами/видимостью/событиями.
/// Координаты — как у твоего HexTile: x, y (целые индексы сетки).
/// </summary>
[CreateAssetMenu(menuName = "Robinson/Adventure/Adventure Asset", fileName = "AdventureAsset")]
public class AdventureAsset : ScriptableObject
{
    [Header("Meta")]
    [Tooltip("Уникальный ID приключения (для аналитики/сейвов).")]
    public string adventureId = System.Guid.NewGuid().ToString();

    [Tooltip("Имя для отображения в UI/дебаге.")]
    public string displayName = "New Adventure";

    [Tooltip("Версия ассета (инкремент при изменениях).")]
    public int version = 1;

    [Header("Grid")]
    [Min(1)] public int width = 5;
    [Min(1)] public int height = 5;

    [Header("Cells")]
    [Tooltip("Список определений клеток. Должна быть хотя бы одна запись на каждую координату (x,y), если клетка нужна.")]
    public List<AdventureCell> cells = new List<AdventureCell>();

    /// <summary>Вернуть клетку по координатам (или null, если отсутствует).</summary>
    public AdventureCell GetCellAt(int x, int y)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (c != null && c.x == x && c.y == y) return c;
        }
        return null;
    }

    /// <summary>
    /// Дополнить список клеток недостающими (по размеру width x height).
    /// Если fillMissingVisible=true — создаются видимые Walkable-ячейки, иначе — невидимые.
    /// </summary>
    public void EnsureAllCellsPresent(bool fillMissingVisible = true)
    {
        if (cells == null) cells = new List<AdventureCell>();
        // удалим null-записи на всякий случай
        cells.RemoveAll(c => c == null);

        for (int yy = 0; yy < height; yy++)
            for (int xx = 0; xx < width; xx++)
            {
                if (GetCellAt(xx, yy) == null)
                {
                    cells.Add(new AdventureCell
                    {
                        x = xx,
                        y = yy,
                        visible = fillMissingVisible,
                        terrain = HexTerrainType.Empty, //fillMissingVisible ? HexTerrainType.Event : HexTerrainType.Empty,
                        eventAsset = null
                    });
                }
            }
    }
}

/// <summary>Типы гексов на карте приключения (минимальный набор).</summary>
public enum HexTerrainType
{
    Empty,
    Event,
    Blocked,
    //Water,
    //Forest,
    //Mountain,
    Start,
    Exit
}

/// <summary>
/// Описание одной клетки карты приключения.
/// Событие назначается ссылкой на EventSO (без overrides).
/// </summary>
[System.Serializable]
public class AdventureCell
{
    public int x;                      // координата X (как у HexTile.x)
    public int y;                      // координата Y (как у HexTile.y)
    public bool visible = true;        // видимый/невидимый. Если false — гекс не показывается и не используется.
    public HexTerrainType terrain = HexTerrainType.Event;
    public EventSO eventAsset;         // ссылка на событие (ScriptableObject), может быть null
}
