using UnityEngine;
using System.Collections.Generic;

//2) Добрать N карт (или значение из «eye» у карты-источника)
[CreateAssetMenu(menuName = "Robinson/Effects/DrawCards", fileName = "DrawCards")]
public class DrawCardsEffectDef : EffectDef
{
    public int amount = 1;                 // Константа по умолчанию
    public bool useEyeAmount = false;      // Если true — брать из ctx.source.def.eye

    // Флаг: использовать ли анимацию (оставил по умолчанию true, можно выключить в ассете при отладке)
    public bool animate = true;                                           // Включает анимированный добор

    public override bool Execute(EffectContext ctx)
    {
        if (ctx == null || ctx.hand == null || ctx.deck == null || ctx.source == null) return false;

        int handCount = ctx.hand.HandCount;
        int maxHand = ctx.hand.maxHand;
        int capacity = Mathf.Max(0, maxHand - handCount);

        int toDraw = useEyeAmount ? ctx.source.def.eye : amount;
        toDraw = Mathf.Min(toDraw, capacity);
        if (toDraw <= 0) return true; // нечего тянуть — не считаем за провал

        List<CardInstance> cards = ctx.deck.DrawMany(toDraw);   // ВАЖНО: CardInstance
        foreach (var ci in cards) ctx.hand.AddCardToHand(ci);

        var cc = CombatController.Instance;                     // Берём синглтон боя, если он есть
        if (cc != null)                                         // Если контроллер найден
            cc.RefreshCombatUIAfterHandChanged();               // Пересчитываем доступность добора

        return true;
    }

    // Запустить добор с анимацией. После завершения анимации — карты добавятся в руку и пайплайн продолжится.
    public void RunAnimated(EffectContext ctx, System.Action onDone)      // Асинхронный путь, без смены сигнатур Execute
    {
        // Базовые проверки
        if (ctx == null || ctx.hand == null || ctx.deck == null || ctx.source == null) { onDone?.Invoke(); return; }

        // Рассчитываем доступную вместимость руки
        int handCount = ctx.hand.HandCount;                               // Текущее количество в руке
        int maxHand = ctx.hand.maxHand;                                   // Максимум руки
        int capacity = Mathf.Max(0, maxHand - handCount);                 // Сколько реально можно взять

        // Считаем, сколько добирать
        int toDraw = useEyeAmount ? ctx.source.def.eye : amount;          // Либо из «глаза», либо константа
        toDraw = Mathf.Min(toDraw, capacity);                             // Ограничиваем вместимостью

        // Если нечего тянуть — просто завершить
        if (toDraw <= 0) { onDone?.Invoke(); return; }                    // Нечего — выходим

        // Берём конкретные экземпляры карт из колоды (НО не добавляем их в руку сейчас)
        var cards = ctx.deck.DrawMany(toDraw);                             // Список CardInstance (уже из колоды)

        // Запускаем анимацию «из колоды → правая часть руки»
        RewardPickupAnimator.Instance?.PlayCardsToHandFromDeck(            // Просим аниматор отыграть полёт
            cards,                                                         // Эти карты полетим
            onDone: () =>                                                  // После приземления
            {
                // Теперь фактически добавляем в руку (UI создаст объекты карт сам)
                foreach (var ci in cards) ctx.hand.AddCardToHand(ci);      // Добавляем по одной
                ctx.hand.RaisePilesChanged();                               // Обновляем UI
                onDone?.Invoke();                                           // Сообщаем пайплайну «готово»

                // Если открыт бой — попросим контроллер пересчитать кнопки «+2/+3»
                var cc = CombatController.Instance;                     // Берём синглтон боя, если он есть
                if (cc != null)                                         // Если контроллер найден
                    cc.RefreshCombatUIAfterHandChanged();               // Пересчитываем доступность добора

            }
        );
    }
}