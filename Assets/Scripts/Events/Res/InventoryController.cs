using System;
using System.Collections.Generic;            // Dictionary
using UnityEngine;                           // MonoBehaviour, Debug

// Примитивный инвентарь: хранит количество по ResourceDef
public class InventoryController : MonoBehaviour
{
    public static InventoryController Instance;   // Синглтон для простоты

    // Оповещение: ресурс res в инвентаре стал равен newTotal
    public event Action<ResourceDef, int> OnResourceChanged;

    // Внутреннее хранилище: сколько единиц каждого ресурса есть у игрока
    private readonly Dictionary<ResourceDef, int> _counts = new();

    // Удобное "публичное чтение" всех текущих значений (без мутации)
    public IReadOnlyDictionary<ResourceDef, int> Counts => _counts;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // DontDestroyOnLoad(gameObject); // если нужно переживать смену сцен
    }

    // Добавить ресурс (можно и отрицательное число — как расход)
    public void AddResource(ResourceDef res, int amount)
    {
        if (res == null || amount == 0) return;   // защита
        if (!_counts.ContainsKey(res)) _counts[res] = 0;
        _counts[res] += amount;
        Debug.Log($"[Inventory] {(amount >= 0 ? "+" : "")}{amount} x {res.displayName} (итого: {_counts[res]})");

        // Сообщаем подписчикам новое итоговое значение
        OnResourceChanged?.Invoke(res, _counts[res]);
    }

    // Получить текущее количество
    public int GetCount(ResourceDef res) => (res != null && _counts.TryGetValue(res, out var n)) ? n : 0;
}
