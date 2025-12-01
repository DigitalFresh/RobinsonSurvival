using System.Collections.Generic; // Подключаем работу с коллекциями (List, Dictionary)
using UnityEngine; // Подключаем Unity API
using System.Linq;                  // --- ADDED: для удобных проверок

// Класс, управляющий всей картой гексов и их взаимодействием
public class HexMapController : MonoBehaviour
{
    public static HexMapController Instance;
    // Статическая ссылка на этот объект — чтобы можно было обращаться из других скриптов без поиска в сцене

    // Словарь, где ключ — это пара координат (x,y), а значение — ссылка на объект HexTile
    private Dictionary<(int x, int y), HexTile> hexMap = new Dictionary<(int, int), HexTile>();

    private readonly HashSet<HexTile> _timidAwaitingCull = new HashSet<HexTile>();

    [Header("Player")]
    public PlayerPawn playerPawn;                            // Ссылка на фишку игрока (задай в инспекторе)
    public bool autoPlacePlayerAtStart = true;               // Флаг: ставить ли игрока автоматически при старте
    public int startX = 0;                                   // Координата X старта (крайний столбец)
    public int startY = 0;                                   // Координата Y старта (переопределим на середину по готовности карты)

    [Header("Combat")]
    public CombatController combatController;          // Ссылка на контроллер боя (подкинь ссылку из сцены, объект Combat_screen)

    private bool _combatRunning = false;               // Флаг: бой запущен, чтобы не стартовать повторно
    private HexTile _pendingCombatTile;                // Какой тайл «занят» боем (чтобы потом очистить и перейти)

    // Сюда складываем ближайший агрессивный бой, найденный при раскрытии соседей
    private HexTile _pendingAggressiveNear;
    private System.Action<bool> _adHocCombatCallback;   // единоразовый колбэк «бой завершён»

    // --- PATHFINDING / MOVE STATE ---
    private bool _pathMoveInProgress = false;           // Идёт ли сейчас пошаговое перемещение
    private HexTile _lastHintTile;                      // Кому показывали подсказку в прошлый кадр

    [HideInInspector] public bool suppressMapCleanupOnce = false;

    private void Awake() // Вызывается при создании объекта в сцене
    {
        Instance = this;
        // Сохраняем ссылку на текущий объект, чтобы был доступ из любого места в коде через HexMapController.Instance
    }

