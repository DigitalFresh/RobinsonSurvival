using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class FitBackgroundToCamera : MonoBehaviour
{
    public Camera cam;
    public bool cover = true; // true: покрыть весь экран; false: вписать (возможны поля)

    SpriteRenderer sr;

    void Reset() { cam = Camera.main; }
    void Awake() { if (!cam) cam = Camera.main; sr = GetComponent<SpriteRenderer>(); }

    void LateUpdate()
    {
        if (!cam || !sr || !sr.sprite) return;

        // Видимая область орто-камеры
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;

        // Размер спрайта в мировых юнитах
        Vector2 s = sr.sprite.bounds.size;

        // Масштаб "cover"/"contain"
        float sx = w / s.x, sy = h / s.y;
        float scale = cover ? Mathf.Max(sx, sy) : Mathf.Min(sx, sy);
        transform.localScale = new Vector3(scale, scale, 1f);

        // Держим фон на XY камеры и фиксируем Z рядом с камерой
        transform.position = new Vector3(cam.transform.position.x,
                                         cam.transform.position.y,
                                         cam.transform.position.z + cam.nearClipPlane + 0.01f);
    }
}