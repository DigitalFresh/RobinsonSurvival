using UnityEngine; // Базовые типы Unity
using System.Collections;            // ДЛЯ IEnumerator  // --- ADDED START ---
using System.Collections.Generic;    // для List

public class PlayerPawn : MonoBehaviour // Скрипт «фишки» игрока на карте
{
    public static PlayerPawn Instance;
    public int x;                    // Текущий X игрока (столбец)
    public int y;                    // Текущий Y игрока (строка)

    [Header("Visual")]
    public Transform visualRoot;                 // Дочерний трансформ со спрайтом — будем вращать его, а не логику узла
    public SpriteRenderer spriteRenderer;        // Рендерер спрайта на фигурке
    public float facingAngleOffset = 0f;         // Сдвиг, если исходный спрайт «смотрит» не по оси X
    public bool flipXInsteadOfRotate = false;    // Если нужно не крутить, а только отражать по X (для 2D-стиля)

    [Header("Animation / Animator")]                          // Заголовок секции в инспекторе
    public Animator animator;                                  // Ссылка на Animator на визуальном объекте (с SpriteRenderer)
    [Tooltip("Имя bool-параметра в Animator, который включает походку")]
    public string isMovingParam = "IsMoving";                  // Имя bool-параметра (настроите в Animator)
                                                               // --- ADDED END ---

    public void PlaceAt(int newX, int newY) // Поставить фишку на координаты (без анимации)
    {
        x = newX;                                           // Сохраняем X
        y = newY;                                           // Сохраняем Y
        var tile = HexMapController.Instance.GetHex(x, y);  // Находим тайл по координатам
        if (tile != null)                                   // Если нашли
        {
            transform.position = tile.transform.position;   // Перемещаем фишку в позицию тайла
        }
    }

    public bool CanMoveTo(HexTile target) // Проверяем, можно ли идти на цель (соседний, проходимый)
    {
        if (target == null) return false;                                 // Нет цели — нельзя
        if (!target.isPassable) return false;                              // Непроходим — нельзя
        var neighbors = HexMapController.Instance.GetNeighbors(x, y);      // Берём соседей текущей позиции
        return neighbors.Contains(target);                                 // Разрешаем, если цель — один из соседей
    }

    public void MoveTo(HexTile target) // Переместиться на соседний тайл
    {
        if (!CanMoveTo(target)) return;                                    // Стоп, если нельзя
        StartCoroutine(MovePawnSmooth(target, 0.8f));
        // transform.position = target.transform.position;                    // Двигаем фишку
        x = target.x;                                                      // Обновляем X
        y = target.y;
        HexMapController.Instance.RevealNeighbors(x, y);                   // Открываем соседей новой позиции
        // Здесь позже вставим: если на тайле событие — запустить его отыгрыш и переместить игрока по правилам DD
    }
    private IEnumerator MovePawnSmooth(HexTile target, float speedUnitsPerSec = 1f)
    {
        if (!target) yield break;                  // Защита от null
        var tr = transform;                            // Трансформ фишки
        Vector3 from = tr.position;                         // Начальная позиция
        Vector3 to = target.transform.position;             // Финальная позиция
        float dist = Vector3.Distance(from, to);            // Расстояние
        float dur = Mathf.Max(0.1f, dist / Mathf.Max(0.001f, speedUnitsPerSec)); // Длительность перемещения
        float t = 0f;
        // Развернуть визуал в сторону цели
        FaceTowards(from, to); // Повернуть спрайт «куда идём»

        if (animator)                                               // Если Animator назначен
            animator.SetBool(isMovingParam, true);                  // ВКЛ: походка (переход Idle→Walk)

        while (t < dur)                                     // Пока не дошли
        {
            t += Time.deltaTime;                            // Тик времени
            float k = Mathf.SmoothStep(0f, 1f, t / dur);    // S-кривая
            tr.position = Vector3.LerpUnclamped(from, to, k); // Лерп позиции
            yield return null;                              // Ждём кадр
        }
        
        tr.position = to;                                   // Фиксируем позицию

        if (animator)                                               // Если Animator назначен
            animator.SetBool(isMovingParam, false);                 // ВЫКЛ: походка (переход Walk→Idle)

    }

    // --- face direction ---
    private void FaceTowards(Vector3 fromWorld, Vector3 toWorld) // Повернуть визуал к цели
    {
        Vector3 dir = (toWorld - fromWorld);     // Вектор направления
        dir.z = 0f;                              // 2D — игнорируем Z
        if (dir.sqrMagnitude < 0.0001f) return;  // Нечего поворачивать

        if (flipXInsteadOfRotate && spriteRenderer) // Вариант «только отражать по X»
        {
            // Если движение «влево» — flipX = true, вправо — false
            spriteRenderer.flipX = (dir.x < 0f); // Простое правило для 2D
            if (visualRoot)                      // При этом держим общий поворот = 0 (на всякий)
                visualRoot.rotation = Quaternion.identity;
            return;
        }

        // Вариант «реальный поворот» вокруг Z
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // Угол в градусах
        angle += facingAngleOffset;               // Компенсация исходной ориентации спрайта
        if (visualRoot)                           // Вращаем только «визуальный» дочерний узел
            visualRoot.rotation = Quaternion.Euler(0f, 0f, angle);
    }


}