    // ЕДИНЫЙ ФЛАГ «занятости» карты (идёт перемещение или бой)
    public bool IsBusyForInput()
    {
        return _pathMoveInProgress || _combatRunning;  // true → клики/ховер блокируем
    }
    // Очистить реестр гексов (когда пересобираем карту)
    public void ClearRegistry()
    {
        // Сбросим словарь, чтобы старые ссылки не мешали и не висели в памяти
        // Новые тайлы будут зарегистрированы AdventureBuilder'ом
        var field = typeof(HexMapController).GetField("hexMap", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var dict = field?.GetValue(this) as System.Collections.IDictionary;
        dict?.Clear();
    }
    // Регистрируем гекс в словаре
    public void RegisterHex(HexTile tile)
    {
        // tile.x и tile.y — это координаты гекса, ключ в словаре
        hexMap[(tile.x, tile.y)] = tile;
        // Сохраняем ссылку на этот гекс
    }

    // Получаем гекс по координатам
    public HexTile GetHex(int x, int y)
    {
        // Пробуем найти гекс в словаре по координатам
        hexMap.TryGetValue((x, y), out var tile);
        return tile; // Вернём найденный гекс или null, если его нет
    }

    // Получаем список соседних гексов для указанной клетки
    public List<HexTile> GetNeighbors(int x, int y) // Соседи для flat-top, смещение по чётности столбца
    {
        List<(int dx, int dy)> dir = (x % 2 == 0)            // Для чётных столбцов…
            ? new() { (+1, 0), (0, +1), (-1, 0), (-1, -1), (0, -1), (+1, -1) }
            : new() { (+1, +1), (0, +1), (-1, +1), (-1, 0), (0, -1), (+1, 0) }; // Для нечётных

        var result = new List<HexTile>();                    // Сюда соберём соседей
        foreach (var (dx, dy) in dir)                        // Перебираем направления
        {
            var n = GetHex(x + dx, y + dy);                  // Пытаемся взять тайл по смещённым координатам
            if (n != null) result.Add(n);                    // Если есть — добавляем
        }
        return result;                                       // Возвращаем список
    }

    public void RevealNeighbors(int x, int y) // Открыть соседей клетки (логика DD)
    {
        foreach (var n in GetNeighbors(x, y))
        {
            n.Reveal();    // Открываем каждый соседний тайл
            // Запомнить открытые timid-события рядом с игроком
            if (n.eventData != null && EventHasTimid(n.eventData))
                _timidAwaitingCull.Add(n);

            // если среди соседей есть агрессивный бой — откладываем его на «после хода»
            var ev = n.eventData;
            if (ev != null && ev.isCombat && ev.isAggressiveCombat && ev.HasCombatEnemies())
                if (_pendingAggressiveNear == null) _pendingAggressiveNear = n; // запоминаем первый
        }

        if (_pendingAggressiveNear != null && !_combatRunning) StartCoroutine(AfterMoveRevealAndMaybeAggressive());
        //TryStartAggressiveCombatNear(x, y);                  // Проверяем соседей: есть ли агрессивный бой
    }
    public void OnHexClicked(HexTile tile) // Вызывается из HexTile.OnMouseDown
    {

        // Блокируем ЛЮБЫЕ клики, если карта занята (движение по пути/бой/модал)
        if (tile == null || playerPawn == null || ModalGate.IsBlocked || IsBusyForInput())
            return;

        // --- ДЛИННЫЙ ХОД ПО ПУТИ ТОЛЬКО ПО ОТКРЫТОМУ ПУСТОМУ ПОЛЮ ---
        if ((tile.type == HexType.Empty || tile.type == HexType.Exit)
            && tile.isRevealed && tile.isPassable && !_pathMoveInProgress)
        {
            CullPendingTimidIfAnyAndNotTarget(tile);
            var cur = GetHex(playerPawn.x, playerPawn.y);          // где стоит фишка
            var path = FindPath(cur, tile);                        // пробуем построить маршрут
            if (path != null && path.Count >= 2)                   // есть путь (2+ клетки)
            {
                StartCoroutine(ExecutePathMoveWithCost(path));     // запускаем корутину движения
                return;
            }
            // если пути нет — ничего не делаем (на ховере уже будет «X»)
        }

        if (tile.type == HexType.Event && tile.isRevealed && playerPawn.CanMoveTo(tile))
        {
            if (tile.eventData != null)                                 // Если есть данные события
            {
                // Если это timid-событие и игрок кликает по нему — НЕ очищаем (игрок вступил во взаимодействие)
                if (tile.eventData != null && EventHasTimid(tile.eventData))
                    _timidAwaitingCull.Remove(tile);
                else
                    CullPendingTimidIfAnyAndNotTarget(tile);

                if (tile.eventData.isCombat && tile.eventData.HasCombatEnemies()) // Это событие — бой?
                {
                    // Неагрессивный бой — старт по клику.
                    // Агрессивный можно тоже запускать кликом (если игрок сам «входит»), но ключевая логика — авто-старт рядом.
                    StartCombatOnTile(tile);                             // Откроем окно боя
                    return;                                              // Дальше UI событий не нужен
                }


                if (tile.eventData.isChoice == true)                                 // Если есть данные события
                {
                    ChooseEventWindowUI.Get().Show(tile.eventData, tile);
                }
                else
                {
                    //Debug.LogWarning(tile.eventData);
                    EventWindowUI.Get()?.Show(tile.eventData, tile);   // Показываем окно события
                }
            }
            else
            {
                Debug.LogWarning($"Hex {tile.name} помечен как Event, но eventData == null");
            }
            return;                                                      // Ждём решения игрока
        }
    }
        public void StartAdHocCombat(HexTile tile, List<EnemySO> enemies, System.Action<bool> onEnd)
    {
        // Защита от повторного старта/плохих аргументов
        if (_combatRunning) { onEnd?.Invoke(false); return; }
        if (!tile || enemies == null || enemies.Count == 0) { onEnd?.Invoke(true); return; }

        var cc = GetCombatController();
        if (!cc) { onEnd?.Invoke(true); return; }

        _combatRunning = true;
        _pendingCombatTile = tile;          // привязка к тайлу — так же, как в штатном запуске боя
        _adHocCombatCallback = onEnd;       // запомним, кого дернуть по завершению

        // Запускаем GUI боя (CombatController уже умеет открывать экран и вести раунды)
        cc.StartCombatAtTile(tile, enemies);
    }

    private System.Collections.IEnumerator ExecutePathMoveWithCost(List<HexTile> path)
    {
        _pathMoveInProgress = true;                            // блокируем клики по карте

        // 1) считаем стоимость и анимируем «колода → центр → сброс»
        int steps = Mathf.Max(0, path.Count - 1);              // переходы между клетками
        int cardsCost = ComputeMoveCostInCards(steps);         // стоимость в картах

        // найдём DeckController и HUD (иконки нам нужны для анимации)
        var deck = FindFirstObjectByType<DeckController>(FindObjectsInactive.Include);
        var hud = FindFirstObjectByType<DeckHUD>(FindObjectsInactive.Include);

        // Анимация (если RewardPickupAnimator умеет; иначе — фолбэк)
        bool animDone = false;
        System.Action onAnimDone = () => { animDone = true; };

        if (cardsCost > 0 && deck != null)
        {
            // фактически перекладываем карты: draw → discard (и уведомляем UI)
            var moved = new List<CardInstance>(cardsCost);
            for (int i = 0; i < cardsCost; i++)
            {
                var one = deck.DrawOne();                      // берём верхнюю из колоды (с автоперетасовкой из сброса)
                if (one == null) break;                        // кончились — останавливаемся
                deck.Discard(one);                             // положили в сброс
                moved.Add(one);                                // просто для отладки/счёта
            }

            // попытка красивой анимации через аниматор (если он есть и HUD задан)
            if (RewardPickupAnimator.Instance != null && hud != null && hud.deckIcon != null && hud.discardIcon != null)
            {
                // предполагаемый API аниматора; если его пока нет — просто выполнится фолбэк ниже
                RewardPickupAnimator.Instance.PlayDeckToDiscard(
                    moved,
                    hud.deckIcon.rectTransform,
                    hud.discardIcon.rectTransform,
                    onAnimDone);
            }
            else
            {
                // нет аниматора/иконок — анимацию пропускаем
                animDone = true;
            }
        }
        else animDone = true;                                   // ход «бесплатный» или нет колоды — сразу готово

        // ждём конца анимации (если была)
        while (!animDone) yield return null;

        // 2) двигаем фишку пошагово по пути
        //    (перемещение делает сам PlayerPawn, по одному тайлу; между шагами ждём корутину)
        playerPawn.BeginMoveBatch();                              // 1) включили походку ОДИН раз

        for (int i = 1; i < path.Count; i++)                     // идём по всем сегментам пути
        {
            var next = path[i];                                  // следующий гекс по пути

            playerPawn.MoveToInPath(next);                       // 2) шаг ВНУТРИ БАТЧА (аниматор тут не трогаем)

            // Ждём окончания шага (корутина MovePawnSmooth сама двигает трансформ)
            while ((playerPawn.transform.position - next.transform.position).sqrMagnitude > 0.0001f)
                yield return null;

            yield return null;                                   // маленькая пауза между сегментами (по желанию)
        }

        playerPawn.EndMoveBatch();

        // 3) финал
        _pathMoveInProgress = false;                            // разблокируем клики

        // Если добрались до выхода — запускаем «финиш-флоу»
        // Финальная клетка пути
        var goal = (path != null && path.Count > 0) ? path[path.Count - 1] : null;

        // ⬇️ Показать соседей И (при необходимости) предупредить об агрессивном бое
        if (goal) RevealNeighbors(goal.x, goal.y); // StartCoroutine(AfterMoveRevealAndMaybeAggressive(goal));

        // Переход на EXIT — как и раньше
        if (goal && goal.type == HexType.Exit)
            StartCoroutine(HandleExitReached(goal));
    }

    public void PlacePlayerAtStartAuto(int gridWidth, int gridHeight) // Авторасстановка игрока при старте
    {
        if (playerPawn == null) return;                       // Нет фишки — выходим

        // По умолчанию — крайний левый столбец, по Y — середина карты
        int px = (gridWidth - 1) / 2;           // Середина по горизонтали ерём X из поля
        int py = gridHeight;                        // берём Y из поля (обычно 0)

        var startTile = GetHex(px, py);                       // Ищем тайл старта
        if (startTile == null || !startTile.isPassable)       // Если нет или непроходим — ищем ближайший проходимый
        {
            // Простое восстановление: идём вниз по Y, пока не найдём проходимый (для прототипа)
            for (int y = gridHeight; y >= 0; y--)              // Перебор всех строк
            {
                var t = GetHex(px, y);                        // Берём тайл (px, y)
                if (t != null && t.isPassable) { startTile = t; break; } // Нашли — выходим
            }
        }

        if (startTile != null)                                // Если нашли клетку старта
        {
            playerPawn.PlaceAt(startTile.x, startTile.y); // Ставим фишку игрока
            //startTile.type = HexType.Empty;
            startTile.Reveal();                               // Открываем клетку старта
            RevealNeighbors(startTile.x, startTile.y);        // Открываем соседей старта по правилам DD

        }
    }

    public void PlacePlayerAtStart(HexTile startTile) // расстановка игрока при старте
    {
        if (playerPawn == null) return;                       // Нет фишки — выходим

        playerPawn.PlaceAt(startTile.x, startTile.y); // Ставим фишку игрока
        //startTile.type = HexType.Empty;
        startTile.Reveal();                               // Открываем клетку старта
        RevealNeighbors(startTile.x, startTile.y);        // Открываем соседей старта по правилам DD
    }

    ///// Проверить соседние тайлы и, если есть агрессивный бой — запустить его
    //private void TryStartAggressiveCombatNear(int x, int y)
    //{
    //    // Если бой уже идёт — ничего не делаем
    //    if (_combatRunning) return;

    //    // Переберём всех соседей
    //    foreach (var n in GetNeighbors(x, y))
    //    {
    //        // Пропускаем пустые/сломанные клетки
    //        if (!n || n.eventData == null) continue;

    //        var ev = n.eventData;                                  // Берём EventSO
    //        // Нас интересуют только события-бои с флагом агрессии и валидными врагами
    //        if (ev.isCombat && ev.isAggressiveCombat && ev.HasCombatEnemies())
    //        {
    //            // Запускаем бой на этом тайле и выходим (только один бой за раз)
    //            StartCombatOnTile(n);
    //            return;
    //        }
    //    }
    //}

    // ================== BARRIERS: «распыление» снятия фишек на соседей ==================
    /// <summary>
    /// Снять по одной фишке (если есть) на всех соседних гексах указанного центра,
    /// НО только на тех, которые уже раскрыты (isRevealed == true).
    /// </summary>
    public void PopOneBarrierOnNeighbors(HexTile center)
    {
        if (center == null) return;                       // защита: нет центрального гекса — нечего делать

        var neigh = GetNeighbors(center.x, center.y);     // получаем список соседей (твой текущий API)
        if (neigh == null) return;                        // защита: нет соседей

        for (int i = 0; i < neigh.Count; i++)
        {
            var n = neigh[i];                             // очередной сосед
            if (n == null) continue;                      // пропуск «пустых» ссылок

            // ⬇️ ключевое условие: снимаем фишку ТОЛЬКО если гекс уже был раскрыт
            if (!n.isRevealed) continue;                  // сосед ещё не открыт — не трогаем его барьеры

            // Снимаем первую фишку (если есть). Сам HexTile внутри проверит наличие и обновит бейдж.
            n.RemoveFirstBarrier();
        }
    }


    /// Безопасно получить CombatController (если поле не выставлено в инспекторе)
    private CombatController GetCombatController()     // Хелпер
    {
        // Если уже есть — вернём
        if (combatController) return combatController;
        // Иначе найдём в сцене (учитывая выключенные)
        combatController = FindFirstObjectByType<CombatController>(FindObjectsInactive.Include);
        return combatController;                       // Может быть null — вызывающий код проверит
    }


    /// Запустить бой, связав его с конкретным тайлом-событием
    private void StartCombatOnTile(HexTile eventTile)
    {
        // Защита от повторного старта
        if (_combatRunning) return;                                      // Если бой уже идёт — ничего не делаем

        if (!eventTile || eventTile.eventData == null) return;           // Нет тайла/данных события — выходим

        var ev = eventTile.eventData;                                    // Короткая ссылка
        if (!ev.isCombat || !ev.HasCombatEnemies()) return;              // Это не бой/нет врагов — выходим

        var cc = GetCombatController();                                   // Берём CombatController
        if (!cc)                                                         // Если его нет — предупредим и выйдем
        {
            Debug.LogWarning("CombatController не найден на сцене. Помести Combat_screen и присвой ссылку.");
            return;
        }

        _combatRunning = true;                                           // Ставим флаг «бой идёт»
        _pendingCombatTile = eventTile;                                   // Запоминаем, какой тайл «привязан» к бою

        // Запускаем бой (CombatController уже показывает экран, блокирует остальной UI и управляет раундами)
        if (cc) cc.StartCombatAtTile(_pendingCombatTile, ev.combatEnemies);
        //cc.StartCombat(ev.combatEnemies);                                // Передаём список EnemySO (1..3)
        // Важно: сам CombatController в момент победы должен вызвать ниже метод OnCombatEnded(true)
        // (см. комментарий к публичному методу — его удобно позвать из EndCombatAndClose)
    }

    /// Публичный метод для CombatController: вызвать при завершении боя.
    /// playerWon == true — победа игрока; false — поражение/отмена.
    public void OnCombatEnded(bool playerWon)
    {
        // Сбрасываем флаг боя
        _combatRunning = false;                                          // Бой закончился

        // Если нет «привязанного» тайла — ничего делать
        if (!_pendingCombatTile) { _pendingCombatTile = null; return; }

        var t = _pendingCombatTile;                                      // Короткая ссылка
        _pendingCombatTile = null;                                       // Чистим поле

        if (!suppressMapCleanupOnce && playerWon)                                                   // Обрабатываем только победу
        {
            // По правилам: событие удаляется, гекс остаётся открытым, игрок перемещается на этот гекс.
            t.SetType(HexType.Empty);                                    // Тип — пустой
            t.eventData = null;                                          // Отвязываем данные события
            t.Reveal();                                                  // Оставляем открытым
            t.UpdateVisual();                                            // Обновляем отрисовку

            if (playerPawn != null)                                      // Если есть фишка игрока
                playerPawn.MoveTo(t);                                    // Двигаем фишку (корутина в PlayerPawn)
        }
        else
        {
            // Здесь можно обработать поражение/отмену при необходимости (ничего не делаем по умолчанию)
        }

        suppressMapCleanupOnce = false;
        var cb = _adHocCombatCallback;
        _adHocCombatCallback = null;
        cb?.Invoke(playerWon);
    }

    // Явно: «проходим для пути» значит открыт, пуст, проходим
    private bool IsTileWalkableForPath(HexTile t)
    {
        if (t == null || !t.isRevealed || !t.isPassable) return false;
        // Путь валиден на пустые и на выход:
        return t.type == HexType.Empty || t.type == HexType.Exit;
    }

    // Построить кратчайший путь «только по открытому пустому проходимому полю».
    // Возвращает список тайлов от start до goal, или null если пути нет.
    private List<HexTile> FindPath(HexTile start, HexTile goal)
    {
        if (start == null || goal == null) return null;
        if (!IsTileWalkableForPath(goal)) return null;           // ходим только на пустые открытые проходимые
        if (start == goal) return new List<HexTile> { start };   // нулевая длина — стоим на месте

        var q = new Queue<HexTile>();                            // очередь для BFS
        var came = new Dictionary<HexTile, HexTile>();           // откуда пришли
        q.Enqueue(start);
        came[start] = null;

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == goal) break;                              // нашли цель

            foreach (var n in GetNeighbors(cur.x, cur.y))        // соседи клетки
            {
                if (!IsTileWalkableForPath(n)) continue;         // фильтр клеток
                if (came.ContainsKey(n)) continue;               // уже посещали
                came[n] = cur;                                   // запомнить предка
                q.Enqueue(n);                                    // добавить в очередь
            }
        }

