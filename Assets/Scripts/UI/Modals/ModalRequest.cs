using System;
using System.Collections.Generic;
using UnityEngine;

public enum ModalKind { Confirm, Info, FreeReward, Small, AltRewardChoice }
public enum ModalSize { Small, Medium, Large }

[Serializable]
public class ModalRequest
{
    // Что показывать
    public ModalKind kind;
    public ModalSize size = ModalSize.Medium;

    // Заголовок/описание/картинка (опционально)
    public string title;
    public string message;
    public Sprite picture;

    // Данные для конкретных модалок
    public List<CardDef> cards;                 // Info (полноценные карты)
    public List<EventSO.Reward> rewards;        // Можно показать как строки «иконка+подпись»
    public List<FreeRewardDef> freeRewards;     // Нативные FreeReward дефы (очередью ShowMany)

    // Кнопки/логика подтверждения
    public bool canCancel = true;
    public string okText = "Ок";
    public string cancelText = "Отмена";

    // AltRewardChoice — сюда кладём РОВНО две альтернативные награды:
    public System.Collections.Generic.List<EventSO.Reward> altRewards;
    // Колбэк выбора: 0 или 1 — индекс выбранной альтернативы; -1 — отмена/закрытие без выбора
    public System.Action<int> onAltChosen;

    // Добавляем: набор «чипов» для Confirm-модалки
    [Serializable]
    public class RestoreChip
    {
        public Sprite icon;
        public string label;
        public Color color = Color.white;
    }
    public List<RestoreChip> restoreLines; // если не null/не пусто — Confirm покажет чипы
}
