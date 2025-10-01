using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Глобальный «шлагбаум» для пользовательского ввода.
/// Любая модалка может Acquire(this) при показе и Release(this) при закрытии.
/// Пока есть хотя бы один владелец — IsBlocked = true.
public class ModalGate : MonoBehaviour
{
    public static ModalGate Instance { get; private set; }

    /// Опционально: полноэкранный Raycast-блокировщик (Image на верхнем Canvas).
    /// Если назначить, он будет вкл/выкл автоматически вместе с блокировкой.
    [Header("Optional overlay")]
    public Graphic globalRaycastBlocker; // Например, прозрачный Image

    private readonly HashSet<object> owners = new HashSet<object>(); // кто держит блокировку

    /// Событие — удобно, чтобы что-то отключать/включать реактивно.
    public static event Action<bool> OnStateChanged;

    public static bool IsBlocked => Instance != null && Instance.owners.Count > 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        UpdateOverlay(); // на всякий случай синхронизировать состояние
    }

    /// Занять блокировку (безопасно вызывать повторно — вторая попытка от того же owner игнорируется).
    public static void Acquire(object owner)
    {
        if (owner == null) owner = typeof(ModalGate); // защитный ключ
        EnsureInstance();

        bool added = Instance.owners.Add(owner);
        if (added)
        {
            Instance.UpdateOverlay();
            OnStateChanged?.Invoke(true);
        }
    }

    /// Освободить блокировку.
    public static void Release(object owner)
    {
        if (Instance == null) return;
        if (owner == null) owner = typeof(ModalGate);

        bool removed = Instance.owners.Remove(owner);
        if (removed)
        {
            Instance.UpdateOverlay();
            OnStateChanged?.Invoke(IsBlocked);
        }
    }

    /// Полностью очистить (на случай аварий).
    public static void ClearAll()
    {
        if (Instance == null) return;
        Instance.owners.Clear();
        Instance.UpdateOverlay();
        OnStateChanged?.Invoke(false);
    }

    private static void EnsureInstance()
    {
        if (Instance != null) return;

        // Если вы не положили ModalGate в сцену заранее,
        // создадим объект на лету на корневом Canvas.
        var rootCanvas = GameObject.FindFirstObjectByType<ModalGate>();
        var go = new GameObject("ModalGate");
        if (rootCanvas != null) go.transform.SetParent(rootCanvas.transform, false);
        Instance = go.AddComponent<ModalGate>();
    }

    private void UpdateOverlay()
    {
        if (globalRaycastBlocker == null) return;
        bool on = owners.Count > 0;
        globalRaycastBlocker.raycastTarget = on;
        globalRaycastBlocker.gameObject.SetActive(on);
    }
}
