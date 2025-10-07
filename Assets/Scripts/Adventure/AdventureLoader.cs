using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Применяет AdventureAsset к текущей сцене:
/// - ищет все HexTile, включает/выключает их по visible,
/// - ставит тип (HexType) по HexTerrainType,
/// - навешивает событие (tile.eventData = EventSO),
/// - перемещает пешку на Start.
/// Требует, чтобы в сцене уже была сгенерирована сетка нужного размера.
/// </summary>
public class AdventureLoader : MonoBehaviour
{
    [Tooltip("Ассет приключения, который нужно применить к сцене.")]
    public AdventureAsset adventure;

    [Tooltip("Применить ассет автоматически в Start()")]
    public bool buildOnStart = true;

    [Tooltip("Скрывать/выключать тайлы, отсутствующие в ассете (за пределами width/height или неописанные).")]
    public bool hideUnspecifiedTiles = true;

    private void Start()
    {
        //HexGridGenerator.Instance.GenerateGrid();

        // Если ассет не задан в инспекторе — берём из выбора главного меню
        if (adventure == null)
            adventure = AdventureRuntime.SelectedAdventure;

        if (buildOnStart && adventure != null)
            BuildIntoScene();
    }

    /// <summary>Основной метод: применить ассет к текущей сцене.</summary>
    [ContextMenu("Build Into Scene")]
    public void BuildIntoScene()
    {
        if (adventure == null)
        {
            Debug.LogError("[AdventureLoader] Не назначен AdventureAsset.");
            return;
        }

        // Собираем все тайлы в словарь по координатам (x,y)
#if UNITY_2023_1_OR_NEWER
        var tiles = Object.FindObjectsByType<HexTile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var tiles = Object.FindObjectsOfType<HexTile>(true); // includeInactive=true
#endif
        if (tiles == null || tiles.Length == 0)
        {
            Debug.LogError("[AdventureLoader] В сцене не найдено ни одного HexTile. Убедись, что сетка создана.");
            return;
        }

        var dict = new Dictionary<(int, int), HexTile>(tiles.Length);
        foreach (var t in tiles)
        {
            dict[(t.x, t.y)] = t;
        }

        // Пройдёмся по всем "описанным" клеткам ассета и применим настройки
        HexTile startTile = null;
        var specified = new HashSet<(int, int)>();

        foreach (var cell in adventure.cells)
        {
            if (cell == null) continue;
            specified.Add((cell.x, cell.y));

            if (!dict.TryGetValue((cell.x, cell.y), out var tile))
            {
                Debug.LogWarning($"[AdventureLoader] В сцене нет HexTile x={cell.x}, y={cell.y} (пропускаю).");
                continue;
            }

            // Видимость
            tile.gameObject.SetActive(cell.visible);
            if (!cell.visible) continue; // если невидим — дальше ничего не делаем

            // Тип
            var hexType = MapTerrainToHexType(cell.terrain);
            tile.SetType(hexType);

            // Событие
            tile.BindEvent(cell.eventAsset);
            //tile.eventData = cell.eventAsset;

            // Обновить визуал
            tile.UpdateVisual();

            // Запомнить старт
            if (cell.terrain == HexTerrainType.Start)
                startTile = tile;
        }

        // Скрыть все "неописанные" тайлы (если включено)
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

        // Поставить пешку игрока на startTile
        if (startTile != null)
        {
            var map = HexMapController.Instance;
            if (map != null && map.playerPawn != null)
            {
                map.PlacePlayerAtStart(startTile);
            }
        }

        Debug.Log($"[AdventureLoader] Применил карту '{adventure.displayName}' ({adventure.width}x{adventure.height}).");
    }

    /// <summary>Маппинг наших типов в типы тайлов движка (подгон под твой HexType).</summary>
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
