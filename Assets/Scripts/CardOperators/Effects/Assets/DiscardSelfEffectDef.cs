using UnityEngine;
using System.Collections.Generic;

// 1) Сбросить саму карту (именно этот инстанс)
[CreateAssetMenu(menuName = "Robinson/Effects/DiscardSelfEffectDef", fileName = "DiscardSelfEffectDef")]
public class DiscardSelfEffectDef : EffectDef
{
    public override bool Execute(EffectContext ctx)
    {
        if (ctx == null || ctx.source == null || ctx.hand == null) return false;
        // Требуется метод в HandController: bool DiscardByInstance(CardInstance inst)
        return ctx.hand.DiscardByInstance(ctx.source);
    }
}