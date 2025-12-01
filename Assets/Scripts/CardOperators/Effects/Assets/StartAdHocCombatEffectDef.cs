using System.Collections.Generic;
using UnityEngine;

/// Эффект «запустить бой прямо сейчас на текущем гексе события».
[CreateAssetMenu(menuName = "Robinson/Effects/Reward/Start Ad-Hoc Combat", fileName = "Start Ad-Hoc Combat")]
public class StartAdHocCombatEffectDef : EffectDef
{
    [Tooltip("С кем именно будет бой (перечень EnemySO).")]
    [Header("Combat payload")]
    public List<EnemySO> enemies = new();               // противники для боя
    [Header("Optional modals")]
    public string preFightCatalogKey;                   // ключ контента МОДАЛКИ до боя (можно пусто)
    public string postFightCatalogKey;                  // ключ контента МОДАЛКИ после боя (можно пусто)


    /// ВАЖНО: сам бой мы не стартуем прямо отсюда.
    /// Его подхватывает окно события (ChooseEventWindowUI): оно собирает все такие эффекты,
    /// запускает бой через HexMapController и ждёт завершения корутиной.
    ///
    /// Возвращаем true, чтобы не останавливать цепочку применения наград/эффектов.
    public override bool Execute(EffectContext ctx)
    {
        // no-op: запуск боя организован снаружи (см. ChooseEventWindowUI.RunAdHocCombatThenContinue)
        return true;
    }
}
