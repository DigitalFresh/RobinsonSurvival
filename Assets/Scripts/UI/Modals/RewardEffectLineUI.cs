using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Линия списка эффектов (иконка + текст)
public class RewardEffectLineUI : MonoBehaviour
{
    public Image icon;                 // Иконка эффекта (может быть пустой)
    public TextMeshProUGUI text;       // Описание эффекта

    // Привязка UI-строки к произвольному EffectDef с метаданными (если поддерживает)
    public void Bind(EffectDef eff)
    {
        if (!text || !icon) return;

        // Попробуем вытащить «UI-мета» через известные нам поля (например, RewardTakeDamageEffectDef)
        Sprite s = null;
        string t = eff != null ? eff.name : "";

        // Если это RewardTakeDamageEffectDef — возьмём его красивые поля
        if (eff is RewardTakeDamageEffectDef dmg)
        {
            s = dmg.uiIcon;
            if (!string.IsNullOrEmpty(dmg.uiDescription)) t = dmg.uiDescription;
        }

        icon.enabled = (s != null);
        icon.sprite = s;
        text.text = t;
    }
}