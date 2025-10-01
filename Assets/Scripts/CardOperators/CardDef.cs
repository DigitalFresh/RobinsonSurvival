using System.Collections.Generic;
using UnityEngine;                              // ScriptableObject, Sprite

// Определение карты (дефиниция/шаблон). Позже будем авторить в редакторе.
// Пока это параллель к твоему CardSO (мы его НЕ ломаем).
[CreateAssetMenu(menuName = "Robinson/CardDef", fileName = "CardDef")]
public class CardDef : ScriptableObject
{
    public string id;                           // Строковый ID (можно GUID) для ссылок и сейвов
    public string displayName;                  // Имя для отображения (позже заменим на локализуемый ключ)
    public Sprite artwork;                      // Спрайт арта карты

    public enum CardColor { Green, Red, Blue }  // Цвет: зелёная/красная/синяя
    public CardColor color;                     // Текущий цвет

    public int hands;                           // «Ладошки» (для событий)
    public int fists;                           // «Кулачки» (для боя)
    public int eye;                             // «Глаз» (синяя способность)

    public int brain;                           // «Ладошки» (для событий)
    public int power;                           // «Кулачки» (для боя)
    public int speed;

    public string[] tags;                       // Теги (пока строки; позже можно Flags-энум)

    public List<AbilityDef> abilities = new();   // например, одна способность «Eye»
}
