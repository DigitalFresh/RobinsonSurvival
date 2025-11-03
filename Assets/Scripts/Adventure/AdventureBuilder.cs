using System.Collections.Generic;
using UnityEngine;

// Генерирует гекс-сетку по AdventureAsset, регистрирует все тайлы в HexMapController,
// биндит события (через HexTile.BindEvent), затем ставит игрока на Start и открывает соседей.
// Работает без HexGridGenerator — он больше не нужен на рантайме.
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

    [Header("Backdrop Catalog (NEW)")]
    public SpriteSheetCatalog sheetCatalog;                 // Глобальное хранилище наборов + дефолты

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

            // Назначаем барьеры из ассета (HexTile сам обновит бейдж при активном объекте)
            tile.SetBarriers(cell.barriers);

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
                case HexTerrainType.Exit: newType = HexType.Exit; break;
            }

            tile.SetType(newType);
            tile.SetPassable(newType != HexType.Blocked);

            // cell — это AdventureCell, tile — HexTile на сцене
            tile.ApplyBackdropPicks(cell.backUnrevealed, cell.backBlocked, cell.backRevealed);

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

    private void BuildCell(AdventureCell cell, HexTile tile)
    {
        // 1) Ставим тип/событие/барьеры — как у вас

        // 2) РЕЗОЛВИМ наборы (если в клетке нет — используем дефолт из каталога)
        var setUnrev = cell.backUnrevealedSet ? cell.backUnrevealedSet : sheetCatalog ? sheetCatalog.defaultUnrevealed : null;
        var setBlocked = cell.backBlockedSet ? cell.backBlockedSet : sheetCatalog ? sheetCatalog.defaultBlocked : null;
        var setRev = cell.backRevealedSet ? cell.backRevealedSet : sheetCatalog ? sheetCatalog.defaultRevealed : null;

        // 3) Выбираем один кадр из набора по правилу (или берём null, если ничего не нашлось)
        Sprite pickUnrev = PickSprite(setUnrev, cell.backUnrevealed, cell.x, cell.y, seedOffset: 17);
        Sprite pickBlocked = PickSprite(setBlocked, cell.backBlocked, cell.x, cell.y, seedOffset: 31);
        Sprite pickRev = PickSprite(setRev, cell.backRevealed, cell.x, cell.y, seedOffset: 47);

        // 4) Отдаём выбранные кадры тайлу (он сам покажет нужный в зависимости от state)
        tile.ApplyChosenBackdropSprites(pickUnrev, pickBlocked, pickRev);
    }

    // Выбор кадра из набора по SpritePickRule (fixedIndex / pool), детерминированно по координатам
    private static Sprite PickSprite(SpriteSheetSet set, AdventureAsset.SpritePickRule rule, int x, int y, int seedOffset)
    {
        if (!set || set.sprites == null || set.sprites.Count == 0) return null; // Нет набора — возвращаем null

        int idx = -1;                                              // Начальный «не выбран»
        if (rule != null)
        {
            if (rule.fixedIndex >= 0)                              // Если указан фикс — берём его
            {
                idx = Mathf.Clamp(rule.fixedIndex, 0, set.sprites.Count - 1);
            }
            else if (rule.pool != null && rule.pool.Count > 0)     // Иначе если есть пул — стабильно выбираем из него
            {
                var valid = new List<int>();                       // Сюда соберём валидные индексы
                for (int i = 0; i < rule.pool.Count; i++)          // Проходим по пулу
                {
                    int pi = rule.pool[i];                         // Кандидат
                    if (pi >= 0 && pi < set.sprites.Count)         // Если в пределах набора
                        valid.Add(pi);                             // Добавляем в валидные
                }
                if (valid.Count > 0)                               // Если есть из чего выбирать
                {
                    unchecked                                         // Делаем детерминированный «рандом» от координат
                    {
                        int seed = x * 73856093 ^ y * 19349663 ^ seedOffset;
                        var rnd = new System.Random(seed);
                        idx = valid[rnd.Next(valid.Count)];           // Выбираем один индекс из пула
                    }
                }
            }
        }

        if (idx < 0) idx = 0;                                      // Если ничего не выбрали — берём 0
        return set.sprites[Mathf.Clamp(idx, 0, set.sprites.Count - 1)]; // Возвращаем спрайт по индексу
    }


    public void SetAdventure(AdventureAsset a) { this.adventure = a; }
}
