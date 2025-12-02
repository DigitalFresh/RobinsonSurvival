using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Модалка показа свободных наград. Умеет показывать очередь (несколько подряд).
public class FreeRewardModalUI : MonoBehaviour
{
    public static FreeRewardModalUI Instance;
    public static FreeRewardModalUI Get() => Instance ?? (Instance = FindFirstObjectByType<FreeRewardModalUI>(FindObjectsInactive.Include));

    [Header("Root")]
    public CanvasGroup canvasGroup;       // показать/скрыть окно
    public Button okButton;               // закрыть/далее

    [Header("Header")]
    public Image rewardIcon;              // большая картинка награды
    public TextMeshProUGUI rewardTitle;   // заголовок
    public TextMeshProUGUI rewardDesc;    // описание

    [Header("Effects list")]
    public Transform effectsParent;       // контейнер для строк эффектов
    public GameObject effectLinePrefab; // префаб одной строки (иконка+текст)

    // Очередь на показ (если наград несколько)
    private readonly Queue<FreeRewardDef> queue = new();

    private void Awake()
    {
        Instance = this;
        HideImmediate();
        if (okButton) okButton.onClick.AddListener(OnOk);
    }

    // Показать одну награду
    public void Show(FreeRewardDef def)
    {
        queue.Clear();
        if (def != null) queue.Enqueue(def);
        ShowNextInternal();
    }

    // Показать несколько наград последовательно
    public void ShowMany(List<FreeRewardDef> defs)
    {
        queue.Clear();
        if (defs != null) foreach (var d in defs) if (d) queue.Enqueue(d);
        ShowNextInternal();
    }

    private void ShowNextInternal()
    {
        // если очередь пуста — закрываемся
        if (queue.Count == 0) { Hide(); return; }

        var def = queue.Dequeue();

        // Заголовок/описание/иконка
        if (rewardTitle) rewardTitle.text = string.IsNullOrEmpty(def.title) ? "Награда" : def.title;
        if (rewardDesc) rewardDesc.text = def.description ?? "";
        if (rewardIcon)
        {
            rewardIcon.enabled = (def.icon != null);
            rewardIcon.sprite = def.icon;
        }

        // Перерисовать список эффектов (иконка+подпись)
        if (effectsParent && effectLinePrefab)
        {
            for (int i = effectsParent.childCount - 1; i >= 0; i--)
                Destroy(effectsParent.GetChild(i).gameObject);

            if (def.effects != null)
                foreach (var eff in def.effects)
                {
                    if (!eff) continue;

                    var line = Instantiate(effectLinePrefab, effectsParent);

                    // достаём ссылки из строки
                    Image icon = null;
                    TextMeshProUGUI label = null;
                    line.TryGetComponent(out icon); // если у префаба Image на корне — ок
                    if (!icon)                    // иначе ищем дочерние
                        icon = line.GetComponentInChildren<Image>(true);
                        label = line.GetComponentInChildren<TextMeshProUGUI>(true);

                    // ИКОНКА
                    if (icon)
                    {
                        if (eff.uiIcon != null)
                        {
                            icon.enabled = true;
                            icon.sprite = eff.uiIcon;
                        }
                        else
                        {
                            icon.enabled = false; // нет иконки — скрываем
                        }
                    }

                    // ТЕКСТ
                    if (label)
                    {
                        var txt = !string.IsNullOrWhiteSpace(eff.uiDescription) ? eff.uiDescription : eff.name;
                        label.text = txt;
                    }
                }
        }

        // Показать модалку
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        gameObject.SetActive(true);

       // ModalGate.Acquire(this); // заблокируем внешние клики, как и другие модалки
    }

    // Показ на «голых данных», без ScriptableObject
    public void ShowRuntime(string title, string description, Sprite icon, List<(Sprite icon, string label)> lines, System.Action onOkDone)
    {
        // Заголовок/описание/иконка
        if (rewardTitle) rewardTitle.text = string.IsNullOrEmpty(title) ? "Награда" : title;
        if (rewardDesc) rewardDesc.text = description ?? "";
        if (rewardIcon)
        {
            rewardIcon.enabled = (icon != null);
            rewardIcon.sprite = icon;
        }

        // Список эффектов
        if (effectsParent && effectLinePrefab)
        {
            // Очистить предыдущие элементы
            for (int i = effectsParent.childCount - 1; i >= 0; i--)
                Destroy(effectsParent.GetChild(i).gameObject);

            // Если массив не дан — скрываем блок
            bool hasLines = (lines != null && lines.Count > 0);
            effectsParent.gameObject.SetActive(hasLines);

            if (hasLines && effectLinePrefab)
            {
                foreach (var (ic, txt) in lines)
                {
                    var go = Instantiate(effectLinePrefab, effectsParent);
                    var img = go.GetComponentInChildren<UnityEngine.UI.Image>(true);
                    var tx = go.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                    if (img) { img.enabled = (ic != null); img.sprite = ic; }
                    if (tx) tx.text = txt ?? "";
                }
            }
        }

        // Показать окно
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        gameObject.SetActive(true);

        // Подменим обработчик OK на разовый коллбек
        if (okButton)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(() =>
            {
                Hide();
                onOkDone?.Invoke();
            });
        }
    }

    /// Показать модалку с заголовком/описанием/картинкой и ресурсными строками
    /// (каждая строка — полноценный префаб res_1 с количеством и подписью).
    public void ShowRuntimeResources(
        string title, string description, Sprite icon,
        List<(ResourceDef def, int amount)> resources,
        System.Action onOkDone)
    {
        // Заголовок/описание/картинка
        if (rewardTitle) rewardTitle.text = string.IsNullOrEmpty(title) ? "Награда" : title;
        if (rewardDesc) rewardDesc.text = description ?? "";
        if (rewardIcon)
        {
            rewardIcon.enabled = (icon != null);
            rewardIcon.sprite = icon;
        }

        // Список строк (ресурсы)
        if (effectsParent)
        {
            // чистим старые
            for (int i = effectsParent.childCount - 1; i >= 0; i--)
                Destroy(effectsParent.GetChild(i).gameObject);

            bool hasAny = (resources != null && resources.Count > 0);
            effectsParent.gameObject.SetActive(hasAny);

            if (hasAny && effectLinePrefab)
            {
                foreach (var (def, amount) in resources)
                {
                    var go = Instantiate(effectLinePrefab, effectsParent);
                    var line = go.GetComponent<RewardEffectLineUI>();
                    if (line != null)
                        line.BindResource(def, amount); // <<< ГЛАВНОЕ МЕСТО
                    else
                    {
                        // фолбэк на "иконка+текст", если по какой-то причине нет компонента
                        var img = go.GetComponentInChildren<Image>(true);
                        var tx = go.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (img) { img.enabled = (def && def.icon); img.sprite = def ? def.icon : null; }
                        if (tx) tx.text = def ? $"{def.displayName} ×{amount}" : $"Resource ×{amount}";
                    }
                }
            }
        }

        // Показ окна (по образцу InfoModalUI)
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        gameObject.SetActive(true);

        // Кнопка ОК
        if (okButton)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(() =>
            {
                Hide();
                onOkDone?.Invoke();
            });
        }

        // Если пользуешься глобальным гейтом — можно активировать здесь
        ModalGate.Acquire(this);
    }

    private void OnOk()
    {
        // если есть ещё элементы очереди — покажем следующий
        if (queue.Count > 0) { ShowNextInternal(); return; }
        Hide();
    }

    public void Hide()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        gameObject.SetActive(false);

        ModalGate.Release(this);
    }

    private void HideImmediate()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        gameObject.SetActive(false);
    }
}
