using System.Collections.Generic;                // List<>
using TMPro;                                     // TextMeshProUGUI
using UnityEngine;                               // MonoBehaviour, RectTransform
using UnityEngine.EventSystems;                  // IDropHandler, IPointerEnterHandler/ExitHandler
using UnityEngine.UI;                            // Image, LayoutRebuilder

using CColor = CardDef.CardColor;                // Удобный алиас на цвет карты

// Зона в окне события, куда перетаскивают карты для выполнения требований
public class EventWindowDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform container;              // Контейнер для визуальной укладки карт (может быть сам RectTransform)
    public TextMeshProUGUI requirementText;      // Текст требований "Нужно ✋ X (сейчас Y)"
    public Image highlight;                      // Подсветка зоны при наведении (опционально)

    [Header("Runtime")]
    public List<CardView> placedCards = new();   // Какие карты сейчас лежат в зоне
    public int requiredHands;                    // Сколько «ладошек» нужно по текущему событию
    public int currentHands;                     // Сколько уже положили суммарно
    public int currentFists;
    public int currentEye;
    public int currentBrain;
    public int currentPower;
    public int currentSpeed;

    public CostType requirementType; // Требуемый тип (по умолчанию — ладошки для совместимости)
    public int requiredAmount = 0;               // Сколько штук нужно (универсально для любого типа)

    private void Awake()                         // Инициализация
    {
        if (container == null)                   // Если контейнер не указан —
            container = transform as RectTransform; // используем собственный RectTransform
        UpdateRequirementText();                 // Сразу обновим надпись
        if (highlight != null) highlight.enabled = false; // Подсветку по умолчанию выключим
    }

    public System.Action OnZoneChanged;  // событие для UI

    public void SetupRequirement(int handsNeed)  // Задать требование (вызывается из EventWindowUI.Show)
    {
        requiredHands = handsNeed;               // Сохраняем требуемые ладошки
        requirementType = CostType.Hands;        // вно помечаем тип как «ладошки»
        requiredAmount = handsNeed;              // дублируем в универсальное поле
        ClearZone();                             // Очищаем зону на всякий случай
        UpdateRequirementText();                 // Обновляем текст
    }
    //: новое типизированное API для событий с «кулачками»/«глазами» ---
    public void SetupRequirementTyped(CostType type, int amount) // Установить тип и количество
    {
        requirementType = type;                 // Запоминаем требуемый тип (Hands/Fists/Eye)
        requiredAmount = Mathf.Max(0, amount); // Нормализуем количество (не отрицательное)
        if (type == CostType.Hands)             // Для совместимости: зеркалим старое поле
            requiredHands = requiredAmount;     // Если это ладошки — оставляем и старое представление
        ClearZone();                            // Полностью очищаем зону
        UpdateRequirementText();                // Обновляем UI-текст
    }
    public void OnDrop(PointerEventData eventData) // Вызывается, когда бросили перетаскиваемый объект в зону
    {
        var cardView = eventData.pointerDrag ? eventData.pointerDrag.GetComponent<CardView>() : null;
        if (cardView == null) return;

        if (!IsDropAllowedForCurrentRequirement(cardView)) // Проверяем, подходит ли карта под требование
        {
            return;                         // Ничего не меняем: OnEndDrag вернёт карту в руку сам
        }

        cardView.transform.SetParent(container, worldPositionStays: false);
        cardView.ownerZone = this;                  // помечаем владельца-зону
        cardView.SetToEventCroppedMode();    // урезаем карту и арт (обрезка снизу)
        cardView.RefreshLocationVisuals();         // сразу пересчитать смещения подложки

        placedCards.Add(cardView);                 // Регистрируем карту в списке

        RecalcTotals();                            // Пересчитываем суммы по параметрам
        EventWindowUI.Get()?.UpdateConfirmInteractable(); // Обновляем доступность кнопки «Разыграть»
        OnZoneChanged?.Invoke();                   // Уведомляем подписчиков
    }

    public void RemoveCard(CardView cv)
    {
        if (cv == null) return;
        if (placedCards.Remove(cv))
        {
            RecalcTotals();
            //currentHands -= cv.data != null ? cv.data.hands : 0;  // скорректировать сумму
            //if (currentHands < 0) currentHands = 0;
            UpdateRequirementText();                              // обновить «Нужно ✋ ...»
            EventWindowUI.Instance?.UpdateConfirmInteractable();  // пересчитать кнопку «Разыграть»
        }
    }

    public void RecalcTotals()
    {
        currentHands = currentFists = currentEye = 0;
        currentBrain = currentPower = currentSpeed = 0;

        foreach (var cardView in placedCards)
        {
            var d = cardView.data; if (d == null) continue;
            currentHands += Mathf.Max(0, d.hands);
            currentFists += Mathf.Max(0, d.fists);
            currentEye += Mathf.Max(0, d.eye);
            currentBrain += Mathf.Max(0, d.brain);
            currentPower += Mathf.Max(0, d.power);
            currentSpeed += Mathf.Max(0, d.speed);
        }
        UpdateRequirementText();
    }



    public void ClearZone()                       // Полностью очистить зону (UI-возврат делает EventWindowUI)
    {
        placedCards.Clear();                      // Очищаем список
        currentHands = currentFists = currentEye = 0;
        currentBrain = currentPower = currentSpeed = 0;
        UpdateRequirementText();                  // Обновляем текст
        OnZoneChanged?.Invoke();
        // Визуальную очистку (перенос карт) выполняет вызывающая сторона
    }

    public void UpdateRequirementText()          // Обновить надпись требований
    {
        if (requirementText == null) return;     // Если текста нет — выходим

        // --- ADDED START: выбираем иконку/счётчик по типу требования ---
        string icon;                             // Строчка с «иконкой» в тексте
        int need;                                // Сколько нужно по требованию
        int now;                                 // Сколько есть сейчас в зоне

        switch (requirementType)                 // Смотрим тип
        {
            case CostType.Fists:                 // Если требуются «кулачки»
                icon = "👊";                     // Иконка «кулачок»
                need = requiredAmount;           // Нужно — из универсального поля
                now = currentFists;             // Сейчас — сумма «fists»
                break;
            case CostType.Eye:                   // Если требуются «глаза»
                icon = "👁";                     // Иконка «глаз»
                need = requiredAmount;
                now = currentEye;
                break;
            case CostType.Hands:                 // Если «ладошки» (по-старому)
            default:
                icon = "✋";                      // Иконка «ладонь»
                need = requiredHands;            // Берём старое поле (синхронизировано в SetupRequirement*)
                now = currentHands;             // Сумма «hands»
                break;
        }

        requirementText.text = $"Нужно {icon} {need} (сейчас {now})"; // Формируем надпись
    }

    public void OnPointerEnter(PointerEventData eventData) // Навели мышь на зону
    {
        if (highlight != null) highlight.enabled = true;    // Включаем подсветку
    }

    public void OnPointerExit(PointerEventData eventData)  // Убрали мышь с зоны
    {
        if (highlight != null) highlight.enabled = false;   // Выключаем подсветку
    }
    // правило — можно ли кидать карту в зону при текущем требовании ---
    private bool IsDropAllowedForCurrentRequirement(CardView cv) // Возвращает, допускает ли зона этот дроп
    {
        if (cv == null || cv.data == null) return false; // Без данных — запрещаем
        var color = cv.data.color;                       // Цвет карты

        switch (requirementType)                         // Смотрим требование
        {
            case CostType.Fists:                         // Требуются «кулачки»
                return (color == CColor.Red);            // Разрешаем ТОЛЬКО красные
            case CostType.Eye:                           // Требуются «глаза»
                return (color == CColor.Blue);           // Разрешаем ТОЛЬКО синие
            case CostType.Hands:                         // «Ладошки»
            default:
                return true;                             // Любые — как раньше
        }
    }
    // сообщает CardView, нужно ли сдвигать чёрную подложку в этой зоне
    public bool ShouldShiftBlackBaseFor(CardView cv)     // true, если событие требует Fists/Eye и карта подходящего цвета
    {
        if (cv == null || cv.data == null) return false; // Защита
        var color = cv.data.color;                       // Цвет карты

        if (requirementType == CostType.Fists)           // Если требуются «кулачки»
            return (color == CColor.Red);                // Сдвигаем подложку для красных карт
        if (requirementType == CostType.Eye)             // Если требуются «глаза»
            return (color == CColor.Blue);               // Сдвигаем подложку для синих карт
        return false;                                    // Для «ладошек» не сдвигаем
    }


}
