using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ModalManager : MonoBehaviour
{
    public static ModalManager Instance { get; private set; }

    [Header("Modals")]
    [SerializeField] private ConfirmModalUI confirm;
    [SerializeField] private InfoModalUI info;
    [SerializeField] private FreeRewardModalUI freeReward;
    [SerializeField] private SmallModalUI small;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!confirm) confirm = FindFirstObjectByType<ConfirmModalUI>(FindObjectsInactive.Include);
        if (!info) info = FindFirstObjectByType<InfoModalUI>(FindObjectsInactive.Include);
        if (!freeReward) freeReward = FindFirstObjectByType<FreeRewardModalUI>(FindObjectsInactive.Include);
    }

    // ЕДИНАЯ ТОЧКА ВХОДА
    public void Show(ModalRequest req, Action<bool> onClose)
    {
        if (req == null) { onClose?.Invoke(false); return; }

        switch (req.kind)
        {
            case ModalKind.Confirm: ShowConfirm(req, onClose); break;
            case ModalKind.Info: ShowInfo(req, onClose); break;
            case ModalKind.FreeReward: ShowFreeReward(req, onClose); break;
            case ModalKind.Small: ShowSmall(req, onClose); break;
        }
    }

    // ─── Confirm ────────────────────────────────────────────────────
    private void ShowConfirm(ModalRequest req, Action<bool> onClose)
    {
        if (!confirm) { Debug.LogError("ConfirmModalUI not found"); onClose?.Invoke(false); return; }
        TryApplySize(confirm.gameObject, req.size);

        bool hasChips = req.restoreLines != null && req.restoreLines.Count > 0;
        if (hasChips)
        {
            // Конвертация в ConfirmModalUI.RestoreLine
            var list = new List<ConfirmModalUI.RestoreLine>(req.restoreLines.Count);
            foreach (var c in req.restoreLines)
                list.Add(new ConfirmModalUI.RestoreLine { icon = c.icon, label = c.label, color = c.color });

            confirm.ShowRich(req.title, req.message, list,
                onYes: () => onClose?.Invoke(true),
                onNo: req.canCancel ? () => onClose?.Invoke(false) : (System.Action)null);
        }
        else
        {
            // Старый «плоский» режим текста
            var text = string.IsNullOrEmpty(req.title) ? (req.message ?? "") : (req.title + "\n\n" + (req.message ?? ""));
            if (req.canCancel)
                confirm.Show(text, onYes: () => onClose?.Invoke(true), onNo: () => onClose?.Invoke(false));
            else
                confirm.Show(text, onYes: () => onClose?.Invoke(true), onNo: null);
        }
    }

    // ─── Info ──────────────────────────────────────────────────────
    private void ShowInfo(ModalRequest req, Action<bool> onClose)
    {
        if (!info) { Debug.LogError("InfoModalUI not found"); onClose?.Invoke(false); return; }
        TryApplySize(info.gameObject, req.size);

        if (req.cards != null && req.cards.Count > 0)
            info.ShowNewCards(string.IsNullOrEmpty(req.title) ? (req.message ?? "") : req.title, req.cards);
        else
            info.Show(req.message ?? "");

        // Ждём именно закрытия:
        void Local()
        {
            info.OnClosed -= Local;
            onClose?.Invoke(true);
        }
        info.OnClosed += Local;
    }

    // ─── FreeReward ─────────────────────────────────────────────────
    private void ShowFreeReward(ModalRequest req, Action<bool> onClose)
    {
        if (!freeReward) { Debug.LogError("FreeRewardModalUI not found"); onClose?.Invoke(false); return; }
        TryApplySize(freeReward.gameObject, req.size);

        // A) Нативные FreeRewardDef (очередь)
        if (req.freeRewards != null && req.freeRewards.Count > 0)
        {
            StartCoroutine(ShowFreeRewardDefsQueue(req.freeRewards, onClose));
            return;
        }

        // B) Простой набор Reward → превращаем в строки (иконка+подпись)
        if (req.rewards != null && req.rewards.Count > 0)
        {
            var effs = ConvertRewardsToEffects(req.rewards);
            var runtime = BuildRuntimeFreeReward(req.title, req.message, req.picture, effs);
            ShowFreeRewardRuntime(runtime, onClose);
            return;
        }

        // C) Ничего — показать хотя бы заголовок/описание
        var empty = BuildRuntimeFreeReward(req.title, req.message, req.picture, null);
        ShowFreeRewardRuntime(empty, onClose);
    }


    //  ─── ShowSmall ────────────────────────────────────────────────────
    private void ShowSmall(ModalRequest req, Action<bool> onClose)
    {
        if (!small) { Debug.LogError("SmallModalUI not found"); onClose?.Invoke(false); return; }
        //TryApplySize(small.gameObject, req.size);                 // можно передать Small/Medium — по вкусу

        small.Show(req.message ?? "", req.picture, () => onClose?.Invoke(true));
    }

    private System.Collections.IEnumerator ShowFreeRewardDefsQueue(List<FreeRewardDef> defs, Action<bool> onClose)
    {
        if (defs == null || defs.Count == 0) { onClose?.Invoke(true); yield break; }

        ModalGate.Acquire(this);
        freeReward.ShowMany(defs);                     // твой нативный показ очереди
        yield return null;
        // Ждём пока окно/очередь закроется (FreeRewardModalUI сам себя скрывает)
        while (freeReward.isActiveAndEnabled) yield return null;
        ModalGate.Release(this);
        onClose?.Invoke(true);
    }

    // ─── Вспомогательные структуры для "рантайм"-показа ─────────────
    [Serializable] private class EffectRuntime { public Sprite icon; public string label; }
    [Serializable]
    private class FreeRewardRuntime
    {
        public Sprite icon; public string title; public string description;
        public List<EffectRuntime> effects = new();
    }

    private List<EffectRuntime> ConvertRewardsToEffects(List<EventSO.Reward> rewards)
    {
        var list = new List<EffectRuntime>();
        if (rewards == null) return list;

        foreach (var r in rewards)
        {
            if (r == null) continue;
            var er = new EffectRuntime();

            switch (r.type)
            {
                case EventSO.RewardType.Resource:
                    er.icon = (r.resource != null) ? r.resource.icon : null;
                    er.label = (r.resource != null)
                        ? $"{r.resource.displayName} +{Mathf.Max(1, r.amount)}"
                        : $"Ресурс +{Mathf.Max(1, r.amount)}";
                    break;

                case EventSO.RewardType.NewCard:
                    er.icon = (r.cardDef != null) ? r.cardDef.artwork : null;
                    er.label = (r.cardDef != null)
                        ? $"{r.cardDef.displayName} ×{Mathf.Max(1, r.cardCount)}"
                        : $"Новая карта ×{Mathf.Max(1, r.cardCount)}";
                    break;

                case EventSO.RewardType.RestoreStat:
                    er.icon = null;
                    er.label = $"Восстановление: {r.stat} +{Mathf.Max(1, r.restoreAmount)}";
                    break;

                case EventSO.RewardType.FreeReward:
                    er.icon = null;
                    er.label = "Свободная награда";
                    break;

                default:
                    er.icon = null;
                    er.label = r.type.ToString();
                    break;
            }
            list.Add(er);
        }
        return list;
    }

    private FreeRewardRuntime BuildRuntimeFreeReward(string title, string desc, Sprite icon, List<EffectRuntime> effects)
    {
        return new FreeRewardRuntime
        {
            title = string.IsNullOrEmpty(title) ? "Награда" : title,
            description = desc ?? "",
            icon = icon,
            effects = effects ?? new List<EffectRuntime>()
        };
    }

    // ! Нужен метод в твоём FreeRewardModalUI:
    // public void ShowRuntime(string title, string desc, Sprite icon, List<(Sprite,string)> lines, Action onOk)
    private void ShowFreeRewardRuntime(FreeRewardRuntime rt, Action<bool> onClose)
    {
        var lines = new List<(Sprite, string)>();
        if (rt.effects != null)
            foreach (var e in rt.effects) lines.Add((e.icon, e.label));

        freeReward.ShowRuntime(rt.title, rt.description, rt.icon, lines, () => onClose?.Invoke(true));
    }

    private void TryApplySize(GameObject modalGO, ModalSize size)
    {
        if (!modalGO) return;
        var panel = modalGO.transform as RectTransform;
        if (!panel) panel = modalGO.GetComponentInChildren<RectTransform>(true);
        if (!panel) return;

        var le = panel.GetComponent<UnityEngine.UI.LayoutElement>();
        if (!le) le = panel.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();

        switch (size)
        {
            case ModalSize.Small: le.preferredWidth = 720f; le.preferredHeight = 420f; break;
            case ModalSize.Medium: le.preferredWidth = 960f; le.preferredHeight = 560f; break;
            case ModalSize.Large: le.preferredWidth = 1200f; le.preferredHeight = 680f; break;
        }
    }
}



