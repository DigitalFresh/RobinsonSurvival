using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{

    //void Start()
    //{
    //    // Если используешь UGS — добавь define USE_UGS (см. ниже)
    //    Analytics.Init(new UgsAnalyticsService(), enabled: true);
    //    Analytics.Event("session_start", new() {
    //    {"build", "ci-2025.10.01"},
    //    {"platform", Application.platform.ToString()},
    //    {"locale", Application.systemLanguage.ToString()},
    //    {"version", Application.version}
    //});
    //}

    public void LoadCampScene()
    {
        SceneManager.LoadScene("Camp");
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Выход из игры (работает только в билде)");
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
