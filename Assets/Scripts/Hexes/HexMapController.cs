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

    [Header("Player")]
    public PlayerPawn playerPawn;                            // Ссылка на фишку игрока (задай в инспекторе)
    public bool autoPlacePlayerAtStart = true;               // Флаг: ставить ли игрока автоматически при старте
    public int startX = 0;                                   // Координата X старта (крайний столбец)
    public int startY = 0;                                   // Координата Y старта (переопределим на середину по готовности карты)

    [Header("Combat")]
    public CombatController combatController;          // Ссылка на контроллер боя (подкинь ссылку из сцены, объект Combat_screen)

    private bool _combatRunning = false;               // Флаг: бой запущен, чтобы не стартовать повторно
    private HexTile _pendingCombatTile;                // Какой тайл «занят» боем (чтобы потом очистить и перейти)

    private void Awake() // Вызывается при создании объекта в сцене
    {
        Instance = this;
        // Сохраняем ссылку на текущий объект, чтобы был доступ из любого места в коде через HexMapController.Instance
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
        foreach (var n in GetNeighbors(x, y)) n.Reveal();    // Открываем каждый соседний тайл
        TryStartAggressiveCombatNear(x, y);                  // Проверяем соседей: есть ли агрессивный бой
    }
    public void OnHexClicked(HexTile tile) // Вызывается из HexTile.OnMouseDown
    {
        
        if (tile == null || playerPawn == null || ModalGate.IsBlocked) return;      // Защита от null
        
        // Правило DD: перемещаться можно на соседние проходимые клетки.
        if (tile.type == HexType.Empty && playerPawn.CanMoveTo(tile))                      // Если цель — допустимый сосед  tile.type == HexType.Empty && 
        {
            playerPawn.MoveTo(tile);                       // Двигаем игрока и открываем соседей новой позиции
            MapCameraFollow.Instance?.SetTarget(playerPawn.transform);  // Обновляем цель (на всякий случай)
            //MapCameraFollow.Instance?.SnapToTarget();                   // Центрируем сразу (или можно без Snap — тогда сгладится)
            return;
            // Здесь позже: если type == Event — запуск окна события / боя / выбора по правилам DD
        }
        if (tile.type == HexType.Event && tile.isRevealed && playerPawn.CanMoveTo(tile))
        {
            if (tile.eventData != null)                                 // Если есть данные события
            {
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

    /// Проверить соседние тайлы и, если есть агрессивный бой — запустить его
    private void TryStartAggressiveCombatNear(int x, int y)
    {
        // Если бой уже идёт — ничего не делаем
        if (_combatRunning) return;

        // Переберём всех соседей
        foreach (var n in GetNeighbors(x, y))
        {
            // Пропускаем пустые/сломанные клетки
            if (!n || n.eventData == null) continue;

            var ev = n.eventData;                                  // Берём EventSO
            // Нас интересуют только события-бои с флагом агрессии и валидными врагами
            if (ev.isCombat && ev.isAggressiveCombat && ev.HasCombatEnemies())
            {
                // Запускаем бой на этом тайле и выходим (только один бой за раз)
                StartCombatOnTile(n);
                return;
            }
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
        cc.StartCombat(ev.combatEnemies);                                // Передаём список EnemySO (1..3)
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

        if (playerWon)                                                   // Обрабатываем только победу
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
    }

}