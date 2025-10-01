using UnityEngine;                              // ScriptableObject
using System.Collections.Generic;               // List

// Триггеры способностей (пока используем ManualActivate для кнопки/клика)
public enum AbilityTrigger { ManualActivate, OnPlay, OnDraw, OnDiscard }

// Способность карты: триггер + список «стоимостей» + список «эффектов»
[CreateAssetMenu(menuName = "Robinson/AbilityDef", fileName = "AbilityDef")]
public class AbilityDef : ScriptableObject
{
    public string abilityId;                     // ID способности (для логики/поиска)
    public AbilityTrigger trigger = AbilityTrigger.ManualActivate; // Когда срабатывает
    public EffectDef[] costs;                    // Список «стоимостей» (сначала применяются)
    public EffectDef[] effects;                  // Список «эффектов» (потом применяются)
}
