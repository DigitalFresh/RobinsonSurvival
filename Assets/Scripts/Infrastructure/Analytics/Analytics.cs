using System.Collections.Generic;


public static class Analytics
{
    public static IAnalyticsService Service;


    public static void Init(IAnalyticsService service, bool enabled)
    {
        Service = service;
        Service.SetEnabled(enabled);
    }


    public static void Event(string name, Dictionary<string, object> props = null)
    {
        Service?.TrackEvent(name, props);
    }
}