//using System;
//using System.Collections.Generic;
//using Unity.VisualScripting;
//using UnityEngine;

//public class ModalManager : MonoBehaviour
//{
//    public static ModalManager Instance { get; private set; }

//    [Header("Modals")]
//    [SerializeField] private ConfirmModalUI confirm;     // назначь в инспекторе
//    [SerializeField] private InfoModalUI info;        // назначь в инспекторе
//    [SerializeField] private FreeRewardModalUI freeReward; // назначь в инспекторе

//    private void Awake()
//    {
//        if (Instance && Instance != this) { Destroy(gameObject); return; }
//        Instance = this;

//        // Попробуем автонайти, если не назначили
//        if (!confirm) confirm = FindFirstObjectByType<ConfirmModalUI>(FindObjectsInactive.Include);
//        if (!info) info = FindFirstObjectByType<InfoModalUI>(FindObjectsInactive.Include);
//        if (!freeReward) freeReward = FindFirstObjectByType<FreeRewardModalUI>(FindObjectsInactive.Include);
//    }
//    [Serializable] public enum ModalKind { Confirm, Info, FreeReward }
//    [Serializable] public enum ModalSize { Small, Medium, Large }

//    [Serializable]
//    public class ModalRequest
//    {
//        public ModalKind kind;
//        public ModalSize size = ModalSize.Medium;

