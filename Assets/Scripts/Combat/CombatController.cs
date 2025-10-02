/*
 * CombatController — улучшенная версия.
 * Изменения (без смены публичных API):
 *  1) Кэшируем WaitForSeconds (меньше GC в корутинах).
 *  2) Добавлен быстрый счётчик карт в боевых зонах (_cardsInZones) + методы уведомления.
 *  3) CountCardsOnTable() теперь O(1) — использует счётчик, а не сканирует иерархию.
 *  4) Подписки на кнопки через метод-группы (без клоужеров).
 *  5) Сбрасываем счётчик на старте боя и корректно уменьшаем при возврате/сбросе карт.
 */
using System.Collections;                                      // IEnumerator
using System.Collections.Generic;                              // Списки
using UnityEngine;                                             // MonoBehaviour, GameObject, Transform, Instantiate
using UnityEngine.UI;                                          // Button
using TMPro;                                                   // TMP

// Главный контроллер боя: старт/раунды/кнопки/добор/резолв/окончание
public class CombatController : MonoBehaviour
{
    public static CombatController Instance;                    // Синглтон (удобно вызывать StartCombat откуда угодно)

    [Header("Screen / Prefabs")]
    public GameObject combatScreen;                             // Префаб/объект окна боя (Combat_screen)
    public Transform arena;                                     // Контейнер Arena (под 1..3 Fighting_block)
    public FightingBlockUI fightingBlockPrefab;                 // Префаб Fighting_block (UI одной стычки)

    [Header("Buttons")]
    public Button btnReturnCards;                               // Return_cards
    public Button btnDraw2_1;                                   // Take_cards_2_1 (−2 энергии → +2 сейчас, +1 в след. раунд)
    public Button btnDraw3_2;                                   // Take_cards_3_2 (−3 энергии → +3 сейчас, +2 в след. раунд)
    public Button btnEndTurn;                                   // END_Turn
    public Button btnLeave;                                     // Leave_button (если нужен выход по условиям)

    [Header("Wounds alert")]
    public GameObject backgroundWound;                          // Background_wound (полупрозрачный фон)
    public GameObject alertRoot;                                // Alert (панель в центре)
    public Image alertIcon;                                     // W_alert (картинка)
    public TextMeshProUGUI playerWoundsText;                    // PlayerWounds (сколько потерял игрок)

    [Header("Deck/HUD integration")]
    public HandController hand;                                 // Рука игрока (UI)
    public DeckController deck;                                 // Колода (модель)
    public PlayerStatsSimple stats;                              // Статы игрока
    public DeckHUD deckHUD;                                     // Чтобы отключать кнопки HUD на время боя (сами счётчики живут)

    [Header("Runtime")]
    public List<FightingBlockUI> blocks = new();                // Текущие стычки (1..3)
    private int deferredDrawNextRound = 0;                      // Отложенный добор в следующий раунд (копится)
    private bool isRunning = false;                             // Идёт бой?
    private int roundIndex = 0;                                 // Номер текущего раунда (с 1)

    // --- PERF-кэш: один раз создаём ожидания, чтобы не аллоцировать каждый раз в корутинах
    private static readonly WaitForSeconds WOUND_ALERT_WAIT = new WaitForSeconds(1.2f); // ожидание для алерта ран — кэшируем объект
    private static readonly WaitForSeconds LOOT_DELAY_WAIT = new WaitForSeconds(0.5f);  // пауза перед полётом лута — кэш
    private static readonly WaitForSeconds END_PAUSE_WAIT = new WaitForSeconds(0.15f); // «выдох» перед закрытием боя — кэш

    // --- Быстрый счётчик карт в боевых зонах (вместо сканирования дочерних трансформов)
    private int _cardsInZones = 0;                                                         // текущее число карт Attack/Defense
    public int CardsInZones => _cardsInZones;                                              // чтение для UI/логики (CardView и др.)

    private void Awake()
    {
        Instance = this;                                        // Сохраняем синглтон
        // Искать зависимости, если не назначены
        if (!hand) hand = FindFirstObjectByType<HandController>();
        if (!deck) deck = FindFirstObjectByType<DeckController>();
        if (!stats) stats = FindFirstObjectByType<PlayerStatsSimple>();
        if (!deckHUD) deckHUD = FindFirstObjectByType<DeckHUD>();

        // Скрываем «раны игрока» по умолчанию
        if (backgroundWound) backgroundWound.SetActive(false);
        if (alertRoot) alertRoot.SetActive(false);
    }

