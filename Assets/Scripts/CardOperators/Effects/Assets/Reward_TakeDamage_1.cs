using UnityEngine;

// Эффект: нанести урон игроку (минус здоровье).
// Подходит для использования внутри FreeRewardDef.effects.
[CreateAssetMenu(menuName = "Robinson/Effects/Reward/TakeDamage", fileName = "Reward_TakeDamage")]
public class RewardTakeDamageEffectDef : EffectDef
{
    [Header("Damage")]
    public int amount = 1;                       // Сколько снять здоровья

    public override bool Execute(EffectContext ctx)
    {
        if (ctx == null || ctx.stats == null) return false;   // Страховка
        ctx.stats.TakeDamage(Mathf.Max(1, amount));           // Наносим урон
        return true;
    }
}
