#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Инспектор для AdventureAsset: быстрый ввод размеров/ячеек и применение в сцену.
/// ВАЖНО: поиск компонентов — через Object.FindFirstObjectByType(...), без устаревших API.
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
        if (GUILayout.Button("Заполнить недостающие"))
        {
            Undo.RecordObject(A, "EnsureAllCellsPresent");
            A.EnsureAllCellsPresent(true);
            EditorUtility.SetDirty(A);
        }
        if (GUILayout.Button("Очистить все ячейки"))
        {
            if (EditorUtility.DisplayDialog("Очистить?", "Удалить все ячейки?", "Да", "Отмена"))
            {
                Undo.RecordObject(A, "ClearCells");
                A.cells.Clear();
                EditorUtility.SetDirty(A);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"Cells ({A.cells.Count})", EditorStyles.boldLabel);
        //EditorGUILayout.HelpBox("MVP: редактируйте список. Полноценный редактор сетки добавим отдельным окном.", MessageType.Info);
        EditorGUILayout.PropertyField(propCells, includeChildren: true);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Применить к сцене (через AdventureLoader на сцене)"))
        {
            // Ищем ЛОАДЕР ТОЛЬКО новым API:
            var loader = Object.FindFirstObjectByType<AdventureLoader>(FindObjectsInactive.Include);

            // Если не нашли — предложим создать автоматически
            if (!loader)
            {
                bool create = EditorUtility.DisplayDialog(
                    "AdventureLoader не найден",
                    "В сцене нет AdventureLoader. Добавить на новый GameObject?",
                    "Добавить", "Отмена"
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

                // Вызываем билд сразу из инспектора
                loader.BuildIntoScene();
            }
            else
            {
                EditorUtility.DisplayDialog("Не удалось применить", "AdventureLoader отсутствует и не был создан.", "Ок");
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
