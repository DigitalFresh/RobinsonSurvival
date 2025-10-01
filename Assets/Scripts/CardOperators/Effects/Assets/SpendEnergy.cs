using UnityEngine;
using System.Collections.Generic;

// 3) Потратить энергию N (стоимость). Если не хватает — вернуть false.
[CreateAssetMenu(menuName = "Robinson/Effects/SpendEnergy", fileName = "SpendEnergy")]
public class SpendEnergyEffectDef : EffectDef
{
    public int amount = 1;

    public override bool Execute(EffectContext ctx)
    {
        if (ctx == null || ctx.stats == null) return false;

        // Допускаем API вида: stats.Energy (int) и stats.SpendEnergy(int)
        if (ctx.stats.Energy < amount) return false;  // не хватает — стоимость провалилась
        ctx.stats.SpendEnergy(amount);                // списываем
        return true;
    }
}