//        public string title;
//        public string message;
//        public Sprite picture;

//        public List<CardDef> cards;                  // для Info
//        public List<EventSO.Reward> rewards;         // маппим в EffectRuntime при необходимости
//        public List<FreeRewardDef> freeRewards;      // новый канал: показывать сразу FreeRewardDef через FreeRewardModalUI

//        public bool canCancel = true;
//        public string okText = "Ок";
//        public string cancelText = "Отмена";
//    }



//    /// Унифицированный показ модалки.
//    /// onClose(true) — пользователь подтвердил / нажал Ок; onClose(false) — отменил/закрыл.
//    /// </summary>
//    public void Show(ModalRequest req, Action<bool> onClose)
//    {
//        if (req == null) { onClose?.Invoke(false); return; }

//        switch (req.kind)
//        {
//            case ModalKind.Confirm:
//                ShowConfirm(req, onClose);
//                break;

//            case ModalKind.Info:
//                ShowInfo(req, onClose);
//                break;

//            case ModalKind.FreeReward:
//                ShowFreeReward(req, onClose);
//                break;
//        }
//    }

//    // ───────────── МАППИНГ В СУЩЕСТВУЮЩИЕ ОКНА ─────────────

//    private void ShowConfirm(ModalRequest req, Action<bool> onClose)
//    {
//        if (!confirm)
//        {
//            Debug.LogError("ConfirmModalUI not found");
//            onClose?.Invoke(false);
//            return;
//        }

//        // (опционально) подтвердить размер окна, если ConfirmModalUI предоставит ApplySize
//        TryApplySize(confirm.gameObject, req.size);

