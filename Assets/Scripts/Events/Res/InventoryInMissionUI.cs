// InventoryInMissionUI.cs
using System.Collections.Generic;           // Dictionary
using System.Collections;                   // ДЛЯ IEnumerator (на случай пинга)  // --- ADDED START ---
using UnityEngine;                          // MonoBehaviour
using UnityEngine.UI;                       // Layout по желанию

public class InventoryInMissionUI : MonoBehaviour
{
    [Header("UI")]
    public Transform contentParent;         // Контейнер для иконок ресурсов в миссии
    public RewardItemUI itemPrefab;         // res_1 prefab (с RewardItemUI)

    private readonly Dictionary<ResourceDef, RewardItemUI> _items = new(); // отображённые элементы

    private readonly Dictionary<ResourceDef, RewardItemUI> _hiddenItems = new(); // Скрытые элементы (ещё не показаны)

    private InventoryController inv;        // ссылка на инвентарь

    private RectTransform _lastChangedSlot; // Кеш: якорь последнего изменённого слота (для анимации приземления)

    private readonly Dictionary<RectTransform, Coroutine> _pingCoBySlot = new(); // Сюда кладём активную корутину пинга по якорю
    private readonly Dictionary<RectTransform, Vector3> _baseScaleBySlot = new(); // Здесь запоминаем «эталонный» scale для слота

    private void Awake()
    {
        inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
    }

    private void OnEnable()
    {
        if (inv == null) inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
        if (inv == null) return;

        // Подписка на изменения
        inv.OnResourceChanged += OnResourceChanged;

        // Первичная синхронизация: создаём/обновляем элементы по текущему состоянию
        InitialSyncFromInventory();
    }

    private void OnDisable()
    {
        if (inv != null) inv.OnResourceChanged -= OnResourceChanged;
    }

    private void EnsureBaseScaleCached(RectTransform rt)               // Гарантирует scale=1 и кэширует «1» как базу
    {
        if (!rt) return;                                               // Защита от null
        rt.localScale = Vector3.one;                                   // Принудительно нормализуем масштаб
        _baseScaleBySlot[rt] = rt.localScale;                          // Кэшируем базовый масштаб для будущих пингов
    }

    private void InitialSyncFromInventory()
    {
        if (contentParent == null || itemPrefab == null) return;

        // (по желанию) можно очистить контейнер, если он переиспользуется между миссиями
        for (int i = contentParent.childCount - 1; i >= 0; i--) Destroy(contentParent.GetChild(i).gameObject);
        _items.Clear();

        if (inv == null || inv.Counts == null) return;

        foreach (var kv in inv.Counts)
        {
            var res = kv.Key;
            var count = kv.Value;
            ApplyResourceVisual(res, count);
        }
    }

    // Реакция на единичное изменение из InventoryController
    private void OnResourceChanged(ResourceDef res, int newTotal)
    {
        ApplyResourceVisual(res, newTotal);
    }

