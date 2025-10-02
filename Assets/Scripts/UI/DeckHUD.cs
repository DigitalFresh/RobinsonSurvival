using System.Collections;
using System.Collections.Generic;                // List<T>
using UnityEngine;                               // MonoBehaviour, Coroutine
using UnityEngine.UI;                            // Image, Button
using TMPro;                                     // TextMeshProUGUI

// HUD колоды: показывает счётчики draw/discard и содержит кнопки добора 2/5 (с тратой энергии)
public class DeckHUD : MonoBehaviour
{
    [Header("Refs: counters")]
    public Image deckIcon;                        // Иконка колоды (опционально)
    public TextMeshProUGUI deckText;              // Текст количества в колоде
    public Image discardIcon;                     // Иконка сброса (опционально)
    public TextMeshProUGUI discardText;           // Текст количества в сбросе

    [Header("Refs: buttons")]
    public GameObject Buttons;
    public Button btnDraw2;                       // «Взять 2 (−1⚡)»
    public Button btnDraw5;                       // «Взять 5 (−2⚡)»
    public TextMeshProUGUI warningText;           // Текст-предупреждение (показываем кратко при ошибках)

    [Header("Links")]
    public PlayerStatsSimple stats;               // Энергия и прочее
    public DeckController deck;                   // Логика колоды/сброса
    public HandController hand;                   // Рука (для лимита и добавления карт)

    [Header("Costs")]
    public int costDraw2 = 1;                     // Стоимость энергии для «2 карты»
    public int costDraw5 = 2;                     // Стоимость энергии для «5 карт»

    private Coroutine warnRoutine;                // текущая корутина показа предупреждения

    private void Awake()
    {
        // Если ссылки не выставлены руками — попробуем найти на сцене
        if (stats == null) stats = FindFirstObjectByType<PlayerStatsSimple>();
        if (deck == null) deck = FindFirstObjectByType<DeckController>();
        if (hand == null) hand = HandController.Instance;

        // Подпишем кнопки
        if (btnDraw2 != null) btnDraw2.onClick.AddListener(() => OnDrawClicked(2, costDraw2));
        if (btnDraw5 != null) btnDraw5.onClick.AddListener(() => OnDrawClicked(5, costDraw5));

        // Скрыть предупреждение на старте
        if (warningText != null) warningText.text = string.Empty;
    }

    private void OnEnable()
    {
        if (deck != null) deck.OnPilesChanged += RefreshAll;     // Меняются стопки — обновляем цифры и кнопки
        if (hand != null) hand.OnPilesChanged += RefreshButtons; // Меняется рука — обновляем кнопки
        if (stats != null) stats.OnStatsChanged += RefreshButtons;// Меняется энергия — обновляем кнопки

        RefreshAll(); // Первичная отрисовка
    }

    private void OnDisable()
    {
        if (deck != null) deck.OnPilesChanged -= RefreshAll;
        if (hand != null) hand.OnPilesChanged -= RefreshButtons;
        if (stats != null) stats.OnStatsChanged -= RefreshButtons;
    }

    private void RefreshAll()
    {
        RefreshCounters();
        RefreshButtons();
    }

    private void RefreshCounters()
    {
        if (deckText != null && deck != null)
            deckText.text = deck.DrawCount.ToString();      // количество карт в draw

        if (discardText != null && deck != null)
            discardText.text = deck.DiscardCount.ToString(); // количество карт в discard
    }

    private void RefreshButtons()
    {
        if (btnDraw2 == null || btnDraw5 == null || stats == null || hand == null || deck == null) return;

        int inHand = hand.HandCount;    // сейчас в руке
        int maxHand = hand.maxHand;      // максимум (обычно 7)
        int totalInDeckSystem = deck.DrawCount + deck.DiscardCount; // всего карт, которые ещё можно вытянуть

        // Правила из ТЗ:
        // 1) Если в руке уже 5 карт — «взять 5» блокируется, «взять 2» остаётся активной.
        bool canPressDraw5ByHand = inHand < 5;
        bool canPressDraw2ByHand = inHand < maxHand;

        // 2) Если энергии недостаточно — блокируем кнопку.
        bool enoughEnergy2 = stats.Energy >= costDraw2;
        bool enoughEnergy5 = stats.Energy >= costDraw5;

        // 3) Если в колоде и сбросе вообще нет карт — обе кнопки блокируются.
        bool hasAnyToDraw = totalInDeckSystem > 0;

        btnDraw2.interactable = canPressDraw2ByHand && enoughEnergy2 && hasAnyToDraw;
        btnDraw5.interactable = canPressDraw5ByHand && enoughEnergy5 && hasAnyToDraw;
    }

