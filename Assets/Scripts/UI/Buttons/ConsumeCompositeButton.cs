using System.Collections.Generic;              // списки для набора чипов
using UnityEngine;                             // базовые типы Unity
using UnityEngine.UI;                          // UI Image
using TMPro;                                   // TMP Text

/// Один «чип»: картинка параметра + число «+X»
[System.Serializable]
public class StatChip
{
    public GameObject root;                    // корневой объект чипа (вкл/выкл)
    public Image icon;                         // иконка параметра (сердце/молния/вода/еда)
    public TextMeshProUGUI amountText;         // текст "+X" с цветом по типу
}

/// Кнопка «потратить карту» с трёхслайсовым фоном и 1..3 динамическими чипами.
public class ConsumeCompositeButton : MonoBehaviour
{
    [Header("Three-slice background")]
    public Image left;                         // левая «крышка» (фикс. ширина)
    public Image middle;                       // центральная «рейка» (растягиваемая по ширине)
    public Image right;                        // правая «крышка» (фикс. ширина)

    [Header("Chips layout")]
    public RectTransform contentRoot;          // контейнер чипов поверх middle
    public StatChip[] chips;                   // максимум 3 чипа (заполняем слева направо)

    [Header("Layout params")]
    public float paddingLeft = 10f;            // отступ слева внутри middle
    public float paddingRight = 10f;           // отступ справа
    public float chipSpacing = 8f;             // расстояние между чипами
    public float minMiddleWidth = 16f;         // минимальная ширина средней рейки

    /// Вход: список (иконка, текст, цвет)
    public void SetItems(List<(Sprite icon, string text, Color color)> items)
    {
        // 1) Активируем только нужное число чипов и заполняем их
        int n = Mathf.Clamp(items != null ? items.Count : 0, 0, chips != null ? chips.Length : 0);
        for (int i = 0; i < (chips?.Length ?? 0); i++)
        {
            bool on = (i < n) && chips[i] != null;
            if (chips[i]?.root) chips[i].root.SetActive(on);
            if (!on) continue;

            var it = items[i];
            if (chips[i].icon) chips[i].icon.sprite = it.icon;
            if (chips[i].amountText) { chips[i].amountText.text = it.text; chips[i].amountText.color = it.color; }
        }

        // 2) Посчитаем общую ширину контента (сумма ширин чипов + интервалы + паддинги)
        float contentW = 0f;
        int active = 0;
        for (int i = 0; i < (chips?.Length ?? 0); i++)
        {
            if (chips[i]?.root && chips[i].root.activeSelf)
            {
                // ширина чипа — берём по RectTransform ширину текста/иконки объединённого блока
                var rt = chips[i].root.transform as RectTransform;
                float w = (rt != null) ? rt.sizeDelta.x : 0f;
                contentW += (active > 0 ? chipSpacing : 0f) + w;
                active++;
            }
        }
        contentW += paddingLeft + paddingRight;

        // 3) Растягиваем middle так, чтобы контент влез (и не меньше минимума)
        float targetMiddle = Mathf.Max(minMiddleWidth, contentW);
        if (middle)
        {
            var rt = middle.rectTransform;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetMiddle);
        }

        // 4) Центруем contentRoot на middle
        if (contentRoot)
        {
            contentRoot.anchoredPosition = Vector2.zero;   // по центру middle (при центр. якорях)
        }
    }
}
