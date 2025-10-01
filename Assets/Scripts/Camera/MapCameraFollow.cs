// Assets/Scripts/Camera/MapCameraFollow.cs
using UnityEngine;                         // базовые типы
using UnityEngine.InputSystem;             // можно оставить, но уже не обязательно

[RequireComponent(typeof(Camera))]         // на всякий случай
public class MapCameraFollow : MonoBehaviour
{
    public static MapCameraFollow Instance;     // статическая ссылка

    [Header("Target")]
    public Transform target;                    // цель (фишка игрока)

    [Header("Follow")]
    public bool followEnabled = true;           // флаг: включить/выключить слежение
    public float smoothTime = 0.15f;            // сглаживание (0 — мгновенно)
    private Vector3 _vel;                       // скорость для SmoothDamp

    private float _z;                           // исходный Z камеры

    private void Awake()                        // инициализация
    {
        Instance = this;                        // сохраняем ссылку
        _z = transform.position.z;              // запоминаем Z (обычно -10)
        var cam = GetComponent<Camera>();       // берём камеру
        cam.orthographic = true;                // гарантируем ортографику
    }

    private void LateUpdate()                   // двигаем ПОСЛЕ логики мира
    {
        if (!followEnabled) return;             // если слежение выключено — выходим
        if (InputSharedState.IsPanning) return; // *** ВАЖНО: если панорамируем — НЕ трогать камеру ***
        if (target == null) return;             // если цели нет — выходим

        Vector3 desired = new Vector3(target.position.x, target.position.y - 0.5f, _z); // желаемая позиция камеры

        if (smoothTime <= 0f)                   // если без сглаживания
            transform.position = desired;       // ставим сразу
        else                                     // иначе — плавно
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
    }

    public void SetTarget(Transform t)          // задать цель
    {
        target = t;                              // сохраняем цель
    }

    public void SnapToTarget()                  // мгновенно прыгнуть к цели
    {
        if (target == null) return;             // защита
        transform.position = new Vector3(target.position.x, target.position.y, _z); // ставим камеру на цель
    }
}

//using UnityEngine;                         // Базовые типы Unity
//using UnityEngine.InputSystem;             // Новый Input System (для проверки зажатой ПКМ)

//// Компонент: центрирует камеру на цели (игроке).
//// Работает с любым Viewport Rect — поэтому игрок окажется в центре *видимой* области (верхние 2/3).
//[RequireComponent(typeof(Camera))]         // Гарантируем, что на объекте есть Camera
//public class MapCameraFollow : MonoBehaviour
//{
//    public static MapCameraFollow Instance;      // Статическая ссылка для удобного доступа

//    [Header("Target")]
//    public Transform target;                     // Кого преследовать (фишка игрока)

//    [Header("Follow")]
//    public bool followEnabled = true;            // Включено ли автоследование
//    public float smoothTime = 0.15f;             // Время сглаживания (0 — жёстко приклеено)
//    private Vector3 _vel;                        // Текущая скорость для SmoothDamp

//    [Header("Pan override")]
//    public bool pauseWhileRightMouseDrag = true; // При зажатой ПКМ временно не следовать (ручное панорамирование)

//    private Camera _cam;                         // Ссылка на камеру
//    private float _z;                            // Z-координата камеры (оставляем постоянной)

//    private void Awake()                         // Инициализация при создании
//    {
//        Instance = this;                         // Сохраняем статическую ссылку
//        _cam = GetComponent<Camera>();           // Берём компонент Camera
//        if (_cam != null) _cam.orthographic = true; // Гарантируем ортографическую проекцию
//        _z = transform.position.z;               // Запоминаем исходный Z (обычно -10)
//    }

//    private void LateUpdate()                    // Делаем следование после всех перемещений (LateUpdate)
//    {
//        if (!followEnabled) return;              // Если следование выключено — ничего не делаем
//        if (target == null) return;              // Если цели нет — выходим

//        // Если включена пауза следования при панорамировании и зажата ПКМ — временно не трогаем камеру
//        if (pauseWhileRightMouseDrag && Mouse.current != null && Mouse.current.rightButton.isPressed)
//            return;                              // Выходим, давая игроку «водить» карту вручную

//        // Желаемая позиция камеры — позиция цели по X/Y, Z оставляем прежним
//        Vector3 desired = new Vector3(target.position.x, target.position.y - 0.6f, _z); // Целевая позиция камеры
//        //Debug.LogWarning(target.position.y);

//        if (smoothTime <= 0f)                    // Если сглаживание 0 или отрицательное
//        {
//            transform.position = desired;        // Ставим камеру сразу (без плавности)
//        }
//        else
//        {
//            transform.position = Vector3.SmoothDamp( // Плавно приближаем позицию камеры к цели
//                transform.position,               // Текущая позиция камеры
//                desired,                          // Желаемая позиция
//                ref _vel,                         // Ссылка на вектор скорости (накапливается внутри)
//                smoothTime                        // Время сглаживания
//            );
//        }
//    }

//    // Публичный метод: задать цель для следования
//    public void SetTarget(Transform t)           // Вызываем, когда появилась/изменилась фишка игрока
//    {
//        target = t;                              // Сохраняем ссылку на цель
//    }

//    // Публичный метод: мгновенно прыгнуть к цели (без сглаживания)
//    public void SnapToTarget()                   // Удобно вызвать сразу после установки фишки
//    {
//        if (target == null) return;              // Если нет цели — выходим
//        transform.position = new Vector3(        // Переносим камеру мгновенно
//            target.position.x,                   // Центр по X = X цели
//            target.position.y,                   // Центр по Y = Y цели
//            _z                                   // Z оставляем прежний
//        );
//    }
//}
