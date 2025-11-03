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
}
