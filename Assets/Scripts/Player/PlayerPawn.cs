using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerPawn : MonoBehaviour
{
    public static PlayerPawn Instance;
    public int x;
    public int y;

    [Header("Visual")]
    public Transform visualRoot;
    public SpriteRenderer spriteRenderer;
    public float facingAngleOffset = 0f;
    public bool flipXInsteadOfRotate = false;

    [Header("Animation / Animator")]
    public Animator animator;
    [Tooltip("Имя bool-параметра в Animator, который включает походку")]
    public string isMovingParam = "IsMoving";

    // ===== NEW: пакетный режим анимации =====
    // Пока счётчик > 0, считаем что идёт «длинное» перемещение; аниматор включён.
    private int _moveBatchDepth = 0;

    /// Включить «пакет» — анимация будет крутиться на протяжении всей серии шагов.
    public void BeginMoveBatch()
    {
        _moveBatchDepth++;                                    // увеличиваем глубину пакета
        if (animator) animator.SetBool(isMovingParam, true);  // включаем походку (если ещё не включена)
    }

    /// Выключить «пакет» — если это был последний, гасим анимацию.
    public void EndMoveBatch()
    {
        _moveBatchDepth = Mathf.Max(0, _moveBatchDepth - 1);  // снижаем глубину (не ниже 0)
        if (_moveBatchDepth == 0 && animator)                 // если пакетов больше нет
            animator.SetBool(isMovingParam, false);           // выключаем походку
    }
    // ========================================

    public void PlaceAt(int newX, int newY)
    {
        x = newX;                                            // сохраняем логические координаты
        y = newY;
        var tile = HexMapController.Instance.GetHex(x, y);   // берём тайл по координатам
        if (tile != null)
            transform.position = tile.transform.position;    // ставим трансформ на позицию тайла
    }

    public bool CanMoveTo(HexTile target)
    {
        if (target == null) return false;                                    // нет цели
        if (!target.isPassable) return false;                                 // не проходим
        var neighbors = HexMapController.Instance.GetNeighbors(x, y);         // соседи текущей клетки
        return neighbors.Contains(target);                                    // можно, если цель — сосед
    }

    // === Публичный ход на соседний тайл (обычный, вне «пакета») ===
    public void MoveTo(HexTile target)
    {
        MoveToInternal(target, inBatch: false);                               // используем общий метод
    }

    // === NEW: ход на соседний тайл «внутри пакета» (используется при длинном пути) ===
    public void MoveToInPath(HexTile target)
    {
        MoveToInternal(target, inBatch: true);                                // не трогаем аниматор на сегменте
    }

    // Общая логика перемещения на соседний тайл
    private void MoveToInternal(HexTile target, bool inBatch)
    {
        if (!CanMoveTo(target)) return;                                       // защитная проверка
        StartCoroutine(MovePawnSmooth(target, 0.8f, inBatch));                // запускаем корутину шага
        x = target.x;                                                         // обновляем логические координаты
        y = target.y;
        HexMapController.Instance.RevealNeighbors(x, y);                      // открываем соседей новой позиции
    }

    // Плавный шаг к соседнему тайлу; inBatch=true → не переключаем аниматор здесь
    private IEnumerator MovePawnSmooth(HexTile target, float speedUnitsPerSec = 1f, bool inBatch = false)
    {
        if (!target) yield break;                                             // защита от null
        var tr = transform;                                                   // кэш трансформа
        Vector3 from = tr.position;                                           // стартовая позиция
        Vector3 to = target.transform.position;                               // целевая позиция
        float dist = Vector3.Distance(from, to);                              // расстояние до цели
        float dur = Mathf.Max(0.1f, dist / Mathf.Max(0.001f, speedUnitsPerSec)); // длительность шага
        float t = 0f;

        FaceTowards(from, to);                                                // поворачиваем визуал «куда идём»

        // Если это одиночный шаг (вне пакета) — включаем походку здесь
        if (!inBatch && animator)
            animator.SetBool(isMovingParam, true);

        while (t < dur)                                                       // интерполируем до конца
        {
            t += Time.deltaTime;                                              // накапливаем время
            float k = Mathf.SmoothStep(0f, 1f, t / dur);                      // сглаженная кривая
            tr.position = Vector3.LerpUnclamped(from, to, k);                 // перемещаемся по кривой
            yield return null;                                                // ждём кадр
        }

        tr.position = to;                                                     // фиксируем финальную позицию

        // Если это одиночный шаг (вне пакета) — выключаем походку здесь
        if (!inBatch && animator)
            animator.SetBool(isMovingParam, false);
    }

    // Поворот визуала к точке назначения
    private void FaceTowards(Vector3 fromWorld, Vector3 toWorld)
    {
        Vector3 dir = (toWorld - fromWorld);
        dir.z = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        if (flipXInsteadOfRotate && spriteRenderer)
        {
            spriteRenderer.flipX = (dir.x < 0f);                              // отражаем спрайт по X вместо поворота
            if (visualRoot) visualRoot.rotation = Quaternion.identity;        // сбрасываем поворот корня визуала
            return;
        }

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;              // угол направления
        angle += facingAngleOffset;                                           // поправка, если исходный спрайт «смотрит» не вправо
        if (visualRoot) visualRoot.rotation = Quaternion.Euler(0f, 0f, angle);// поворачиваем дочерний визуальный узел
    }
}
