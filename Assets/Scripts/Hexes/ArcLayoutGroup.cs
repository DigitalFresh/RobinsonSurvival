using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Layout/Arc Layout Group")]
public class ArcLayoutGroup : LayoutGroup
{
    [Header("Геометрия дуги")]
    public float radius = 200f;
    [Range(0f, 360f)] public float arcAngle = 180f;
    public float startAngle = 90f;   // 0=вправо, 90=вверх
    public bool clockwise = false;

    [Header("Размер детей")]
    public bool controlChildSize = false;
    public Vector2 childSize = new Vector2(100f, 100f);

    [Header("Ориентация элементов")]
    public bool orientToTangent = false;
    public float rotationOffset = 0f;

    // Вызывается движком, формирует rectChildren
    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
    }

    public override void CalculateLayoutInputVertical() { }

    public override void SetLayoutHorizontal() => PositionChildren();
    public override void SetLayoutVertical() => PositionChildren();

    void PositionChildren()
    {
        int n = rectChildren.Count;
        if (n == 0) return;

        float step = (n == 1) ? 0f : arcAngle / (n - 1);
        float dir = clockwise ? -1f : 1f;

        for (int i = 0; i < n; i++)
        {
            var child = rectChildren[i];

            float ang = startAngle + dir * step * i;
            float rad = ang * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

            float w = controlChildSize ? childSize.x : child.rect.width;
            float h = controlChildSize ? childSize.y : child.rect.height;

            SetChildAlongAxis(child, 0, pos.x - w * child.pivot.x, w);
            SetChildAlongAxis(child, 1, pos.y - h * child.pivot.y, h);

            if (orientToTangent)
            {
                float rot = ang + (clockwise ? 90f : -90f) + rotationOffset;
                child.localRotation = Quaternion.Euler(0, 0, rot);
            }
            else
            {
                child.localRotation = Quaternion.identity;
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SetDirty();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetDirty();
    }
#endif
}
