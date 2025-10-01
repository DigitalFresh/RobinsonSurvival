using UnityEngine;                                  // ������ � MonoBehaviour, CreateAssetMenu � �.�.

public enum EventProviderMode { Resources, Catalog } // �����: ������� �� Resources ��� �� Catalog

public class EventProviderBehaviour : MonoBehaviour  // ��������� �� �����, ����� ������� � ������� ����������
{
    public static EventProviderBehaviour Instance;   // ����������� ������ (���������� ��������)

    [Header("Mode")]
    public EventProviderMode mode = EventProviderMode.Catalog; // �� ��������� ���������� �������

    [Header("Resources Provider")]
    public string resourcesPath = "Events";          // ���� ������ Resources (���� ������ ����� Resources)

    [Header("Catalog Provider")]
    public EventCatalog eventCatalog;                // ������ �� EventCatalog.asset (���� ������ ����� Catalog)

    private IEventProvider _provider;                // ������� ��������� (��������� �� ������)

    private void Awake()                             // ������ �������������
    {
        Instance = this;                             // ��������� ������ �� ����

        switch (mode)                                // ������� ��������� �����
        {
            case EventProviderMode.Resources:        // ���� ����� Resources
                _provider = new ResourcesEventProvider(resourcesPath); // ������ ���������� Resources
                break;                               // ������� �� switch

            case EventProviderMode.Catalog:          // ���� ����� Catalog
                _provider = new CatalogEventProvider(eventCatalog); // ������ ���������� Catalog
                break;                               // ������� �� switch
        }
    }

    public IEventProvider GetProvider()              // ������ ������� ��������� (��� ������ ������)
    {
        if (_provider == null)                       // ���� ��������� ��� �� ������ (�� ������ ������)
        {
            Awake();                                 // �������������� ������
        }
        return _provider;                            // ���������� ����������
    }
}