        if (!came.ContainsKey(goal)) return null;                // не достижимо

        // восстанавливаем путь: goal → ... → start
        var path = new List<HexTile>();
        var t = goal;
        while (t != null)
        {
            path.Add(t);
            t = came[t];
        }
        path.Reverse();                                          // start → ... → goal
        return path;
    }

    // Сколько карт надо сбросить за длину пути (шагов): 1 карта за каждые 3 или менее гексов.
    private int ComputeMoveCostInCards(int steps)
    {
        if (steps <= 0) return 0;
        return Mathf.CeilToInt(steps / 3f);                      // 1..3 → 1, 4..6 → 2, и т.д.
    }
  
    private static bool EventHasTimid(EventSO ev)
    {
        if (ev == null || !ev.isCombat || ev.combatEnemies == null) return false;

        for (int i = 0; i < ev.combatEnemies.Count; i++)           // по врагам события
        {
            var enemy = ev.combatEnemies[i];
            if (enemy == null || enemy.tags == null) continue;

            for (int j = 0; j < enemy.tags.Count; j++)             // по тегам врага
            {
                var tag = enemy.tags[j];
                if (tag && !string.IsNullOrEmpty(tag.id) &&
                    string.Equals(tag.id, "Timid", System.StringComparison.OrdinalIgnoreCase))
                    return true;                                    // нашли Timid
            }
        }
        return false;                                               // нет Timid
    }

    private void CullPendingTimidIfAnyAndNotTarget(HexTile target)
    {
        if (_timidAwaitingCull.Count == 0) return;

        // Если клик не по timid-тайлу, все помеченные — очистить
        if (target == null || !_timidAwaitingCull.Contains(target))
        {
            foreach (var t in _timidAwaitingCull)
            {
                if (!t) continue;
                t.ClearEvent();            // гекс становится пустым
                t.UpdateVisual();          // UI обновится, бейдж спрячется
            }
        }
        _timidAwaitingCull.Clear();
    }

    // Обновить подсказку перемещения при наведении
    public void OnHoverHex(HexTile tile)
    {
        // Если ранее подсвечивали другой тайл — обязательно спрячем
        if (_lastHintTile != null && _lastHintTile != tile)
            _lastHintTile.HideMoveHint();

        // Если «уйти» с гекса (tile == null) — скрыть и сбросить ссылку
        if (tile == null)
        {
            _lastHintTile = null;                // забываем ссылку
            return;                              // и ничего не показываем
        }

        _lastHintTile = tile;                    // запоминаем текущий «ховер»

        // Во время боя/движения подсказок не показываем
        if (_combatRunning || _pathMoveInProgress) { tile.HideMoveHint(); return; }

        // Показываем подсказки только на открытых пустых проходимых гексах
        if (!IsTileWalkableForPath(tile)) { tile.HideMoveHint(); return; }

        // На клетке, где стоит игрок, НЕ показываем вообще ничего
        var cur = GetHex(playerPawn.x, playerPawn.y);
        if (tile == cur) { tile.HideMoveHint(); return; }

        // Пробуем построить путь только по видимым пустым проходимым
        var path = FindPath(cur, tile);

        // Если пути нет — НЕ показываем «X» (по ТЗ: го/стоимость только если путь доступен)
        if (path == null || path.Count < 2) { tile.HideMoveHint(); return; }

        // Есть валидный путь → показываем «go» и стоимость
        int steps = path.Count - 1;                     // длина пути в шагах
        int cost = ComputeMoveCostInCards(steps);      // 1 карта за каждые ≤3 шага (ceil)
        tile.ShowMoveHint(true, cost);
    }

    private System.Collections.IEnumerator AfterMoveRevealAndMaybeAggressive()
    {
        // 1) открыть соседей финальной клетки (это и пометит агрессивный бой, если он рядом)
        //RevealNeighbors(atTile.x, atTile.y);
        yield return null; // дать кадр на отрисовку

        // 2) если при раскрытии нашли агрессивный бой — покажем предупреждение и только затем стартанём бой
        if (_pendingAggressiveNear != null && !_combatRunning)
        {
            var ev = _pendingAggressiveNear.eventData;
            string enemyName = ev != null && ev.GetPreviewEnemy() != null
                ? ev.GetPreviewEnemy().displayName
                : "враг";

            // Текст берём из провайдера (ключ, например, "aggressiveWarn")
            var provider = ModalContentProvider.Instance;
            var content = provider ? provider.Resolve("aggressiveWarn")
                                   : new ResolvedModalContent { title = "Опасность!", description = "", image = null };

            // Если в описании есть плейсхолдер — подставим имя
            string msg = string.IsNullOrWhiteSpace(content.description)
                ? $"Агрессивный {enemyName} нападёт — вы подошли слишком близко."
                : content.description.Replace("{enemy}", enemyName);

            // Сконструируем запрос модалки: маленькая, без «Отмены»
            var req = new ModalRequest
            {
                kind = ModalKind.Small,                 // было Confirm → стало Small
                message = msg,
                picture = content.image,
                canCancel = false
            };

            bool closed = false;
            ModalManager.Instance?.Show(req, _ => closed = true);
            while (!closed) yield return null;  // ждём, пока игрок нажмёт ОК

            // Теперь — старт боя на отложенном тайле
            StartCombatOnTile(_pendingAggressiveNear);
            _pendingAggressiveNear = null;
        }
    }

    private System.Collections.IEnumerator HandleExitReached(HexTile exitTile)
    {
        // 1) Собрать «эффекты» для модалки из инвентаря: все ресурсы, которые накопил игрок
        var inv = InventoryController.Instance;
        var effects = new List<EventSO.Reward>(); // используем твою структуру наград как контейнер строк
        if (inv != null)
        {
            foreach (var kv in inv.Counts)
            {
                var res = kv.Key;    // ResourceDef
                var cnt = kv.Value;  // количество
                if (res == null || cnt <= 0) continue;

                effects.Add(new EventSO.Reward
                {
                    type = EventSO.RewardType.Resource,
                    resource = res,
                    amount = cnt
                });
            }
        }

        // 2) Показ FreeRewardModal: заголовок/картинка/описание + ресурсы из инвентаря

        var provider = ModalContentProvider.Instance;
        string key = CampaignManager.Instance ? CampaignManager.Instance.ExitModalKey : "exit"; // ключ записи
        var content = provider ? provider.Resolve(key)
                               : new ResolvedModalContent { title = "Приключение завершено", description = "", image = null };


        // Готовим список ресурсов (def, amount)
        //var inv = InventoryController.Instance;
        var resList = new List<(ResourceDef def, int amount)>();
        if (inv != null)
        {
            foreach (var kv in inv.Counts)
                if (kv.Key && kv.Value > 0)
                    resList.Add((kv.Key, kv.Value));
        }

        // Покажем через сам FreeRewardModalUI (т.к. ModalManager пока не принимает список ресурсов напрямую)
        var frm = FindFirstObjectByType<FreeRewardModalUI>(FindObjectsInactive.Include);
        bool closed = false;
        frm.ShowRuntimeResources(content.title, content.description, content.image, resList, () => closed = true);
        while (!closed) yield return null;

        // 3) Цена перехода: 1 thirst и 1 hunger (как в ТЗ). Если их нет — уйдёт HP (логика внутри PlayerStatsSimple)
        var stats = FindFirstObjectByType<PlayerStatsSimple>(FindObjectsInactive.Include);
        if (stats != null)
        {
            stats.ConsumeThirst(1);
            stats.ConsumeHunger(1);

            // Если от нехватки упало HP → смерть (модалка и рестарт приключения)
            if (stats.Health <= 0)
            {
                stats.CheckDeathNow();
                yield break;
            }
        }

        // 4) Переход в следующее приключение
        var cm = CampaignManager.Instance;
        if (cm != null)
        {
            if (!cm.BuildNextStageInThisScene())           // если этапов нет — маршрут окончен
                Debug.Log("[HexMapController] Кампания завершена (следующего этапа нет).");
        }
        else
        {
            Debug.LogWarning("[HexMapController] CampaignManager не найден — остаёмся в этой же миссии.");
        }
    }

}