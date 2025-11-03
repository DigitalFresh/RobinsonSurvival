using UnityEngine;
using System.Collections.Generic; // ← нужен для List<...>

/**
 * Эффект: восстановить указанный параметр игрока на amount
 * + запуск анимации “полёта” иконки для Hunger/Thirst из центра экрана.
 */
[CreateAssetMenu(menuName = "Robinson/Effects/Restore Stat", fileName = "Eff_RestoreStat")]
public class RestoreStatEffectDef : EffectDef
{
    public enum StatKind { Health, Energy, Thirst, Hunger }

    [Header("Restore")]
    public StatKind stat;        // какой параметр восстанавливаем
    [Min(0)] public int amount;  // на сколько

    public override bool Execute(EffectContext ctx)
    {
        if (ctx == null) return false;                                                     // нет контекста — выходим
        var stats = ctx.stats ?? Object.FindFirstObjectByType<PlayerStatsSimple>(          // ищем PlayerStatsSimple в сцене,
                            FindObjectsInactive.Include);                                   // если не пришёл в контексте
        if (!stats || amount <= 0) return false;                                            // ничего восстанавливать — выходим

        // 1) Применяем восстановление к параметрам игрока
        switch (stat)
        {
            case StatKind.Health: stats.Heal(amount); break;      // здоровье +amount
            case StatKind.Energy: stats.GainEnergy(amount); break;  // энергия +amount
            case StatKind.Thirst: stats.Drink(amount); break;     // жажда +amount
            case StatKind.Hunger: stats.Eat(amount); break;     // голод +amount
        }

        // 2) Запускаем анимацию полёта иконки — ТОЛЬКО для воды и еды (по ТЗ)
        if (stat == StatKind.Thirst || stat == StatKind.Hunger)
        {
            var tuple = (MapToEventPlayerStat(stat), amount);

            // Если AbilityRunner собирает батч — только буферизуем
            if (ctx.collectRestoreFx)
            {
                if (ctx.restoreFxBuffer == null)
                    ctx.restoreFxBuffer = new System.Collections.Generic.List<(EventSO.PlayerStat, int)>();
                ctx.restoreFxBuffer.Add(tuple);
            }
            else
            {
                // Обычный одиночный случай — играем немедленно (как раньше)
                var anim = RewardPickupAnimator.Instance
                          ?? UnityEngine.Object.FindFirstObjectByType<RewardPickupAnimator>(FindObjectsInactive.Include);
                if (anim != null)
                {
                    var one = new System.Collections.Generic.List<(EventSO.PlayerStat, int)>(1) { tuple };
                    anim.PlayStatRestoreFromCenter(one, onDone: null);
                }
            }
        }

        return true; // эффект успешно выполнен
    }

    // Локальный маппер: StatKind (эффекта) → EventSO.PlayerStat (для анимации)
    private static EventSO.PlayerStat MapToEventPlayerStat(StatKind k)
    {
        switch (k)
        {
            case StatKind.Health: return EventSO.PlayerStat.Health;
            case StatKind.Energy: return EventSO.PlayerStat.Energy;
            case StatKind.Thirst: return EventSO.PlayerStat.Thirst;
            case StatKind.Hunger: return EventSO.PlayerStat.Hunger;
            default: return EventSO.PlayerStat.Hunger;
        }
    }
}