    public void NotifyCardEnteredZone(CardView cv)                                         // вызов при дропе карты в зону
    {
        _cardsInZones++;                                                                   // увеличиваем счётчик
        RefreshDrawButtons();                                                              // кнопки добора могли измениться
    }

    public void NotifyCardLeftZone(CardView cv)                                            // вызов при выходе карты из зоны
    {
        _cardsInZones = Mathf.Max(0, _cardsInZones - 1);                                   // уменьшаем, не уходим в минус
        RefreshDrawButtons();                                                              // обновляем доступность кнопок
    }

    // === ПУБЛИЧНЫЙ СТАРТ БОЯ ===
    public void StartCombat(List<EnemySO> enemies)             // Запустить бой с 1..3 врагами
    {
        if (isRunning) return;                                  // Уже идёт — игнор

        // Очистим арену
        for (int i = arena.childCount - 1; i >= 0; i--)         // Перебираем детей
            Destroy(arena.GetChild(i).gameObject);              // Уничтожаем блоки, если были

        blocks.Clear();
        _cardsInZones = 0;                                                   // сброс счётчика карт на столе при старте боя
                                                                             // Чистим список блоков

        // Создаём блоки по числу врагов (макс. 3)
        int n = Mathf.Clamp(enemies != null ? enemies.Count : 0, 1, 3); // Сколько спавним
        for (int i = 0; i < n; i++)                              // Цикл по врагам
        {
            var fb = Instantiate(fightingBlockPrefab, arena);    // Спавним префаб Fighting_block
            blocks.Add(fb);                                      // В список
            fb.BindEnemy(enemies[i]);                            // Привязываем врага
            if (fb.enemyView && fb.enemyView.traitsButton)              // Если у view есть кнопка Traits
            {
                fb.enemyView.traitsButton.onClick.RemoveAllListeners(); // Снимаем старые обработчики
                fb.enemyView.traitsButton.onClick.AddListener(          // Вешаем наш колбэк
                    fb.enemyView.ToggleTraitsPanel                      // Переключить описание свойств
                );
            }

            // Привяжем drop-зоны к блоку
            var atk = fb.zoneAttack.GetComponent<CombatDropZone>();    // Берём скрипт зоны атаки
            var def = fb.zoneDefense.GetComponent<CombatDropZone>();   // Берём скрипт зоны защиты
            if (atk) { atk.block = fb; atk.zoneType = CombatZoneType.Attack; } // Настраиваем зону
            if (def) { def.block = fb; def.zoneType = CombatZoneType.Defense; } // Настраиваем зону
        }

        // Показать экран боя и заблокировать остальной UI
        if (combatScreen) combatScreen.SetActive(true);         // Включаем окно боя
        ModalGate.Acquire(this);                                 // Блокируем внешние клики

        // Отключим кнопки добора в DeckHUD (счётчики остаются живыми)
        if (deckHUD)
        {
            //Debug.Log("сюда доходит");
            if (deckHUD.Buttons) deckHUD.Buttons.SetActive(false); ; // Выключаем внешние доборы
        }

        // Подписки на кнопки боя
        if (btnReturnCards) { btnReturnCards.onClick.RemoveAllListeners(); btnReturnCards.onClick.AddListener(ReturnAllToHand); }  // метод-группа без клоужера
        if (btnDraw2_1) { btnDraw2_1.onClick.RemoveAllListeners(); btnDraw2_1.onClick.AddListener(OnClickDraw2_1); }       // без клоужера
        if (btnDraw3_2) { btnDraw3_2.onClick.RemoveAllListeners(); btnDraw3_2.onClick.AddListener(OnClickDraw3_2); }       // без клоужера
        if (btnEndTurn) { btnEndTurn.onClick.RemoveAllListeners(); btnEndTurn.onClick.AddListener(OnClickEndTurn); }       // без клоужера
        //if (btnReturnCards) { btnReturnCards.onClick.RemoveAllListeners(); btnReturnCards.onClick.AddListener(ReturnAllToHand); }
        //if (btnDraw2_1) { btnDraw2_1.onClick.RemoveAllListeners(); btnDraw2_1.onClick.AddListener(() => OnDrawNowPlusNext(2, 1, 2)); }
        //if (btnDraw3_2) { btnDraw3_2.onClick.RemoveAllListeners(); btnDraw3_2.onClick.AddListener(() => OnDrawNowPlusNext(3, 2, 3)); }
        //if (btnEndTurn) { btnEndTurn.onClick.RemoveAllListeners(); btnEndTurn.onClick.AddListener(() => StartCoroutine(ResolveRoundAndContinue())); }

        // Начальный раунд
        isRunning = true;                                         // Бой активен
        roundIndex = 1;                                           // Первый раунд
        // пересчёт сумм по всем блокам (на случай последних перетаскиваний)
        foreach (var fb in blocks) if (fb) fb.RecountSums();        // Обновляем Fist/Shield/Wounds

        StartCoroutine(RoundStartDrawIfDeferred());               // Отдать отложенный добор (если был)
        RefreshDrawButtons();                                     // Пересчитать доступность «доборов»
    }


