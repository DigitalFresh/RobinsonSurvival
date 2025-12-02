using UnityEngine;                      // ScriptableObject
using System.Collections.Generic;       // List<T>

// Базовый эффект
public abstract class EffectDef : ScriptableObject
{

    // === UI (опционально для модалки свободной награды) ===
    [Header("UI (optional for reward modal)")]
    [Tooltip("Если задано — модалка свободной награды покажет эту иконку рядом с эффектом.")]
    public Sprite uiIcon;               // можно оставить пустым
    [TextArea]
    [Tooltip("Если задано — модалка свободной награды покажет этот текст. Иначе возьмётся имя ассета.")]
    public string uiDescription;        // можно оставить пустым


    // Вернуть true, если эффект выполнен успешно
    public abstract bool Execute(EffectContext ctx);
}

