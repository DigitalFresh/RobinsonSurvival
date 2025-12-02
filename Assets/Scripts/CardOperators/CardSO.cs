using UnityEngine; // Доступ к ScriptableObject, Sprite

// Цвет карты (по DD): зелёный, красный, синий
public enum CardColor { Green, Red, Blue } // Перечисление цветов

[CreateAssetMenu(fileName = "CardSO", menuName = "Robinson/Card")] // Пункт меню создания ассета карты
public class CardSO : ScriptableObject // ScriptableObject — контейнер данных карты
{
    [Header("Identity")]                 // Группа в инспекторе
    public string cardId;                // Уникальный ID карты (для сохранений)
    public string displayName;           // Отображаемое имя
    public CardColor color;              // Цвет (Green/Red/Blue)
    public Sprite artwork;               // Спрайт/иконка карты

    [Header("Primary Values")]           // Основные параметры карты
    public int hands;                    // «Ладошки» — для событий
    public int fists;                    // «Кулачки» — для боя
    public int eye = 0;                  // 👁 «Глаз» — сколько карт добрать при сбросе этой карты (0 — нет способности)

    [Header("Tags (simplified)")]        // Упрощённые теги прототипа
    public bool isFood;                  // Еда?
    public bool isMedicine;              // Лекарство?

    private void OnValidate()            // Валидация в редакторе
    {
        if (string.IsNullOrEmpty(cardId)) // Если ID пустой
            cardId = System.Guid.NewGuid().ToString(); // Генерируем GUID
    }
}