using UnityEngine;                                      // MonoBehaviour, Transform, Vector3, Color
using UnityEngine.EventSystems;                         // IDropHandler, IPointerEnterHandler, IPointerExitHandler
using UnityEngine.UI;                                   // Graphic, Image, LayoutRebuilder

// Тип зоны (куда кладём карту) — уже используется в проекте
public enum CombatZoneType { Attack, Defense }          // Две зоны

// ЗОНА приёма карт: ставим на Attack_cards / Defense_cards
public class CombatDropZone : MonoBehaviour, IDropHandler
{
    [Header("Links")]
    public FightingBlockUI block;                        // Владелец-«стычка» (Fighting_blockUI)
    public CombatZoneType zoneType;                      // Какой тип зоны это (Attack / Defense)

    [Header("View")]
    [Range(0.3f, 1f)]
    public float zoneScale = 0.8f;                       // Масштаб карт внутри зоны

    [Header("Debug")]
    public bool autoMakeRaycastTarget = true;            // Автоматически сделать зону «ловимой» для UI-луча

    private void Awake()                                 // На старте зоны
    {
        if (!autoMakeRaycastTarget) return;              // Если авто-настройка выключена — выходим

        var g = GetComponent<Graphic>();                 // Берём любой Graphic на этом GO
        if (!g)                                          // Если графики нет (RectTransform пустой)
        {
            var img = gameObject.AddComponent<Image>();  // Добавляем прозрачный Image
            img.color = new Color(0, 0, 0, 0.001f);      // Почти невидимый (alpha > 0)
            img.raycastTarget = true;                    // Ловим UI-луч
            var rt = transform as RectTransform;         // Тянем на весь родитель
            if (rt)
            {
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }
        else
        {
            g.raycastTarget = true;                      // Если Graphic есть — убедимся, что он ловит луч
        }
    }

    public void OnDrop(PointerEventData eventData)       // Когда карту бросили в зону
    {
        if (eventData == null || !eventData.pointerDrag) return;       // Нет данных/объекта — выходим

        var card = eventData.pointerDrag.GetComponent<CardView>();     // Берём CardView
        if (!card) return;                                             // Не карта — игнор
        // Если это зона Attack И карта зелёная — дроп запрещён
        if ((zoneType == CombatZoneType.Attack && card.data != null &&
            card.data.color == CardDef.CardColor.Green) || (card.data != null &&
            card.data.color == CardDef.CardColor.Blue))             // Проверяем цвет карты
        {
            // ВАЖНО: ничего не делаем с парентом — оставляем карту под dragCanvas.
            // CardView.OnEndDrag сам вернёт её в originalParent (рука).
            return;                                                 // Выходим — дропа в зону не будет
        }
        // Если карта уже была в другой зоне — сообщим предыдущему владельцу
        var attach = card.GetComponent<CombatCardAttachment>();        // Наш «ярлык» на карте
        if (attach && attach.owner)                                    // Если карта числится в другой зоне
            attach.owner.OnCardRemovedFromZone(card, attach.zone);     // Сообщим блоку, что карта ушла

        // Обновим/создадим «ярлык» на карте: чей блок и какая зона
        if (!attach) attach = card.gameObject.AddComponent<CombatCardAttachment>(); // Добавим, если не было
        attach.owner = block;                                          // Теперь владелец — этот FightingBlockUI
        attach.zone = zoneType;                                       // Зона — текущая

        // Пересаживаем карту в контейнер и уменьшаем
        card.transform.SetParent(transform, false);                    // Родитель = эта зона
        card.rect.localScale = Vector3.one * zoneScale;                // Масштаб 0.7

        // Перестроим Layout немедленно (если используется)
        var rt = transform as RectTransform;                           // RectTransform зоны
        if (rt) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);       // Пересчёт лейаута

        // Обновим числа Fist/Shield/Wounds в блоке
        if (block) block.RecountSums();                                // Пересчитать суммы и UI

        // Обновим доступность кнопок «добора» по лимиту 7
       // var cc = FindFirstObjectByType<CombatController>(FindObjectsInactive.Include);
       // if (cc) cc.RefreshDrawButtons();                               // Актуализировать +2/+3
    }
}
