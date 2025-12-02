using UnityEngine;                       // MonoBehaviour, Mathf
using UnityEngine.UI;                    // Image (дл€ полосок)
using TMPro;                             // TextMeshProUGUI

// HUD параметров игрока (иконка + число + полоска)
public class StatsHUD : MonoBehaviour
{
    // ¬ложенна€ структура Ч одна строка HUD (иконка, число, полоска)
    [System.Serializable]                // чтобы было видно в инспекторе
    public class StatRow
    {
        public Image icon;               // картинка слева (сердце/молни€/капл€/€блоко)
        public TextMeshProUGUI value;    // число р€дом
        public Image barFill;            // полоска (Image Type = Filled, Horizontal, Origin Left)
    }

    [Header("Rows")]
    public StatRow healthRow;            // строка Ђжизньї
    public StatRow energyRow;            // строка Ђэнерги€ї
    public StatRow thirstRow;            // строка Ђжаждаї
    public StatRow hungerRow;            // строка Ђсытостьї

    [Header("Source")]
    public PlayerStatsSimple stats;      // откуда брать значени€

    private void Awake()                 // инициализаци€ ссылок
    {
        if (stats == null)               // если не назначили в инспекторе
            stats = FindFirstObjectByType<PlayerStatsSimple>(); // ищем на сцене
        RefreshAll();                    // сразу отрисуем стартовые значени€
    }

    private void OnEnable()              // подписка на событи€
    {
        if (stats != null)               // если есть источник
            stats.OnStatsChanged += RefreshAll; // подписываемс€ на обновлени€
    }

    private void OnDisable()             // отписка
    {
        if (stats != null)               // если источник задан
            stats.OnStatsChanged -= RefreshAll; // снимаем подписку
    }

    private void RefreshAll()            // обновить все 4 строки HUD
    {
        if (stats == null) return;       // защита

        // ∆»«Ќ№
        if (healthRow != null && healthRow.value != null && healthRow.barFill != null)
        {
            healthRow.value.text = stats.Health.ToString();                          // число
            healthRow.barFill.fillAmount = Safe01((float)stats.Health / stats.maxHealth); // дол€ полоски
        }

        // ЁЌ≈–√»я
        if (energyRow != null && energyRow.value != null && energyRow.barFill != null)
        {
            energyRow.value.text = stats.Energy.ToString();                          // число
            energyRow.barFill.fillAmount = Safe01((float)stats.Energy / stats.maxEnergy); // дол€ полоски
        }

        // ∆ј∆ƒј (уровень воды)
        if (thirstRow != null && thirstRow.value != null && thirstRow.barFill != null)
        {
            thirstRow.value.text = stats.Thirst.ToString();                          // число
            thirstRow.barFill.fillAmount = Safe01((float)stats.Thirst / stats.maxThirst); // дол€ полоски
        }

        // —џ“ќ—“№
        if (hungerRow != null && hungerRow.value != null && hungerRow.barFill != null)
        {
            hungerRow.value.text = stats.Hunger.ToString();                          // число
            hungerRow.barFill.fillAmount = Safe01((float)stats.Hunger / stats.maxHunger); // дол€ полоски
        }
    }

    private float Safe01(float v)        // помощник: безопасно зажать в [0..1]
    {
        return Mathf.Clamp01(v);         // ограничиваем
    }
}
