using UnityEngine;                              // Для FindFirstObjectByType
using System.Collections.Generic;               // Для словаря параметров

// Контекст, доступный любому эффекту при выполнении.
public class EffectContext
{
    public CardInstance source;                  // Какая карта инициирует способность
    public HandController hand;                  // Контроллер руки (добавление/сброс карт)
    public DeckController deck;                  // Контроллер колоды (добор/перетасовка)
    public PlayerStatsSimple stats;              // Параметры игрока (энергия и т.д.)

    public Dictionary<string, int> intParams;    // Доп.параметры (например, "DrawAmount" и т.п.)

    public AbilityDef ability;

    // Когда true — RestoreStatEffectDef НЕ запускает анимацию сам,
    // а кладёт (stat, amount) сюда, чтобы раннер сыграл их одной пачкой.
    public bool collectRestoreFx;

    // Буфер для анимаций восстановления (сыграет раннер)
    public List<(EventSO.PlayerStat stat, int amount)> restoreFxBuffer;

    public EffectContext(CardInstance src, AbilityDef ability)       // Конструктор по источнику
    {
        source = src;                            // Сохраняем источник
        this.ability = ability;
        hand = HandController.Instance;          // Берём синглтон руки (как в прототипе)
        deck = Object.FindFirstObjectByType<DeckController>(); // Находим колоду в сцене
        stats = Object.FindFirstObjectByType<PlayerStatsSimple>(); // Находим игрока
        intParams = new Dictionary<string, int>();             // Инициализируем словарь
    }

    // === УДОБНЫЕ ПЕРЕГРУЗКИ ===

    // 1) Только источник (без явной Ability) — удобно для карт
    public EffectContext(CardInstance src)
        : this(src, null) { }   // пробрасываем в основной конструктор

    // 2) «Системный» контекст без источника и без способности — идеально для FreeReward
    public EffectContext()
        : this(null, null) { }  // пробрасываем в основной конструктор
}
