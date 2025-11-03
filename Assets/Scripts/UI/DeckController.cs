using System.Collections.Generic;                 // List<T>
using UnityEngine;                                // MonoBehaviour, Debug, Random

// Контроллер колоды: draw/discard хранят CardInstance (НЕ CardSO)
// Отвечает за стартовую сборку, перетасовку, добор и сброс.
public class DeckController : MonoBehaviour, IDeckPresetConsumer
{
    [Header("Starting deck (defs)")]
    public List<CardDef> startingDeckDefs = new(); // Ассеты CardDef, из которых соберём стартовую колоду (можно дубликаты)

    // Рантайм-стопки:
    private readonly List<CardInstance> drawPile = new(); // Стопка добора (верх — конец списка)
    private readonly List<CardInstance> discardPile = new(); // Сброс (верх — конец списка)

    [Header("Debug")]
    public bool shuffleOnStart = true;            // Перетасовать стартовую колоду при запуске
    public int initialHand = 5;                   // Сколько карт выдать в руку при старте (если нужно)

    // Событие «кучи изменились» — удобно для обновления UI
    public event System.Action OnPilesChanged;    // Подписываются HandController и счётчики UI

    public event System.Action OnDeckReshuffled; // подписка внешних систем - перетасовали колоду

    // Доступ к размерам стопок (для UI)
    public int DrawCount => drawPile.Count;     // Сколько карт в draw
    public int DiscardCount => discardPile.Count;  // Сколько карт в discard

    private void Awake()                          // Инициализация на сцене
    {
        BuildFromStartingDefs();                  // Собираем рантайм-колоду из ассетов
        if (shuffleOnStart) Shuffle(drawPile);    // Перетасовываем drawPile
        RaisePilesChanged();                      // Сообщаем подписчикам
        // Стартовую руку раздаёт HandController (чтобы всё было в одном месте UI)
    }

    /// Применить пресет колоды: полностью пересобрать draw/discard из пресета,
    /// опционально перетасовать draw, оповестить UI через OnPilesChanged.
    /// ВАЖНО: тут не вызываем OnDeckReshuffled — это не «боевой» reshuffle, а смена этапа.
    public void ApplyDeckPreset(DeckPreset preset)
    {
        ApplyDeckPreset(preset, shuffle: true, alsoUpdateStartingDefs: false);
    }

    // Расширенная версия с параметрами:
    /// shuffle — перетасовать ли новую колоду (обычно да);
    /// alsoUpdateStartingDefs — синхронизировать ли startingDeckDefs для отладки/сейвов.
    public void ApplyDeckPreset(DeckPreset preset, bool shuffle, bool alsoUpdateStartingDefs)
    {
        // Защита от null: если пресет не задан — просто очистим колоду.
        // Это позволяет «начать с пустой колоды», если вдруг понадобится.
        // (Можно сделать ранний return, но явная очистка даёт предсказуемое состояние.)

        // 1) Полностью очищаем runtime-стопки: и добор, и сброс
        drawPile.Clear();          // всё, что было в draw, выбрасываем
        discardPile.Clear();       // сброс — тоже чистый

        // 2) Если есть пресет — переносим из него карты в drawPile
        if (preset != null && preset.cards != null)
        {
            for (int i = 0; i < preset.cards.Count; i++)        // перебираем записи пресета
            {
                var entry = preset.cards[i];                    // элемент: (CardDef, count)
                if (entry == null || entry.card == null) continue; // пропускаем пустые
                var cnt = Mathf.Max(0, entry.count);            // отрицательные → 0

                for (int k = 0; k < cnt; k++)                   // кладём указанное число экземпляров
                {
                    drawPile.Add(new CardInstance(entry.card)); // новая «живая» карта на верх draw
                }
            }
        }

        // 3) По желанию — перетасовываем draw
        if (shuffle) Shuffle(drawPile);                         // стандартный Фишер–Йейтс

        // 4) При необходимости — синхронизируем стартовый список (для инспектора/сейвов)
        if (alsoUpdateStartingDefs)
        {
            startingDeckDefs.Clear();                           // очищаем дефы для отладки
            if (preset != null && preset.cards != null)
            {
                for (int i = 0; i < preset.cards.Count; i++)
                {
                    var entry = preset.cards[i];
                    if (entry == null || entry.card == null) continue;
                    var cnt = Mathf.Max(0, entry.count);
                    for (int k = 0; k < cnt; k++)
                        startingDeckDefs.Add(entry.card);       // отражаем состав в инспекторе
                }
            }
        }

        // 5) ВАЖНО: не трогаем OnDeckReshuffled — reshuffle-штраф за еду/воду не должен срабатывать
        // при смене приключения. Достаточно сообщить UI, что стопки изменились.
        RaisePilesChanged();                                    // уведомляем все подписчики UI
    }



