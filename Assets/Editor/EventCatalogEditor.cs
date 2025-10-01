#if UNITY_EDITOR                                      // �����������, ��� ��� �� ������ � ����
using UnityEditor;                                    // ������ � AssetDatabase, EditorGUILayout
using UnityEngine;                                    // ������ � Debug, ScriptableObject
using System.Collections.Generic;                     // ��� List
using System.Linq;                                    // ��� LINQ (Distinct, OrderBy)
using System.IO;                                      // ��� ������ � ������

[CustomEditor(typeof(EventCatalog))]                  // ������� Unity: ���� �������� � ��� EventCatalog
public class EventCatalogEditor : Editor              // ����������� �� Editor
{
    private SerializedProperty eventsProp;            // ��������������� �������� ������ events
    private string scanFolder = "Assets/Resources/Events"; // ����� �� ��������� ��� ������������

    private void OnEnable()                           // ����� �������� ������������
    {
        eventsProp = serializedObject.FindProperty("events"); // ������� ��������������� ���� "events"
    }

    public override void OnInspectorGUI()             // ������ ��������� ���������
    {
        serializedObject.Update();                    // �������������� ������������

        // ���� ����� ����� ������������
        EditorGUILayout.LabelField("������������ �����", EditorStyles.boldLabel); // ��������� ������
        scanFolder = EditorGUILayout.TextField("�����:", scanFolder);             // ������ ����� ����

        EditorGUILayout.BeginHorizontal();            // �������� �������������� ����
        if (GUILayout.Button("����������� � ���������")) // ������ ������������
        {
            ScanAndFill(scanFolder);                  // ��������� ������������ �����
        }
        if (GUILayout.Button("�������� ������"))     // ������ �������
        {
            eventsProp.ClearArray();                  // ������� ������ �������
        }
        EditorGUILayout.EndHorizontal();              // ��������� �������������� ����

        EditorGUILayout.Space();                      // ���������� ������

        if (GUILayout.Button("����������� �� ����� ������")) // ������ ����������
        {
            SortByAssetName();                        // ���������� ������
        }

        if (GUILayout.Button("����-��������� ������ eventId (GUID)")) // ������ ������������� ID
        {
            AutoAssignMissingIds();                   // ��������� ID ��� �����
        }

        if (GUILayout.Button("Build Index + Validate")) // ������ ���������� ������� � ���������
        {
            var cat = (EventCatalog)target;           // �������� ������ �� �������
            cat.BuildIndex();                         // ������ ������
            Validate(cat);                            // ����������
        }

        EditorGUILayout.Space();                      // ������

        // ������ ��������� ��������� (������� ������ events)
        EditorGUILayout.PropertyField(eventsProp, includeChildren: true); // ���������� ������ �������

        serializedObject.ApplyModifiedProperties();   // ��������� ���������
    }

    private void ScanAndFill(string folderPath)       // ��������� ������ ��������� �� �����
    {
        if (!AssetDatabase.IsValidFolder(folderPath)) // ���������, ���������� �� �����
        {
            EditorUtility.DisplayDialog("������", $"����� �� �������:\n{folderPath}", "OK"); // ������ �� ������
            return;                                   // �������
        }

        var guids = AssetDatabase.FindAssets("t:EventSO", new[] { folderPath }); // ������� ��� GUID ������� EventSO
        var list = new List<EventSO>();               // ��������� ������ ��������� �������

        foreach (var guid in guids)                   // ��� �� ���� ��������� GUID
        {
            var path = AssetDatabase.GUIDToAssetPath(guid); // �������� ���� ������
            var so = AssetDatabase.LoadAssetAtPath<EventSO>(path); // ��������� EventSO �� ����
            if (so != null) list.Add(so);             // ��������� � ������ ���� ������� ���������
        }

        // ������� ��������� (�� ������) � ��������� ��������������� ����
        var distinct = list.Distinct().ToList();      // ������� �����
        eventsProp.ClearArray();                      // ������� ������� ����
        for (int i = 0; i < distinct.Count; i++)      // ���������� ��� ���������
        {
            eventsProp.InsertArrayElementAtIndex(i);  // ��������� ������� �������
            eventsProp.GetArrayElementAtIndex(i).objectReferenceValue = distinct[i]; // ������ ������ �� EventSO
        }

        serializedObject.ApplyModifiedProperties();   // ��������� ���������
        EditorUtility.SetDirty(target);               // �������� ������ ��� ���������
        AssetDatabase.SaveAssets();                   // ��������� �����

        Debug.Log($"EventCatalog: ��������� ������� � {distinct.Count}"); // �������� ���������
    }

    private void SortByAssetName()                    // ����������� ������ �� ����� ������
    {
        var cat = (EventCatalog)target;               // �������� ������ �� EventCatalog
        cat.events = cat.events                       // ������ ������
            .Where(e => e != null)                    // ������� null
            .OrderBy(e => e.name)                     // ��������� �� ����� ������
            .ToList();                                // ���������� � List

        EditorUtility.SetDirty(cat);                  // �������� ����� ��� ���������
        AssetDatabase.SaveAssets();                   // ���������
        Debug.Log("EventCatalog: ������������ �� ����� ������"); // ���
    }

    private void AutoAssignMissingIds()               // ��������� GUID ���, ��� eventId ������
    {
        var cat = (EventCatalog)target;               // ������ �� �������
        int changed = 0;                              // ������� ���������
        foreach (var e in cat.events)                 // ���������� �������
        {
            if (e == null) continue;                  // ���������� ������
            if (string.IsNullOrEmpty(e.eventId))      // ���� ID ������
            {
                e.eventId = System.Guid.NewGuid().ToString(); // ���������� GUID
                EditorUtility.SetDirty(e);            // �������� EventSO ��� ���������
                changed++;                            // �������������� �������
            }
        }
        AssetDatabase.SaveAssets();                   // ��������� ���������
        Debug.Log($"EventCatalog: ��������� ����� eventId � {changed}"); // ��������
    }

    private void Validate(EventCatalog cat)           // ������� ��������� ��������
    {
        var seen = new HashSet<string>();             // ��������� ��� �������� ������������ ID
        int errors = 0;                               // ������� ������
        foreach (var e in cat.events)                 // ���������� ��� �������
        {
            if (e == null) { Debug.LogError("������ ������ � ��������"); errors++; continue; } // ������: null

            if (string.IsNullOrEmpty(e.eventId)) { Debug.LogError($"������ eventId: {e.name}"); errors++; } // ������: ������ ID
            else if (!seen.Add(e.eventId)) { Debug.LogError($"�������� eventId: {e.eventId} ({e.name})"); errors++; } // ������: �������� ID

            if (e.icon == null) { Debug.LogWarning($"��� icon � �������: {e.name}"); } // ��������������: ��� ������
            // ��� ����� �������� ������ �������� (��������, �������, ����� �� ���� � �.�.)
        }
        if (errors == 0) Debug.Log("EventCatalog: ��������� ������ ��� ������"); // ���� ��� ������
        else Debug.LogError($"EventCatalog: ������ � {errors}");                 // ���� � ����������� ������
    }
}
#endif