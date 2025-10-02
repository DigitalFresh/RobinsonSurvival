using System.Collections.Generic;                        // Списки
using UnityEngine;                                       // MonoBehaviour, Transform, GameObject
using UnityEngine.UI;                                    // Image
using TMPro;                                             // TextMeshProUGUI

// Один «Fighting_block»: враг + зоны + суммы + визуал ран
public class FightingBlockUI : MonoBehaviour
{
    [Header("Hierarchy")]                                 // Ссылки на подузлы
    public Transform EnemyPosition;
    public EnemyView enemyView;                            // Привязчик EnemyPrefab (внутри этого блока)
    public Transform zoneAttack;                           // Attack_cards (контейнер)
    public Transform zoneDefense;                          // Defense_cards (контейнер)
    public TextMeshProUGUI txtShields;                    // PLayer_total/Shields — сумма защиты (кулачков)
    public TextMeshProUGUI txtFist;                       // PLayer_total/Fist — сумма удара (кулачков)
    public GameObject woundsRoot;                         // PLayer_total/Wounds — корневой объект
    public Image woundsImage;                              // PLayer_total/Wounds/Image — картинка фона
    public TextMeshProUGUI woundsText;                    // PLayer_total/Wounds/hits — сколько ран по игроку

    [Header("Wounds blink")]                                   // Группа настроек
    public float woundsBlinkInterval = 0.20f;                  // Интервал мигания (сек)
    private Coroutine _woundsBlinkCo;                          // Текущая корутина мигания (если запущена)


    [Header("Runtime")]                                    // Рантайм-поля
    public EnemySO enemy;                                  // Ассет врага
    public int cachedAttack;                               // Сумма кулачков в зоне Attack (за раунд)
    public int cachedDefense;                              // Сумма кулачков в зоне Defense (за раунд)

    public void BindEnemy(EnemySO so)                      // Привязка врага
    {
        enemy = so;                                        // Запоминаем ассет врага в поле блока (нужно для расчётов)
                                                           // Удалим старую визу (если вдруг была) из точки EnemyPosition
        for (int i = EnemyPosition.childCount - 1; i >= 0; i--) // Перебираем детей EnemyPosition
            Destroy(EnemyPosition.GetChild(i).gameObject); // Выпиливаем, чтобы не плодить дубликаты

        var inst = Instantiate(enemyView, EnemyPosition);  // Инстанцируем префаб EnemyView под нашим слотом
        enemyView = inst;                                  // ВАЖНО: теперь поле enemyView указывает на СЦЕНОВЫЙ инстанс
        if (enemyView) enemyView.Bind(so);                 // Биндим данные врага в UI (выставит HP=maxHP, иконку, тексты)
        ClearZones();                                      // Чистим зоны на старте
        UpdateUI(0);                                       // Сброс визуала ран
    }

    public void ClearZones()                               // Удалить все карты из обеих зон (в руку вернёт CombatController)
    {
        // Просто оставим пустыми — CombatController сам вернёт карты в руку при необходимости
    }

    public void RecountSums()                              // Пересчитать суммы кулачков по зонам
    {
        cachedAttack = SumFistsIn(zoneAttack);             // Посчитать кулачки в атаке
        cachedDefense = SumFistsIn(zoneDefense);           // Посчитать кулачки в защите
        // Обновить текстовые поля
        if (txtFist) txtFist.text = cachedAttack.ToString();     // Печатаем атаку
        if (txtShields) txtShields.text = cachedDefense.ToString(); // Печатаем щиты
        // Предварительно рассчитать раны по игроку (пока без применения)
        int wounds = Mathf.Max(0, (enemy != null ? enemy.attack : 0) - cachedDefense); // Раны = атака - защита
        UpdateUI(wounds);                              // Покажем/скроем узел Wounds
        int damageToEnemy = Mathf.Max(0, cachedAttack - (enemy != null ? enemy.armor : 0)); // Урон по врагу, если жать END сейчас
        if (enemyView) enemyView.PreviewIncomingDamage(damageToEnemy);                      // Запускаем/обновляем мигание
    }

    private int SumFistsIn(Transform container)            // Сумма CardDef.fists у карт в контейнере
    {
        if (!container) return 0;                          // Нет контейнера — 0
        int sum = 0;                                       // Аккумулятор
        var cards = container.GetComponentsInChildren<CardView>(); // Берём все CardView в зоне
        foreach (var cv in cards)                          // Перебор карт
        {
            if (cv && cv.data != null)                    // Проверка
                sum += Mathf.Max(0, cv.data.fists);       // Прибавляем кулачки
        }
        return sum;                                       // Возвращаем результат
    }

