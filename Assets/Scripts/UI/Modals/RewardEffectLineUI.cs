using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq; // для простого поиска по детям

// Линия списка эффектов (иконка + текст)
public class RewardEffectLineUI : MonoBehaviour
{
    [Header("Legacy (иконка+текст)")]
    public Image icon;                 // Иконка эффекта (может быть пустой)
    public TextMeshProUGUI text;       // Описание эффекта

    [Header("Resource prefab mode")]
    public RectTransform contentRoot;        // контейнер, куда инстанциируем res_1
    public GameObject resourcePrefab;        // сюда назначь префаб res_1
    private GameObject _spawnedResourceUI;   // текущий инстанс, чтобы чистить

    // Привязка UI-строки к произвольному EffectDef с метаданными (если поддерживает)
    public void Bind(EffectDef eff)
    {
        if (!text || !icon) return;

        // Попробуем вытащить «UI-мета» через известные нам поля (например, RewardTakeDamageEffectDef)
        Sprite s = null;
        string t = eff != null ? eff.name : "";

        // Если это RewardTakeDamageEffectDef — возьмём его красивые поля
        if (eff is RewardTakeDamageEffectDef dmg)
        {
            s = dmg.uiIcon;
            if (!string.IsNullOrEmpty(dmg.uiDescription)) t = dmg.uiDescription;
        }

        icon.enabled = (s != null);
        icon.sprite = s;
        text.text = t;
    }

    // Привязка к «иконка+текст»
    public void BindIconText(Sprite s, string label)
    {
        ClearContent();                               // чистим предыдущий контент
        if (icon) { icon.enabled = (s != null); icon.sprite = s; }
        if (text) text.text = label ?? "";
    }

    /// Показать полноценный ресурс: заспавнить res_1, центрировать, увеличить на 1.2,
    /// поставить иконку/количество; а название показать в text у самой строки.
    public void BindResource(ResourceDef def, int amount)
    {
        ClearContent();                                                         // очистить предыдущий контент

        // Если нет контейнера/префаба — упадём в «иконка+текст».
        if (!contentRoot) contentRoot = transform as RectTransform;             // подстраховка
        if (!contentRoot || !resourcePrefab || !def)
        {
            BindIconText(def ? def.icon : null,
                (def ? def.displayName : "Resource") + " ×" + Mathf.Max(1, amount));
            return;
        }

        // 1) Спавним res_1 внутрь contentRoot
        _spawnedResourceUI = Instantiate(resourcePrefab, contentRoot);

        // 2) Центруем и чуть увеличиваем (pivot/anchors/позиция/скейл)
        var rt = _spawnedResourceUI.GetComponent<RectTransform>();
        if (rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);             // центр по якорям
            rt.pivot = new Vector2(0.5f, 0.5f);                            // центр по pivot
            rt.anchoredPosition = Vector2.zero;                                // в центр контейнера
            rt.localScale = Vector3.one * 1.2f;                                // чуть крупнее
        }

        // 3) Отключаем служебные элементы в префабе, чьи имена содержат "res_req"
        DisableChildrenBySubstring(_spawnedResourceUI.transform, "res_req");

        // 4) Ищем и настраиваем ИКОНКУ (поддержка имён: icon/art/image/pic)
        var img = FindByName(_spawnedResourceUI, out Image _, "icon", "art", "image", "pic");
        if (img)
        {
            img.enabled = (def.icon != null);
            img.sprite = def.icon;
        }

        // 5) Ищем и настраиваем КОЛИЧЕСТВО (поддержка TMP и обычного Text; имена: count/amount/qty/value/num/number)
        var countTMP = FindByName(_spawnedResourceUI, out TextMeshProUGUI _, "count", "amount", "qty", "value", "num", "number");
        var countText = FindByName(_spawnedResourceUI, out UnityEngine.UI.Text _, "count", "amount", "qty", "value", "num", "number");
        var amountStr = Mathf.Max(0, amount).ToString();
        if (countTMP) countTMP.text = amountStr;
        if (countText) countText.text = amountStr;

        // 6) Подпись ресурса: в самом res_1 ЕЁ НЕТ → пишем в text у LinePrefab
        if (text)
        {
            var label = string.IsNullOrEmpty(def.displayName) ? def.name : def.displayName;
            text.enabled = (label != null);
            text.text = label;                                                 // ← имя ресурса под картинкой
        }
    }

    // ─────────────────────────── ВСПОМОГАТЕЛЬНЫЕ ───────────────────────────

    private void ClearContent()
    {
        if (_spawnedResourceUI) { Destroy(_spawnedResourceUI); _spawnedResourceUI = null; }
        if (icon) { icon.enabled = false; icon.sprite = null; }
        if (text) text.text = "";
    }

    /// Отключить (SetActive(false)) всех потомков, у кого имя содержит подстроку (без регистра).
    private void DisableChildrenBySubstring(Transform root, string substrLower)
    {
        if (!root || string.IsNullOrEmpty(substrLower)) return;
        substrLower = substrLower.ToLowerInvariant();

        var list = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < list.Length; i++)
        {
            var t = list[i];
            if (t == root) continue;
            if (t.name.ToLowerInvariant().Contains(substrLower))
                t.gameObject.SetActive(false);
        }
    }

    /// Найти первый компонент типа T в дочерних по фрагментам имени (без регистра).
    private T FindByName<T>(GameObject go, out T found, params string[] keys) where T : Component
    {
        found = null;
        if (!go || keys == null || keys.Length == 0) return null;

        var all = go.GetComponentsInChildren<T>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var nm = all[i].name.ToLowerInvariant();
            for (int k = 0; k < keys.Length; k++)
                if (nm.Contains(keys[k].ToLowerInvariant()))
                    return found = all[i];
        }
        return null;
    }
}