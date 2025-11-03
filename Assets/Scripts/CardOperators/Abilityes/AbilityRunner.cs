using UnityEngine;                      // Debug
using System.Linq;                      // FirstOrDefault
using System.Collections.Generic; // наверху файла, если нет

public static class AbilityRunner
{
    // Выполнить первую способность с триггером ManualActivate у данного инстанса карты
    public static bool RunManualAbility(CardInstance inst)
    {
        if (inst == null || inst.def == null || inst.def.abilities == null) return false;

        var ability = inst.def.abilities.FirstOrDefault(a => a != null && a.trigger == AbilityTrigger.ManualActivate);
        if (ability == null) return false;

        var ctx = new EffectContext(inst, ability);   // твой существующий контекст (hand, deck, stats)
        RunAbility(ctx, costIndex: 0, effectIndex: 0);  // старт пайплайна
        return true; // запустили (в т.ч. если будет ожидание подтверждения)
    } 

    // Внутренний «двигатель» пайплайна, к которому мы вернёмся после подтверждения
    public static void RunAbility(EffectContext ctx, int costIndex, int effectIndex)
    {
        var ability = ctx.ability;
        if (ability == null) return;

        // 1) Стоимости
        if (ability.costs != null)
        {
            for (int i = costIndex; i < ability.costs.Length; i++)
            {
                var cost = ability.costs[i];
                if (cost == null) continue;

                // Особый случай: ConfirmCost — показывает модалку и продолжает пайплайн по «Да»
                if (cost is ConfirmCostEffectDef confirm)
                {
                    confirm.ShowAndContinue(ctx, () =>
                    {
                        // по подтверждению — продолжаем со следующего cost
                        RunAbility(ctx, i + 1, effectIndex);
                    });
                    return; // прерываемся до клика игрока
                }

                if (cost is ConfirmConsumeCostEffectDef consumeConfirm)
                {
                    consumeConfirm.ShowAndContinue(ctx, () =>
                    {
                        // По подтверждению продолжаем со следующего cost
                        RunAbility(ctx, i + 1, effectIndex);
                    });
                    return; // выходим до ответа игрока
                }

                // Обычная стоимость
                bool ok = cost.Execute(ctx);
                if (!ok) { Debug.Log("[AbilityRunner] Cost failed, abort."); return; }
            }

        }


        // EFFECTS
        if (ability.effects != null)
        {
            for (int j = effectIndex; j < ability.effects.Length; j++)
            {
                var eff = ability.effects[j];
                if (eff == null) continue;

                if (eff is DrawCardsEffectDef drawEff && drawEff.animate && RewardPickupAnimator.Instance != null)
                {
                    // Запускаем анимированный добор, а продолжение пайплайна делаем когда анимация завершится
                    drawEff.RunAnimated(ctx, () =>                                                     // Просим эффект сам завершить добор
                    {
                        RunAbility(ctx, costIndex, j + 1);                                            // Продолжаем со следующего эффекта
                    });
                    return;                                                                            // Выходим из метода — ждём анимацию
                }

                if (eff is RestoreStatEffectDef)
                {
                    // 1) Включаем режим сборки
                    ctx.collectRestoreFx = true;
                    ctx.restoreFxBuffer = new List<(EventSO.PlayerStat, int)>();

                    // 2) Применяем подряд ИМЕННО RestoreStatEffectDef, чтобы они только буферизовались
                    int k = j;
                    for (; k < ability.effects.Length; k++)
                    {
                        var r = ability.effects[k] as RestoreStatEffectDef;
                        if (r == null) break;              // упёрлись в следующий «не-restore» — хватит
                        r.Execute(ctx);                    // изменяет статы и складывает (stat, amount) в ctx.restoreFxBuffer
                    }

                    // 3) Выключаем сборку
                    ctx.collectRestoreFx = false;

                    // 4) Если что-то накопили — запускаем последовательную анимацию из центра и продол­жаем пайплайн после onDone
                    if (ctx.restoreFxBuffer != null && ctx.restoreFxBuffer.Count > 0 && RewardPickupAnimator.Instance != null)
                    {
                        RewardPickupAnimator.Instance.PlayStatRestoreFromCenter(ctx.restoreFxBuffer, () =>
                        {
                            RunAbility(ctx, costIndex, k); // продолжаем со следующего эффекта после пачки
                        });
                        return; // ждём анимацию
                    }

                    // 5) Если нечего анимировать — просто перескочим эти эффекты
                    j = k - 1;   // т.к. for увеличит j ещё раз
                    continue;
                }

                bool ok = eff.Execute(ctx);
                if (!ok) { Debug.Log("[AbilityRunner] Effect failed."); }
            }
        }

        // 3) Уведомить UI, что стопки/рука могли поменяться
        ctx.hand?.RaisePilesChanged();
        //return true;

    }
    
}

