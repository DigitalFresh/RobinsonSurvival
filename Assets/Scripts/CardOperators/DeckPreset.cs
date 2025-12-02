using System;
using System.Collections.Generic;
using UnityEngine;

/// Пресет стартовой колоды для приключения.
/// Храним пары (какая карта, сколько штук).
/// Пресет стартовой колоды для приключения.

[CreateAssetMenu(fileName = "DeckPreset", menuName = "Campaign/Deck Preset")]
public class DeckPreset : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public CardDef card;        // ссылка на дефиницию карты
        [Min(0)] public int count;  // сколько штук положить в колоду
    }

    public List<Entry> cards = new List<Entry>();   // полный список состава колоды
}
