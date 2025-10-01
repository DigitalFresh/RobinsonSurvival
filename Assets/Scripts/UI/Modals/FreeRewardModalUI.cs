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
