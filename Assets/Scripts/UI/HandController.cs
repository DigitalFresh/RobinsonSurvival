using System.Collections.Generic;                 // List<T>
using UnityEngine;                                // MonoBehaviour, GameObject, Transform, Instantiate
using UnityEngine.UI;                             // Для возможных UI-ссылок

// Контроллер «руки»: спавнит CardView под handPanel, считает размер руки, сбрасывает в DeckController.
public class HandController : MonoBehaviour
{
    public static HandController Instance;        // Синглтон для удобства доступа (как раньше)

    [Header("Refs")]
    public Transform handPanel;                   // Контейнер UI для карт в руке (Horizontal/Content Size Fitter)
    public Transform discardUIContainer;          // Временный контейнер для немедленного уменьшения childCount при сбросе
    public CardView cardPrefab;                   // Префаб визуальной карты (смешан с Image/TMP)

    public DeckController deck;                   // Ссылка на DeckController (для добора/сброса)
    public PlayerStatsSimple stats;               // Ссылка на статы игрока (для энергии и т.п., если нужно)

    [Header("Rules")]
    public int maxHand = 7;                       // Максимум карт в руке
    public int initialHand = 5;                   // Сколько добирать при старте

    // Событие «кучи изменились» (локальное, для подписчиков на руку)
    public event System.Action OnPilesChanged;    // Например, CardView обновляет кнопки «Глаз»

    // Текущее количество карт в руке (по дочерним объектам в handPanel)
    public int HandCount => handPanel != null ? handPanel.childCount : 0; // Считаем UI-объекты

    private void Awake()                          // Инициализация
    {
        Instance = this;                          // Сохраняем синглтон
        if (deck == null) deck = FindFirstObjectByType<DeckController>(); // Ищем колоду в сцене
        if (stats == null) stats = FindFirstObjectByType<PlayerStatsSimple>(); // Ищем статы
    }

    private void Start()                          // Стартовая раздача
    {
        if (deck != null)                         // Если колода найдена
        {
            var cards = deck.DrawMany(initialHand); // Берём initialHand карт
            foreach (var inst in cards)           // Перебираем инстансы
                AddCardToHand(inst);              // Спавним в руку
                //Debug.Log("rect.sizeDelta: " + inst);
        }

        RaisePilesChanged();                      // Сообщаем подписчикам
        if (deck != null) deck.OnPilesChanged += RaisePilesChanged; // Слушаем изменения стопок колоды
    }

    private void OnDestroy()                      // Отписка
    {
        if (deck != null) deck.OnPilesChanged -= RaisePilesChanged; // Снимаем подписку
    }

    // Добавить одну карту (CardInstance) в руку (UI)
    public void AddCardToHand(CardInstance inst)
    {
        if (inst == null || cardPrefab == null || handPanel == null) return; // Защита
        if (HandCount >= maxHand) return;            // Если рука полная — не кладём

        var cv = Instantiate(cardPrefab, handPanel); // Создаём UI-карту под руки
        cv.Bind(inst);                                // Привязываем данные (CardInstance → CardDef отобразится)
        //cv.SetToHandSize();       // вернуть полную высоту и полную маску арта
        RaisePilesChanged();                          // Сообщаем, что рука изменилась
    }

    public bool DiscardByInstance(CardInstance inst)
    {
        if (inst == null) return false;

        CardView target = null;

        // Сначала ищем под панелью руки (даже если объект временно неактивен)
        if (handPanel != null)
        {
            var views = handPanel.GetComponentsInChildren<CardView>(includeInactive: true);
            foreach (var v in views)
            {
                if (v != null && v.instance == inst)
                {
                    target = v;
                    break;
                }
            }
        }

        // Если нашли — сбрасываем через существующий метод
        if (target != null)
        {
            DiscardCard(target);   // уменьшит HandCount, перенесёт в discardPile и уничтожит UI
            return true;
        }

        return false; // не нашли соответствующий UI-объект
    }

    // Сбросить одну карту из руки (по CardView)
    public void DiscardCard(CardView cv)
    {
        if (cv == null || deck == null) return;       // Защита
        var inst = cv.instance;                        // Берём рантайм-экземпляр
        deck.Discard(inst);                            // Кладём его в discard колоды

        // Немедленно уменьшаем HandCount: переносим под временный контейнер (или под this.transform)
        if (discardUIContainer != null) cv.transform.SetParent(discardUIContainer, false); // Перекладываем UI
        else cv.transform.SetParent(transform, false); // Если контейнер не задан — под сам контроллер

        Destroy(cv.gameObject);                        // Уничтожаем UI-объект карты (в конце кадра)

        RaisePilesChanged();                           // Сообщаем про изменение руки
    }

    // Сбросить несколько карт (например, после подтверждения события)
    public void DiscardCards(List<CardView> views)
    {
        if (views == null) return;                     // Защита
        foreach (var cv in views) DiscardCard(cv);     // По одной
        RaisePilesChanged();                           // Уведомление (на всякий случай)
    }

    // Добрать N карт (например, по кнопке «+2»/«+5», энергия проверяется снаружи)
    public void DrawFromDeck(int n)
    {
        if (deck == null) return;                      // Нет колоды — выходим

        int capacity = Mathf.Max(0, maxHand - HandCount); // Сколько ещё влезет
        int toDraw = Mathf.Min(n, capacity);         // Не больше вместимости
        if (toDraw <= 0) { RaisePilesChanged(); return; } // Если некуда — просто уведомим и выйдем

        var list = deck.DrawMany(toDraw);              // Забираем из draw
        foreach (var inst in list) AddCardToHand(inst);// Спавним в руку
        RaisePilesChanged();                           // Уведомление
    }

    // Внешняя точка для обновления (дергают DeckController и UI)
    public void RaisePilesChanged() => OnPilesChanged?.Invoke(); // Вызов события
}