    // Обработчики кнопок — без лямбда-замыканий (избегаем лишних аллокаций)
    private void OnClickDraw2_1() { OnDrawNowPlusNext(2, 1, 2); }                  // −2 энергии → +2 сейчас, +1 в следующий раунд
    private void OnClickDraw3_2() { OnDrawNowPlusNext(3, 2, 3); }                  // −3 энергии → +3 сейчас, +2 в следующий раунд
    private void OnClickEndTurn() { StartCoroutine(ResolveRoundAndContinue()); }  // завершить раунд

    // === КНОПКА «Вернуть карты» ===
    private void ReturnAllToHand()
    {
        if (!hand) return;                                        // Защита
        // Собираем все карты во всех зонах и переносим обратно в руку
        foreach (var fb in blocks)                                 // По всем блокам
        {
            if (!fb) continue;                                     // Защита
            MoveAllZoneCardsBackToHand(fb.zoneAttack);             // Вернуть из атаки
            MoveAllZoneCardsBackToHand(fb.zoneDefense);            // Вернуть из защиты
            fb.RecountSums();                                      // Пересчитать суммы после возврата
        }
        RefreshDrawButtons();                                      // После возврата — пересчитать доступность доборов
    }

    private void MoveAllZoneCardsBackToHand(Transform zone)        // Переместить все карты из зоны в руку
    {
        if (!zone || !hand) return;                                // Проверка
        var views = zone.GetComponentsInChildren<CardView>();      // Все CardView под зоной
        foreach (var cv in views)                                   // Перебор
        {
            if (!cv) continue;                                      // Пропуск
            var attach = cv.GetComponent<CombatCardAttachment>();   // Ищем маркер
            if (attach) Destroy(attach);                            // Снимаем привязку к бою
            _cardsInZones = Mathf.Max(0, _cardsInZones - 1);         // Декремент счётчика — карта покинула боевую зону
            cv.transform.SetParent(hand.handPanel, false);          // Кладём в руку
            cv.rect.localScale = Vector3.one;                       // Восстановить масштаб
        }
    }

    // === КНОПКИ «Добрать 2/3 (и +1/+2 в след.)» ===
    private void OnDrawNowPlusNext(int now, int plusNext, int energyCost)
    {
        if (!hand || !deck || !stats) return;                      // Проверка
        if (stats.Energy < energyCost) return;                     // Недостаточно энергии
        stats.SpendEnergy(energyCost);                              // Списываем
        // Сколько сейчас на столе (рука + все зоны)
        int totalOnTable = CountCardsOnTable();                    // Считаем все карты
        int canTakeNow = Mathf.Clamp(7 - totalOnTable, 0, now);    // Сколько можем взять прямо сейчас (до лимита 7)

        // Вытягиваем реальные экземпляры (не добавляя в руку сразу — сначала анимация)
        var cards = deck.DrawMany(canTakeNow);                     // Забираем из колоды
        if (cards != null && cards.Count > 0 && RewardPickupAnimator.Instance) // Если есть что анимировать
        {
            // Полёт карт из колоды в правую часть руки
            RewardPickupAnimator.Instance.PlayCardsToHandFromDeck(
                cards,                                             // Список карт
                onDone: () =>                                      // После приземления
                {
                    foreach (var ci in cards) hand.AddCardToHand(ci); // Фактически положить в руку
                    hand.RaisePilesChanged();                      // Перерисовать руку
                    RefreshDrawButtons();                          // Обновить доступность доборов
                }
            );
        }

        // Отложенный добор на следующий раунд — копится
        deferredDrawNextRound += plusNext;                         // Добавляем к запасу
        RefreshDrawButtons();                                      // Кнопки могли поменяться
    }

