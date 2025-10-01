using System;                       // Action
using UnityEngine;                  // MonoBehaviour
using UnityEngine.UI;               // Button
using TMPro;                        // TextMeshProUGUI

// Простое модальное окно подтверждения с блокировкой кликов под ним
public class ConfirmModalUI : MonoBehaviour
{
    public static ConfirmModalUI Instance;              // Синглтон

    public CanvasGroup canvasGroup;                     // Для видимости/блокировки
    public TextMeshProUGUI messageText;                 // Текст вопроса
    public Button yesButton;                            // Кнопка "Да"
    public Button noButton;                             // Кнопка "Нет"

    private Action _onYes;                              // Колбэк по "Да"
    private Action _onNo;                               // Колбэк по "Нет"

    private void Awake()                                // Инициализация
    {
        Instance = this;                                // Запоминаем синглтон
        HideImmediate();                                // Прячем модалку на старте
        if (yesButton != null) yesButton.onClick.AddListener(OnYesClicked); // Подписка "Да"
        if (noButton != null) noButton.onClick.AddListener(OnNoClicked);   // Подписка "Нет"
    }

    public void Show(string message, Action onYes, Action onNo = null) // Показать с текстом и колбэками
    {
        _onYes = onYes;                                  // Сохраняем обработчик "Да"
        _onNo = onNo;                                   // И "Нет" (может быть null)
        if (messageText != null) messageText.text = message; // Ставим текст

        gameObject.SetActive(true);                      // Включаем объект
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;                      // Делаем видимым
            canvasGroup.blocksRaycasts = true;           // Блокируем клики под модалкой
            canvasGroup.interactable = true;             // Делаем интерактивным

            ModalGate.Acquire(this); // <— включаем глобальную блокировку
        }
    }

    public void Hide()                                    // Спрятать с анимацией/без — тут без
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;                       // Невидимо
            canvasGroup.blocksRaycasts = false;           // Не блокирует
            canvasGroup.interactable = false;             // Не интерактивно
        }
        gameObject.SetActive(false);                      // Выключаем объект
        _onYes = null;                                    // Чистим колбэки
        _onNo = null;

        ModalGate.Release(this); // <— снимаем глобальную блокировку
    }

    private void HideImmediate()                          // Спрятать в Awake
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        gameObject.SetActive(false);
    }

    private void OnYesClicked()                           // Нажато "Да"
    {
        var cb = _onYes;                                  // Сохраняем ссылку
        Hide();                                           // Прячем окно
        cb?.Invoke();                                     // Вызываем обработчик
    }

    private void OnNoClicked()                            // Нажато "Нет"
    {
        var cb = _onNo;                                   // Сохраняем ссылку
        Hide();                                           // Прячем окно
        cb?.Invoke();                                     // Вызываем обработчик (если есть)
    }

    // Вспомогательное: быстро проверить, открыто ли окно
    public static bool IsOpen => Instance != null && Instance.canvasGroup != null
                               && Instance.canvasGroup.blocksRaycasts
                               && Instance.canvasGroup.alpha > 0.9f;
}
