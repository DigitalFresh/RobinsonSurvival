#if USE_UGS                                                     // Компилируем этот файл, только если включён дефайн USE_UGS
using System.Collections.Generic;                               // Словари/коллекции для параметров событий
using System.Threading.Tasks;                                   // Task для асинхронной инициализации
using Unity.Services.Core;                                      // UnityServices.InitializeAsync — запуск сервисов Unity
using Unity.Services.Analytics;                                 // AnalyticsService и CustomEvent из UGS
using UnityEngine;                                              // Логи/платформа/версия и базовые Unity-типы

// Реализация нашего интерфейса IAnalyticsService через Unity Gaming Services Analytics
public class UgsAnalyticsService : IAnalyticsService            // Реализуем методы: IsEnabled/SetEnabled/TrackEvent/SetUserProperty
{
    private bool _enabled;                                      // Флаг — включена ли отправка событий (по согласию игрока)

    public bool IsEnabled => _enabled;                          // Публичное свойство только для чтения

    // Асинхронная инициализация UGS (нужно позвать один раз на старте игры)
    public async Task InitializeAsync()                          // Возвращаем Task — можно await в Bootstrap
    {
        // Инициализируем сервисы Unity только один раз
        if (UnityServices.State != ServicesInitializationState.Initialized)   // Проверяем, не инициализировано ли уже
            await UnityServices.InitializeAsync();               // Запускаем ядро сервисов (в т.ч. Analytics)

        // В UGS Analytics 6.0.x управление сбором данных — через Start/StopDataCollection
        if (_enabled)                                           // Если сбор разрешён (по согласию)
            AnalyticsService.Instance.StartDataCollection();     // Включаем сбор телеметрии
    }

    // Включаем/выключаем аналитику во время работы (по изменению согласия)
    public void SetEnabled(bool enabled)                         // Метод интерфейса — меняем флаг
    {
        _enabled = enabled;                                      // Запоминаем состояние

        // В Analytics 6.0.x включение/выключение — этими методами
        if (enabled) AnalyticsService.Instance.StartDataCollection(); // Разрешаем сбор
        else AnalyticsService.Instance.StopDataCollection();  // Запрещаем сбор
    }

    // Отправка пользовательских событий с произвольными параметрами
    public void TrackEvent(string name, Dictionary<string, object> props = null) // name — имя события, props — пары ключ/значение
    {
        if (!_enabled) return;                                   // Если выключено — выходим тихо

        // Без параметров можно отправлять короткой формой
        if (props == null || props.Count == 0)                   // Проверяем, есть ли параметры
        {
            AnalyticsService.Instance.RecordEvent(name);         // Шлём событие без полей
            return;                                              // Выходим
        }

        // Конфликт имён: в проекте есть Unity.VisualScripting.CustomEvent,
        // поэтому ЯВНО указываем, что нам нужен UGS-вариант типа:
        var evt = new Unity.Services.Analytics.CustomEvent(name); // Создаём объект события с заданным именем

        // Пробегаемся по всем парам ключ/значение и добавляем их в событие
        foreach (var kv in props)                                 // Итерируем словарь параметров
        {
            var val = kv.Value;                                   // Берём исходное значение

            // UGS принимает базовые типы: string/int/long/float/double/bool.
            // Всё остальное безопасно приводим к строке (например, enum/Vector2/сложные объекты).
            if (!(val is string || val is int || val is long || val is float || val is double || val is bool))
                val = val?.ToString();                            // Нулл-условная защита и конвертация в строку

            evt.Add(kv.Key, val);                                 // Добавляем поле в событие по ключу (допустим и индексатор evt[k]=v)
        }

        AnalyticsService.Instance.RecordEvent(evt);               // Отправляем сформированное событие
    }

    public void SetUserProperty(string key, object value)         // В UGS 6.0.x произвольных «user properties» нет
    {
        // Рекомендация: хранить пользовательские атрибуты в параметрах событий,
        // либо использовать Cloud Save/Remote Config при необходимости.
    }
}
#endif

//#if USE_UGS
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Unity.Services.Core;
//using Unity.Services.Analytics;
//using UnityEngine;

//// ВАЖНО: не добавляй здесь using Unity.VisualScripting;
//// если он уже подключен где-то глобально — мы всё равно
//// используем полное имя типа для события.

//public class UgsAnalyticsService : IAnalyticsService
//{
//    private bool _enabled;
//    public bool IsEnabled => _enabled;

//    // Вызывай один раз при старте (см. пример ниже).
//    public async Task InitializeAsync()
//    {
//        if (UnityServices.State != ServicesInitializationState.Initialized)
//            await UnityServices.InitializeAsync();

//        if (_enabled)
//            AnalyticsService.Instance.StartDataCollection(); // SDK 6.0.x
//    }

//    public void SetEnabled(bool enabled)
//    {
//        _enabled = enabled;

//        // Для SDK 6.0.x — включаем/выключаем сбор.
//        if (enabled) AnalyticsService.Instance.StartDataCollection();
//        else AnalyticsService.Instance.StopDataCollection();
//    }

//    public void TrackEvent(string name, Dictionary<string, object> props = null)
//    {
//        if (!_enabled) return;

//        if (props == null || props.Count == 0)
//        {
//            AnalyticsService.Instance.RecordEvent(name);
//            return;
//        }

//        // ЯВНО указываем нужный тип: Unity.Services.Analytics.CustomEvent
//        var evt = new Unity.Services.Analytics.CustomEvent(name);

//        foreach (var kv in props)
//        {
//            var val = kv.Value;

//            // Допускаются только примитивы; всё остальное — ToString()
//            if (!(val is string || val is int || val is long || val is float || val is double || val is bool))
//                val = val?.ToString();

//            evt.Add(kv.Key, val); // или: evt[kv.Key] = val;
//        }

//        AnalyticsService.Instance.RecordEvent(evt);
//    }

//    public void SetUserProperty(string key, object value)
//    {
//        // В UGS 6.0.x нет кастомных user properties — клади в параметры событий или Cloud Save.
//    }
//}
//#endif