    private void OnDrawClicked(int requested, int energyCost)
    {
        if (stats == null || deck == null || hand == null) return;

        // Проверка энергии
        if (stats.Energy < energyCost)
        {
            ShowWarning("Недостаточно энергии");
            RefreshButtons();
            return;
        }

        // Сколько реально поместится в руку
        int space = Mathf.Max(0, hand.maxHand - hand.HandCount);
        int toDraw = Mathf.Min(requested, space);

        // Списываем энергию сразу (по вашей логике, даже если карт вытянется меньше/ноль)
        stats.SpendEnergy(energyCost);                                                                // Списали энергию
        RefreshButtons();                                                                             // Мгновенно обновим доступность кнопок по новой энергии

        // Если тянуть нечего (рука полная), то на этом всё: просто обновим UI и выходим
        if (toDraw <= 0)                                                                              // В руку уже ничего не влезает
        {
            RefreshAll();                                                                             // Обновим счётчики/кнопки
            return;                                                                                   // Выходим
        }

        // Тянем из колоды КОНКРЕТНЫЕ экземпляры, но НЕ добавляем в руку сейчас (под анимацию)
        List<CardInstance> cards = deck.DrawMany(toDraw);                                             // DeckController вернёт 0..toDraw карт (с учётом перетасовки)

        // Если колода реально не дала карт (пусто), просто обновим UI и предупредим при полном опустошении
        if (cards == null || cards.Count == 0)                                                        // Нечего анимировать
        {
            RefreshAll();                                                                             // Обновим счётчики/кнопки
            if (deck.DrawCount == 0 && deck.DiscardCount == 0) ShowWarning("В колоде и сбросе не осталось карт"); // Сообщим, если всё пусто
            return;                                                                                   // Выходим
        }

        // На время анимации выключим кнопки (защита от повторных кликов); ModalGate у аниматора тоже поможет
        if (btnDraw2 != null) btnDraw2.interactable = false;                                          // Отключаем «взять 2»
        if (btnDraw5 != null) btnDraw5.interactable = false;

        // Если аниматор по какой-то причине отсутствует — делаем fallback: мгновенно кладём карты в руку
        if (RewardPickupAnimator.Instance == null)                                                    // Нет аниматора — аварийный режим
        {
            foreach (var inst in cards) hand.AddCardToHand(inst);                                     // Сразу добавляем карты в руку
            hand.RaisePilesChanged();                                                                  // Сообщаем руке/UI об изменениях
            RefreshAll();                                                                             // Обновляем счётчики/кнопки
            return;                                                                                   // Выходим
        }

        // --- ADDED START ---
        // Запускаем анимацию «карты из колоды → центр → правая часть HandPanel»
        RewardPickupAnimator.Instance.PlayCardsToHandFromDeck(                                        // Просим аниматор отыграть полёт
            cards,                                                                                     // Список карт, которые реально попадут в руку
            onDone: () =>                                                                              // Коллбек: вызывается ПОСЛЕ приземления всех иконок
            {
                // Только теперь фактически добавляем карты в руку (чтобы они «появились» после полёта)
                foreach (var inst in cards)                                                            // Перебираем вытянутые карты
                    hand.AddCardToHand(inst);                                                          // Кладём в руку

                hand.RaisePilesChanged();                                                              // Сообщаем UI руки о переменах (перестроение/анимации)
                RefreshAll();                                                                          // Обновляем счётчики draw/discard и состояние кнопок

                // Если после добора всё пусто — покажем предупреждение (как и раньше)
                if (deck.DrawCount == 0 && deck.DiscardCount == 0)                                     // Оба пусты
                    ShowWarning("В колоде и сбросе не осталось карт");                                 // Сообщение игроку
            }
        );
    }

    private void ShowWarning(string msg)
    {
        if (warningText == null) return;
        if (warnRoutine != null) StopCoroutine(warnRoutine);
        warnRoutine = StartCoroutine(ShowWarnRoutine(msg));
    }

