using UnityEngine;                 // Базовые Unity-типы
using TMPro;                       // Для отображения в UI (если хочешь)
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Misc;

public class PlayerStatsSimple : MonoBehaviour
{
    // --- МАКСИМАЛЬНЫЕ ЗНАЧЕНИЯ(можно менять в инспекторе) ---
    [Header("Max values")]
    public int maxHealth = 6;              // максимум жизней
    public int maxEnergy = 25;             // максимум энергии
    public int maxThirst = 6;              // максимум «жажды» (уровень воды)
    public int maxHunger = 6;              // максимум «сытости»

    // --- ТЕКУЩИЕ ЗНАЧЕНИЯ ---
    [Header("Current values (runtime)")]
    [SerializeField] private int health;   // текущее здоровье
    [SerializeField] private int energy;   // текущая энергия
    [SerializeField] private int thirst;   // текущий уровень воды
    [SerializeField] private int hunger;   // текущая сытость

    // Прочее (оставляем совместимость с ранними заглушками наград)
    [SerializeField] private int xp;       // опыт (упрощённо)


    [Header("Animation/FX (optional)")]
    [Tooltip("Иконка ресурса Еды")]
    public Sprite foodIcon;
    [Tooltip("Иконка ресурса Воды")]
    public Sprite waterIcon;
    [Tooltip("Иконка ресурса Энергии")]
    public Sprite energyIcon;
    [Tooltip("Иконка Здоровья")]
    public Sprite healthIcon;

    // Сколько ждать между фазами (настраиваемые паузы)
    [Header("Reshuffle FX timing")]
    [SerializeField, Min(0f)] private float reshuffleInitialDelay = 0.15f; // пауза после «обычных» FX
    [SerializeField, Min(0f)] private float betweenResDelay = 0.12f; // пауза между water и food
    [SerializeField, Min(0f)] private float beforeHpDelay = 0.12f; // пауза перед HP-bounce

    [Header("Game Over / Death Modal")]
    [SerializeField] private string deathModalKey = "death";  // ключ в каталоге
    [SerializeField] private bool restartByReloadScene = true; // true — перезагрузка сцены; false — попробуем через AdventureBuilder
    private bool _deathModalShown;  // защита от повторного вызова

    // Событие: кто-то подписан — обновим HUD
    public System.Action OnStatsChanged;   // вызывать при любых изменениях

    private enum ResourceKind { Food, Water, Energy } // пока только для анимации

    private void Awake()                   // Инициализация значений
    {
        health = Mathf.Clamp(health <= 0 ? maxHealth : health, 0, maxHealth); // если не задано — старт = максимум
        energy = Mathf.Clamp(energy <= 0 ? maxEnergy : energy, 0, maxEnergy); // стартовое значение энергии
        thirst = Mathf.Clamp(thirst <= 0 ? maxThirst : thirst, 0, maxThirst); // старт «воды»
        hunger = Mathf.Clamp(hunger <= 0 ? maxHunger : hunger, 0, maxHunger); // старт «сытости»
        RaiseChanged();                   // сразу обновим HUD

        var deck = FindFirstObjectByType<DeckController>(FindObjectsInactive.Include);
        if (deck) deck.OnDeckReshuffled += HandleDeckReshuffled;

    }

    // --- ПУБЛИЧНЫЕ ПРОЧТЕНИЯ (если где-то нужно) ---
    public int Health => health;           // текущее здоровье
    public int Energy => energy;           // текущая энергия
    public int Thirst => thirst;           // текущая «вода»
    public int Hunger => hunger;           // текущая сытость


    // --- БАЗОВЫЕ ОПЕРАЦИИ С ПАРАМЕТРАМИ ---
    public void TakeDamage(int dmg)        // получить урон
    {
        health = Mathf.Clamp(health - Mathf.Max(0, dmg), 0, maxHealth); // уменьшаем здоровье
        RaiseChanged();                                                  // сообщаем подписчикам
        TryHandleDeath();
    }

    public void Heal(int amount)           // вылечиться
    {
        health = Mathf.Clamp(health + Mathf.Max(0, amount), 0, maxHealth); // увеличиваем здоровье
        RaiseChanged();                                                      // сообщаем HUD
    }

    public int SpendEnergy(int amount)    // потратить энергию (вернёт true, если хватило)
    {
        int done = 0;
        amount = Mathf.Max(0, amount);
        for (int i = 0; i < amount; i++)
        {
            if (energy > 0) { energy = Mathf.Max(0, energy - 1); done++; }
            else { SpendHealthWithBounce(1); if (health <= 0) break; } // HP уходит «в очередь»
        }
        RaiseChanged();
        TryHandleDeath();
        return done;                                                // ок
    }

