using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// Модалка выбора одной из двух альтернативных наград.
/// Показ/закрытие — через ModalManager.Show(kind = AltRewardChoice).
/// </summary>
public class AltRewardChoiceModalUI : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TextMeshProUGUI titleText;       // Заголовок окна (может быть пустым)
    [SerializeField] private TextMeshProUGUI messageText;     // Описание/подсказка (может быть пустым)
    [SerializeField] private Image headerIcon;                // Картинка заголовка (по желанию)

    [Header("Left option")]
    [SerializeField] private RewardItemUI leftItem;           // Плитка награды слева
    [SerializeField] private Button leftChooseButton;         // Кнопка «Выбрать этот результат» под левой плиткой
    [SerializeField] private TextMeshProUGUI leftBtnLabel;    // Надпись на кнопке (необязательно)

    [Header("Right option")]
    [SerializeField] private RewardItemUI rightItem;          // Плитка награды справа
    [SerializeField] private Button rightChooseButton;        // Кнопка «Выбрать этот результат» под правой плиткой
    [SerializeField] private TextMeshProUGUI rightBtnLabel;   // Надпись на кнопке (необязательно)

    [Header("Footer")]
    [SerializeField] private Button cancelButton;             // Если отмена разрешена — закрыть без выбора

    // callback, в который вернём 0/1 — выбранную альтернативу (или -1 при отмене)
    private Action<int> _onChosen;

    // Кэшируем пришедшие альтернативы (должно быть 2)
    private List<EventSO.Reward> _alts;

    /// <summary>
    /// Точка входа из ModalManager.
    /// </summary>
    public void Show(ModalRequest req, Action<int> onChosen)
    {
        _onChosen = onChosen ?? (_ => { });

        // Заголовок/текст/иконка
        if (titleText) titleText.text = string.IsNullOrEmpty(req.title) ? "Выбор награды" : req.title;
        if (messageText) messageText.text = string.IsNullOrEmpty(req.message) ? "Выберите один из вариантов" : req.message;
        if (headerIcon)
        {
            headerIcon.enabled = (req.picture != null);
            headerIcon.sprite = req.picture;
        }

        // Безопасно достаём альтернативы (ожидаем ровно 2)
        _alts = req.altRewards != null ? new List<EventSO.Reward>(req.altRewards) : new List<EventSO.Reward>();
        if (_alts.Count < 2)
        {
            Debug.LogWarning("[AltRewardChoiceModalUI] Требуются две альтернативы награды.");
            CloseWith(-1);
            return;
        }

        // Биндим плитки наград через RewardItemUI — он умеет Resource/RestoreStat/NewCard/FreeReward
        if (leftItem) leftItem.Bind(_alts[0]);   // Resource / RestoreStat / NewCard / FreeReward
        if (rightItem) rightItem.Bind(_alts[1]);  // см. реализацию Bind внутри RewardItemUI

        // Подписи кнопок — на вкус
        if (leftBtnLabel) leftBtnLabel.text = "Выбрать этот результат";
        if (rightBtnLabel) rightBtnLabel.text = "Выбрать этот результат";

        // Клики
        if (leftChooseButton) leftChooseButton.onClick.AddListener(() => CloseWith(0));
        if (rightChooseButton) rightChooseButton.onClick.AddListener(() => CloseWith(1));
        if (cancelButton)
        {
            if (req.canCancel) cancelButton.onClick.AddListener(() => CloseWith(-1));
            else cancelButton.gameObject.SetActive(false);
        }
    }

    /// <summary>Закрыть окно и сообщить выбор (-1 = отмена).</summary>
    private void CloseWith(int idx)
    {
        try { _onChosen?.Invoke(idx); } catch { /*ignored*/ }
        // Никакого ModalManager.CloseTop — просто уничтожаем инстанс модалки
        Destroy(gameObject);
    }
}
