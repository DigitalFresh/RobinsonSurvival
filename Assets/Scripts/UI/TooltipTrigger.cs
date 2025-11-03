using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea] public string customText;
    public TagDef tagDef;
    public float delay = 0.45f;

    Coroutine _co;

    public void OnPointerEnter(PointerEventData e)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(ShowDelayed());
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_co != null) StopCoroutine(_co);
        TooltipUI.Instance?.Hide();
    }

    void OnDisable()
    {
        if (_co != null) StopCoroutine(_co);
        TooltipUI.Instance?.Hide();
    }

    IEnumerator ShowDelayed()
    {
        yield return new WaitForSecondsRealtime(delay);
        string txt = tagDef && !string.IsNullOrWhiteSpace(tagDef.description) ? tagDef.description : customText;
        if (!string.IsNullOrWhiteSpace(txt))
            TooltipUI.Instance?.Show(txt);
    }
}
