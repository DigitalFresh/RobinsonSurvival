using System;                       // Action
using System.Collections.Generic;
using TMPro;                        // TextMeshProUGUI
using UnityEngine;                  // MonoBehaviour
using UnityEngine.UI;               // Button

// Простое модальное окно подтверждения с блокировкой кликов под ним
public class ConfirmModalUI : MonoBehaviour
{
    [Header("Restore chips (optional)")]
    [SerializeField] private RectTransform restoreStatBlock;   // Panel/RestoreStatBlock
    [SerializeField] private Image chipIcon1, chipIcon2, chipIcon3;   // component 1/2/3 Image
    [SerializeField] private TextMeshProUGUI chipText1, chipText2, chipText3; // component 1/2/3 Text

    public static ConfirmModalUI Instance;              // Синглтон

    public CanvasGroup canvasGroup;                     // Для видимости/блокировки
    public TextMeshProUGUI messageText;                 // Текст вопроса
    public Button yesButton;                            // Кнопка "Да"
    public Button noButton;                             // Кнопка "Нет"

    private Action _onYes;                              // Колбэк по "Да"
    private Action _onNo;                               // Колбэк по "Нет"

    /// Данные одного «чипа» (иконка + «+Х» + цвет текста)
    [System.Serializable]
    public struct RestoreLine
    {
        public Sprite icon;
        public string label;
        public Color color;
    }

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
        if (restoreStatBlock) restoreStatBlock.gameObject.SetActive(false);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;                      // Делаем видимым
            canvasGroup.blocksRaycasts = true;           // Блокируем клики под модалкой
            canvasGroup.interactable = true;             // Делаем интерактивным

            ModalGate.Acquire(this); // <— включаем глобальную блокировку
        }
    }

    ///  «богатый» показ: заголовок+текст + набор чипов
    public void ShowRich(string title, string message, List<RestoreLine> lines, System.Action onYes, System.Action onNo = null)
    {
        // 1) Подготовим основной текст (title + \n\n + message)
        string final = string.IsNullOrEmpty(title) ? (message ?? "") : (title + "\n\n" + (message ?? ""));
        if (messageText) messageText.text = final;

        // 2) Включим/заполним блок чипов
        var chips = new (Image img, TextMeshProUGUI txt)[] {
        (chipIcon1, chipText1), (chipIcon2, chipText2), (chipIcon3, chipText3)
    };
        int n = (lines != null) ? Mathf.Min(lines.Count, chips.Length) : 0;

        if (restoreStatBlock) restoreStatBlock.gameObject.SetActive(n > 0);

        for (int i = 0; i < chips.Length; i++)
        {
            bool on = i < n;
            if (chips[i].img) chips[i].img.transform.parent.gameObject.SetActive(on); // включаем component i
            if (!on) continue;

            var ln = lines[i];
            if (chips[i].img) chips[i].img.sprite = ln.icon;              // иконка
            if (chips[i].txt)
            {
                chips[i].txt.text = ln.label;              // «+X»
                chips[i].txt.color = ln.color;
            }           // цвет
        }

        // 3) Показываем окно (как в обычном Show)
        _onYes = onYes;
        _onNo = onNo;

        gameObject.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            ModalGate.Acquire(this);
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
