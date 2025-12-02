using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InfoModalUI : MonoBehaviour
{
    public CanvasGroup canvasGroup;        // назначь в инспекторе
    public TextMeshProUGUI messageText;    // заголовок/сообщение
    public Transform cardsParent;          // контейнер для полноценных карточек (CardView)
    public CardView cardPrefab;            // префаб карты (UI)

    public Button okButton;                // закрыть

    // Список raycaster’ов, которые мы временно отключили (чтобы вернуть потом)
    private readonly List<Behaviour> _disabledSceneRaycasters = new();

    public event System.Action OnClosed;   // подпишется ModalManager
    public bool IsOpen => canvasGroup && canvasGroup.interactable;

    void Awake()
    {
        HideImmediate();
        if (okButton)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(HideAndNotify);
        }
    }

    private void HideAndNotify()
    {
        Hide();                 // текущий метод скрытия (оставляем твою логику)
        OnClosed?.Invoke();     // сообщаем подписчикам (ModalManager) о закрытии
    }

    // === Показ полноценных UICard (CardView) по CardDef ===
    public void ShowNewCards(string message, List<CardDef> cardDefs)
    {
        // 0) Чистим старые карточки
        if (cardsParent)
        {
            for (int i = cardsParent.childCount - 1; i >= 0; i--)
                Destroy(cardsParent.GetChild(i).gameObject);
        }

        // 1) Спавним карточки
        if (cardsParent && cardPrefab && cardDefs != null)
        {
            foreach (var def in cardDefs)
            {
                // 1.1) Создаём UI-карту
                var cv = Instantiate(cardPrefab, cardsParent);

                // 1.2) Привязываем «временный» инстанс, чтобы карта отрисовалась как обычно
                var inst = new CardInstance(def);
                cv.Bind(inst);

                // 1.3) Визуально «как в руке» (твоя штатная высота/раскладка)
                cv.SetToHandSize();

                // 1.5) Отключить перехват лучей у корня карточки
                //      (именно это тебе и нужно было: «на UICard отключить Raycast»)
                var cg = cv.canvasGroup ? cv.canvasGroup : cv.GetComponent<CanvasGroup>();
                if (!cg) cg = cv.gameObject.AddComponent<CanvasGroup>(); // если не было — добавим
                cg.blocksRaycasts = false;  // <<< корневой Raycast off
                cg.interactable = false;  // на всякий случай

                // 1.6) Отключить RectMask2D у узла ArtMask
                var artMaskTr = cv.transform.Find("ArtMask");
                if (artMaskTr)
                {
                    var rm = artMaskTr.GetComponent<RectMask2D>();
                    if (rm) rm.enabled = false;      // <<< маску выключаем для превью
                }

                // 1.7) Установить PosY = 0 у изображения арта (объект "artImage")
                //      (в твоём префабе это дочерний Image внутри ArtMask)
                Image artImg = null;
                if (artMaskTr)
                {
                    var artTr = artMaskTr.Find("artImage");
                    if (artTr) artImg = artTr.GetComponent<Image>();
                }
                // если не нашли по имени — используем ссылку из самого CardView
                if (!artImg && cv.artImage) artImg = cv.artImage;

                if (artImg)
                {
                    var artRT = artImg.rectTransform;
                    var ap = artRT.anchoredPosition;
                    ap.y = 0f;                         // <<< PosY = 0
                    artRT.anchoredPosition = ap;

                    artImg.enabled = true;            // на всякий случай — включим рендер
                    artImg.raycastTarget = false;     // и не ловим лучи на арте
                }

                // 1.8) Прячем EyeButton (он в превью не нужен)
                if (cv.eyeDrawButton) cv.eyeDrawButton.gameObject.SetActive(false);
                else
                {
                    var eyeTr = cv.transform.Find("EyeButton");
                    if (eyeTr) eyeTr.gameObject.SetActive(false);
                }

                ////1.9) На всякий случай выключим raycastTarget у всех дочерних график,
                //    // чтобы точно ничто не перехватывало мышь(не обязательно, но полезно)
                //foreach (var img in cv.GetComponentsInChildren<Image>(true))
                //    img.raycastTarget = false;
                //foreach (var tmp in cv.GetComponentsInChildren<TMP_Text>(true))
                //    tmp.raycastTarget = false;
                //foreach (var btn in cv.GetComponentsInChildren<Button>(true))
                //    btn.interactable = false;

                // 1.10) Важно: сам CardView оставляем включённым — нам нужны его биндинги,
                //        но он уже не будет мешать (блокировка лучей на CanvasGroup выше).
            }
        }

        // 2) Сообщение и показ окна
        if (messageText) messageText.text = message ?? "";

        // <<< важная строка: глушим «мировые» клики
        //DisableSceneRaycasters();

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;  // само окно — интерактивно
        canvasGroup.interactable = true;
        gameObject.SetActive(true);

        ModalGate.Acquire(this); // <— включаем глобальную блокировку
    }

    public void Show (string message)
    {
        // Сообщение и показ окна
        if (messageText) messageText.text = message ?? "";

        // <<< важная строка: глушим «мировые» клики
        //DisableSceneRaycasters();

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;  // само окно — интерактивно
        canvasGroup.interactable = true;
        gameObject.SetActive(true);

        ModalGate.Acquire(this); // <— включаем глобальную блокировку
    }


    public void Hide()
    {
        // <<< возвращаем raycaster’ы в исходное состояние
        //RestoreSceneRaycasters();

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        gameObject.SetActive(false);

        ModalGate.Release(this); // <— снимаем глобальную блокировку
    }

    private void HideImmediate()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        gameObject.SetActive(false);

    }

    //private void DisableSceneRaycasters()
    //{
    //    _disabledSceneRaycasters.Clear();

    //    // проходим по всем активным камерам
    //    foreach (var cam in Camera.allCameras)
    //    {
    //        var pr3d = cam.GetComponent<PhysicsRaycaster>();
    //        if (pr3d && pr3d.enabled) { pr3d.enabled = false; _disabledSceneRaycasters.Add(pr3d); }

    //        var pr2d = cam.GetComponent<Physics2DRaycaster>();
    //        if (pr2d && pr2d.enabled) { pr2d.enabled = false; _disabledSceneRaycasters.Add(pr2d); }
    //    }
    //}

    //private void RestoreSceneRaycasters()
    //{
    //    // возвращаем только те, что сами отключали
    //    for (int i = 0; i < _disabledSceneRaycasters.Count; i++)
    //        if (_disabledSceneRaycasters[i]) _disabledSceneRaycasters[i].enabled = true;

    //    _disabledSceneRaycasters.Clear();
    //}

}
