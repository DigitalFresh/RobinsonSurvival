using UnityEngine;

//Стоимость: полностью удалить исходную карту из игры (не в сброс)
[CreateAssetMenu(menuName = "Robinson/Effects/Cost/Remove Card From Game", fileName = "Cost_RemoveCard")]
public class RemoveCardFromGameCostEffectDef : EffectDef
{
    public override bool Execute(EffectContext ctx)
    {
        if (ctx == null || ctx.source == null) return false;

        //// Удаляем инстанс из всех куч
        //var deck = ctx.deck ? ctx.deck : Object.FindFirstObjectByType<DeckController>(FindObjectsInactive.Include);
        //if (deck != null)
        //    deck.RemoveFromGame(ctx.source); // метод в DeckController — см. ниже

        // Уничтожаем CardView (если карта в руке)
        if (ctx.hand != null)
        {
            var views = ctx.hand.GetComponentsInChildren<CardView>(includeInactive: true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] && views[i].instance == ctx.source)
                {
                    Object.Destroy(views[i].gameObject);
                    break;
                }
            }
        }

        return true;
    }
}
