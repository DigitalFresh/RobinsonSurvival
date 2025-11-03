// Assets/Scripts/Campaign/CampaignManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Синглтон кампании: знает текущий этап, даёт ключ модалки выхода, переключает приключения,
/// даёт пресет колоды и применяет его в сцене.

public class CampaignManager : MonoBehaviour
{
    public static CampaignManager Instance { get; private set; } // глобальный доступ

    [Header("Route")]
    [SerializeField] private CampaignRoute route; // ссылка на ScriptableObject-маршрут
    [SerializeField] private int currentIndex;    // текущий индекс этапа в маршруте

    [Header("Options")]
    [SerializeField] private bool persistBetweenScenes = true; // сохранять ли объект между сценами

    private void Awake()
    {
        // Один синглтон на игру
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Сохраняем при смене сцен (если надо)
        if (persistBetweenScenes) DontDestroyOnLoad(gameObject);

        // Без маршрута дальше ничего не сможем
        if (!route || route.stages == null || route.stages.Count == 0)
            Debug.LogWarning("[CampaignManager] Маршрут пуст — назначьте CampaignRoute в инспекторе.");
    }

    //Текущий этап (или null, если не задан)
    public CampaignRoute.Stage CurrentStage =>
        (route && currentIndex >= 0 && currentIndex < route.stages.Count) ? route.stages[currentIndex] : null;

    //Ключ модалки выхода для текущего этапа.
    public string ExitModalKey => CurrentStage != null ? CurrentStage.exitModalKey : "exit";

    //Adventure текущего этапа.
    public AdventureAsset CurrentAdventure => CurrentStage != null ? CurrentStage.adventure : null;

    //Пресет колоды для текущего этапа.
    public DeckPreset CurrentDeckPreset => CurrentStage != null ? CurrentStage.deckPreset : null;

    //Перейти к следующему этапу. Вернёт false, если этапов больше нет.
    public bool Advance()
    {
        if (!route) return false;
        if (currentIndex + 1 >= route.stages.Count) return false; // маршрут закончился
        currentIndex++;
        return true;
    }

    /// Построить текущее приключение «в этой же сцене»: вызвать AdventureBuilder, применить пресет колоды.
    public void BuildCurrentStageInThisScene()
    {
        var stage = CurrentStage;                                // берём текущий этап
        if (stage == null || stage.adventure == null)            // без Adventure смысла нет
        {
            Debug.LogError("[CampaignManager] CurrentStage/Adventure не задан — нечего строить.");
            return;
        }

        // 1) Найдём билдёр приключения в сцене
        var builder = FindFirstObjectByType<AdventureBuilder>(FindObjectsInactive.Include);
        if (!builder)
        {
            Debug.LogError("[CampaignManager] AdventureBuilder не найден в сцене.");
            return;
        }

        // 2) Укажем нужный Adventure и соберём поле/тайлы/привязку событий
        builder.SetAdventure(stage.adventure);                   // установить ассет приключения
        builder.BuildAll();                                      // построить всё заново

        // 3) Применим пресет колоды через стандартный интерфейс
        ApplyDeckPresetToScene(stage.deckPreset);                // раздаём стартовую колоду

        // 3.5) Сразу выдать стартовую руку из новой колоды
        var hand = FindFirstObjectByType<HandController>(FindObjectsInactive.Include);
        if (hand != null)
        {
            hand.RedealInitialHand(); // очистит UI-руку и доберёт initialHand из DeckController
        }
        else
        {
            Debug.LogWarning("[CampaignManager] HandController не найден в сцене — стартовая раздача пропущена.");
        }

    }

    /// Построить СЛЕДУЮЩИЙ этап этого маршрута (если есть) в этой же сцене.
    /// Возвращает true, если получилось.
    public bool BuildNextStageInThisScene()
    {
        if (!Advance())                                          // переключиться на следующий
        {
            Debug.Log("[CampaignManager] Этапов больше нет — маршрут завершён.");
            return false;
        }
        BuildCurrentStageInThisScene();                          // строим текущий (уже следующий)
        return true;
    }

    /// Найти в сцене «потребителя» пресета колоды и вызвать у него ApplyDeckPreset(preset).
    public void ApplyDeckPresetToScene(DeckPreset preset)
    {
        // Находим любой компонент, реализующий IDeckPresetConsumer (например, DeckPresetRelay на DeckController)
        var consumers = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < consumers.Length; i++)
        {
            var c = consumers[i] as IDeckPresetConsumer;         // пробуем привести
            if (c != null)                                       // нашли — применяем пресет
            {
                c.ApplyDeckPreset(preset);
                return;                                          // достаточно одного потребителя в сцене
            }
        }
        Debug.LogWarning("[CampaignManager] В сцене нет IDeckPresetConsumer — пресет колоды не применён.");
    }
}
