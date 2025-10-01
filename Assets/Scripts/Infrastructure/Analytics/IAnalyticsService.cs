using System.Collections.Generic;


public interface IAnalyticsService
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
    void TrackEvent(string name, Dictionary<string, object> props = null);
    void SetUserProperty(string key, object value);
}