    public void OnCardRemovedFromZone(CardView card, CombatZoneType from) // Сообщение от соседней зоны при переносе
    {
        // Пересчитаем суммы (вызывать безопасно хоть каждый раз)
        RecountSums();                                     // Обновить Fist/Shield/Wounds
    }

    private void UpdateUI(int wounds)                      // Отрисовать/скрыть блок «Wounds»
    {
        if (!woundsRoot) return;                           // Нет ссылки — выходим
        if (wounds <= 0)                                   // Нет ран
        {
            // Остановить мигание и вернуть видимость
            if (_woundsBlinkCo != null)                        // Если корутина шла
            {
                StopCoroutine(_woundsBlinkCo);                 // Остановить
                _woundsBlinkCo = null;                         // Сбросить ссылку
            }
            SetWoundsAlpha(1f);                                // Вернуть полную видимость элементов
            woundsRoot.SetActive(false);                       // Спрятать блок
        }
        else                                                   // Раны есть
        {
            woundsRoot.SetActive(true);                        // Включить блок
            if (woundsText)                                    // Если есть текст
                woundsText.text = "-" + wounds.ToString();    // Обновить число

            if (_woundsBlinkCo == null)                        // Если ещё не мигаем
                _woundsBlinkCo = StartCoroutine(WoundsBlinkRoutine()); // Запустить мигание
        }
    }

    public bool Resolve(out int playerWounds, out bool enemyKilled, out List<EventSO.Reward> lootToAnimate)
    {
        // Возвращаем значения вверх
        playerWounds = 0;                                  // Сюда вернём раны по игроку
        enemyKilled = false;                               // Убит ли враг
        lootToAnimate = null;                              // Лут для анимации

        if (enemy == null || enemyView == null) return false; // Без врага — ничего

        // Урон по врагу: (атака игрока - броня), минимум 0
        int damageToEnemy = Mathf.Max(0, cachedAttack - enemy.armor); // Сколько снимем жизней
        // Урон по игроку: (атака врага - защита), минимум 0
        playerWounds = Mathf.Max(0, enemy.attack - cachedDefense);    // Сколько потеряет игрок
        // Снимаем жизни у врага
        if (damageToEnemy > 0)                                        // Если пробили броню
        {
            if (enemyView) enemyView.StopDamagePreview(false);        // Остановить мигание (сам RebuildHearts выполнится ниже)
            enemyView.currentHP = Mathf.Max(0, enemyView.currentHP - damageToEnemy); // Уменьшаем HP
            enemyView.RebuildHearts();                                // Перерисовываем сердца
        }

        // Проверяем смерть
        if (enemyView.currentHP <= 0)                                  // Враг пал
        {
            enemyKilled = true;                                        // Помечаем
            // Готовим лут к анимации (переводим EnemySO.LootEntry → Reward)
            lootToAnimate = new List<EventSO.Reward>();                // Создаём список вознаграждений
            if (enemy.loot != null)
                foreach (var le in enemy.loot)                         // Перебираем записи
                {
                    if (le == null || le.resource == null) continue;   // Пропуск
                    lootToAnimate.Add(new EventSO.Reward               // Формируем Reward
                    {
                        type = EventSO.RewardType.Resource,            // Тип: ресурс
                        resource = le.resource,                         // Сам ресурс
                        amount = Mathf.Max(1, le.amount),              // Сколько
                        gatedByAdditional = false                      // Без гейта
                    });
                }
        }

        return true;                                                   // Разрешение выполнено
    }

    // Установить альфу (прозрачность) одновременно для картинки и текста «Wounds»
    private void SetWoundsAlpha(float a)                       // Хелпер «видимость блока»
    {
        if (woundsImage)                                       // Если есть картинка
        {
            var c = woundsImage.color;                         // Текущий цвет
            c.a = a;                                           // Меняем альфу
            woundsImage.color = c;                             // Применяем
        }
        if (woundsText)                                        // Если есть текст
        {
            var c2 = woundsText.color;                         // Текущий цвет
            c2.a = a;                                          // Меняем альфу
            woundsText.color = c2;                             // Применяем
        }
    }

    // Корутина мигания: попеременно 1 → 0 → 1 → ...
    private System.Collections.IEnumerator WoundsBlinkRoutine()// Собственно мигание
    {
        var wait = new WaitForSeconds(woundsBlinkInterval);
        while (true)                                           // Пока не остановят
        {
            SetWoundsAlpha(1f);                                // Показать (альфа = 1)
            yield return wait; // Подождать
            SetWoundsAlpha(0f);                                // Скрыть (альфа = 0)
            yield return wait; // Подождать
        }
    }
}