    private IEnumerator RoundStartDrawIfDeferred()                 // Начало раунда: выдать отложенный добор
    {
        yield return null;                                         // На кадр — чтобы UI обновился
        if (deferredDrawNextRound <= 0) yield break;               // Нечего выдавать
        int totalOnTable = CountCardsOnTable();                    // Текущее число карт на столе
        int canTake = Mathf.Clamp(7 - totalOnTable, 0, deferredDrawNextRound); // Сколько влезет
        // Вытянуть и анимировать
        var cards = deck.DrawMany(canTake);                        // Забираем из колоды
        deferredDrawNextRound -= canTake;                          // Снижаем долг
        if (cards != null && cards.Count > 0 && RewardPickupAnimator.Instance) // Если есть карты для анимации
        {
            RewardPickupAnimator.Instance.PlayCardsToHandFromDeck(
                cards,                                             // Эти карты летят
                onDone: () =>                                      // После приземления
                {
                    foreach (var ci in cards) hand.AddCardToHand(ci); // Фактически добавить в руку
                    hand.RaisePilesChanged();                      // Обновить руку
                    RefreshDrawButtons();                          // Кнопки
                }
            );
        }
        // Остаток (если ещё был) — просто остаётся отложенным дальше
    }

    private int CountCardsOnTable()                                // Рука + все зоны
    {
        int total = hand ? hand.HandCount : 0;                     // Сколько в руке
        total += _cardsInZones;                              // Карты в боевых зонах (наш счётчик)
        //foreach (var fb in blocks)                                  // По всем стычкам
        //{
        //    if (!fb) continue;                                      // Защита
        //    total += fb.zoneAttack ? fb.zoneAttack.GetComponentsInChildren<CardView>().Length : 0; // Карты в атаке
        //    total += fb.zoneDefense ? fb.zoneDefense.GetComponentsInChildren<CardView>().Length : 0; // Карты в защите
        //}
        return total;                                               // Возвращаем сумму
    }

    // Публичный хук: вызвать, когда изменилось количество карт на столе/в руке
    public void RefreshCombatUIAfterHandChanged()           // Вызывается из эффектов (добор/скидка и т.п.)
    {
        if (!isRunning) return;                             // Если бой не идёт — ничего не делаем
        RefreshDrawButtons();                               // Пересчитываем доступность «+2/+3» с учётом лимита 7
    }

    private void RefreshDrawButtons()                              // Пересчитать доступность «добрать 2/3»
    {
        int total = CountCardsOnTable();                            // На столе
        if (btnDraw3_2) btnDraw3_2.interactable = (total <= 4) && stats && stats.Energy >= 3; // 5+ карт — нельзя брать 3
        if (btnDraw2_1) btnDraw2_1.interactable = (total <= 6) && stats && stats.Energy >= 2; // 7+ карт — нельзя брать 2
    }

