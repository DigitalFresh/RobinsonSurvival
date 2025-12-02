using UnityEngine;

/// На старте сцены «Adventure» строит текущий этап из CampaignManager.
/// (Если менеджер/маршрут не заданы — выводит предупреждение.)
public class AdventureSceneStarter : MonoBehaviour
{
    private void Start()
    {
        // Берём singleton
        var cm = CampaignManager.Instance;

        // Если нет менеджера — ничего не делаем
        if (cm == null)
        {
            Debug.LogWarning("[AdventureSceneStarter] CampaignManager.Instance == null — пропускаю автозапуск.");
            return;
        }

        // Строим текущий этап (сцену Adventure вы уже загрузили)
        cm.BuildCurrentStageInThisScene();
    }
}
