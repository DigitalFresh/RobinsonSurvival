using System;                       // Action
using UnityEngine;                  // MonoBehaviour
using UnityEngine.UI;               // Button
using TMPro;                        // TextMeshProUGUI

// ������� ��������� ���� ������������� � ����������� ������ ��� ���
public class ConfirmModalUI : MonoBehaviour
{
    public static ConfirmModalUI Instance;              // ��������

    public CanvasGroup canvasGroup;                     // ��� ���������/����������
    public TextMeshProUGUI messageText;                 // ����� �������
    public Button yesButton;                            // ������ "��"
    public Button noButton;                             // ������ "���"

    private Action _onYes;                              // ������ �� "��"
    private Action _onNo;                               // ������ �� "���"

    private void Awake()                                // �������������
    {
        Instance = this;                                // ���������� ��������
        HideImmediate();                                // ������ ������� �� ������
        if (yesButton != null) yesButton.onClick.AddListener(OnYesClicked); // �������� "��"
        if (noButton != null) noButton.onClick.AddListener(OnNoClicked);   // �������� "���"
    }

    public void Show(string message, Action onYes, Action onNo = null) // �������� � ������� � ���������
    {
        _onYes = onYes;                                  // ��������� ���������� "��"
        _onNo = onNo;                                   // � "���" (����� ���� null)
        if (messageText != null) messageText.text = message; // ������ �����

        gameObject.SetActive(true);                      // �������� ������
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;                      // ������ �������
            canvasGroup.blocksRaycasts = true;           // ��������� ����� ��� ��������
            canvasGroup.interactable = true;             // ������ �������������

            ModalGate.Acquire(this); // <� �������� ���������� ����������
        }
    }

    public void Hide()                                    // �������� � ���������/��� � ��� ���
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;                       // ��������
            canvasGroup.blocksRaycasts = false;           // �� ���������
            canvasGroup.interactable = false;             // �� ������������
        }
        gameObject.SetActive(false);                      // ��������� ������
        _onYes = null;                                    // ������ �������
        _onNo = null;

        ModalGate.Release(this); // <� ������� ���������� ����������
    }

    private void HideImmediate()                          // �������� � Awake
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        gameObject.SetActive(false);
    }

    private void OnYesClicked()                           // ������ "��"
    {
        var cb = _onYes;                                  // ��������� ������
        Hide();                                           // ������ ����
        cb?.Invoke();                                     // �������� ����������
    }

    private void OnNoClicked()                            // ������ "���"
    {
        var cb = _onNo;                                   // ��������� ������
        Hide();                                           // ������ ����
        cb?.Invoke();                                     // �������� ���������� (���� ����)
    }

    // ���������������: ������ ���������, ������� �� ����
    public static bool IsOpen => Instance != null && Instance.canvasGroup != null
                               && Instance.canvasGroup.blocksRaycasts
                               && Instance.canvasGroup.alpha > 0.9f;
}
