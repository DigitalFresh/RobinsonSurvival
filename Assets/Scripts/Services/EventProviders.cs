using UnityEngine;                                  // Доступ к Debug, Resources
                                                    // (Мы не используем Addressables здесь — их можно добавить позже)

// Контракт «как получить массив событий»
public interface IEventProvider                      // Интерфейс для подменяемых источников данных
{
    EventSO[] LoadAllEvents();                       // Возвращает все доступные события
}

// Провайдер через папку Resources/Events
public class ResourcesEventProvider : IEventProvider // Реализация: загрузка из Resources
{
    private readonly string _path;                   // Путь внутри Resources (например, "Events")

    public ResourcesEventProvider(string resourcesPath) // Конструктор провайдера с путём
    {
        _path = resourcesPath;                       // Сохраняем путь
    }

    public EventSO[] LoadAllEvents()                 // Загрузка всех EventSO из Resources
    {
        var all = Resources.LoadAll<EventSO>(_path); // Загружаем все EventSO по пути
        if (all == null || all.Length == 0)          // Если ничего не нашли
        {
            Debug.LogWarning($"ResourcesEventProvider: не найдено EventSO по пути Resources/{_path}"); // Предупреждаем
            return new EventSO[0];                   // Возвращаем пустой массив
        }
        return all;                                  // Возвращаем найденные события
    }
}

// Провайдер через EventCatalog (ScriptableObject)
public class CatalogEventProvider : IEventProvider   // Реализация: загрузка из каталога
{
    private readonly EventCatalog _catalog;          // Ссылка на ассет-каталог

    public CatalogEventProvider(EventCatalog catalog) // Конструктор провайдера с каталогом
    {
        _catalog = catalog;                          // Сохраняем ссылку
    }

    public EventSO[] LoadAllEvents()                 // Загрузка из каталога
    {
        if (_catalog == null)                        // Если каталог не задан
        {
            Debug.LogError("CatalogEventProvider: catalog == null"); // Сообщаем об ошибке
            return new EventSO[0];                   // Возвращаем пустой массив
        }
        _catalog.BuildIndex();                       // Перестраиваем индекс (на случай изменений)
        return _catalog.GetAll();                    // Возвращаем все события
    }
}