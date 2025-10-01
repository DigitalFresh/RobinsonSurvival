using System.Collections.Generic;                 // List<T>
using UnityEngine;                                // MonoBehaviour, Debug, Random

// Контроллер колоды: draw/discard хранят CardInstance (НЕ CardSO)
// Отвечает за стартовую сборку, перетасовку, добор и сброс.
public class DeckController : MonoBehaviour
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

    // Хелпер для оповещения
    private void RaisePilesChanged() => OnPilesChanged?.Invoke(); // Вызов события
}

