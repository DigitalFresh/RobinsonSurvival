using UnityEngine;                              // MonoBehaviour не нужен; делаем обычный класс

// Рантайм-обёртка поверх CardDef: состояние экземпляра (в какой зоне, каунтеры и т.п.)
public class CardInstance
{
    public CardDef def;                          // Ссылка на дефиницию (что это за карта)
    public object owner;                         // Владелец (позже: Player/Enemy), сейчас пусть будет object
    public string zone;                          // Текущая зона (Hand/Deck/Discard/PlayArea/Exile и т.п.)
    public int durability;                       // Пример каундера (если потребуется)
    public int charges;                          // Пример каундера (если потребуется)

    public CardInstance(CardDef d)               // Простой конструктор по дефиниции
    {
        def = d;                                 // Сохраняем def
        zone = "Hand";                           // По умолчанию считаем «в руке» (переопределяется снаружи)
    }

    // ВРЕМЕННЫЙ мост: создать CardInstance «на лету» из legacy CardSO (чтобы не ломать текущий код)
    public static CardInstance FromLegacy(CardSO so)   // CardSO — твой действующий SO
    {
        // На время миграции соберём краткий CardDef «в памяти» (без ассета)
        var tempDef = ScriptableObject.CreateInstance<CardDef>(); // создаём временный ScriptableObject
        tempDef.id = so.name;                              // ID — имя SO (для простоты)
        tempDef.displayName = so.displayName;             // Имя — из старого SO
        tempDef.artwork = so.artwork;                     // Спрайт — из старого SO
        // Цвет пока не различаем точно — можно по eye>0 считать Blue и т.п.
        tempDef.color = (so.eye > 0) ? CardDef.CardColor.Blue : CardDef.CardColor.Green;
        tempDef.hands = so.hands;                         // Ладошки
        tempDef.fists = so.fists;                         // Кулачки
        tempDef.eye = so.eye;                           // Глаз
        tempDef.tags = System.Array.Empty<string>();     // Пока пусто
        tempDef.abilities = null;                         // Позже прикрутим через редактор

        return new CardInstance(tempDef);                 // Возвращаем рантайм-экземпляр
    }
}