    // Собрать drawPile из списка CardDef
    public void BuildFromStartingDefs()
    {
        drawPile.Clear();                         // Чистим draw
        discardPile.Clear();                      // Чистим сброс

        foreach (var def in startingDeckDefs)     // Перебираем дефиниции
        {
            if (def == null) continue;            // Пропускаем пустые
            drawPile.Add(new CardInstance(def));  // Создаём инстанс и кладём в draw
        }
    }

    // Общая перетасовка Фишера–Йейтса
    private void Shuffle(List<CardInstance> pile)
    {
        for (int i = pile.Count - 1; i > 0; i--)  // Идём с конца к началу
        {
            int j = Random.Range(0, i + 1);       // Случайный индекс 0..i
            (pile[i], pile[j]) = (pile[j], pile[i]); // Меняем местами
        }
    }

    // Добрать N карт (с учётом перетасовки сброса при необходимости)
    public List<CardInstance> DrawMany(int n)
    {
        var result = new List<CardInstance>(n);   // Сюда соберём карты
        for (int i = 0; i < n; i++)               // Повторяем n раз
        {
            var card = DrawOne();                 // Пробуем взять одну
            if (card == null) break;              // Если больше нечего — выходим
            result.Add(card);                     // Добавляем в результат
        }
        if (result.Count > 0) RaisePilesChanged(); // Сообщаем, что стопки изменились
        return result;                            // Возвращаем взятые карты
    }

    // Добрать одну карту
    public CardInstance DrawOne()
    {
        //Debug.Log("rect.sizeDelta: " + drawPile.Count);
        if (drawPile.Count == 0)                  // Если draw пуст
        {
            if (discardPile.Count == 0) return null; // И discard пуст — добирать нечего
            // Иначе: переносим discard в draw и тасуем
            MoveDiscardToDrawAndShuffle();        // Перенос + перетасовка
        }
        var top = drawPile[^1];                    // Берём верхнюю (последнюю)
        drawPile.RemoveAt(drawPile.Count - 1);    // Удаляем из draw
        return top;                               // Возвращаем
    }

    // Сбросить карту (из руки/игры) в discard
    public void Discard(CardInstance inst)
    {
        if (inst == null) return;                 // Защита
        discardPile.Add(inst);                    // Кладём в сброс (верх — конец)
        RaisePilesChanged();                      // Сообщаем подписчикам
    }

    // Полный перенос discard в draw и перетасовка
    private void MoveDiscardToDrawAndShuffle()
    {
        // Переносим ВСЁ содержимое discard в draw
        drawPile.AddRange(discardPile);           // Добавляем всё к draw
        discardPile.Clear();                      // Очищаем discard
        Shuffle(drawPile);                        // Тасуем draw
        OnDeckReshuffled?.Invoke();             // ← СООБЩАЕМ о перетасовке
        RaisePilesChanged();                      // Уведомляем UI
    }

    // Положить ГОТОВЫЙ инстанс на верх колоды
    public void AddToTop(CardInstance inst)
    {
        if (inst == null) return;                 // защита от null
        drawPile.Add(inst);                       // верх стопки — конец списка
        RaisePilesChanged();                      // оповестим UI/слушателей
    }

    // Удобный перегруз: создать инстанс из CardDef и положить на верх
    public void AddToTop(CardDef def)
    {
        if (def == null) return;
        AddToTop(new CardInstance(def));
    }

    // Положить несколько на верх (сохраним порядок списка: первый → ниже, последний → самый верх)
    public void AddManyToTop(IEnumerable<CardInstance> list)
    {
        if (list == null) return;
        foreach (var ci in list)
            if (ci != null) drawPile.Add(ci);
        RaisePilesChanged();
    }

    //public void RemoveFromGame(CardInstance inst)
    //{
    //    if (inst == null) return;
    //    bool removed = false;
    //    removed |= drawPile.Remove(inst);
    //    removed |= discardPile.Remove(inst);
    //    removed |= hand.Remove(inst);
    //    if (removed) OnPilesChanged?.Invoke();
    //}


    // Хелпер для оповещения
    private void RaisePilesChanged() => OnPilesChanged?.Invoke(); // Вызов события
}

