using UnityEngine;
using System.Collections.Generic;

// —вободна€ награда: произвольна€ картинка/текст + список эффектов (EffectDef),
// которые последовательно выполн€ютс€ при подтверждении событи€.
[CreateAssetMenu(menuName = "Robinson/Rewards/FreeReward", fileName = "FreeReward")]
public class FreeRewardDef : ScriptableObject
{
    [Header("UI")]
    public string resourceId;            // ”никальный ID (дл€ сохранений/поиска)
    public string title;                 // «аголовок в модалке
    [TextArea] public string description; // ќписание награды
    public Sprite icon;                  // Ѕольша€ иконка награды

    [Header("Effects (executed in order)")]
    public List<EffectDef> effects = new(); // Ёффекты, исполн€ютс€ сверху вниз
}
