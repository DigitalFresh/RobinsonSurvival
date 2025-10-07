using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private string gameplayScene = "Adventure"; // ��� ����� �����������
    [SerializeField] private AdventureAsset firstAdventure;      // ����� ������ ����������� 5x5

    public void StartAdventure1()
    {
        if (firstAdventure == null)
        {
            Debug.LogError("[MainMenuUI] �� �������� FirstAdventure � ����������.");
            return;
        }

        AdventureRuntime.SelectedAdventure = firstAdventure; // ����������� ����� ������ � �������-��������
        SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single); // ��������� � ����� �����������
    }
}
