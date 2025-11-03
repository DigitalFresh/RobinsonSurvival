using UnityEngine;                 // Базовое API (MonoBehaviour, Vector3)
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;     // Новый Input System (Mouse, Touchscreen, InputAction)
using System.Collections.Generic;  // Для List, если понадобится

public class HexInputController : MonoBehaviour // Контроллер ввода для карты
{
    [Header("Raycast")]
    public LayerMask hexLayerMask = ~0;        // Маска слоёв для пикера (по умолчанию — все слои)
    public float zPlane = 0f;                  // Плоскость мира, где лежат гексы (обычно Z=0)

    private InputAction pointAction;           // Действие "указатель" (позиция курсора/тапа)
    private InputAction clickAction;           // Действие "клик/тап" (нажатие)
    private Camera cam;                        // Ссылка на главную камеру

    private HexTile hoveredTile;               // Текущий гекс под курсором (для подсветки)

    private static readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();

    private void Awake()                       // Ранняя инициализация
    {
        cam = Camera.main;                     // Кэшируем главную камеру

        // Создаём action для позиции указателя (мышь/тач)
        pointAction = new InputAction(         // Новый InputAction
            name: "Point",                     // Имя действия
            type: InputActionType.PassThrough, // Тип — сквозное (всегда отдаёт текущее значение)
            binding: "<Pointer>/position"      // Биндим на абстрактный указатель (мышь/тач/перо)
        );

        // Создаём action для клика (ЛКМ/тап)
        clickAction = new InputAction(         // Новый InputAction
            name: "Click",                     // Имя действия
            type: InputActionType.Button       // Тип — кнопка
        );
        clickAction.AddBinding("<Mouse>/leftButton");         // ЛКМ
        clickAction.AddBinding("<Touchscreen>/primaryTouch/press"); // Тач: первичный тап
    }

    private void OnEnable()                    // Когда объект активируется
    {
        pointAction.Enable();                  // Включаем слежение за позицией курсора/тача
        clickAction.Enable();                  // Включаем слежение за кликом/тапом
        clickAction.performed += OnClickPerformed; // Подписываемся на событие "клик выполнен"
    }

    private void OnDisable()                   // Когда объект выключается
    {
        clickAction.performed -= OnClickPerformed; // Отписываемся от клика
        clickAction.Disable();                // Отключаем action клика
        pointAction.Disable();                // Отключаем action позиции
    }

    private void Update()
    {
        // Если сейчас идёт панорамирование — сбросить ховер и выйти
        if (InputSharedState.IsPanning)
        {
            if (hoveredTile != null)
            {
                hoveredTile.SetHover(false);
                hoveredTile = null;
            }
            HexMapController.Instance?.OnHoverHex(null); // спрятать go/X и стоимость
            return;
        }

        // если указатель над UI → снимаем ховер и выходим
        if (IsPointerOverUI())
        {
            if (hoveredTile != null)
            {
                hoveredTile.SetHover(false);
                hoveredTile = null;
            }
            HexMapController.Instance?.OnHoverHex(null); // спрятать go/X и стоимость
            return;
        }

        // Если карта занята (движение/бой) — скрыть ховер и не считать попадания
        var map = HexMapController.Instance;
        if (map != null && map.IsBusyForInput())
        {
            if (hoveredTile != null)
            {
                hoveredTile.SetHover(false);      // снять визуальный ховер
                hoveredTile = null;
            }
            HexMapController.Instance?.OnHoverHex(null); // скрыть go/X и стоимость
            return;                                // выходим до любых оверлапов
        }

        // 1) Позиция указателя → мировая точка на плоскости Z=zPlane
        Vector2 screenPos = pointAction.ReadValue<Vector2>();
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, cam.nearClipPlane));
        worldPos.z = zPlane;

        // 2) Берём коллайдер под курсором по маске слоёв и пытаемся получить HexTile
        Collider2D hit = Physics2D.OverlapPoint(worldPos, hexLayerMask);
        HexTile tileUnderCursor = hit ? hit.GetComponent<HexTile>() : null;

        // 3) Если целевой гекс сменился — обновляем подсветку и уведомляем карту
        if (tileUnderCursor != hoveredTile)
        {
            if (hoveredTile != null) hoveredTile.SetHover(false);
            hoveredTile = tileUnderCursor;
            if (hoveredTile != null) hoveredTile.SetHover(true);

            // Сообщаем HexMapController, чтобы он показал/скрыл go/X и посчитал стоимость
            HexMapController.Instance?.OnHoverHex(hoveredTile);
        }
    }

    ///Находится ли указатель мыши над любым UI-элементом, который блокирует клики.</summary>
    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        var pos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition; // fallback на старый ввод

        var ped = new PointerEventData(EventSystem.current) { position = pos };

        _uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(ped, _uiRaycastResults);

        // Важно: CanvasGroup.blocksRaycasts и GraphicRaycaster сами исключат «сквозные» элементы
        return _uiRaycastResults.Count > 0;
    }

    private void OnClickPerformed(InputAction.CallbackContext ctx) // Обработчик клика/тапа
    {
        // NEW: клики по UI не должны уходить в карту
        if (IsPointerOverUI())
            return;

        // NEW: любой клик ЛКМ — вернуть автоследование
        if (MapCameraFollow.Instance)
            MapCameraFollow.Instance.followEnabled = true;

        // Если карта занята — клик игнорируем
        var map = HexMapController.Instance;
        if (map != null && map.IsBusyForInput())
            return;

        if (hoveredTile == null || (ModalGate.IsBlocked)) return;                // Если под курсором нет гекса — ничего не делаем

        // Передаём клик в логику карты (перемещение/открытие/события)
        HexMapController.Instance?.OnHexClicked(hoveredTile); // Вызываем обработчик клика у контроллера карты
    }
}