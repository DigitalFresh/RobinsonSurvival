using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// Маленькая модалка: текст + (опционально) картинка + одна кнопка ОК.
public class SmallModalUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image picture;       // можно не назначать, если не нужна картинка
    [SerializeField] private TMP_Text text;       // основной текст
    [SerializeField] private Button okButton;     // кнопка ОК
    [SerializeField] private CanvasGroup cg;      // опционально, для клика-блокера/видимости

    private Action _onOk;

    private void Awake()
    {
        if (!cg) cg = GetComponentInChildren<CanvasGroup>(true);
        if (okButton) okButton.onClick.AddListener(OnOk);
        HideImmediate();
    }

    public void Show(string message, Sprite icon = null, Action onOk = null)
    {
        _onOk = onOk;
        if (text) text.text = message ?? "";

        if (picture)
        {
            picture.sprite = icon;
            picture.enabled = icon != null;
        }

        gameObject.SetActive(true);
        if (cg) { cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true; }
    }

    private void OnOk()
    {
        HideImmediate();
        _onOk?.Invoke();
    }

    private void HideImmediate()
    {
        if (cg) { cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false; }
        gameObject.SetActive(false);
    }
}
