using UnityEngine;                               // MonoBehaviour, RectTransform
using UnityEngine.EventSystems;                  // IDropHandler
using UnityEngine.UI;                            // LayoutRebuilder

// Зона приёма карт на панели руки. Позволяет перетащить карту обратно из PlayArea в руку.
public class HandPanelDropZone : MonoBehaviour, IDropHandler
{
    public RectTransform container;              // Куда класть карты в руке (если null — используем свой Transform)

    private void Awake()
    {
        if (container == null) container = transform as RectTransform; // По умолчанию — сам объект HandPanel
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;
        var card = eventData.pointerDrag.GetComponent<CardView>();
        if (card == null) return;

        // Если карта лежала в какой-то зоне события — удалим её оттуда
        if (card.ownerZone != null)
        {
            var zone = card.ownerZone;
            zone.placedCards.Remove(card);
            if (card.data != null) zone.currentHands -= card.data.hands;
            if (zone.currentHands < 0) zone.currentHands = 0;
            zone.UpdateRequirementText();
            card.ownerZone = null;                              // Больше не принадлежит зоне
            //EventWindowUI.Instance?.UpdateConfirmInteractable();// Кнопка «Разыграть» может измениться
            //ChooseEventWindowUI.Instance?.UpdateConfirmInteractable(); // Обновляем доступность кнопки «Разыграть»
        }

        var combatController = FindFirstObjectByType<CombatController>(FindObjectsInactive.Include);
        if (combatController)
        {   // Если карта уже была в другой зоне — сообщим предыдущему владельцу
            var attach = card.GetComponent<CombatCardAttachment>();        // Наш «ярлык» на карте
            if (attach && attach.owner)                                    // Если карта числится в другой зоне
                attach.owner.OnCardRemovedFromZone(card, attach.zone);     // Сообщим блоку, что карта ушла
            combatController.RefreshCombatUIAfterHandChanged();
        }
        // Перекладываем карту в руку
        card.transform.SetParent(container, worldPositionStays: false);
       // card.SetToHandSize();                                   // Восстановить визуальную высоту 347 px

        // Перестроим лейаут руки немедленно
        if (container != null) LayoutRebuilder.ForceRebuildLayoutImmediate(container);
    }
}