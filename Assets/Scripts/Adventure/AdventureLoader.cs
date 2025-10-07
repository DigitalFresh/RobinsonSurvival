using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ��������� AdventureAsset � ������� �����:
/// - ���� ��� HexTile, ��������/��������� �� �� visible,
/// - ������ ��� (HexType) �� HexTerrainType,
/// - ���������� ������� (tile.eventData = EventSO),
/// - ���������� ����� �� Start.
/// �������, ����� � ����� ��� ���� ������������� ����� ������� �������.
/// </summary>
public class AdventureLoader : MonoBehaviour
{
    [Tooltip("����� �����������, ������� ����� ��������� � �����.")]
    public AdventureAsset adventure;

    [Tooltip("��������� ����� ������������� � Start()")]
    public bool buildOnStart = true;

    [Tooltip("��������/��������� �����, ������������� � ������ (�� ��������� width/height ��� �����������).")]
    public bool hideUnspecifiedTiles = true;

    private void Start()
    {
        //HexGridGenerator.Instance.GenerateGrid();

        // ���� ����� �� ����� � ���������� � ���� �� ������ �������� ����
        if (adventure == null)
            adventure = AdventureRuntime.SelectedAdventure;

        if (buildOnStart && adventure != null)
            BuildIntoScene();
    }

    /// <summary>�������� �����: ��������� ����� � ������� �����.</summary>
    [ContextMenu("Build Into Scene")]
    public void BuildIntoScene()
    {
        if (adventure == null)
        {
            Debug.LogError("[AdventureLoader] �� �������� AdventureAsset.");
            return;
        }

        // �������� ��� ����� � ������� �� ����������� (x,y)
#if UNITY_2023_1_OR_NEWER
        var tiles = Object.FindObjectsByType<HexTile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var tiles = Object.FindObjectsOfType<HexTile>(true); // includeInactive=true
#endif
        if (tiles == null || tiles.Length == 0)
        {
            Debug.LogError("[AdventureLoader] � ����� �� ������� �� ������ HexTile. �������, ��� ����� �������.");
            return;
        }

        var dict = new Dictionary<(int, int), HexTile>(tiles.Length);
        foreach (var t in tiles)
        {
            dict[(t.x, t.y)] = t;
        }

        // �������� �� ���� "���������" ������� ������ � �������� ���������
        HexTile startTile = null;
        var specified = new HashSet<(int, int)>();

        foreach (var cell in adventure.cells)
        {
            if (cell == null) continue;
            specified.Add((cell.x, cell.y));

            if (!dict.TryGetValue((cell.x, cell.y), out var tile))
            {
                Debug.LogWarning($"[AdventureLoader] � ����� ��� HexTile x={cell.x}, y={cell.y} (���������).");
                continue;
            }

            // ���������
            tile.gameObject.SetActive(cell.visible);
            if (!cell.visible) continue; // ���� ������� � ������ ������ �� ������

            // ���
            var hexType = MapTerrainToHexType(cell.terrain);
            tile.SetType(hexType);

            // �������
            tile.BindEvent(cell.eventAsset);
            //tile.eventData = cell.eventAsset;

            // �������� ������
            tile.UpdateVisual();

            // ��������� �����
            if (cell.terrain == HexTerrainType.Start)
                startTile = tile;
        }

        // ������ ��� "�����������" ����� (���� ��������)
        if (hideUnspecifiedTiles)
        {
            foreach (var t in tiles)
            {
                var key = (t.x, t.y);
                bool existsInAssetBounds = (t.x >= 0 && t.x < adventure.width && t.y >= 0 && t.y < adventure.height);
                if (!existsInAssetBounds || !specified.Contains(key))
                    t.gameObject.SetActive(false);
            }
        }

        // ��������� ����� ������ �� startTile
        if (startTile != null)
        {
            var map = HexMapController.Instance;
            if (map != null && map.playerPawn != null)
            {
                map.PlacePlayerAtStart(startTile);
            }
        }

        Debug.Log($"[AdventureLoader] �������� ����� '{adventure.displayName}' ({adventure.width}x{adventure.height}).");
    }

    /// <summary>������� ����� ����� � ���� ������ ������ (������ ��� ���� HexType).</summary>
    private HexType MapTerrainToHexType(HexTerrainType t)
    {
        switch (t)
        {
            case HexTerrainType.Empty: return HexType.Empty;
            case HexTerrainType.Event: return HexType.Event;
            case HexTerrainType.Blocked: return HexType.Blocked;
            //case HexTerrainType.Water: return HexType.Water;
            //case HexTerrainType.Forest: return HexType.Forest;
            //case HexTerrainType.Mountain: return HexType.Mountain;
            //case HexTerrainType.Start: return HexType.Start;
            //case HexTerrainType.Exit: return HexType.Exit;
            default: return HexType.Empty;
        }
    }
}
