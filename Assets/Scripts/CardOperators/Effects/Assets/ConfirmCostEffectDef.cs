using UnityEngine;

[CreateAssetMenu(menuName = "Robinson/Effects/ConfirmCost", fileName = "ConfirmCost")]
public class ConfirmCostEffectDef : EffectDef
{
    [TextArea] public string messageTemplate = "�������� �{card}� � ������� {amount} ����(�)?";
    public bool useEyeAmount = true;    // ��� ������ ���� ����� �� �����
    public int amountConst = 1;         // �������� �������

    // ��������� �������� �� ���������� (�������� ���������� ����� ShowAndContinue)
    public override bool Execute(EffectContext ctx) => false;

    // ���������� ConfirmModal � �� ��� ���������� ��������
    public void ShowAndContinue(EffectContext ctx, System.Action onConfirmed)
    {
        if (ConfirmModalUI.Instance == null) { onConfirmed?.Invoke(); return; }

        // �������, ������� ������� ������ ����� ������ ���� �����
        int handCount = ctx.hand.HandCount;
        int maxHand = ctx.hand.maxHand;
        int capacityAfterDiscard = Mathf.Max(0, maxHand - (handCount - 1)); // -1 � ��� ����� ���� � �����

        int baseAmount = useEyeAmount ? ctx.source.def.eye : amountConst;
        int willDraw = Mathf.Min(baseAmount, capacityAfterDiscard);

        string cardName = ctx.source.def.displayName;
        string msg = messageTemplate
            .Replace("{card}", cardName)
            .Replace("{amount}", willDraw.ToString());

        ConfirmModalUI.Instance.Show(
            msg,
            onYes: () => onConfirmed?.Invoke(),
            onNo: () => { /* ������, ����� ������� */ }
        );
    }
}