//        // Заголовок можно включать в текст (или доработать ConfirmModalUI: добавить поле titleText)
//        string text = string.IsNullOrEmpty(req.title) ? req.message : (req.title + "\n\n" + req.message);

//        if (req.canCancel)
//        {
//            confirm.Show(text,
//                onYes: () => onClose?.Invoke(true),
//                onNo: () => onClose?.Invoke(false));
//        }
//        else
//        {
//            // если «отмены» не предполагается — используем Confirm как «Ок», onNo == null
//            confirm.Show(text, onYes: () => onClose?.Invoke(true), onNo: null);
//        }
//    }

//    private void ShowInfo(ModalRequest req, Action<bool> onClose)
//    {
//        if (!info)
//        {
//            Debug.LogError("InfoModalUI not found");
//            onClose?.Invoke(false);
//            return;
//        }

//        TryApplySize(info.gameObject, req.size);

//        // Если пришли карты — показываем их; иначе просто сообщение
//        if (req.cards != null && req.cards.Count > 0)
//            info.ShowNewCards(string.IsNullOrEmpty(req.title) ? req.message : req.title, req.cards);
//        else
//            info.Show(req.message);

//        // InfoModal закрывается по кнопке «Ок» внутри самого окна → добавь в InfoModalUI вызов onClose, если нужно
//        // Для простоты — подпишемся на кнопку «Ок» тут нельзя (она внутри). Можно сделать отдельный InfoModalUI.OnClosed событие.
//        // На первых порах — просто сообщаем «true» сразу (по факту открытия). При желании — добавлю коллбек закрытия.
//        onClose?.Invoke(true);
//    }

//    private void ShowFreeReward(ModalRequest req, Action<bool> onClose)
//    {
//        if (!freeReward)
//        {
//            Debug.LogError("FreeRewardModalUI not found");
//            onClose?.Invoke(false);
//            return;
//        }

//        TryApplySize(freeReward.gameObject, req.size);

//        // A) Если пришли FreeRewardDef — используем родной UI с очередью:
//        if (req.freeRewards != null && req.freeRewards.Count > 0)
//        {
//            StartCoroutine(ShowFreeRewardDefsQueue(req.freeRewards, onClose));
//            return;
//        }

//        // B) Если пришли EventSO.Reward — сконвертируем в плоские «иконка+подпись»
//        if (req.rewards != null && req.rewards.Count > 0)
//        {
//            var effs = ConvertRewardsToEffects(req.rewards);
//            var runtime = BuildRuntimeFreeReward(req.title, req.message, req.picture, effs);
//            ShowFreeRewardRuntime(runtime, onClose);
//            return;
//        }

//        // C) Пусто — просто заголовок/описание
//        var empty = BuildRuntimeFreeReward(req.title, req.message, req.picture, null);
//        ShowFreeRewardRuntime(empty, onClose);
//    }


//    // ───────────── РАНТАЙМ-ОПИСАНИЕ ДЛЯ FreeReward (без ScriptableObject) ─────────────

//    [Serializable]
//    private class EffectRuntime
//    {
//        public Sprite icon;
//        public string label;
//    }

//    [Serializable]
//    private class FreeRewardRuntime
//    {
//        public Sprite icon;
//        public string title;
//        public string description;
//        public List<EffectRuntime> effects = new();
//    }

//    // Конвертация EventSO.Reward → плоские «иконка+подпись» для FreeReward UI
//    private List<EffectRuntime> ConvertRewardsToEffects(List<EventSO.Reward> rewards)
//    {
//        var list = new List<EffectRuntime>();
//        if (rewards == null) return list;

//        foreach (var r in rewards)
//        {
//            if (r == null) continue;
//            var er = new EffectRuntime();

//            switch (r.type)
//            {
//                case EventSO.RewardType.Resource:
//                    er.icon = (r.resource != null) ? r.resource.icon : null;
//                    er.label = (r.resource != null)
//                        ? $"{r.resource.displayName} +{Mathf.Max(1, r.amount)}"
//                        : $"Ресурс +{Mathf.Max(1, r.amount)}";
//                    break;

