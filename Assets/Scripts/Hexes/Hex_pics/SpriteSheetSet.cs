// Assets/Scripts/Adventure/Backdrop/SpriteSheetSet.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpriteSheetSet", menuName = "Robinson/Backdrop/Sprite Sheet Set")]
public class SpriteSheetSet : ScriptableObject
{
    [Header("Display")]
    public string displayName;                 // Человекочитаемое имя набора (для выпадающих списков)

    [Header("Source")]
    public Texture2D sourceSheet;              // Исходная текстура (sliced). Не обязательно, но удобно, чтобы подгружать спрайты.

    [Tooltip("Если включено — при OnValidate заполнит 'sprites' из sourceSheet (LoadAllAssetsAtPath).")]
    public bool autoCollectFromSource = true;  // Автозаполнение спрайтов из sourceSheet

    [Header("Slices")]
    public List<Sprite> sprites = new();       // Собственно кадры этого набора; индекс совпадает с тем, что вы выбираете в редакторе

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!autoCollectFromSource || !sourceSheet) return;      // Если автосборка выключена или нет исходника — выходим
        var path = UnityEditor.AssetDatabase.GetAssetPath(sourceSheet); // Путь до текстуры в проекте
        if (string.IsNullOrEmpty(path)) return;                   // Если путь пустой — дальше нечего делать
        var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path); // Загружаем все саб-ассеты по этому пути
        sprites.Clear();                                          // Очищаем текущий список
        // Собираем только спрайты и сортируем по имени (обычно Unity режет их с порядковыми именами)
        foreach (var a in all) if (a is Sprite s) sprites.Add(s);
        sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));// Стабильный порядок превью в редакторе
    }
#endif
}
