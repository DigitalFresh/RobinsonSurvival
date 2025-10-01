using UnityEngine;                      // Debug
using System.Linq;                      // FirstOrDefault

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

                bool ok = eff.Execute(ctx);
                if (!ok) { Debug.Log("[AbilityRunner] Effect failed."); }
            }
        }

        // 3) Уведомить UI, что стопки/рука могли поменяться
        ctx.hand?.RaisePilesChanged();
        //return true;

    }
    
}



//using UnityEngine;                              // Debug
//using System.Linq;                              // FirstOrDefault

//// Статический «исполнитель» способностей — применяет costs, затем effects
//public static class AbilityRunner
//{
//    // Выполнить первую способность с триггером ManualActivate
//    public static bool RunManualAbility(CardInstance inst) // Возвращает успех/провал
//    {
//        if (inst == null || inst.def == null || inst.def.abilities == null) return false; // Нет способностей — нечего делать

//        // Ищем первую подходящую способность
//        var ability = inst.def.abilities.FirstOrDefault(a => a != null && a.trigger == AbilityTrigger.ManualActivate);
//        if (ability == null) return false;       // Ничего не нашли — выходим

//        // Формируем контекст
//        var ctx = new EffectContext(inst);       // Контекст выполнения (hand, deck, stats и т.п.)

//        // Сначала — «стоимости»
//        if (ability.costs != null)               // Если есть список стоимостей
//        {
//            foreach (var cost in ability.costs)  // Перебираем их по порядку
//            {
//                if (cost == null) continue;      // Пропускаем пустые элементы
//                bool ok = cost.Execute(ctx);     // Пытаемся применить стоимость
//                if (!ok)                         // Если какая-то стоимость провалилась
//                {
//                    Debug.Log("[AbilityRunner] Cost failed, abort."); // Логируем
//                    return false;               // Прерываем выполнение способности
//                }
//            }
//        }

//        // Затем — эффекты
//        if (ability.effects != null)             // Если есть список эффектов
//        {
//            foreach (var eff in ability.effects) // Перебираем их по порядку
//            {
//                if (eff == null) continue;       // Пропускаем пустые элементы
//                bool ok = eff.Execute(ctx);      // Применяем эффект
//                if (!ok)                         // Если эффект не сработал
//                {
//                    Debug.Log("[AbilityRunner] Effect failed."); // Лог
//                    // По простой логике не откатываем; можно добавить rollback по желанию
//                }
//            }
//        }

//        return true;                              // Успех
//    }
//}
