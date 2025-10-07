using System.Security.Principal;
using UnityEngine;

public class HexGridGenerator : MonoBehaviour
{
    public GameObject hexPrefab; // Префаб гекса, который будем копировать при генерации
    public int gridWidth = 5;    // Количество гексов по горизонтали
    public int gridHeight = 5;   // Количество гексов по вертикали
    public float hexWidth = 1f;  // Ширина одного гекса (в юнитах)
    public float hexHeight = 0.866f; // Высота гекса для flat-top ориентации (0.866 ≈ √3/2 при ширине 1)

    public static HexGridGenerator Instance;

    private EventSO[] allEvents;        // Кэш всех событий, загруженных из Resources

    void Start() // Метод, который запускается автоматически при старте сцены
    {
        // ПОЛУЧАЕМ ПРОВАЙДЕРА И ВЫГРУЖАЕМ ВСЕ СОБЫТИЯ
        var provider = EventProviderBehaviour.Instance?.GetProvider(); // Берём провайдер из сервис-компонента
        allEvents = provider != null ? provider.LoadAllEvents() : new EventSO[0]; // Загружаем все EventSO

      GenerateGrid(); // Генерируем сетку при старте
        // После генерации можно поставить игрока:
       // HexMapController.Instance?.PlacePlayerAtStartAuto(gridWidth, gridHeight); // Автостарт фишки игрока

        // Получаем ссылку на фишку игрока из контроллера карты
        var pawn = HexMapController.Instance?.playerPawn;           // Берём фишку игрока (если есть)
                                                                    // Сообщаем камере цель и прыгаем к ней (без плавности)
        if (pawn != null)
        {
            MapCameraFollow.Instance?.SetTarget(pawn.transform);    // Задаём цель следования
            MapCameraFollow.Instance?.SnapToTarget();               // Мгновенно центрируем камеру на игроке
        }

    }

    //private void LoadEvents()           // Загрузка ассетов событий из Resources
    //{
    //    allEvents = Resources.LoadAll<EventSO>("Events"); // Загружаем все EventSO из Assets/Resources/Events
    //    if (allEvents == null || allEvents.Length == 0)   // Если ничего не нашли
    //    {
    //        Debug.LogWarning("Не найдено ни одного EventSO в Resources/Events"); // Предупреждение в консоль
    //    }
    //}

    public void GenerateGrid()
    {
        // 1️ Удаляем предыдущие гексы, если они уже есть в объекте
        for (int i = transform.childCount - 1; i >= 0; i--) // Перебираем всех детей объекта с конца к началу
        {
            Transform child = transform.GetChild(i); // Получаем ссылку на дочерний объект
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);     // Удаляем его (DestroyImmediate работает в редакторе)
        }

        // 2️ Генерация новой сетки гексов
        for (int y = 0; y < gridHeight; y++) // Цикл по вертикали
        {
            for (int x = 0; x < gridWidth; x++) // Цикл по горизонтали
            {
                // Смещение по X — каждый следующий гекс немного смещён, чтобы получалась гексагональная форма
                float xOffset = x * hexWidth * 0.75f;

                // Смещение по Y — зависит от того, чётный или нечётный столбец (x % 2)
                // Если столбец нечётный, гекс поднимается на половину своей высоты
                float yOffset = (y + (x % 2) * 0.5f) * hexHeight;

                // Позиция гекса в мировых координатах
                // Минус перед yOffset нужен, чтобы гексы шли сверху вниз (по экрану)
                Vector3 position = new Vector3(xOffset, -yOffset, 0);
                // Создаём новый гекс в сцене
                GameObject hex = Instantiate(hexPrefab, position, Quaternion.identity, transform);

                // сразу ставим порядок отрисовки
                foreach (var r in hex.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    r.sortingLayerName = "Default"; // или "Tiles"
                    r.sortingOrder = 10;
                }

                HexTile tile = hex.GetComponent<HexTile>(); // Пытаемся получить компонент HexTile с префаба
                if (tile == null)                           // Если по ошибке его нет…
                {
                    tile = hex.AddComponent<HexTile>();     // …подстрахуемся и добавим компонент на лету
                }

                tile.Init(x, y);                  // Передаём координаты гексу (и обновляем его имя внутри, если нужно)

                //if (allEvents != null && allEvents.Length > 0)                  // Если есть события
                //{
                //    var randomEvent = allEvents[Random.Range(0, allEvents.Length)]; // Случайно выбираем одно
                //    tile.BindEvent(randomEvent);                                // Привязываем событие к тайлу
                //}
                //else
                //{
                //    tile.SetType(HexType.Empty);                                // Иначе — пустой тайл
                //}

                //// --- Проходимость и стартовое открытие (для прототипа) ---
                //tile.SetPassable(tile.type != HexType.Blocked);        // Заблокированные делаем непроходимыми

                HexMapController.Instance.RegisterHex(tile);
            }
        }

    }



    //void FrameCameraToGrid(Camera cam, Transform gridRoot, float padding = 1.05f)
    //{
    //    if (cam == null || gridRoot == null) return; // Защита

    //    // Считаем Bounds по всем дочерним объектам (гексам)
    //    var renderers = gridRoot.GetComponentsInChildren<Renderer>(); // Подойдёт SpriteRenderer
    //    if (renderers.Length == 0) return; // Нечего фреймить

    //    Bounds b = renderers[0].bounds; // Начальные границы
    //    for (int i = 1; i < renderers.Length; i++)
    //        b.Encapsulate(renderers[i].bounds); // Расширяем границы

    //    // Центруем камеру
    //    Vector3 center = b.center;
    //    cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);

    //    // Подгоняем orthographicSize, чтобы вся сетка влезла по высоте ВНУТРИ viewport камеры
    //    float halfHeight = b.extents.y * padding; // половина высоты
    //    float halfWidth = b.extents.x * padding; // половина ширины
    //    float aspect = cam.aspect * cam.rect.width / cam.rect.height; // учтём Viewport Rect, если не на весь экран

    //    float sizeByHeight = halfHeight;           // высота в world units
    //    float sizeByWidth = halfWidth / aspect;   // конвертируем ширину в world units по аспекту

    //    cam.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth); // берём большее — чтобы влезло и по ширине, и по высоте
    //}

}
