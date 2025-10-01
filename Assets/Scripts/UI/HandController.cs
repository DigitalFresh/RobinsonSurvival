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





//using System.Collections.Generic;              // List<T> для списков карт
//using UnityEngine;                             // MonoBehaviour, Transform, Debug
//using UnityEngine.UI;                          // LayoutRebuilder (для немедленной переразметки UI)

//// Контроллер "руки" игрока: создание UI-карт в руке, ограничение по количеству,
//// отправка карт в сброс и обслуживание счётчиков колоды (через DeckController).
//public class HandController : MonoBehaviour
//{
//    public static HandController Instance;      // Простая статическая ссылка  // Синглтон для удобного доступа из других скриптов

//    [Header("UI Refs")]
//    public RectTransform handPanel;               // Контейнер для уничтожаемой карты (на один кадр)
//    public RectTransform HandManager;             // Контейнер (родитель) для карточек в руке (обычно HorizontalLayoutGroup)
//    public GameObject cardPrefab;                 // Префаб одной UI-карты (с компонентом CardView)

//    [Header("Limits")]
//    public int maxHand = 7;                       // Максимальное количество карт в руке

//    //public CardView cardViewPrefab;             // Префаб UI-карты
//    //public List<CardSO> startCards = new();     // Набор карт для стартовой руки (заполни в инспекторе)

//    [Header("Runtime (optional)")]
//    public List<CardView> runtimeHand = new();    // Необязательно: кэш текущих CardView (удобно для отладки)

//    private DeckController deck;                  // Ссылка на контроллер колоды (чтобы класть данные в сброс)

//    //[Header("Runtime")]
//    //public List<CardView> hand = new();         // Текущие карты в руке (UI)
//    //public List<CardSO> discard = new();        // Сброс (данные)

//    public event System.Action OnPilesChanged;  // кто-то подписывается, чтобы узнать, что рука/сброс/колода изменились
//    private void RaisePilesChanged() => OnPilesChanged?.Invoke();

//    private void Awake()                        // Инициализация
//    {
//        // Синглтон: если экземпляр ещё не назначен — назначаем себя
//        if (Instance == null) Instance = this;
//        // Если в сцене уже есть другой экземпляр — можно защититься и уничтожить дубликат (по необходимости)

//        // Кешируем ссылку на DeckController (возьмём первый попавшийся в сцене)
//        deck = FindFirstObjectByType<DeckController>(FindObjectsInactive.Exclude);
//    }

//    // Свойство: текущее количество карт в руке (по числу дочерних объектов у handPanel)
//    public int HandCount
//    {
//        get
//        {
//            // Если панель не назначена — считаем 0 (защита от NullReference)
//            return handPanel != null ? handPanel.childCount : 0;
//        }
//    }

//    public void AddCardToHand(CardSO data)
//    {
//        // Защита: нет данных/префаба/панели — ничего не делаем
//        if (data == null || cardPrefab == null || handPanel == null) return;

//        // Проверка лимита: если рука уже заполнена — не создаём новую карту
//        Debug.Log("wHandCount: " + HandCount);
//        if (HandCount >= maxHand) return;

//        // Инстанциируем префаб как ребёнка handPanel (UI автоматически уложит через LayoutGroup)
//        GameObject go = Instantiate(cardPrefab, handPanel);

//        // Пытаемся взять CardView с созданного объекта (должен быть на корне префаба)
//        CardView cv = go.GetComponent<CardView>();

//        // Если CardView найден — биндим данные и ставим правильный визуальный размер для "руки"
//        if (cv != null)
//        {
//            cv.Bind(data);                       // Передаём вьюшке CardSO (имя, иконка, ✋ и т.д.)
//            cv.SetToHandSize();                  // Высота 347 px (как мы договорились для HandPanel)
//            runtimeHand.Add(cv);                 // Кладём в отладочный список (по желанию)
//        }

//        // Принудительно перестроим лейаут (чтобы карточка сразу заняла место в этот кадр)
//        if (handPanel != null)
//            LayoutRebuilder.ForceRebuildLayoutImmediate(handPanel);

//        RaisePilesChanged();
//    }


//    // Отправить набор UI-карт в сброс: данные уйдут в DeckController.discardPile,
//    // UI-объекты будут уничтожены.
//    public void DiscardCards(List<CardView> used)
//    {
//        // Защита: если список пустой — нечего делать
//        if (used == null || used.Count == 0) return;

//        // Убедимся, что у нас есть ссылка на DeckController (если сцену перезагружали и ссылка пропала)
//        if (deck == null)
//            deck = FindFirstObjectByType<DeckController>(FindObjectsInactive.Exclude);

//        // Пройдём по всем переданным карточкам
//        for (int i = 0; i < used.Count; i++)
//        {
//            CardView cv = used[i];               // Берём ссылку на UI-карту
//            if (cv == null) continue;            // Если карта уже разрушена — пропускаем

//            // Если есть данные карты — положим их в сброс колоды
//            if (deck != null && cv.data != null)
//                deck.AddToDiscard(cv.data);      // Сохраняем CardSO в discardPile (DeckController сообщит HUD'у)

//            // Удалим UI-объект карты из руки (и из runtimeHand — для чистоты)
//            runtimeHand.Remove(cv);              // Из отладочного списка
//            cv.transform.SetParent(HandManager, false); // сменим родителя, чтобу правильно считались карты в руке
//            Destroy(cv.gameObject);              // Уничтожаем визуал (UI)
//        }

//        // После удаления — перестроим лейаут руки (чтобы соседние карты "съехались")
//        if (handPanel != null)
//            LayoutRebuilder.ForceRebuildLayoutImmediate(handPanel);

//        RaisePilesChanged();
//    }

//    // Вспомогательный метод: отправить ОДНУ карту в сброс (если понадобится из другого кода)
//    public void DiscardCard(CardView cv)
//    {
//        if (cv == null) return;                  // Защита от null
//        if (deck == null)
//            deck = FindFirstObjectByType<DeckController>(FindObjectsInactive.Exclude);

//        if (deck != null && cv.data != null)
//            deck.AddToDiscard(cv.data);          // Сохраняем данные в discard

//        runtimeHand.Remove(cv);                  // Уберём из отладочного списка
//        cv.transform.SetParent(HandManager, false); // сменим родителя, чтобу правильно считались карты в руке
//        Destroy(cv.gameObject);                  // Уничтожим визуал
//        Debug.Log("HandCount после уничтожения: " + HandCount);

//        if (handPanel != null)
//            LayoutRebuilder.ForceRebuildLayoutImmediate(handPanel); // Перестроим лейаут

//        RaisePilesChanged();
//    }

//    // (Опционально) Полностью очистить руку (например, при смене сцены/режима)
//    public void ClearHand()
//    {
//        // Пройдём по детям панели с конца и уничтожим объекты
//        if (handPanel == null) return;

//        for (int i = handPanel.childCount - 1; i >= 0; i--)
//        {
//            Transform child = handPanel.GetChild(i); // Берём ребёнка по индексу
//            CardView cv = child.GetComponent<CardView>(); // Пытаемся получить CardView
//            runtimeHand.Remove(cv);                 // Убираем из списка (если был)
//            cv.transform.SetParent(HandManager, false);  // сменим родителя, чтобу правильно считались карты в руке
//            Destroy(child.gameObject);              // Уничтожаем UI-объект
//        }

//        // Перестроим лейаут после очистки
//        LayoutRebuilder.ForceRebuildLayoutImmediate(handPanel);

//        RaisePilesChanged();
//    }

//}
