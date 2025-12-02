using System.Collections.Generic;
using UnityEngine;

/// Стоимость для «карта → восстановление»: показать Confirm (локализуем),
/// перечислить восстанавливаемые параметры и по «ОК» продолжить пайплайн ability.

[CreateAssetMenu(menuName = "Robinson/Effects/Cost/Confirm Consume", fileName = "Cost_ConfirmConsume")]
public class ConfirmConsumeCostEffectDef : EffectDef
{
    [Header("Modal content")]
    [Tooltip("Ключ записи в ModalContentCatalog (заголовок/описание/картинка). Например: 'consume_card'.")]
    public string catalogKey = "consume_card";

    [Tooltip("Добавлять к описанию строки вида «• Параметр +X» из RestoreStatEffectDef в ability.effects.")]
    public bool appendAutoRestoreLines = true;

    [Header("Icons for restore chips")]
    public Sprite hpIcon;      // сердце
    public Sprite energyIcon;  // молния
    public Sprite waterIcon;   // капля
    public Sprite foodIcon;    // еда

    public override bool Execute(EffectContext ctx)
    {
        // Исполняется через AbilityRunner.ShowAndContinue — здесь ничего не делаем
        return false;
    }

    public void ShowAndContinue(EffectContext ctx, System.Action onConfirmed)
    {
        // 1) Базовый локализованный контент из каталога
        var resolved = ModalContentProvider.Instance
            ? ModalContentProvider.Instance.Resolve(catalogKey)
            : new ResolvedModalContent { title = "Подтверждение", description = "", image = null };  // фолбэк
        // 2) Соберём человекочитаемое описание, подставив имя карты
        var cardName = ctx.source?.def?.displayName ?? "карту";
        var desc = string.IsNullOrWhiteSpace(resolved.description)
            ? $"Использовать «{cardName}» для восстановления? (карта будет потеряна)"
            : resolved.description.Replace("{card}", cardName);

        // 3) Собираем чипы из RestoreStatEffectDef
        var chips = new List<ModalRequest.RestoreChip>();
        foreach (var eff in ctx.ability.effects)
        {
            var r = eff as RestoreStatEffectDef;
            if (r == null || r.amount <= 0) continue;

            Sprite icon; Color col; string label = $"+{r.amount}";
            switch (r.stat)
            {
                case RestoreStatEffectDef.StatKind.Health: icon = hpIcon; col = new Color32(220, 54, 54, 255); break;
                case RestoreStatEffectDef.StatKind.Energy: icon = energyIcon; col = new Color32(240, 200, 48, 255); break;
                case RestoreStatEffectDef.StatKind.Thirst: icon = waterIcon; col = new Color32(70, 140, 220, 255); break;
                default: icon = foodIcon; col = new Color32(70, 190, 70, 255); break;
            }
            chips.Add(new ModalRequest.RestoreChip { icon = icon, label = label, color = col });
        }


        // 4) Покажем Confirm через агрегатор; по «ОК» — продолжаем пайплайн
        ModalManager.Instance?.Show(new ModalRequest
        {
            kind = ModalKind.Confirm,
            size = ModalSize.Small,
            title = resolved.title,
            message = desc,
            picture = resolved.image,
            canCancel = true,
            restoreLines = chips
        },
        onClose: ok => { if (ok) onConfirmed?.Invoke(); });
    }
}
