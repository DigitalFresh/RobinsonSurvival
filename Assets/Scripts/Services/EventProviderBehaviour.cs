using UnityEngine;                                  // Доступ к MonoBehaviour, CreateAssetMenu и т.д.

public enum EventProviderMode { Resources, Catalog } // Режим: грузить из Resources или из Catalog

public class EventProviderBehaviour : MonoBehaviour  // Компонент на сцене, чтобы выбрать и раздать провайдера
{
    public static EventProviderBehaviour Instance;   // Статическая ссылка (упрощённый синглтон)

    [Header("Mode")]
    public EventProviderMode mode = EventProviderMode.Catalog; // По умолчанию используем каталог

    [Header("Resources Provider")]
    public string resourcesPath = "Events";          // Путь внутри Resources (если выбран режим Resources)

    [Header("Catalog Provider")]
    public EventCatalog eventCatalog;                // Ссылка на EventCatalog.asset (если выбран режим Catalog)

    private IEventProvider _provider;                // Текущий провайдер (выбранный по режиму)

    private void Awake()                             // Ранняя инициализация
    {
        Instance = this;                             // Сохраняем ссылку на себя

        switch (mode)                                // Смотрим выбранный режим
        {
            case EventProviderMode.Resources:        // Если режим Resources
                _provider = new ResourcesEventProvider(resourcesPath); // Создаём провайдера Resources
                break;                               // Выходим из switch

            case EventProviderMode.Catalog:          // Если режим Catalog
                _provider = new CatalogEventProvider(eventCatalog); // Создаём провайдера Catalog
                break;                               // Выходим из switch
        }
    }

    public IEventProvider GetProvider()              // Выдать текущий провайдер (для других систем)
    {
        if (_provider == null)                       // Если провайдер ещё не создан (на всякий случай)
        {
            Awake();                                 // Инициализируем заново
        }
        return _provider;                            // Возвращаем провайдера
    }
}