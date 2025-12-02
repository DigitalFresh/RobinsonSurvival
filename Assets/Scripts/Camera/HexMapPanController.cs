
// Assets/Scripts/Camera/HexMapPanController.cs
using UnityEngine;                        // MonoBehaviour, Camera, Vector3
using UnityEngine.InputSystem;            // Новый Input System (Mouse)

//[RequireComponent(typeof(Camera))]        // требуем наличие Camera на этом объекте
public class HexMapPanController : MonoBehaviour
{
    public Camera cam;                    // ссылка на камеру (если null — возьмём с объекта)
    public float zoomSpeed = 2.0f;        // скорость зума колёсиком
    public float minOrthoSize = 1f;       // минимальный ортографический размер
    public float maxOrthoSize = 7f;      // максимальный ортографический размер

    private bool isDragging;              // флаг: сейчас тащим ПКМ?
    private Vector2 dragOriginScreen;      // Экранная точка (px) в момент нажатия ПКМ
    private Vector3 camOrigin;             // Позиция камеры в момент нажатия ПКМ
    private float camZ;                    // Постоянный Z камеры

    private void Awake()                  // инициализация
    {
        if (cam == null) cam = Camera.main;       // Берём главную камеру, если поле не заполнено
        if (cam != null) cam.orthographic = true; // Гарантируем ортографику
        camZ = cam.transform.position.z;          // Запомним Z (обычно -10)
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        // ЗУМ КОЛЕСОМ (всегда активен)
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float size = cam.orthographicSize - scroll * 0.1f * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(size, minOrthoSize, maxOrthoSize);
        }

        // НАЧАЛО ПАНОРАМИРОВАНИЯ (ПКМ нажат)
        if (mouse.rightButton.wasPressedThisFrame)
        {
            isDragging = true;
            InputSharedState.IsPanning = true;            // Сообщаем остальным системам
            camOrigin = cam.transform.position;           // Запоминаем стартовую позицию камеры
            dragOriginScreen = mouse.position.ReadValue(); // Запоминаем стартовую экранную позицию (px)

            // пока панорамируем — выключить автоследование
            if (MapCameraFollow.Instance)                   // если в проекте используется MapCameraFollow
                MapCameraFollow.Instance.followEnabled = false;
        }

        // КОНЕЦ ПАНОРАМИРОВАНИЯ (ПКМ отпущен)
        if (mouse.rightButton.wasReleasedThisFrame)
        {
            isDragging = false;
            InputSharedState.IsPanning = false;
        }
    }

    private void LateUpdate()
    {
        if (!isDragging) return;
        var mouse = Mouse.current;
        if (mouse == null) return;

        // Δэкран в пикселях с момента старта драга
        Vector2 screenDelta = mouse.position.ReadValue() - dragOriginScreen;

        // Переводим Δэкран → Δмир для ортокамеры (учитываем viewport камеры!)
        // Сколько мировых юнитов приходится на 1 пиксель по вертикали:
        float unitsPerPixelY = (cam.orthographicSize * 2f) / cam.pixelHeight;
        // По горизонтали домножаем на аспект:
        float unitsPerPixelX = unitsPerPixelY * cam.aspect;

        // Мировое смещение (перетаскивая курсор вправо, «толкаем» мир влево → вычитаем)
        Vector3 worldDelta = new Vector3(screenDelta.x * unitsPerPixelX,
                                         screenDelta.y * unitsPerPixelY,
                                         0f);

        Vector3 target = camOrigin - worldDelta;                  // Новая позиция камеры из исходной
        cam.transform.position = new Vector3(target.x, target.y, camZ); // Двигаем камеру (Z фикс)
    }
}