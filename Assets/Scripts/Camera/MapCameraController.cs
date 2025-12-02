using UnityEngine;
using UnityEngine.InputSystem;

// ОДИН скрипт управляет и панорамированием, и автоследованием.
// Во время ПКМ — только панорамирование; в остальные моменты — мягкое слежение за целью.
[RequireComponent(typeof(Camera))]
public class MapCameraController : MonoBehaviour
{
    [Header("Target follow")]
    public Transform target;                 // фишка игрока
    public bool followEnabled = true;        // включить автоследование
    public float followSmoothTime = 0.15f;   // сглаживание слежения (0 = без сглаживания)

    [Header("Pan (RMB) + Zoom")]
    public float zoomSpeed = 2.0f;           // скорость зума
    public float minOrthoSize = 1f;          // мин. размер
    public float maxOrthoSize = 7f;         // макс. размер

    private Camera cam;                      // ссылка на камеру
    private float camZ;                      // Z камеры (фиксируем)
    private Vector3 followVel;               // скорость для SmoothDamp
    private bool isDragging;                 // сейчас панорамируем?
    private Vector3 dragOriginWorld;         // мировая точка под курсором в момент нажатия ПКМ
    private Vector3 camOrigin;               // позиция камеры в момент начала панорамирования

    private void Awake()
    {
        cam = GetComponent<Camera>();        // берём камеру
        cam.orthographic = true;             // орто
        camZ = transform.position.z;         // запомним Z
    }

    // Вызывайте один раз после появления/перемещения фишки
    public void SetTargetAndSnap(Transform t)
    {
        target = t;                          // назначаем цель
        if (target != null)
            transform.position = new Vector3(target.position.x, target.position.y, camZ); // мгновенно центрируем
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // ЗУМ
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float size = cam.orthographicSize - scroll * 0.1f * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(size, minOrthoSize, maxOrthoSize);
        }

        // НАЧАЛО ПАНОРАМИРОВАНИЯ (ПКМ)
        if (mouse.rightButton.wasPressedThisFrame)
        {
            isDragging = true;
            InputSharedState.IsPanning = true; // общий флаг — чтобы другие системы молчали
            camOrigin = transform.position;
            dragOriginWorld = ScreenToWorldOnPlane(mouse.position.ReadValue());
        }

        // КОНЕЦ ПАНОРАМИРОВАНИЯ
        if (mouse.rightButton.wasReleasedThisFrame)
        {
            isDragging = false;
            InputSharedState.IsPanning = false;
        }
    }

    private void LateUpdate()
    {
        var mouse = Mouse.current;

        if (isDragging && mouse != null)
        {
            // ПАНОРАМИРОВАНИЕ: только пан, без автоследования
            Vector3 nowWorld = ScreenToWorldOnPlane(mouse.position.ReadValue());
            Vector3 delta = dragOriginWorld - nowWorld;
            Vector3 targetCamPos = camOrigin + delta;
            transform.position = new Vector3(targetCamPos.x, targetCamPos.y, camZ);
            return; // ВАЖНО: не даём слежению выполниться в этот кадр
        }

        // АВТОСЛЕЖЕНИЕ: только когда не панорамируем
        if (followEnabled && target != null)
        {
            Vector3 desired = new Vector3(target.position.x, target.position.y, camZ);
            if (followSmoothTime <= 0f)
                transform.position = desired;
            else
                transform.position = Vector3.SmoothDamp(transform.position, desired, ref followVel, followSmoothTime);
        }
    }

    private Vector3 ScreenToWorldOnPlane(Vector2 screen)
    {
        float dist = Mathf.Abs(cam.transform.position.z);     // расстояние до плоскости Z=0
        Vector3 sp = new Vector3(screen.x, screen.y, dist);
        Vector3 wp = cam.ScreenToWorldPoint(sp);
        wp.z = 0f;
        return wp;
    }
}
