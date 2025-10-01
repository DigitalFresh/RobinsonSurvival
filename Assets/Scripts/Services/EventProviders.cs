using UnityEngine;                                  // ������ � Debug, Resources
                                                    // (�� �� ���������� Addressables ����� � �� ����� �������� �����)

// �������� ���� �������� ������ �������
public interface IEventProvider                      // ��������� ��� ����������� ���������� ������
{
    EventSO[] LoadAllEvents();                       // ���������� ��� ��������� �������
}

// ��������� ����� ����� Resources/Events
public class ResourcesEventProvider : IEventProvider // ����������: �������� �� Resources
{
    private readonly string _path;                   // ���� ������ Resources (��������, "Events")

    public ResourcesEventProvider(string resourcesPath) // ����������� ���������� � ����
    {
        _path = resourcesPath;                       // ��������� ����
    }

    public EventSO[] LoadAllEvents()                 // �������� ���� EventSO �� Resources
    {
        var all = Resources.LoadAll<EventSO>(_path); // ��������� ��� EventSO �� ����
        if (all == null || all.Length == 0)          // ���� ������ �� �����
        {
            Debug.LogWarning($"ResourcesEventProvider: �� ������� EventSO �� ���� Resources/{_path}"); // �������������
            return new EventSO[0];                   // ���������� ������ ������
        }
        return all;                                  // ���������� ��������� �������
    }
}

// ��������� ����� EventCatalog (ScriptableObject)
public class CatalogEventProvider : IEventProvider   // ����������: �������� �� ��������
{
    private readonly EventCatalog _catalog;          // ������ �� �����-�������

    public CatalogEventProvider(EventCatalog catalog) // ����������� ���������� � ���������
    {
        _catalog = catalog;                          // ��������� ������
    }

    public EventSO[] LoadAllEvents()                 // �������� �� ��������
    {
        if (_catalog == null)                        // ���� ������� �� �����
        {
            Debug.LogError("CatalogEventProvider: catalog == null"); // �������� �� ������
            return new EventSO[0];                   // ���������� ������ ������
        }
        _catalog.BuildIndex();                       // ������������� ������ (�� ������ ���������)
        return _catalog.GetAll();                    // ���������� ��� �������
    }
}