    public void GainEnergy(int amount)     // получить энергию
    {
        energy = Mathf.Clamp(energy + Mathf.Max(0, amount), 0, maxEnergy); // пополняем
        RaiseChanged();                                                     // обновим HUD
    }

    public void Drink(int amount)          // утолить жажду
    {
        thirst = Mathf.Clamp(thirst + Mathf.Max(0, amount), 0, maxThirst);  // поднимаем «воду»
        RaiseChanged();                                                     // обновим HUD
    }

    public void Eat(int amount)            // поесть (сытость)
    {
        hunger = Mathf.Clamp(hunger + Mathf.Max(0, amount), 0, maxHunger);  // поднимаем «сытость»
        RaiseChanged();                                                     // обновим HUD
    }

    public int ConsumeThirst(int amount)  // расход воды (при меше/перемешке колоды и т.п.)
    {
        int done = 0;
        amount = Mathf.Max(0, amount);
        for (int i = 0; i < amount; i++)
        {
            if (thirst > 0) { thirst = Mathf.Clamp(thirst - 1, 0, maxThirst); done++; }
            else { SpendHealthWithBounce(1); if (health <= 0) break; }
        }
        RaiseChanged();
        TryHandleDeath();
        return done;                                                     // обновим HUD
    }

    public int ConsumeHunger(int amount)  // расход сытости
    {
        int done = 0;                                        // сколько реально сняли «еды»
        amount = Mathf.Max(0, amount);                       // защита от отрицательных
        for (int i = 0; i < amount; i++)
        {
            if (hunger > 0) { hunger = Mathf.Clamp(hunger - 1, 0, maxHunger); done++; }
            else { SpendHealthWithBounce(1); if (health <= 0) break; }
        }
        RaiseChanged();                                      // обновим HUD
        TryHandleDeath();                                    // проверим смерть
        return done;
    }

    // 2) ПЕРЕХОД ЗДОРОВЬЕМ (с анимацией «всплеск в центре и назад в угол»)
    private void SpendHealthWithBounce(int amount)
    {
        // уменьшить здоровье безопасно
        int dmg = Mathf.Max(0, amount);
        if (dmg <= 0) return;
        health = Mathf.Clamp(health - dmg, 0, maxHealth);

        // анимация: иконка HP летит из ЛЕВОГО НИЖНЕГО угла → центр → обратно
        var rfx = RewardPickupAnimator.Instance;
        if (rfx && healthIcon)
        {
            // Левый-низ → центр — как оговаривали
            Vector2 fromScreen = new Vector2(40f, 40f);
            Vector2 midScreen = new Vector2(Screen.width * 0.4f, Screen.height * 0.75f);

            // ВАЖНО: не играть сразу, а поставить «в очередь»
            rfx.EnqueueHealthBounce(dmg, healthIcon, fromScreen, midScreen);
        }
    }

    //// 3) АНИМАЦИИ РАСХОДА ЕДЫ/ВОДЫ/ЭНЕРГИИ (угол → центр → иконка колоды)
    //private void AnimateSpendResource(ResourceKind kind, int count)
    //{
    //    var rfx = RewardPickupAnimator.Instance;             // наш FX-менеджер
    //    var hud = FindFirstObjectByType<DeckHUD>(FindObjectsInactive.Include);
    //    if (rfx == null || hud == null || hud.deckIcon == null) return;

    //    // старт — ЛЕВЫЙ ВЕРХНИЙ угол экрана
    //    Vector2 fromScreen = new Vector2(40f, Screen.height - 40f);
    //    // центр экрана
    //    Vector2 midScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    //    // цель — иконка колоды
    //    RectTransform toRT = hud.deckIcon.rectTransform;

    //    // подбираем спрайт ресурса
    //    Sprite icon = null;
    //    switch (kind)
    //    {
    //        case ResourceKind.Food: icon = foodIcon; break;
    //        case ResourceKind.Water: icon = waterIcon; break;
    //        case ResourceKind.Energy: icon = energyIcon; break;
    //    }
    //    if (!icon) return;

    //    // запускаем красивый полёт (count штук; можно и 1)
    //    rfx.PlayResourceToDeck(count, icon, fromScreen, midScreen, toRT, null);
    //}

    // 4) ОБРАБОТКА СМЕРТИ: открыть модалку и перезапустить сцену
    private void TryHandleDeath()
    {
        // Если игрок ещё жив — ничего не делаем
        if (health > 0) return;

        // Защита от повторного входа
        if (_deathModalShown) return;
        _deathModalShown = true;

        // Запускаем сценарий завершения/перезапуска
        StartCoroutine(DeathSequence());
    }

