using UnityEngine;                 // Базовое API (MonoBehaviour, Vector3)
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

    private void Update()                      // Обновление каждый кадр
    {
        if (InputSharedState.IsPanning)
        {
            if (hoveredTile != null)                    // Если был подсвечен прошлый тайл
            {
                hoveredTile.SetHover(false);            // Уберём подсветку
                hoveredTile = null;                     // Сбросим ссылку
            }
            return;                                     // Полностью пропускаем Update ховера
        }

        Vector2 screenPos = pointAction.ReadValue<Vector2>(); // Читаем позицию указателя в пикселях экрана
        Vector3 worldPos = cam.ScreenToWorldPoint(            // Конвертируем в мировые координаты
            new Vector3(screenPos.x, screenPos.y, cam.nearClipPlane) // Z берём nearClip, но для 2D он не критичен
        );
        worldPos.z = zPlane;                                  // Проецируем на плоскость Z, где лежат гексы

        // Делаем точечный пик 2D-коллайдеров в этой точке
        Collider2D hit = Physics2D.OverlapPoint(worldPos, hexLayerMask); // Ищем коллайдер в точке по маске слоёв

        // Определяем гекс под курсором
        HexTile tileUnderCursor = hit ? hit.GetComponent<HexTile>() : null; // Пробуем взять HexTile с найденного коллайдера

        // Обновляем подсветку «навёл/убрал»
        if (tileUnderCursor != hoveredTile)             // Если гекс под курсором сменился
        {
            if (hoveredTile != null)                    // Если ранее был наведен другой гекс
                hoveredTile.SetHover(false);            // Убираем подсветку с предыдущего
            hoveredTile = tileUnderCursor;              // Запоминаем новый гекс
            if (hoveredTile != null)                    // Если теперь навели на гекс
                hoveredTile.SetHover(true);             // Включаем подсветку наведения
        }
    }

    private void OnClickPerformed(InputAction.CallbackContext ctx) // Обработчик клика/тапа
    {
        if (hoveredTile == null || (ModalGate.IsBlocked)) return;                // Если под курсором нет гекса — ничего не делаем

        // Передаём клик в логику карты (перемещение/открытие/события)
        HexMapController.Instance?.OnHexClicked(hoveredTile); // Вызываем обработчик клика у контроллера карты
    }
}