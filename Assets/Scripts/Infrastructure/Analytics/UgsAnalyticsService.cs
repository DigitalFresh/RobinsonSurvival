#if USE_UGS                                                     // ����������� ���� ����, ������ ���� ������� ������ USE_UGS
using System.Collections.Generic;                               // �������/��������� ��� ���������� �������
using System.Threading.Tasks;                                   // Task ��� ����������� �������������
using Unity.Services.Core;                                      // UnityServices.InitializeAsync � ������ �������� Unity
using Unity.Services.Analytics;                                 // AnalyticsService � CustomEvent �� UGS
using UnityEngine;                                              // ����/���������/������ � ������� Unity-����

// ���������� ������ ���������� IAnalyticsService ����� Unity Gaming Services Analytics
public class UgsAnalyticsService : IAnalyticsService            // ��������� ������: IsEnabled/SetEnabled/TrackEvent/SetUserProperty
{
    private bool _enabled;                                      // ���� � �������� �� �������� ������� (�� �������� ������)

    public bool IsEnabled => _enabled;                          // ��������� �������� ������ ��� ������

    // ����������� ������������� UGS (����� ������� ���� ��� �� ������ ����)
    public async Task InitializeAsync()                          // ���������� Task � ����� await � Bootstrap
    {
        // �������������� ������� Unity ������ ���� ���
        if (UnityServices.State != ServicesInitializationState.Initialized)   // ���������, �� ���������������� �� ���
            await UnityServices.InitializeAsync();               // ��������� ���� �������� (� �.�. Analytics)

        // � UGS Analytics 6.0.x ���������� ������ ������ � ����� Start/StopDataCollection
        if (_enabled)                                           // ���� ���� �������� (�� ��������)
            AnalyticsService.Instance.StartDataCollection();     // �������� ���� ����������
    }

    // ��������/��������� ��������� �� ����� ������ (�� ��������� ��������)
    public void SetEnabled(bool enabled)                         // ����� ���������� � ������ ����
    {
        _enabled = enabled;                                      // ���������� ���������

        // � Analytics 6.0.x ���������/���������� � ����� ��������
        if (enabled) AnalyticsService.Instance.StartDataCollection(); // ��������� ����
        else AnalyticsService.Instance.StopDataCollection();  // ��������� ����
    }

    // �������� ���������������� ������� � ������������� �����������
    public void TrackEvent(string name, Dictionary<string, object> props = null) // name � ��� �������, props � ���� ����/��������
    {
        if (!_enabled) return;                                   // ���� ��������� � ������� ����

        // ��� ���������� ����� ���������� �������� ������
        if (props == null || props.Count == 0)                   // ���������, ���� �� ���������
        {
            AnalyticsService.Instance.RecordEvent(name);         // ��� ������� ��� �����
            return;                                              // �������
        }

        // �������� ���: � ������� ���� Unity.VisualScripting.CustomEvent,
        // ������� ���� ���������, ��� ��� ����� UGS-������� ����:
        var evt = new Unity.Services.Analytics.CustomEvent(name); // ������ ������ ������� � �������� ������

        // ����������� �� ���� ����� ����/�������� � ��������� �� � �������
        foreach (var kv in props)                                 // ��������� ������� ����������
        {
            var val = kv.Value;                                   // ���� �������� ��������

            // UGS ��������� ������� ����: string/int/long/float/double/bool.
            // �� ��������� ��������� �������� � ������ (��������, enum/Vector2/������� �������).
            if (!(val is string || val is int || val is long || val is float || val is double || val is bool))
                val = val?.ToString();                            // ����-�������� ������ � ����������� � ������

            evt.Add(kv.Key, val);                                 // ��������� ���� � ������� �� ����� (�������� � ���������� evt[k]=v)
        }

        AnalyticsService.Instance.RecordEvent(evt);               // ���������� �������������� �������
    }

    public void SetUserProperty(string key, object value)         // � UGS 6.0.x ������������ �user properties� ���
    {
        // ������������: ������� ���������������� �������� � ���������� �������,
        // ���� ������������ Cloud Save/Remote Config ��� �������������.
    }
}
#endif

//#if USE_UGS
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Unity.Services.Core;
//using Unity.Services.Analytics;
//using UnityEngine;

//// �����: �� �������� ����� using Unity.VisualScripting;
//// ���� �� ��� ��������� ���-�� ��������� � �� �� �����
//// ���������� ������ ��� ���� ��� �������.

//public class UgsAnalyticsService : IAnalyticsService
//{
//    private bool _enabled;
//    public bool IsEnabled => _enabled;

//    // ������� ���� ��� ��� ������ (��. ������ ����).
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

//        // ��� SDK 6.0.x � ��������/��������� ����.
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

//        // ���� ��������� ������ ���: Unity.Services.Analytics.CustomEvent
//        var evt = new Unity.Services.Analytics.CustomEvent(name);

//        foreach (var kv in props)
//        {
//            var val = kv.Value;

//            // ����������� ������ ���������; �� ��������� � ToString()
//            if (!(val is string || val is int || val is long || val is float || val is double || val is bool))
//                val = val?.ToString();

//            evt.Add(kv.Key, val); // ���: evt[kv.Key] = val;
//        }

//        AnalyticsService.Instance.RecordEvent(evt);
//    }

//    public void SetUserProperty(string key, object value)
//    {
//        // � UGS 6.0.x ��� ��������� user properties � ����� � ��������� ������� ��� Cloud Save.
//    }
//}
//#endif