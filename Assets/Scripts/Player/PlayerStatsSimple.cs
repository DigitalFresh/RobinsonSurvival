using UnityEngine;                 // Ѕазовые Unity-типы
using TMPro;                       // ƒл€ отображени€ в UI (если хочешь)

public class PlayerStatsSimple : MonoBehaviour
{
    // --- ћј —»ћјЋ№Ќџ≈ «Ќј„≈Ќ»я(можно мен€ть в инспекторе) ---
    [Header("Max values")]
    public int maxHealth = 6;              // максимум жизней
    public int maxEnergy = 25;             // максимум энергии
    public int maxThirst = 6;              // максимум Ђжаждыї (уровень воды)
    public int maxHunger = 6;              // максимум Ђсытостиї

    // --- “≈ ”ў»≈ «Ќј„≈Ќ»я ---
    [Header("Current values (runtime)")]
    [SerializeField] private int health;   // текущее здоровье
    [SerializeField] private int energy;   // текуща€ энерги€
    [SerializeField] private int thirst;   // текущий уровень воды
    [SerializeField] private int hunger;   // текуща€ сытость

    // ѕрочее (оставл€ем совместимость с ранними заглушками наград)
    [SerializeField] private int xp;       // опыт (упрощЄнно)

    // —обытие: кто-то подписан Ч обновим HUD
    public System.Action OnStatsChanged;   // вызывать при любых изменени€х

    private void Awake()                   // »нициализаци€ значений
    {
        health = Mathf.Clamp(health <= 0 ? maxHealth : health, 0, maxHealth); // если не задано Ч старт = максимум
        energy = Mathf.Clamp(energy <= 0 ? maxEnergy : energy, 0, maxEnergy); // стартовое значение энергии
        thirst = Mathf.Clamp(thirst <= 0 ? maxThirst : thirst, 0, maxThirst); // старт Ђводыї
        hunger = Mathf.Clamp(hunger <= 0 ? maxHunger : hunger, 0, maxHunger); // старт Ђсытостиї
        RaiseChanged();                   // сразу обновим HUD
    }

    // --- ѕ”ЅЋ»„Ќџ≈ ѕ–ќ„“≈Ќ»я (если где-то нужно) ---
    public int Health => health;           // текущее здоровье
    public int Energy => energy;           // текуща€ энерги€
    public int Thirst => thirst;           // текуща€ Ђводаї
    public int Hunger => hunger;           // текуща€ сытость


    // --- Ѕј«ќ¬џ≈ ќѕ≈–ј÷»» — ѕј–јћ≈“–јћ» ---
    public void TakeDamage(int dmg)        // получить урон
    {
        health = Mathf.Clamp(health - Mathf.Max(0, dmg), 0, maxHealth); // уменьшаем здоровье
        RaiseChanged();                                                  // сообщаем подписчикам
    }

    public void Heal(int amount)           // вылечитьс€
    {
        health = Mathf.Clamp(health + Mathf.Max(0, amount), 0, maxHealth); // увеличиваем здоровье
        RaiseChanged();                                                      // сообщаем HUD
    }

    public bool SpendEnergy(int amount)    // потратить энергию (вернЄт true, если хватило)
    {
        amount = Mathf.Max(0, amount);                              // защита
        if (energy < amount) return false;                          // не хватает Ч выходим
        energy -= amount;                                           // тратим
        RaiseChanged();                                             // обновим HUD
        return true;                                                // ок
    }

    public void GainEnergy(int amount)     // получить энергию
    {
        energy = Mathf.Clamp(energy + Mathf.Max(0, amount), 0, maxEnergy); // пополн€ем
        RaiseChanged();                                                     // обновим HUD
    }

    public void Drink(int amount)          // утолить жажду
    {
        thirst = Mathf.Clamp(thirst + Mathf.Max(0, amount), 0, maxThirst);  // поднимаем Ђводуї
        RaiseChanged();                                                     // обновим HUD
    }

    public void Eat(int amount)            // поесть (сытость)
    {
        hunger = Mathf.Clamp(hunger + Mathf.Max(0, amount), 0, maxHunger);  // поднимаем Ђсытостьї
        RaiseChanged();                                                     // обновим HUD
    }

    public void ConsumeThirst(int amount)  // расход воды (при меше/перемешке колоды и т.п.)
    {
        thirst = Mathf.Clamp(thirst - Mathf.Max(0, amount), 0, maxThirst);  // уменьшаем воду
        RaiseChanged();                                                     // обновим HUD
    }

    public void ConsumeHunger(int amount)  // расход сытости
    {
        hunger = Mathf.Clamp(hunger - Mathf.Max(0, amount), 0, maxHunger);  // уменьшаем сытость
        RaiseChanged();                                                     // обновим HUD
    }

    // --- —ќ¬ћ≈—“»ћќ—“№ — –јЌЌ»ћ» ¬џ«ќ¬јћ» Ќј√–јƒ (из EventWindowUI) ---
    public void AddFood(int amount)        // награда Ђедаї: здесь упрощЄнно повышаем сытость
    {
        if (amount <= 0) return;           // если ноль Ч ничего
        Eat(amount);                       // используем нашу логику сытости
    }

    public void AddWater(int amount)       // награда Ђводаї: повышаем Ђводуї
    {
        if (amount <= 0) return;           // ноль Ч ничего
        Drink(amount);                     // используем нашу логику жажды
    }

    public void AddXP(int amount)          // награда Ђопытї (просто копим число)
    {
        xp = Mathf.Max(0, xp + amount);    // увеличиваем XP
        // опыт HUD пока не показывает Ч без RaiseChanged()
    }

    // ¬спомогательное: уведомить подписчиков
    private void RaiseChanged()            // вызвать событие
    {
        OnStatsChanged?.Invoke();          // если кто-то подписан Ч сообщаем
    }
}