    // === РЕЗОЛВ РАУНДА ===
    private IEnumerator ResolveRoundAndContinue()
    {
        // Сначала — пересчёт сумм по всем блокам (на случай последних перетаскиваний)
        foreach (var fb in blocks) if (fb) fb.RecountSums();        // Обновляем Fist/Shield/Wounds

        // TODO: хуки «до расчёта» (traits до пункта «a») — здесь вызов эффектов врагов/карт
        // (оставляю место: вызов эффектов, ожидание модалок и т.п.)

        int totalPlayerWounds = 0;                                  // Сколько суммарно потеряет игрок в этом раунде
        var deadPairs = new List<(FightingBlockUI fb, List<EventSO.Reward> loot)>(); // Убитые враги и их лут

        // Проходим по блокам слева направо
        foreach (var fb in blocks)
        {
            if (!fb || fb.enemy == null) continue;                  // Пропуск пустых
            // Разрешаем один блок
            if (fb.Resolve(out int w, out bool killed, out List<EventSO.Reward> loot))
            {
                totalPlayerWounds += w;                             // Копим раны игрока
                if (killed)
                {
                    deadPairs.Add((fb, loot));                      // Запомним для анимации лута
                }
            }

            // TODO: эффекты «после расчёта блока» (traits после пункта «c») — место для хуков
        }

        // Если игрок получил урон в сумме по всем стычкам — показываем алерт
        if (totalPlayerWounds > 0)                                // Был урон игроку?
        {
            if (stats != null) stats.TakeDamage(totalPlayerWounds); // Снимаем здоровье со статов

            if (backgroundWound) backgroundWound.SetActive(true); // Включаем затемнение/фон
            if (alertRoot) alertRoot.SetActive(true);             // Включаем корень алерта
            if (playerWoundsText)                                  // Печатаем число ран
                playerWoundsText.text = totalPlayerWounds.ToString();

            yield return WOUND_ALERT_WAIT;               // Небольшая пауза, чтобы игрок увидел

            if (alertRoot) alertRoot.SetActive(false);           // Прячем алерт
            if (backgroundWound) backgroundWound.SetActive(false); // Прячем фон
        }

        // Если есть убитые враги — анимируем их лут (по одному блоку подряд)
        foreach (var pair in deadPairs)
        {
            var fb = pair.fb;                                       // Сам блок
            var loot = pair.loot;                                   // Список ресурсов
            // Визуал: «враг мёртв» → все сердца потеряны → «dead» оверлей
            if (fb.enemyView)
            {
                fb.enemyView.SetAllHeartsLostVisual();              // Все сердца — потеряны
                fb.enemyView.ShowDeadOverlay(true);                 // Включить «dead»
            }

            yield return LOOT_DELAY_WAIT;                 // Полсекунды паузы до полёта лута - для звука

            // Пакуем onBefore: начислить ресурсы в инвентарь (как в событиях)
            System.Action before = () =>                        // Локальный колбэк «до посадки в инвентарь»
            {                                                   // (выполняем начисление ресурсов в модель)
                var inv = InventoryController.Instance;         // Берём singleton инвентаря
                if (inv != null && loot != null)                // Если есть инвентарь и список лута
                    foreach (var r in loot)                     // Проходим по всем наградам
                        inv.AddResource(r.resource,             // Начисляем ресурс
                                       Mathf.Max(1, r.amount)); // С количеством не меньше 1
            };

            // Вычислим UI-якорь старта полёта — картинка врага (EnemyView.Picture)
            RectTransform startAnchor =                          // Берём RectTransform картинки врага
                (fb.enemyView && fb.enemyView.pictureImage)      // Если EnemyView и картинка заданы
                ? fb.enemyView.pictureImage.rectTransform        // Используем её как старт
                : null;                                          // Иначе — пусть аниматор возьмёт фоллбэк

            // Запускаем анимацию полёта наград из UI-якоря в инвентарь
            if (RewardPickupAnimator.Instance)                   // Если аниматор доступен
            {
                bool done = false;                               // Флаг завершения
                RewardPickupAnimator.Instance                    // Вызываем публичный метод «из якоря UI»
                    .PlayRewardsFromUIAnchor(
                        startAnchor,                              // Откуда летим
                        loot,                                     // Что летит (список Reward)
                        onBeforeInventoryApply: before,           // Перед посадкой — начислить в модель
                        onAfterDone: () => done = true            // По завершении — поднять флаг
                    );
                while (!done) yield return null;                  // Ждём окончания полёта
            }
        }

        // После расчёта раунда — все карты со столов (атака/защита) уходят в сброс
        ReturnAllCardsFromBlocksToDiscard();                      // Утилита: собрать и отправить в сброс

        // 1) Подержим «dead»-картинку ещё немного, чтобы игрок увидел результат
        yield return LOOT_DELAY_WAIT;                 // Полсекунды паузы после полёта лута

        // 3) Сформируем список блоков с погибшими врагами, чтобы удалить их с арены
        var deadBlocks = new List<FightingBlockUI>();          // Временный список для удаления
        foreach (var pair in deadPairs)                        // Идём по списку пар «блок + лут»
        {
            if (pair.fb) deadBlocks.Add(pair.fb);              // Собираем сами блоки (если не null)
        }

        // 4) Удаляем собранные блоки из списка активных и уничтожаем их GameObject
        foreach (var dead in deadBlocks)                       // Для каждого погибшего блока
        {
            blocks.Remove(dead);                               // Убираем из runtime-списка стычек
            if (dead) Destroy(dead.gameObject);                // Уничтожаем UI Fighting_block с арены
        }

        // Проверяем, остались ли живые враги
        bool anyAlive = false;                                    // Флаг — есть ли живые
        foreach (var fb in blocks)                                // Идём по стычкам
        {
            if (fb == null) continue;                                  // Блок отсутствует — пропуск
            var ev = fb.enemyView;                                     // Кэш EnemyView (может быть null)
            if (ev == null || ev.data == null) continue;               // Нет визу/данных — пропуск
            if (ev.currentHP > 0) { anyAlive = true; break; }          // Нашли живого — выходим
        }

        if (!anyAlive)                                           // Все враги мертвы?
        {
            StartCoroutine(RoundStartDrawIfDeferred());
            yield return EndCombatAndClose();                    // Закрываем бой (корутина)
            yield break;                                         // Завершаем выполнение
        }

        // Иначе — начинаем следующий раунд
        roundIndex++;                                          // Счётчик раундов
        foreach (var fb in blocks) if (fb) fb.RecountSums();        // Обновляем Fist/Shield/Wounds
        StartCoroutine(RoundStartDrawIfDeferred());                             // Отложенный добор (кнопки «2/3»)
        RefreshDrawButtons();                                    // Обновляем доступность доборов
        //isResolving = false;                                     // Разрешаем действия
    }