    private System.Collections.IEnumerator DeathSequence()
    {
        Debug.Log("closed");
        // 1) Получить локализованный контент из каталога
        var provider = ModalContentProvider.Instance;
        var content = new ResolvedModalContent
        {
            title = "Вы погибли",
            description = "Приключение",
            image = null
        };
        if (provider) content = provider.Resolve(deathModalKey);

        // 2) Показать модалку через ModalManager (FreeReward без effect-строк)
        ModalGate.Acquire(this); // блокируем фоновые клики (если используешь этот гейт)
        bool closed = false;
        //Debug.Log("closed" + closed);
        ModalManager.Instance?.Show(new ModalRequest
        {
            kind = ModalKind.FreeReward,
            size = ModalSize.Medium,
            title = content.title,
            message = content.description,
            picture = content.image
            // rewards/freeRewards — не задаём, список эффектов пустой
        }, _ => closed = true);

        // 3) Ждём закрытия модалки
        while (!closed) yield return null;

        // 4) Перезапуск приключения
        if (!restartByReloadScene)
        {
            // Попробуем мягкую пересборку через AdventureBuilder (если он есть в сцене)
            var builder = FindFirstObjectByType<AdventureBuilder>(FindObjectsInactive.Include);
            if (builder)
            {
                builder.BuildAll(); // твой метод пересборки приключения
                _deathModalShown = false; // позволим умереть снова :)
                yield break;
            }
        }

        // Фолбэк: перезагрузка текущей сцены
        var active = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(active);
    }

    private void HandleDeckReshuffled()
    {
        // Запускаем корутину, которая выстроит: (пауза) → water → (пауза) → food → (пауза) → HP
        StartCoroutine(ReshuffleSequence());
    }

    private IEnumerator ReshuffleSequence()
    {
        var fx = RewardPickupAnimator.Instance;
        var hud = FindFirstObjectByType<DeckHUD>(FindObjectsInactive.Include);
        if (fx == null || hud == null || hud.deckIcon == null) yield break;

        // (A) Дай кадр(ы) на "поднятие" _activeFx анимацией карт,
        // чтобы while(IsFxBusy) не промахнулся по гонке того же кадра:
        yield return null;                      // 1 кадр
        yield return new WaitForEndOfFrame();   // ещё один безопасный барьер

        // (B) Дождаться завершения ВСЕГО, что уже играет (карты/ресы/штрафы и т.п.)
        while (fx.IsFxBusy) yield return null;

        // Небольшая пауза дыхания (как и было)
        if (reshuffleInitialDelay > 0f) yield return new WaitForSeconds(reshuffleInitialDelay);

        // (C) ОТКРЫТЬ «скобку» ДО списания еды/воды,
        // чтобы EnqueueHealthBounce() НЕ смог запуститься раньше времени:
        fx.BeginFxBlock();                       // <<< ПЕРЕНЕСЕНО ВВЕРХ

        // 1) Списать показатели — БЕЗ ресурсной анимации (она будет ниже).
        // Если ресурса нет, SpendHealthWithBounce поставит HP в ОЧЕРЕДЬ.
        int waterDone = ConsumeThirst(1);
        int foodDone = ConsumeHunger(1);

        // Геометрия полёта ресурсов
        Vector2 fromScreen = new Vector2(20f, Screen.height - 20f);
        Vector2 midScreen = new Vector2(Screen.width * 0.4f, Screen.height * 0.75f);
        var toRT = hud.deckIcon.rectTransform;

        // 2) WATER первым (если реально списали)
        if (waterDone > 0 && waterIcon)
        {
            bool done = false;
            fx.PlayResourceToDeck(waterDone, waterIcon, fromScreen, midScreen, toRT, () => done = true);
            while (!done) yield return null;                   // строго дождаться конца партии WATER
            if (betweenResDelay > 0f) yield return new WaitForSeconds(betweenResDelay);
        }

        // 3) Потом FOOD (если реально списали)
        if (foodDone > 0 && foodIcon)
        {
            bool done = false;
            fx.PlayResourceToDeck(foodDone, foodIcon, fromScreen, midScreen, toRT, () => done = true);
            while (!done) yield return null;                   // строго дождаться конца партии FOOD
            if (beforeHpDelay > 0f) yield return new WaitForSeconds(beforeHpDelay);
        }

        // 4) Закрыть «скобку»: теперь очередь HP «выплеснется» ТОЛЬКО ПОСЛЕ ресурсов → колода
        fx.EndFxBlock();                                       // <<< остаётся тут

        // 5) На случай летального исхода
        TryHandleDeath();
    }

    public void CheckDeathNow()
    {
        //Debug.Log("health" + health);
        if (health <= 0)
        StartCoroutine(DeathSequence()); // твоя корутина показа модалки смерти и рестарта
    }



    // Вспомогательное: уведомить подписчиков
    private void RaiseChanged()            // вызвать событие
    {
        OnStatsChanged?.Invoke();          // если кто-то подписан — сообщаем
    }
}
