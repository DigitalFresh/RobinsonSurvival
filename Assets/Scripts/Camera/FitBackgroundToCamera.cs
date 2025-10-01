using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class FitBackgroundToCamera : MonoBehaviour
{
    public Camera cam;
    public bool cover = true; // true: ������� ���� �����; false: ������� (�������� ����)

    SpriteRenderer sr;

    void Reset() { cam = Camera.main; }
    void Awake() { if (!cam) cam = Camera.main; sr = GetComponent<SpriteRenderer>(); }

    void LateUpdate()
    {
        if (!cam || !sr || !sr.sprite) return;

        // ������� ������� ����-������
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;

        // ������ ������� � ������� ������
        Vector2 s = sr.sprite.bounds.size;

        // ������� "cover"/"contain"
        float sx = w / s.x, sy = h / s.y;
        float scale = cover ? Mathf.Max(sx, sy) : Mathf.Min(sx, sy);
        transform.localScale = new Vector3(scale, scale, 1f);

        // ������ ��� �� XY ������ � ��������� Z ����� � �������
        transform.position = new Vector3(cam.transform.position.x,
                                         cam.transform.position.y,
                                         cam.transform.position.z + cam.nearClipPlane + 0.01f);
    }
}