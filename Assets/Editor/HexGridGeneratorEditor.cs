using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HexGridGenerator))]
public class HexGridGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // –исуем обычный интерфейс инспектора
        DrawDefaultInspector();

        // —сылка на объект, дл€ которого редактор (HexGridGenerator)
        HexGridGenerator generator = (HexGridGenerator)target;

        //  нопка генерации
        if (GUILayout.Button("—генерировать сетку"))
        {
            generator.GenerateGrid();
        }

    }
}