    private void ApplyResourceVisual(ResourceDef res, int count)
    {
        if (res == null || contentParent == null || itemPrefab == null) return; // Защита от null

        if (count <= 0)                                                   // Если ресурс обнулился/удалён
        {
            if (_items.TryGetValue(res, out var vis) && vis)             // Если есть видимый слот
            {
                var rt = vis.transform as RectTransform;                 // --- ADDED: возьмём RT
                _pingCoBySlot.Remove(rt);                                // --- ADDED: уберём следы пинга
                _baseScaleBySlot.Remove(rt);                             // --- ADDED: уберём базу

                Destroy(vis.gameObject);                                  // Уничтожаем его
                _items.Remove(res);                                       // Убираем из словаря
            }
            if (_hiddenItems.TryGetValue(res, out var hid) && hid)       // Если есть скрытый слот
            {
                var rt = hid.transform as RectTransform;                 // --- ADDED: возьмём RT
                _pingCoBySlot.Remove(rt);                                // --- ADDED
                _baseScaleBySlot.Remove(rt);                             // --- ADDED

                Destroy(hid.gameObject);                                  // Уничтожаем его
                _hiddenItems.Remove(res);                                 // Убираем из словаря
            }
            return;                                                       // Готово
        }

        if (_items.TryGetValue(res, out var item) && item)               // Если уже есть ВИДИМЫЙ слот
        {
            var r = new EventSO.Reward                                   // Формируем данные для бинда
            {
                type = EventSO.RewardType.Resource,                      // Тип — ресурс
                resource = res,                                          // Какой ресурс
                amount = count,                                          // Новое количество
                gatedByAdditional = false                                // Без гейтинга
            };
            item.Bind(r);                                                // Обновляем иконку/число
            item.SetGateState(true);                                     // Рамка — «ок»
            _lastChangedSlot = item.transform as RectTransform;          // Запомним якорь последнего изменения

            // нормализуем масштаб и кэшируем базу ---
            var rt = item.transform as RectTransform;                    // Берём RectTransform слота
            EnsureBaseScaleCached(rt);                                   // Принудительно scale=1 и запоминаем базу

            return;                                                      // Готово
        }

        if (_hiddenItems.TryGetValue(res, out var hidden) && hidden)     // Если уже есть СКРЫТЫЙ слот
        {
            var r = new EventSO.Reward                                   // Перебиндим количество (слот всё ещё скрыт)
            {
                type = EventSO.RewardType.Resource,                      // Тип — ресурс
                resource = res,                                          // Ресурс
                amount = count,                                          // Количество
                gatedByAdditional = false                                // Без гейта
            };
            hidden.Bind(r);                                              // Обновляем визуальные данные
            hidden.SetGateState(true);                                   // Рамка — «ок»
                                                                         // НЕ раскрываем — он должен стать видимым ТОЛЬКО по команде аниматора

            // нормализуем масштаб и кэшируем базу ---
            var rt = hidden.transform as RectTransform;                  // Берём RectTransform скрытого слота
            EnsureBaseScaleCached(rt);                                   // Приводим scale=1 и кэшируем базу

            return;                                                      // Готово
        }

        // 3) Ресурса ВООБЩЕ НЕТ в UI → создаём НОВЫЙ СКРЫТЫЙ слот (alpha=0)
        //    Он будет раскрыт ИСКЛЮЧИТЕЛЬНО аниматором через RevealHiddenSlot(...)

        var ui = Instantiate(itemPrefab, contentParent);   // создаём компонент RewardItemUI
        var go = ui.gameObject;                            // берём его GameObject (для CanvasGroup)
        var rNew = new EventSO.Reward
        {
            type = EventSO.RewardType.Resource,
            resource = res,
            amount = count,
            gatedByAdditional = false
        };
        ui.Bind(rNew);                                     // привязать визуал
        ui.SetGateState(true);                             // белая рамка

        // Делаем элемент невидимым и неинтерактивным (появится при RevealHiddenSlot)
        var cg = go.GetComponent<CanvasGroup>();           // ИСКАТЬ НА GameObject
        if (!cg) cg = go.AddComponent<CanvasGroup>();      // ДОБАВИТЬ НА GameObject (НЕ на RewardItemUI)
        cg.alpha = 0f;                                     // скрыт
        cg.interactable = false;                           // не кликабелен
        cg.blocksRaycasts = false;                         // не перехватывает лучи
        // нормализуем масштаб и кэшируем базу ---
        var rtCreated = ui.transform as RectTransform;                   // RT созданного слота
        EnsureBaseScaleCached(rtCreated);                                // Приводим scale=1 и запоминаем базу

        _hiddenItems[res] = ui;                            // регистрируем как СКРЫТЫЙ
    }

    public bool HasVisualForResource(ResourceDef res)
    {
        return res != null && (_items.ContainsKey(res) || _hiddenItems.ContainsKey(res));                   // true, если уже есть видимый слот
    }

    public RectTransform EnsureHiddenSlotForResource(ResourceDef res, int initialCount)
    {
        if (res == null || contentParent == null || itemPrefab == null)  // Проверяем входные данные и ссылки на UI
            return null;                                                 // Если чего-то нет — выходим

        if (_items.TryGetValue(res, out var vis) && vis)                 // Если слот уже существует и видим
            return vis.transform as RectTransform;                       // Возвращаем его якорь

        if (_hiddenItems.TryGetValue(res, out var hid) && hid)           // Если уже есть скрытый слот
            return hid.transform as RectTransform;                       // Возвращаем его якорь

        // Создаём НОВЫЙ элемент слота — ИСПОЛЬЗУЕМ перегрузку Instantiate, которая вернёт RewardItemUI
        var ui = Instantiate(itemPrefab, contentParent);                 // Инстанс компонента RewardItemUI под нужным родителем
        var go = ui.gameObject;                                          // Берём соответствующий GameObject

        // Готовим «данные» для биндинга слота (иконка/число)
        var r = new EventSO.Reward                                       // Создаём временную структуру награды
        {
            type = EventSO.RewardType.Resource,                          // Указываем тип: ресурс
            resource = res,                                              // Сам ресурс
            amount = initialCount,                                       // Предварительное значение (обновится через OnResourceChanged)
            gatedByAdditional = false                                    // Без гейта для инвентаря
        };
        ui.Bind(r);                                                      // Привязываем данные к UI
        ui.SetGateState(true);                                           // Делаем рамку «ок»

        // ИЩЕМ/ДОБАВЛЯЕМ CanvasGroup — ВАЖНО: НА GAMEOBJECT, А НЕ НА КОМПОНЕНТЕ RewardItemUI
        var cg = go.GetComponent<CanvasGroup>();                         // Пытаемся найти CanvasGroup на GO
        if (!cg)                                                         // Если его нет
            cg = go.AddComponent<CanvasGroup>();                         // ДОБАВЛЯЕМ ЕГО НА GO (исправляет вашу ошибку)

        // Делаем слот невидимым и неинтерактивным, чтобы он «появился» только при приземлении анимации
        cg.alpha = 0f;                                                   // Полностью прозрачный
        cg.interactable = false;                                         // Не кликабельный
        cg.blocksRaycasts = false;                                       // Не блокирует клики

        _hiddenItems[res] = ui;                                          // Регистрируем как «скрытый» слот
        return ui.transform as RectTransform;                            // Возвращаем якорь для анимации
    }

