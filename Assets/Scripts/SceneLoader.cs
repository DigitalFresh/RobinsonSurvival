using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{

    void Start()
    {
        // ���� ����������� UGS � ������ define USE_UGS (��. ����)
        Analytics.Init(new UgsAnalyticsService(), enabled: true);
        Analytics.Event("session_start", new() {
        {"build", "ci-2025.10.01"},
        {"platform", Application.platform.ToString()},
        {"locale", Application.systemLanguage.ToString()},
        {"version", Application.version}
    });
    }

    public void LoadCampScene()
    {
        SceneManager.LoadScene("Camp");
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("����� �� ���� (�������� ������ � �����)");
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
