using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ����� �����������: ������ ������� ����� � ������ ����� � ������/����������/���������.
/// ���������� � ��� � ������ HexTile: x, y (����� ������� �����).
/// </summary>
[CreateAssetMenu(menuName = "Robinson/Adventure/Adventure Asset", fileName = "AdventureAsset")]
public class AdventureAsset : ScriptableObject
{
    [Header("Meta")]
    [Tooltip("���������� ID ����������� (��� ���������/������).")]
    public string adventureId = System.Guid.NewGuid().ToString();

    [Tooltip("��� ��� ����������� � UI/������.")]
    public string displayName = "New Adventure";

    [Tooltip("������ ������ (��������� ��� ����������).")]
    public int version = 1;

    [Header("Grid")]
    [Min(1)] public int width = 5;
    [Min(1)] public int height = 5;

    [Header("Cells")]
    [Tooltip("������ ����������� ������. ������ ���� ���� �� ���� ������ �� ������ ���������� (x,y), ���� ������ �����.")]
    public List<AdventureCell> cells = new List<AdventureCell>();

    /// <summary>������� ������ �� ����������� (��� null, ���� �����������).</summary>
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
    /// ��������� ������ ������ ������������ (�� ������� width x height).
    /// ���� fillMissingVisible=true � ��������� ������� Walkable-������, ����� � ���������.
    /// </summary>
    public void EnsureAllCellsPresent(bool fillMissingVisible = true)
    {
        if (cells == null) cells = new List<AdventureCell>();
        // ������ null-������ �� ������ ������
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

/// <summary>���� ������ �� ����� ����������� (����������� �����).</summary>
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
/// �������� ����� ������ ����� �����������.
/// ������� ����������� ������� �� EventSO (��� overrides).
/// </summary>
[System.Serializable]
public class AdventureCell
{
    public int x;                      // ���������� X (��� � HexTile.x)
    public int y;                      // ���������� Y (��� � HexTile.y)
    public bool visible = true;        // �������/���������. ���� false � ���� �� ������������ � �� ������������.
    public HexTerrainType terrain = HexTerrainType.Event;
    public EventSO eventAsset;         // ������ �� ������� (ScriptableObject), ����� ���� null
}