    private IEnumerator ShowWarnRoutine(string msg)
    {
        warningText.text = msg;
        warningText.alpha = 1f;
        yield return new WaitForSeconds(1.2f);

        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            warningText.alpha = Mathf.Lerp(1f, 0f, t / 0.35f);
            yield return null;
        }
        warningText.text = string.Empty;
        warningText.alpha = 1f;
        warnRoutine = null;
    }
}


//using System.Collections;
//using System.Collections.Generic;                // List<T>
//using UnityEngine;                               // MonoBehaviour, Coroutine
//using UnityEngine.UI;                            // Image, Button
//using TMPro;                                     // TextMeshProUGUI

//// HUD колоды: показывает счётчики draw/discard и содержит кнопки добора 2/5 (с тратой энергии)
//public class DeckHUD : MonoBehaviour
//{
//    [Header("Refs: counters")]
//    public Image deckIcon;                        // Иконка колоды (опционально)
//    public TextMeshProUGUI deckText;              // Текст количества в колоде
//    public Image discardIcon;                     // Иконка сброса (опционально)
//    public TextMeshProUGUI discardText;           // Текст количества в сбросе

//    [Header("Refs: buttons")]
//    public Button btnDraw2;                       // «Взять 2 (−1⚡)»
//    public Button btnDraw5;                       // «Взять 5 (−2⚡)»
//    public TextMeshProUGUI warningText;           // Текст-предупреждение (показываем кратко при ошибках)

//    [Header("Links")]
//    public PlayerStatsSimple stats;               // Энергия и прочее
//    public DeckController deck;                   // Логика колоды/сброса
//    public HandController hand;                   // Рука (для лимита и добавления карт)

//    [Header("Costs")]
//    public int costDraw2 = 1;                     // Стоимость энергии для «2 карты»
//    public int costDraw5 = 2;                     // Стоимость энергии для «5 карт»

//    private Coroutine warnRoutine;                // текущая корутина показа предупреждения

//    private void Awake()                          // Инициализация
//    {
//        // Если ссылки не выставлены руками — попробуем найти на сцене
//        if (stats == null) stats = FindFirstObjectByType<PlayerStatsSimple>();       // Найдём PlayerStats
//        if (deck == null) deck = FindFirstObjectByType<DeckController>();          // Найдём DeckController
//        if (hand == null) hand = HandController.Instance;                          // Возьмём синглтон руки

//        // Подпишем кнопки
//        if (btnDraw2 != null) btnDraw2.onClick.AddListener(() => OnDrawClicked(2, costDraw2)); // По клику — тянуть 2
//        if (btnDraw5 != null) btnDraw5.onClick.AddListener(() => OnDrawClicked(5, costDraw5)); // По клику — тянуть 5

//        // Скрыть предупреждение на старте
//        if (warningText != null) warningText.text = string.Empty;                    // Текст пустой
//    }

//    private void OnEnable()                      // Подписки на события
//    {
//        if (deck != null) deck.OnPilesChanged += RefreshCounters;                    // Изменились стопки — обновить цифры
//        if (stats != null) stats.OnStatsChanged += RefreshButtons;                   // Изменилась энергия — обновить кнопки
//        RefreshAll();                                                                 // Первичная отрисовка
//    }

//    private void OnDisable()                     // Снять подписки
//    {
//        if (deck != null) deck.OnPilesChanged -= RefreshCounters;                    // Отписка
//        if (stats != null) stats.OnStatsChanged -= RefreshButtons;                   // Отписка
//    }

//    private void RefreshAll()                    // Полное обновление HUD
//    {
//        RefreshCounters();                       // Счётчики колоды/сброса
//        RefreshButtons();                        // Состояние кнопок
//    }

//    private void RefreshCounters()               // Обновить тексты количества карт
//    {
//        if (deckText != null && deck != null)    // Если есть текст и колода
//            deckText.text = deck.drawPile.Count.ToString();                          // Пишем число в колоде

//        if (discardText != null && deck != null) // Если есть текст и колода
//            discardText.text = deck.discardPile.Count.ToString();                    // Пишем число в сбросе
//    }

//    private void RefreshButtons()                // Логика активности кнопок
//    {
//        if (btnDraw2 == null || btnDraw5 == null || stats == null || hand == null) return; // Защита

//        int inHand = hand.HandCount;             // Сколько сейчас в руке
//        int maxHand = hand.maxHand;              // Максимум (7)

