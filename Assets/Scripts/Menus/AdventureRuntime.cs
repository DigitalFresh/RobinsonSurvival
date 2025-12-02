using UnityEngine;

/// <summary>
/// Простой рантайм-контекст: хранит выбранный AdventureAsset между сценами (через static).
/// </summary>
public static class AdventureRuntime
{
    public static AdventureAsset SelectedAdventure;
}