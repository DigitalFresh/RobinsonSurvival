using UnityEngine;

/// Провайдер контента модалок: берёт нужный Entry из каталога и выбирает язык.
/// Можно держать на любом объекте в сцене (например, на UI Root).
/// </summary>
public class ModalContentProvider : MonoBehaviour
{
    public static ModalContentProvider Instance { get; private set; }

    [SerializeField] private ModalContentCatalog catalog;  // назначь созданный asset
    [SerializeField] private SystemLanguage fallbackLanguage = SystemLanguage.English; // запасной язык

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// Вернуть локализованный контент по ключу (например "death").
    /// Алгоритм: current → fallback → первый доступный вариант → заглушки.
    /// </summary>
    public ResolvedModalContent Resolve(string key)
    {
        var result = new ResolvedModalContent { title = "—", description = "", image = null };

        if (!catalog)
            return result;

        var entry = catalog.Find(key);
        if (entry == null)
            return result;

        // 1) Пытаемся найти по текущему языку системы
        var lang = Application.systemLanguage;
        var v = entry.variants.Find(x => x.language == lang);

        // 2) Фолбэк: указанный в настройках
        if (v == null)
            v = entry.variants.Find(x => x.language == fallbackLanguage);

        // 3) Фолбэк: просто первый элемент
        if (v == null && entry.variants.Count > 0)
            v = entry.variants[0];

        // Заполняем результат
        if (v != null)
        {
            result.title = string.IsNullOrWhiteSpace(v.title) ? entry.key : v.title;
            result.description = v.description ?? "";
            result.image = v.imageOverride ? v.imageOverride : entry.defaultImage;
        }
        else
        {
            // Нет варианта вообще — берём хотя бы общую картинку
            result.title = entry.key;
            result.description = "";
            result.image = entry.defaultImage;
        }

        return result;
    }
}
