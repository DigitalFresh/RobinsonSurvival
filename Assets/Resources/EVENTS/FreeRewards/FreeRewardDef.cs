using UnityEngine;
using System.Collections.Generic;

// Свободная награда: произвольная картинка/текст + список эффектов (EffectDef),
// которые последовательно выполняются при подтверждении события.
[CreateAssetMenu(menuName = "Robinson/Rewards/FreeReward", fileName = "FreeReward")]
public class FreeRewardDef : ScriptableObject
{
    [Header("Optional intro modal (before any effects)")]
    [Tooltip("Показать модалку ПЕРЕД выполнением эффектов этой Free Reward (боем и т.п.).")]
    public bool showModalBeforeEffects = false;

    [Tooltip("Ключ контента в ModalContentProvider для intro-модалки.")]
    public string modalCatalogKey;

    [Header("UI")]
    public string resourceId;            // Уникальный ID (для сохранений/поиска)
    public string title;                 // Заголовок в модалке
    [TextArea] public string description; // Описание награды
    public Sprite icon;                  // Большая иконка награды

    [Header("Effects (executed in order)")]
    public List<EffectDef> effects = new(); // Эффекты, исполняются сверху вниз
}