    private void ReturnAllCardsFromBlocksToDiscard()                 // Собрать все карты из всех зон и сбросить
    {
        var hand = HandController.Instance;                          // Берём контроллер руки
        if (hand == null) return;                                    // Если нет — выходим

        var temp = new List<CardView>();                             // Временная корзина карт
        foreach (var fb in blocks)                                   // Идём по каждому блоку
        {
            if (!fb) continue;                                       // Защита
            CollectCardsUnder(fb.zoneAttack, temp);                   // Собрать из зоны атаки
            CollectCardsUnder(fb.zoneDefense, temp);                  // Собрать из зоны защиты
        }
        hand.DiscardCards(temp);
        _cardsInZones = 0;                                                   // все карты со столов отправлены в сброс
                                                                             // Отправить всё в сброс (рука сама уничтожит UI)
    }

    private void CollectCardsUnder(Transform zone, List<CardView> dst) // Собрать CardView из контейнера
    {
        if (!zone || dst == null) return;                            // Защита
        for (int i = 0; i < zone.childCount; i++)                    // Перебираем детей
        {
            var cv = zone.GetChild(i).GetComponent<CardView>();      // Пытаемся взять CardView
            if (cv) dst.Add(cv);                                     // Если есть — добавляем в список
        }
    }

    private IEnumerator EndCombatAndClose()                          // Закрытие экрана боя
    {
        //isResolving = true;                                          // Блокируем ввод
        yield return END_PAUSE_WAIT;                      // Небольшая пауза «выдохнуть»
        HexMapController.Instance?.OnCombatEnded(true); // победили
        isRunning = false;
        if (combatScreen) combatScreen.SetActive(false);             // Прячем экран боя
        if (deckHUD) { if (deckHUD.Buttons) deckHUD.Buttons.SetActive(true); }
        ModalGate.Release(this);                                     // Снимаем «замок» модалок/инпута

        // Здесь можно триггерить финальные модалки или продолжение сценария,
        // если бой стартовал как «награда» другого события.
        // (Пока оставляю как no-op — логика показа уже есть в Event/Choose окнах.)
    }

    // Список временно отключённых контроллеров карты, чтобы потом вернуть как было
    private readonly List<MonoBehaviour> _disabledHexInputs = new List<MonoBehaviour>(); // Храним, кого отключили


}

