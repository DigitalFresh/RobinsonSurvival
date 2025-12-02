using System;
using System.Collections.Generic;
using UnityEngine;

/// Маршрут кампании: список «этапов» — приключение + ключ модалки выхода + пресет колоды.

[CreateAssetMenu(fileName = "CampaignRoute", menuName = "Campaign/Route")]
public class CampaignRoute : ScriptableObject
{
    [Serializable]
    public class Stage
    {
        public string id;                   // произвольный ID/человекочитаемое имя (для отладки)
        public AdventureAsset adventure;    // какой Adventure загружать на этом этапе
        public string exitModalKey = "exit";// какой ключ искать в ModalContentCatalog при выходе
        public DeckPreset deckPreset;       // какой состав колоды дать игроку на входе в приключение
    }

    public List<Stage> stages = new List<Stage>();  // по порядку прохождения
}
