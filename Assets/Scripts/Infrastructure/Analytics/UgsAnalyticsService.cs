#if USE_UGS
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Analytics;
using UnityEngine;

// ¬ј∆Ќќ: не добавл€й здесь using Unity.VisualScripting;
// если он уже подключен где-то глобально Ч мы всЄ равно
// используем полное им€ типа дл€ событи€.

public class UgsAnalyticsService : IAnalyticsService
{
    private bool _enabled;
    public bool IsEnabled => _enabled;

    // ¬ызывай один раз при старте (см. пример ниже).
    public async Task InitializeAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (_enabled)
            AnalyticsService.Instance.StartDataCollection(); // SDK 6.0.x
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;

        // ƒл€ SDK 6.0.x Ч включаем/выключаем сбор.
        if (enabled) AnalyticsService.Instance.StartDataCollection();
        else AnalyticsService.Instance.StopDataCollection();
    }

    public void TrackEvent(string name, Dictionary<string, object> props = null)
    {
        if (!_enabled) return;

        if (props == null || props.Count == 0)
        {
            AnalyticsService.Instance.RecordEvent(name);
            return;
        }

        // я¬Ќќ указываем нужный тип: Unity.Services.Analytics.CustomEvent
        var evt = new Unity.Services.Analytics.CustomEvent(name);

        foreach (var kv in props)
        {
            var val = kv.Value;

            // ƒопускаютс€ только примитивы; всЄ остальное Ч ToString()
            if (!(val is string || val is int || val is long || val is float || val is double || val is bool))
                val = val?.ToString();

            evt.Add(kv.Key, val); // или: evt[kv.Key] = val;
        }

        AnalyticsService.Instance.RecordEvent(evt);
    }

    public void SetUserProperty(string key, object value)
    {
        // ¬ UGS 6.0.x нет кастомных user properties Ч клади в параметры событий или Cloud Save.
    }
}
#endif