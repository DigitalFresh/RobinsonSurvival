#if UNITY_EDITOR                                      // Гарантируем, что код не попадёт в билд
using UnityEditor;                                    // Доступ к AssetDatabase, EditorGUILayout
using UnityEngine;                                    // Доступ к Debug, ScriptableObject
using System.Collections.Generic;                     // Для List
using System.Linq;                                    // Для LINQ (Distinct, OrderBy)
using System.IO;                                      // Для работы с путями

[CustomEditor(typeof(EventCatalog))]                  // Говорим Unity: этот редактор — для EventCatalog
public class EventCatalogEditor : Editor              // Наследуемся от Editor
{
    private SerializedProperty eventsProp;            // Сериализованное свойство списка events
    private string scanFolder = "Assets/Resources/Events"; // Папка по умолчанию для сканирования

    private void OnEnable()                           // Когда редактор активируется
    {
        eventsProp = serializedObject.FindProperty("events"); // Находим сериализованное поле "events"
    }

    public override void OnInspectorGUI()             // Рисуем кастомный инспектор
    {
        serializedObject.Update();                    // Синхронизируем сериализацию

        // Поле ввода папки сканирования
        EditorGUILayout.LabelField("Сканирование папок", EditorStyles.boldLabel); // Заголовок секции
        scanFolder = EditorGUILayout.TextField("Папка:", scanFolder);             // Строка ввода пути

        EditorGUILayout.BeginHorizontal();            // Начинаем горизонтальный блок
        if (GUILayout.Button("Сканировать и заполнить")) // Кнопка сканирования
        {
            ScanAndFill(scanFolder);                  // Запускаем сканирование папки
        }
        if (GUILayout.Button("Очистить список"))     // Кнопка очистки
        {
            eventsProp.ClearArray();                  // Очищаем массив событий
        }
        EditorGUILayout.EndHorizontal();              // Завершаем горизонтальный блок

        EditorGUILayout.Space();                      // Визуальный отступ

        if (GUILayout.Button("Сортировать по имени ассета")) // Кнопка сортировки
        {
            SortByAssetName();                        // Сортировка списка
        }

        if (GUILayout.Button("Авто-назначить пустые eventId (GUID)")) // Кнопка автогенерации ID
        {
            AutoAssignMissingIds();                   // Генерация ID где пусто
        }

        if (GUILayout.Button("Build Index + Validate")) // Кнопка построения индекса и валидации
        {
            var cat = (EventCatalog)target;           // Получаем ссылку на каталог
            cat.BuildIndex();                         // Строим индекс
            Validate(cat);                            // Валидируем
        }

        EditorGUILayout.Space();                      // Отступ

        // Рисуем дефолтный инспектор (покажет список events)
        EditorGUILayout.PropertyField(eventsProp, includeChildren: true); // Отображаем список событий

        serializedObject.ApplyModifiedProperties();   // Применяем изменения
    }

    private void ScanAndFill(string folderPath)       // Заполнить список событиями из папки
    {
        if (!AssetDatabase.IsValidFolder(folderPath)) // Проверяем, существует ли папка
        {
            EditorUtility.DisplayDialog("Ошибка", $"Папка не найдена:\n{folderPath}", "OK"); // Диалог об ошибке
            return;                                   // Выходим
        }

        var guids = AssetDatabase.FindAssets("t:EventSO", new[] { folderPath }); // Находим все GUID ассетов EventSO
        var list = new List<EventSO>();               // Временный список найденных событий

        foreach (var guid in guids)                   // Идём по всем найденным GUID
        {
            var path = AssetDatabase.GUIDToAssetPath(guid); // Получаем путь ассета
            var so = AssetDatabase.LoadAssetAtPath<EventSO>(path); // Загружаем EventSO по пути
            if (so != null) list.Add(so);             // Добавляем в список если успешно загрузили
        }

        // Убираем дубликаты (по ссылке) и заполняем сериализованное поле
        var distinct = list.Distinct().ToList();      // Удаляем дубли
        eventsProp.ClearArray();                      // Очищаем текущее поле
        for (int i = 0; i < distinct.Count; i++)      // Перебираем все найденные
        {
            eventsProp.InsertArrayElementAtIndex(i);  // Добавляем элемент массива
            eventsProp.GetArrayElementAtIndex(i).objectReferenceValue = distinct[i]; // Ставим ссылку на EventSO
        }

        serializedObject.ApplyModifiedProperties();   // Применяем изменения
        EditorUtility.SetDirty(target);               // Помечаем объект как изменённый
        AssetDatabase.SaveAssets();                   // Сохраняем ассет

        Debug.Log($"EventCatalog: добавлено событий — {distinct.Count}"); // Логируем результат
    }

    private void SortByAssetName()                    // Сортировать список по имени ассета
    {
        var cat = (EventCatalog)target;               // Получаем ссылку на EventCatalog
        cat.events = cat.events                       // Достаём список
            .Where(e => e != null)                    // Убираем null
            .OrderBy(e => e.name)                     // Сортируем по имени ассета
            .ToList();                                // Возвращаем в List

        EditorUtility.SetDirty(cat);                  // Помечаем ассет как изменённый
        AssetDatabase.SaveAssets();                   // Сохраняем
        Debug.Log("EventCatalog: отсортирован по имени ассета"); // Лог
    }

    private void AutoAssignMissingIds()               // Назначить GUID там, где eventId пустой
    {
        var cat = (EventCatalog)target;               // Ссылка на каталог
        int changed = 0;                              // Счётчик изменений
        foreach (var e in cat.events)                 // Перебираем события
        {
            if (e == null) continue;                  // Пропускаем пустые
            if (string.IsNullOrEmpty(e.eventId))      // Если ID пустой
            {
                e.eventId = System.Guid.NewGuid().ToString(); // Генерируем GUID
                EditorUtility.SetDirty(e);            // Помечаем EventSO как изменённый
                changed++;                            // Инкрементируем счётчик
            }
        }
        AssetDatabase.SaveAssets();                   // Сохраняем изменения
        Debug.Log($"EventCatalog: назначено новых eventId — {changed}"); // Логируем
    }

    private void Validate(EventCatalog cat)           // Простая валидация каталога
    {
        var seen = new HashSet<string>();             // Множество для проверки уникальности ID
        int errors = 0;                               // Счётчик ошибок
        foreach (var e in cat.events)                 // Перебираем все события
        {
            if (e == null) { Debug.LogError("Пустая ссылка в каталоге"); errors++; continue; } // Ошибка: null

            if (string.IsNullOrEmpty(e.eventId)) { Debug.LogError($"Пустой eventId: {e.name}"); errors++; } // Ошибка: пустой ID
            else if (!seen.Add(e.eventId)) { Debug.LogError($"Дубликат eventId: {e.eventId} ({e.name})"); errors++; } // Ошибка: дубликат ID

            if (e.icon == null) { Debug.LogWarning($"Нет icon у события: {e.name}"); } // Предупреждение: нет иконки
            // Тут можно дописать больше проверок (описание, награды, флаги по типу и т.д.)
        }
        if (errors == 0) Debug.Log("EventCatalog: валидация прошла без ошибок"); // Итог без ошибок
        else Debug.LogError($"EventCatalog: ошибок — {errors}");                 // Итог с количеством ошибок
    }
}
#endif