#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ��������� ��� AdventureAsset: ������� ���� ��������/����� � ���������� � �����.
/// �����: ����� ����������� � ����� Object.FindFirstObjectByType(...), ��� ���������� API.
/// </summary>
[CustomEditor(typeof(AdventureAsset))]
public class AdventureAssetInspector : Editor
{
    private AdventureAsset A;
    private SerializedProperty propWidth, propHeight, propCells;

    private void OnEnable()
    {
        A = (AdventureAsset)target;
        propWidth = serializedObject.FindProperty("width");
        propHeight = serializedObject.FindProperty("height");
        propCells = serializedObject.FindProperty("cells");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Adventure Asset", EditorStyles.boldLabel);
        A.adventureId = EditorGUILayout.TextField("Adventure ID", A.adventureId);
        A.displayName = EditorGUILayout.TextField("Display Name", A.displayName);
        A.version = EditorGUILayout.IntField("Version", A.version);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(propWidth);
        EditorGUILayout.PropertyField(propHeight);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("��������� �����������"))
        {
            Undo.RecordObject(A, "EnsureAllCellsPresent");
            A.EnsureAllCellsPresent(true);
            EditorUtility.SetDirty(A);
        }
        if (GUILayout.Button("�������� ��� ������"))
        {
            if (EditorUtility.DisplayDialog("��������?", "������� ��� ������?", "��", "������"))
            {
                Undo.RecordObject(A, "ClearCells");
                A.cells.Clear();
                EditorUtility.SetDirty(A);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"Cells ({A.cells.Count})", EditorStyles.boldLabel);
        //EditorGUILayout.HelpBox("MVP: ������������ ������. ����������� �������� ����� ������� ��������� �����.", MessageType.Info);
        EditorGUILayout.PropertyField(propCells, includeChildren: true);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("��������� � ����� (����� AdventureLoader �� �����)"))
        {
            // ���� ������ ������ ����� API:
            var loader = Object.FindFirstObjectByType<AdventureLoader>(FindObjectsInactive.Include);

            // ���� �� ����� � ��������� ������� �������������
            if (!loader)
            {
                bool create = EditorUtility.DisplayDialog(
                    "AdventureLoader �� ������",
                    "� ����� ��� AdventureLoader. �������� �� ����� GameObject?",
                    "��������", "������"
                );

                if (create)
                {
                    var go = new GameObject("AdventureLoader");
                    loader = go.AddComponent<AdventureLoader>();
                    Undo.RegisterCreatedObjectUndo(go, "Create AdventureLoader");
                }
            }

            if (loader)
            {
                Undo.RecordObject(loader, "Assign Adventure Asset");
                loader.adventure = A;
                EditorUtility.SetDirty(loader);

                // �������� ���� ����� �� ����������
                loader.BuildIntoScene();
            }
            else
            {
                EditorUtility.DisplayDialog("�� ������� ���������", "AdventureLoader ����������� � �� ��� ������.", "��");
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