    public void RevealHiddenSlot(ResourceDef res)
    {
        if (res == null) return;                                         // Защита от null
        if (_hiddenItems.TryGetValue(res, out var hid) && hid)           // Если есть скрытый слот
        {
            var cg = hid.GetComponent<CanvasGroup>();                    // Берём CanvasGroup
            if (cg)                                                      // Если он есть
            {
                cg.alpha = 1f;                                           // Делаем видимым
                cg.interactable = true;                                  // Разрешаем интеракции (по желанию)
                cg.blocksRaycasts = true;                                 // Разрешаем перехват лучей
            }
            _items[res] = hid;                                           // Переносим в таблицу видимых
            _hiddenItems.Remove(res);                                    // Убираем из скрытых
            _lastChangedSlot = hid.transform as RectTransform;           // Обновляем якорь
            EnsureBaseScaleCached(_lastChangedSlot);                 // Приводим к scale=1 и запоминаем базовый масштаб
        }
    }

    public RectTransform GetSlotAnchorForResource(ResourceDef res)
    {
        if (res != null && _items.TryGetValue(res, out var ui) && ui)    // Если есть видимый
            return ui.transform as RectTransform;                        // Возвращаем его якорь

        if (res != null && _hiddenItems.TryGetValue(res, out var hid) && hid) // Если есть скрытый
            return hid.transform as RectTransform;                        // Возвращаем его якорь (он невидим, но есть)

        return RewardPickupAnimator.Instance != null                      // Иначе — fall-back на правый якорь
            ? RewardPickupAnimator.Instance.rightSideAnchor               // Общая «полка»
            : null;                                                       // Или null
    }


    /// Лёгкая подсветка слота (пульс масштаба)
    public void PingSlot(RectTransform slot)
    {
        if (!slot) return;                                // Защита от null
        if (_pingCoBySlot.TryGetValue(slot, out var running) && running != null) // Если пинг уже идёт
        {
            StopCoroutine(running);                                   // Останавливаем старую корутину
            if (_baseScaleBySlot.TryGetValue(slot, out var bs))       // Если знаем базовый масштаб
                slot.localScale = bs;                                 // Восстанавливаем базу
            else
                slot.localScale = Vector3.one;                        // Иначе вернём к 1
            _pingCoBySlot.Remove(slot);                               // Чистим запись
        }

        if (!_baseScaleBySlot.ContainsKey(slot))                      // Если базы ещё не было
            _baseScaleBySlot[slot] = slot.localScale;                 // Зафиксируем текущий как базовый (должен быть =1)

        var co = StartCoroutine(PingRoutine(slot));                   // Запускаем новую корутину пинга
        _pingCoBySlot[slot] = co;                                     // Запоминаем, что она активна
    }

    private IEnumerator PingRoutine(RectTransform slot)
    {
        if (!slot) yield break;                                       // Защита от null

        //  работаем только вокруг зафиксированной базы, а не «текущего» масштаба ---
        var baseScale = _baseScaleBySlot.TryGetValue(slot, out var bs) ? bs : Vector3.one; // Базовый масштаб (обычно 1)
        float t = 0f;                                                 // Текущее время пика
        float dur = 0.18f;                                            // Длительность одного пульса
        while (t < dur)                                               // Пока идёт пульс
        {
            t += Time.deltaTime;                                      // Тик времени
            float k = Mathf.Sin((t / dur) * Mathf.PI);                // 0→1→0
            float s = 1f + 0.1f * k;                                  // Амплитуда 10% вокруг 1
            if (!slot) yield break;                                   // Защита от уничтожения
            slot.localScale = baseScale * s;                          // Масштабируем относительно базы, НЕ относительно «текущего»
            yield return null;                                        // Кадр
        }
        if (slot) slot.localScale = baseScale;                        // В самом конце строго возвращаем базу
        _pingCoBySlot.Remove(slot);                                    // Снимаем пометку «пинг активен»
    }
}
