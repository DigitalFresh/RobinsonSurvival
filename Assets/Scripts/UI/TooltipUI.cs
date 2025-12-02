using UnityEngine;
using UnityEngine.UI;
using TMPro;
// Новый ввод (безопасно: в рантайме проверяем Mouse.current != null)
using UnityEngine.InputSystem;

/// Окно-подсказка (одиночка). Держать под HUD_Canvas, выключенным по умолчанию.
public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance { get; private set; }

    [SerializeField] RectTransform panel;      // корневой RT подсказки
    [SerializeField] TMP_Text label;           // текст
    [SerializeField] Vector2 screenOffset = new(16, -16);
    [SerializeField] Canvas rootCanvas;        // HUD canvas (Screen Space Overlay/Camera)
    [SerializeField] CanvasGroup cg;           // чтобы гарантированно не ловить клики

    RectTransform canvasRT;
    bool visible;

    void Awake()
    {
        Instance = this;
        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();
        canvasRT = rootCanvas ? rootCanvas.transform as RectTransform : null;

        if (!cg) cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;   // подсказка не должна перехватывать наведение/клики
        cg.interactable = false;

        // На всякий случай отключим raycastTarget у всех графиков
        foreach (var g in GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;

        Hide();
    }

    void OnDisable()
    {
        // если объект отключили — «схлопнем» состояние
        visible = false;
    }

    public void Show(string text)
    {
        if (!panel || !label) return;
        label.text = text ?? "";
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        visible = true;
        MoveToMouse(); // сразу у курсора
    }

    public void Hide()
    {
        visible = false;
        if (panel) panel.gameObject.SetActive(false);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (visible) MoveToMouse();
    }

    void MoveToMouse()
    {
        if (!canvasRT || !panel || rootCanvas == null) return;

        // Позиция курсора для New Input System с безопасным fallback
        Vector2 mousePos = Mouse.current != null
            ? (Vector2)Mouse.current.position.ReadValue()
            : (Vector2)UnityEngine.Input.mousePosition;

        Vector2 pos = mousePos + screenOffset;

        // Клиппинг по экрану
        var size = panel.sizeDelta;
        float maxX = Screen.width - size.x * 0.5f - 8f;
        float maxY = Screen.height - size.y * 0.5f - 8f;
        pos.x = Mathf.Clamp(pos.x, 8f + size.x * 0.5f, maxX);
        pos.y = Mathf.Clamp(pos.y, 8f + size.y * 0.5f, maxY);

        // В локальные координаты Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT,
            pos,
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
            out var local);

        panel.anchoredPosition = local;
        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
    }
}