//        // Правила из ТЗ:
//        // 1) Если в руке уже 5 карт — «взять 5» блокируется, «взять 2» остаётся активной.
//        bool canPressDraw5ByHand = inHand < 5;   // нажимать 5 можно только если < 5 в руке
//        bool canPressDraw2ByHand = inHand < maxHand; // нажимать 2 можно, если рука не полная (7)

//        // 2) Если энергии недостаточно — блокируем кнопку (и покажем предупреждение на попытке)
//        bool enoughEnergy2 = stats.Energy >= costDraw2; // хватает ли энергии на 2
//        bool enoughEnergy5 = stats.Energy >= costDraw5; // хватает ли энергии на 5

//        // Итоговая доступность
//        btnDraw2.interactable = canPressDraw2ByHand && enoughEnergy2;               // Взять 2
//        btnDraw5.interactable = canPressDraw5ByHand && enoughEnergy5;               // Взять 5
//    }

//    private void OnDrawClicked(int requested, int energyCost) // Обработчик клика на любую кнопку «взять N»
//    {
//        if (stats == null || deck == null || hand == null) return;                   // Защита

//        // 1) Проверка энергии (по ТЗ — если не хватает, блокируем и показываем предупреждение)
//        if (stats.Energy < energyCost)                                               // Энергии не хватает
//        {
//            ShowWarning("Недостаточно энергии");                                     // Сообщение на HUD
//            RefreshButtons();                                                        // Обновить состояние кнопок
//            return;                                                                  // Ничего не делаем
//        }

//        int space = hand.maxHand - hand.HandCount;                                   // Сколько ещё поместится
//        int toDraw = Mathf.Min(requested, Mathf.Max(0, space));                      // Сколько реально возьмём (0..requested)

//        // 2) Списываем энергию СРАЗУ (по ТЗ списывается даже если не все карты поместятся / даже если не вытянули)
//        stats.SpendEnergy(energyCost);                                               // Списали

//        // 3) Тянем карты (с перетасовкой, если drawPile опустела)
//        List<CardSO> cards = deck.DrawMany(toDraw);                                  // Тянем «toDraw» (может быть и 0)

//        // 4) Кладём в руку каждую (если есть)
//        foreach (var c in cards)                                                     // Перебираем, что вытянули
//        {
//            hand.AddCardToHand(c);                                                   // Добавляем в UI руки
//        }

//        // 5) Если после попытки обе стопки пусты — блокируем кнопки и показываем предупреждение
//        if (deck.drawPile.Count == 0 && deck.discardPile.Count == 0)                 // Нечего больше тянуть
//        {
//            ShowWarning("В колоде и сбросе не осталось карт");                      // Сообщение
//            if (btnDraw2 != null) btnDraw2.interactable = false;                    // Блокируем 2
//            if (btnDraw5 != null) btnDraw5.interactable = false;                    // Блокируем 5
//        }
//        else
//        {
//            // Иначе просто обновим актуальные состояния
//            RefreshCounters();                                                       // Обновить цифры
//            RefreshButtons();                                                        // Обновить кнопки по руке/энергии
//        }
//    }

//    private void ShowWarning(string msg)         // Короткий показ предупреждения
//    {
//        if (warningText == null) return;         // Если текста нет — просто молчим
//        if (warnRoutine != null) StopCoroutine(warnRoutine);                         // Остановим прошлую корутину
//        warnRoutine = StartCoroutine(ShowWarnRoutine(msg));                          // Запустим новую
//    }

//    private IEnumerator ShowWarnRoutine(string msg)  // Короутина показа и затухания
//    {
//        warningText.text = msg;                  // Ставим текст
//        warningText.alpha = 1f;                  // Полная видимость
//        yield return new WaitForSeconds(1.2f);   // Подержать на экране
//        // Лёгкое затухание
//        float t = 0f;                             // таймер
//        while (t < 0.35f)                        // ~0.35 сек
//        {
//            t += Time.deltaTime;                  // наращиваем
//            warningText.alpha = Mathf.Lerp(1f, 0f, t / 0.35f); // линейное затухание
//            yield return null;                    // кадр
//        }
//        warningText.text = string.Empty;          // очистить текст
//        warningText.alpha = 1f;                   // вернуть альфу (на будущее)
//        warnRoutine = null;                       // корутина завершена
//    }
//}
