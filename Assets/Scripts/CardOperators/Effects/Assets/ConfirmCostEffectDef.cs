using UnityEngine;

[CreateAssetMenu(menuName = "Robinson/Effects/ConfirmCost", fileName = "ConfirmCost")]
public class ConfirmCostEffectDef : EffectDef
{
    [TextArea] public string messageTemplate = "Сбросить «{card}» и добрать {amount} карт(ы)?";
    public bool useEyeAmount = true;    // для «Глаза» берём число из карты
    public int amountConst = 1;         // запасной вариант

    // Выполнять напрямую не используем (пайплайн перехватит через ShowAndContinue)
    public override bool Execute(EffectContext ctx) => false;

    // Показываем ConfirmModal и по «Да» продолжаем пайплайн
    public void ShowAndContinue(EffectContext ctx, System.Action onConfirmed)
    {
        if (ConfirmModalUI.Instance == null) { onConfirmed?.Invoke(); return; }

        // Считаем, сколько реально доберём ПОСЛЕ сброса этой карты
        int handCount = ctx.hand.HandCount;
        int maxHand = ctx.hand.maxHand;
        int capacityAfterDiscard = Mathf.Max(0, maxHand - (handCount - 1)); // -1 — эта карта уйдёт в сброс

        int baseAmount = useEyeAmount ? ctx.source.def.eye : amountConst;
        int willDraw = Mathf.Min(baseAmount, capacityAfterDiscard);

        string cardName = ctx.source.def.displayName;
        string msg = messageTemplate
            .Replace("{card}", cardName)
            .Replace("{amount}", willDraw.ToString());

        ConfirmModalUI.Instance.Show(
            msg,
            onYes: () => onConfirmed?.Invoke(),
            onNo: () => { /* ничего, игрок отменил */ }
        );
    }
}
