using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HexGridGenerator))]
public class HexGridGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // ������ ������� ��������� ����������
        DrawDefaultInspector();

        // ������ �� ������, ��� �������� �������� (HexGridGenerator)
        HexGridGenerator generator = (HexGridGenerator)target;

        // ������ ���������
        if (GUILayout.Button("������������� �����"))
        {
            generator.GenerateGrid();
        }

    }
}
