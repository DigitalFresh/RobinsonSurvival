using UnityEngine;
using System;
using System.Collections.Generic;
using static UnityEngine.InputSystem.LowLevel.InputStateHistory;

public enum CostType { Hands, Fists, Eye }              // основная стоимость
public enum AddTag { Brain, Power, Speed }             // доп.теги на картах
public enum StatType { Hunger, Thirst, Energy, Health }  // штрафуемые параметры

// Добавляет пункт меню Assets → Create → Robinson → Event для создания .asset
[CreateAssetMenu(fileName = "Robinson/Resources/EVENTS/SimpleEvent", menuName = "Event_Simple")]
public class EventSO : ScriptableObject // ScriptableObject — контейнер данных, не компонент на сцене
{
    [Header("Базовые данные")]                 // Заголовок блока в инспекторе
    public string eventId;                     // Уникальный ID события (для сохранений/ссылок)
    public string eventName;                   // Отображаемое имя события
    [TextArea] public string description;      // Описание (для всплывающих окон)
    public Sprite icon;                        // Иконка события (когда гекс открыт)
    public HexType hexType = HexType.Event;    // Тип гекса: чаще всего Event (по умолчанию)

    [Header("Классификация")]                  // Категория/флаги
    public bool isCombat;                      // Это бой?
    public bool isChoice;                      // Это событие-выбор?
    public bool isResource;                    // Это добыча ресурса?

    [Header("Main cost")]
    public CostType mainCostType = CostType.Hands;   // ✋ / 👊 / 👁
    public int mainCostAmount = 1;

    [Header("Additional costs (sum on cards in PlayArea)")]
    public List<AdditionalCost> additionalCosts = new(); // можно 0..3
    public bool additionalMandatory = false;             // если true — без них Confirm недоступен

    [Header("Penalties (apply on confirm)")]
    public List<Penalty> penalties = new();              // показываем в Req_back (0..4)

    [Header("Rewards (up to 4)")]
    public List<Reward> rewards = new();                 // res_1..res_4
    public enum RewardType { Resource, RestoreStat, NewCard, FreeReward }
    public enum PlayerStat { Hunger, Thirst, Energy, Health } // порядок не важен, но согласуем со спрайтами

    [Header("Alternative rewards (two-choice)")]
    public bool rewardsAreAlternative = false;   // если true — вместо обычных 4 слотов показываем 2 слота-выбора
    public List<Reward> alternativeRewards = new(); // ожидаем 0, 1 или 2 элемента; по умолчанию выбран первый

    // контейнер выбора в самом событии
    [Header("Choice (if isChoice = true)")]
    public List<ChoiceOption> choices = new();  // 2 или 3 варианта для события-выбора

    [Header("Combat event")]
    public bool isAggressiveCombat = false;                // Агрессивный бой (стартует, если игрок встал рядом)

    [Tooltip("Список противников (1..3) для этого боя")]
    public List<EnemySO> combatEnemies = new List<EnemySO>(); // Противники боя

    [Tooltip("Какого противника показывать на бейдже карты")]
    [Min(0)]
    public int previewEnemyIndex = 0;                      // Индекс врага для предпросмотра на гексе


    [Serializable]
    public class AdditionalCost
    {
        public AddTag tag;           // Brain/Power/Speed
        public int amount = 1;       // сколько нужно суммарно
    }

    [Serializable]
    public class Penalty
    {
        public StatType stat;
        public int amount = 1;
    }

    [Serializable]
    public class Reward
    {
        public RewardType type = RewardType.Resource;   // тип награды

        public Sprite icon;

        // --- для Resource ---
        public ResourceDef resource;                           // индекс ресурса (иконка берётся из массива в UI)
        public int amount = 1;                          // количество ресурса

        // --- для RestoreStat ---
        public PlayerStat stat;                         // какой параметр восстановить
        public int restoreAmount = 1;                   // сколько восстановить
                                                        // (для Energy показываем число; для остальных — выбираем спрайт 1..5)

        // --- для NewCard (новая карточка) ---
        public CardDef cardDef;                          // какую карту выдаём
        public int cardCount = 1;                        // сколько копий
        public bool knownPreview = true;                 // в UI: показывать «известную» иконку (или оставлять icon как «неизвестная»)

        // --- для FreeReward ---
        public FreeRewardDef freeReward;                // ScriptableObject «свободной награды» (описание + эффекты)

        // gating (как раньше)
        public bool gatedByAdditional = false;          // нужна ли доп. «стоимость»
        public string tooltip;       // подпись (подсказка)
        public AddTag requiredTag;   // какой именно тег нужен (если gated)
        public int requiredAmount = 1; // сколько нужно (если gated)
    }

    [Serializable]
    public class HiddenOutcome          // «иконки возможных последствий» (вместо явных наград)
    {
        public Sprite icon;            // спрайт подсказки (ресурсы, враг, информация, неизвестность)
        public string tooltip;         // всплывающая подсказка
    }

    [Serializable]
    public class ChoiceOption           // один вариант выбора в событии-выборе
    {
        [TextArea] public string description;                 // описание этого варианта (UI: Option/Description)
        public bool showRewards = true;                       // показывать реальные награды...
        public bool showHiddenOutcomes = false;               // ...или скрытые исходы (иконки)

        // Главная стоимость для ЭТОГО варианта (может отличаться от mainCostType/Amount в «простом» событии)
        public CostType mainCostType = CostType.Hands;        // ✋/👊/👁
        public int mainCostAmount = 1;

        // Доп. стоимость — ВСЕГДА обязательна для вариантов выбора по вашим правилам
        public List<AdditionalCost> additionalCosts = new();  // 0..3 Brain/Power/Speed (обязательные)

        // Потери параметров игрока (0..4)
        public List<Penalty> penalties = new();

        // Реальные награды этого варианта (0..N). Их мы выдадим при Confirm.
        public List<Reward> rewards = new();

        // Если «на экране» награды скрыты — вместо них показываем список из 1..4 иконок «возможных последствий»
        public List<HiddenOutcome> hiddenOutcomes = new();
    }


    [Header("Подсказка на закрытом гексе (по умолчанию)")]
    public HexHintType defaultHint = HexHintType.None; // Какую подсказку показывать, если тайл закрыт


    /// Вернуть врага для предпросмотра (безопасно по индексам)
    public EnemySO GetPreviewEnemy()                       // Хелпер для HexEventBadgeUI
    {
        // Если списка нет или он пуст — вернём null (бейдж сам подстрахуется)
        if (combatEnemies == null || combatEnemies.Count == 0) return null;
        // Кладём индекс в допустимые границы
        int idx = Mathf.Clamp(previewEnemyIndex, 0, combatEnemies.Count - 1);
        // Возвращаем выбранного врага
        return combatEnemies[idx];
    }

    /// Есть ли валидные враги у события боя?
    public bool HasCombatEnemies()                         // Удобный чекер
    {
        // Истина, если есть хотя бы один EnemySO
        return combatEnemies != null && combatEnemies.Count > 0;
    }
}