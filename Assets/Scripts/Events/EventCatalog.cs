using System.Collections.Generic;                 // Подключаем коллекции (List, Dictionary)
using UnityEngine;                                // Доступ к ScriptableObject, Debug, CreateAssetMenu и т.д.

[CreateAssetMenu(fileName = "EventCatalog", menuName = "Robinson/Event Catalog")] // Добавляем пункт меню для создания ассета
public class EventCatalog : ScriptableObject      // ScriptableObject — хранит данные, живёт как .asset
{
    [Tooltip("Список всех событий (EventSO), вручную или через тулзы редактора")]
    public List<EventSO> events = new List<EventSO>(); // Публичный список событий для заполнения/редактирования

    [System.NonSerialized]                         // Не сериализуем в ассет (рабочее поле для рантайма/редактора)
    private Dictionary<string, EventSO> _byId;     // Индекс для быстрого доступа: eventId → EventSO

    public void BuildIndex()                       // Переcтроить словарь индекса по списку
    {
        _byId = new Dictionary<string, EventSO>();      // Создаём пустой словарь
        foreach (var e in events)                       // Перебираем все элементы списка
        {
            if (e == null) continue;                    // Пропускаем пустые ссылки
            if (string.IsNullOrEmpty(e.eventId))        // Если нет ID — логируем предупреждение
            {
                Debug.LogWarning($"EventSO без eventId: {e.name}"); // Не критично, но лучше заполнить
                continue;                                   // Пропускаем такие элементы при индексации
            }
            if (_byId.ContainsKey(e.eventId))               // Проверяем на дубликат ID
            {
                Debug.LogError($"Дубликат eventId: {e.eventId} в каталоге"); // Сообщаем об ошибке
                continue;                                   // Пропустим дубликат
            }
            _byId.Add(e.eventId, e);                        // Кладём в словарь: id → EventSO
        }
    }

    public EventSO GetById(string id)                 // Получить событие по строковому ID
    {
        if (_byId == null) BuildIndex();             // Если индекс ещё не построен — строим
        _byId.TryGetValue(id, out var so);           // Пытаемся получить EventSO
        return so;                                   // Возвращаем EventSO (или null)
    }

    public EventSO[] GetAll()                         // Удобный доступ ко всем событиям массивом
    {
        return events.ToArray();                      // Возвращаем копию списка как массив
    }
}