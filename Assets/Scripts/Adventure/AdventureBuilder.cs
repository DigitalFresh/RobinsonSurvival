using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Генерирует гекс-сетку по AdventureAsset, регистрирует все тайлы в HexMapController,
/// биндит события (через HexTile.BindEvent), затем ставит игрока на Start и открывает соседей.
/// Работает без HexGridGenerator — он больше не нужен на рантайме.
/// </summary>
public class AdventureBuilder : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private AdventureAsset adventure;     // Ассет приключения (FirstAdventure и т.п.)

    [Header("Grid Generation")]
    [SerializeField] private GameObject hexPrefab;         // Префаб гекса (с HexTile, badgeAnchor и т.д.)
    [SerializeField] private Transform gridRoot;           // Корень для гексов (если null — берём свой transform)
    [SerializeField] private float hexWidth = 1f;          // размер по ширине (как в HexGridGenerator)
    [SerializeField] private float hexHeight = 0.866f;     // размер по высоте (flat-top): ~√3/2

    [Header("Build Options")]
    [SerializeField] private bool buildOnStart = true;     // собрать автоматически в Start()
    [SerializeField] private bool hideUnspecifiedTiles = true; // выключать всё вне ассета/неописанное

    [SerializeField] private HexMapController _map;                         // кэш контроллера карты
    private Dictionary<(int x, int y), HexTile> _tiles;    // реестр сгенерированных тайлов

    private void Awake()
    {
        // Подстрахуемся: если gridRoot не задан — используем собственный трансформ
        if (!gridRoot) gridRoot = transform;

        // Получим карту без устаревших API
        _map = HexMapController.Instance ? HexMapController.Instance
              : FindFirstObjectByType<HexMapController>(FindObjectsInactive.Include);
    }

    private void Start()
    {
        // Если ассет не задан в инспекторе — пробуем взять выбор из меню
        if (!adventure) adventure = AdventureRuntime.SelectedAdventure;

        if (buildOnStart && adventure && hexPrefab)
            BuildAll();
    }

    [ContextMenu("Build Adventure Now")]
    public void BuildAll()
    {
        if (!adventure) { Debug.LogError("[AdventureBuilder] AdventureAsset не задан."); return; }
        if (!hexPrefab) { Debug.LogError("[AdventureBuilder] hexPrefab не задан."); return; }
        if (_map == null) { Debug.LogError("[AdventureBuilder] HexMapController не найден в сцене."); return; }


        // 0) Сбросим предыдущий реестр (если был) и удалим старых детей у gridRoot
        _map.ClearRegistry(); // см. правку в HexMapController ниже
        ClearChildren(gridRoot);

        // 1) Сгенерируем сетку точно под размеры ассета
        _tiles = new Dictionary<(int, int), HexTile>(adventure.width * adventure.height);
        for (int y = 0; y < adventure.height; y++)
        {
            for (int x = 0; x < adventure.width; x++)
            {
                // плоская «шапка» (flat-top): смещения как в HexGridGenerator
                float xOffset = x * hexWidth * 0.75f;
                float yOffset = (y + (x % 2) * 0.5f) * hexHeight;

                var pos = new Vector3(xOffset, -yOffset, 0f);
                var go = Instantiate(hexPrefab, pos, Quaternion.identity, gridRoot);

                // Нормализация сортинга детей (как в генераторе)
                foreach (var r in go.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    r.sortingLayerName = "Default";
                    r.sortingOrder = 10;
                }

                var tile = go.GetComponent<HexTile>();
                if (!tile) tile = go.AddComponent<HexTile>();
                tile.Init(x, y);                 // задаём координаты, подложки и первичный визуал
                tile.SetType(HexType.Empty);     // базовый тип до применения ассета
                tile.SetPassable(true);          // по умолчанию проходимый

                _tiles[(x, y)] = tile;
                _map.RegisterHex(tile);          // ОБЯЗАТЕЛЬНО: вносим в каталог карты (важно для PlaceAt/MoveTo/RevealNeighbors)
            }
        }

        // 2) Применим содержимое ассета к сгенерированным тайлам + определим старт
        HexTile startTile = null;
        var specified = new HashSet<(int, int)>();

        foreach (var cell in adventure.cells)
        {
            if (cell == null) continue;
            specified.Add((cell.x, cell.y));
            if (!_tiles.TryGetValue((cell.x, cell.y), out var tile))
            {
                Debug.LogWarning($"[AdventureBuilder] Не сгенерирован HexTile ({cell.x},{cell.y}) — пропуск.");
                continue;
            }

            // Видимость: невидимые гексы выключаем целиком
            tile.gameObject.SetActive(cell.visible);
            if (!cell.visible) continue;

            // Типы: в текущей модели HexTile поддерживает {Empty, Event, Blocked}
            // Start/Exit трактуем как Empty (проходимые) либо Event при наличии eventAsset (для «выхода-события»).
            HexType newType = HexType.Empty;
            switch (cell.terrain)
            {
                case HexTerrainType.Empty: newType = HexType.Empty; break;
                case HexTerrainType.Event: newType = HexType.Event; break;
                case HexTerrainType.Blocked: newType = HexType.Blocked; break;
                case HexTerrainType.Start: newType = HexType.Empty; startTile = tile; break;
                case HexTerrainType.Exit: newType = cell.eventAsset ? HexType.Event : HexType.Empty; break;
            }

            tile.SetType(newType);
            tile.SetPassable(newType != HexType.Blocked);

            // Событие: биндим ТОЛЬКО через HexTile.BindEvent — так гарантирован бейдж (badgeAnchor + HexEventBadgeUI)
            // (Если тип Blocked/Empty — биндим null, чтобы спрятать бейдж)
            var ev = (newType == HexType.Event) ? cell.eventAsset : null;
            tile.BindEvent(ev);

            // Закрытость/подсказки: на старте карта закрыта, бейдж виден только на открытом гексе (см. UpdateVisual / BindEvent)
            // tile.isRevealed = false; tile.UpdateVisual(); // не обязательно — Init это уже сделал
        }

        // 3) Выключим всё неописанное/вне ассета, если надо
        if (hideUnspecifiedTiles)
        {
            foreach (var kv in _tiles)
            {
                var (x, y) = kv.Key;
                if (!specified.Contains((x, y)))
                    kv.Value.gameObject.SetActive(false);
            }
        }

        // 4) Ставим фишку на старт + открываем соседей (вся логика есть в HexMapController/PlayerPawn)
        if (startTile != null)
        {
            _map.PlacePlayerAtStart(startTile);   // PlaceAt + Reveal + RevealNeighbors (как у тебя реализовано)
        }

        // 5) Камера — привязать к пешке и снепнуть, если есть MapCameraFollow (как у тебя делалось)
        var pawn = _map.playerPawn;
        if (pawn != null)
        {
            MapCameraFollow.Instance?.SetTarget(pawn.transform);
            MapCameraFollow.Instance?.SnapToTarget();
        }

        Debug.Log($"[AdventureBuilder] Карта построена: {adventure.displayName} ({adventure.width}x{adventure.height}).");
    }

    private void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}
