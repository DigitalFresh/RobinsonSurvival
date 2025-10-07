using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private string gameplayScene = "Adventure"; // имя сцены приключения
    [SerializeField] private AdventureAsset firstAdventure;      // ассет нашего приключения 5x5

    public void StartAdventure1()
    {
        if (firstAdventure == null)
        {
            Debug.LogError("[MainMenuUI] Не назначен FirstAdventure в инспекторе.");
            return;
        }

        AdventureRuntime.SelectedAdventure = firstAdventure; // прокидываем выбор игрока в рантайм-контекст
        SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single); // переходим в сцену приключения
    }
}
