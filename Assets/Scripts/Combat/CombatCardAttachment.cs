using UnityEngine;

public class CombatCardAttachment : MonoBehaviour
{
    public FightingBlockUI owner;          // В какой «стычке» лежит карта
    public CombatZoneType zone;            // В какой зоне (Attack/Defense)


    void OnTransformParentChanged()
    {
        // Если есть владелец и карта больше НЕ является ребёнком его зоны — сообщаем, что карта ушла
        if (!owner) return;

        var shouldBeParent = (zone == CombatZoneType.Attack) ? owner.zoneAttack : owner.zoneDefense;
        if (!shouldBeParent) { owner = null; return; }

        // Карта покинула контейнер зоны → уведомляем блок и стираем «ярлык»
        if (transform.parent != shouldBeParent)
        {
            var cv = GetComponent<CardView>();
            if (cv) owner.OnCardRemovedFromZone(cv, zone);   // пересчёт и сброс превью внутри блока
            owner = null;                                    // больше не числимся в зоне

            // Вернулись (скорее всего) в руку — дайте CardView переотразить «режим руки»
            if (cv) cv.RefreshLocationVisuals();
        }
    }
}