//                case EventSO.RewardType.NewCard:
//                    er.icon = (r.cardDef != null) ? r.cardDef.artwork : null;
//                    er.label = (r.cardDef != null)
//                        ? $"{r.cardDef.displayName} ×{Mathf.Max(1, r.cardCount)}"
//                        : $"Новая карта ×{Mathf.Max(1, r.cardCount)}";
//                    break;

//                case EventSO.RewardType.RestoreStat:
//                    // Иконку стата можно не ставить (оставим null) — подписи достаточно
//                    er.icon = null;
//                    er.label = $"Восстановление: {r.stat} +{Mathf.Max(1, r.restoreAmount)}";
//                    break;

//                case EventSO.RewardType.FreeReward:
//                    // Этот тип обычно несёт ScriptableObject с уже готовыми эффектами.
//                    // Его лучше показывать как FreeRewardDef (см. ShowFreeReward ниже), а не здесь.
//                    er.icon = null;
//                    er.label = "Свободная награда";
//                    break;

//                default:
//                    er.icon = null;
//                    er.label = r.type.ToString();
//                    break;
//            }

//            list.Add(er);
//        }
//        return list;
//    }


//    private FreeRewardRuntime BuildRuntimeFreeReward(string defTitle, string defDesc, Sprite icon, List<EffectRuntime> effects)
//    {
//        return new FreeRewardRuntime
//        {
//            icon = icon,
//            title = string.IsNullOrEmpty(defTitle) ? "Награда" : defTitle,
//            description = defDesc ?? "",
//            effects = effects ?? new List<EffectRuntime>()
//        };
//    }

//    private void ShowFreeRewardRuntime(FreeRewardRuntime rt, Action<bool> onClose)
//    {
//        // FreeRewardModalUI в проекте ждёт ScriptableObject (FreeRewardDef).
//        // Добавь (один раз) в FreeRewardModalUI маленький «рантайм»-метод:
//        //
//        // public void ShowRuntime(string title, string desc, Sprite icon, List<(Sprite,string)> lines)
//        // {
//        //     // выставить заголовок/иконку/описание и сгенерировать строки effectsParent
//        // }
//        //
//        // Здесь используем именно его:

//        var lines = new List<(Sprite, string)>();
//        if (rt.effects != null)
//            foreach (var e in rt.effects)
//                lines.Add((e.icon, e.label));

//        // вызов рантайм-оверлоада (см. патч ниже)
//        freeReward.ShowRuntime(rt.title, rt.description, rt.icon, lines, () =>
//        {
//            onClose?.Invoke(true);
//        });
//    }

//    // ───────────── Применение размера (по возможности) ─────────────

//    private void TryApplySize(GameObject modalGO, ModalSize size)
//    {
//        if (!modalGO) return;
//        var panel = modalGO.transform as RectTransform;
//        if (!panel) panel = modalGO.GetComponentInChildren<RectTransform>(true);
//        if (!panel) return;

//        var le = panel.GetComponent<UnityEngine.UI.LayoutElement>();
//        if (!le) le = panel.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();

//        switch (size)
//        {
//            case ModalSize.Small:
//                le.preferredWidth = 720f; le.preferredHeight = 420f; break;
//            case ModalSize.Medium:
//                le.preferredWidth = 960f; le.preferredHeight = 560f; break;
//            case ModalSize.Large:
//                le.preferredWidth = 1200f; le.preferredHeight = 680f; break;
//        }
//    }

//    private System.Collections.IEnumerator ShowFreeRewardDefsQueue(List<FreeRewardDef> defs, Action<bool> onClose)
//    {
//        if (defs == null || defs.Count == 0) { onClose?.Invoke(true); yield break; }

//        ModalGate.Acquire(this);
//        freeReward.ShowMany(defs);                               // используем твой нативный показ очереди
//        yield return null;                                       // кадр на активацию окна
//                                                                 // Ждём закрытия всей очереди:
//        while (freeReward.isActiveAndEnabled) yield return null;
//        ModalGate.Release(this);
//        onClose?.Invoke(true);
//    }

//}
