using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ModalCatalogEditorWindow : EditorWindow
{
    // ── Состояние окна ─────────────────────────────────────────────
    [SerializeField] private ModalContentCatalog catalog;     // редактируемый asset
    private Vector2 _leftScroll, _rightScroll, _previewScroll;
    private string _search = "";
    private int _selectedIndex = -1;

    // Для превью
    private SystemLanguage _previewLanguage = SystemLanguage.English;
    private ModalKind _previewKind = ModalKind.Info;
    private ModalSize _previewSize = ModalSize.Medium;

    // Геометрия превью-рамки (примерно как в ModalManager.TryApplySize)
    private static readonly Vector2 PREV_SMALL = new(720, 420);
    private static readonly Vector2 PREV_MEDIUM = new(960, 560);
    private static readonly Vector2 PREV_LARGE = new(1200, 680);

    [MenuItem("Robinson/Modal Catalog Editor")]
    public static void Open()
    {
        var w = GetWindow<ModalCatalogEditorWindow>("Modal Catalog");
        w.minSize = new Vector2(980, 560);
        w.Show();
    }

    private void OnGUI()
    {
        DrawTopBar();
        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawLeftList();
            DrawRightInspector();
        }

        EditorGUILayout.Space(4);
        DrawPreview();
    }

    // ────────────────────────────────────────────────────────────────
    private void DrawTopBar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            // Поле для выбора/создания каталога
            var newCat = (ModalContentCatalog)EditorGUILayout.ObjectField(
                catalog, typeof(ModalContentCatalog), false, GUILayout.Width(position.width - 280));
            if (newCat != catalog) { catalog = newCat; _selectedIndex = -1; }

            // Создать новый
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "Create ModalContentCatalog", "ModalContentCatalog", "asset", "");
                if (!string.IsNullOrEmpty(path))
                {
                    var asset = ScriptableObject.CreateInstance<ModalContentCatalog>();
                    AssetDatabase.CreateAsset(asset, path);
                    AssetDatabase.SaveAssets();
                    catalog = asset; _selectedIndex = -1;
                }
            }

            // Поиск
            GUILayout.FlexibleSpace();
            _search = GUILayout.TextField(_search, GUI.skin.FindStyle("ToolbarSeachTextField") ?? GUI.skin.textField,
                GUILayout.Width(200));
            if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(24)))
                _search = string.Empty;
        }
    }

    private void DrawLeftList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(260, position.width * 0.34f))))
        {
            EditorGUILayout.LabelField("Catalog Entries", EditorStyles.boldLabel);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_leftScroll, GUILayout.ExpandHeight(true)))
            {
                _leftScroll = scroll.scrollPosition;

                if (!catalog)
                {
                    EditorGUILayout.HelpBox("Assign or create ModalContentCatalog asset.", MessageType.Info);
                }
                else
                {
                    var list = FilteredEntries();
                    for (int i = 0; i < list.Count; i++)
                    {
                        var e = list[i];
                        var idx = catalog.entries.IndexOf(e);
                        if (idx < 0) continue;

                        using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
                        {
                            var icon = e.defaultImage ? e.defaultImage.texture
                                                      : Texture2D.grayTexture;
                            GUILayout.Label(icon, GUILayout.Width(32), GUILayout.Height(32));

                            GUIStyle s = (idx == _selectedIndex)
                                ? EditorStyles.whiteBoldLabel
                                : EditorStyles.label;

                            if (GUILayout.Button(string.IsNullOrWhiteSpace(e.key) ? "<no key>" : e.key, s))
                                _selectedIndex = idx;
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = catalog;
                if (GUILayout.Button("+ Add"))
                {
                    Undo.RecordObject(catalog, "Add Entry");
                    catalog.entries.Add(new ModalContentCatalog.Entry { key = "new_key" });
                    EditorUtility.SetDirty(catalog);
                    _selectedIndex = catalog.entries.Count - 1;
                }

                GUI.enabled = catalog && _selectedIndex >= 0 && _selectedIndex < catalog.entries.Count;
                if (GUILayout.Button("− Remove"))
                {
                    Undo.RecordObject(catalog, "Remove Entry");
                    catalog.entries.RemoveAt(_selectedIndex);
                    _selectedIndex = Mathf.Clamp(_selectedIndex - 1, -1, (catalog?.entries.Count ?? 0) - 1);
                    EditorUtility.SetDirty(catalog);
                }

                if (GUILayout.Button("Duplicate"))
                {
                    var src = SafeSelected();
                    if (src != null)
                    {
                        Undo.RecordObject(catalog, "Duplicate Entry");
                        var copy = new ModalContentCatalog.Entry
                        {
                            key = src.key + "_copy",
                            defaultImage = src.defaultImage,
                            variants = src.variants
                                .Select(v => new ModalContentCatalog.Localized
                                {
                                    language = v.language,
                                    title = v.title,
                                    description = v.description,
                                    imageOverride = v.imageOverride
                                }).ToList()
                        };
                        catalog.entries.Add(copy);
                        _selectedIndex = catalog.entries.Count - 1;
                        EditorUtility.SetDirty(catalog);
                    }
                }
                GUI.enabled = true;
            }
        }
    }

    private void DrawRightInspector()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
        {
            EditorGUILayout.LabelField("Entry Inspector", EditorStyles.boldLabel);

            if (!catalog) { GUILayout.FlexibleSpace(); return; }
            var e = SafeSelected();
            if (e == null)
            {
                EditorGUILayout.HelpBox("Select an entry on the left.", MessageType.Info);
                GUILayout.FlexibleSpace();
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_rightScroll, GUILayout.ExpandHeight(true)))
            {
                _rightScroll = scroll.scrollPosition;

                // Ключ (проверяем уникальность)
                EditorGUI.BeginChangeCheck();
                var newKey = EditorGUILayout.DelayedTextField("Key", e.key);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!catalog.entries.Any(x => !ReferenceEquals(x, e) &&
                                                  string.Equals(x.key, newKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        Undo.RecordObject(catalog, "Rename Key");
                        e.key = newKey;
                        EditorUtility.SetDirty(catalog);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Duplicate key", $"Key '{newKey}' already exists.", "OK");
                    }
                }

                // Общая картинка
                EditorGUI.BeginChangeCheck();
                var img = (Sprite)EditorGUILayout.ObjectField("Default Image", e.defaultImage, typeof(Sprite), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(catalog, "Change Default Image");
                    e.defaultImage = img;
                    EditorUtility.SetDirty(catalog);
                }

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Localized Variants", EditorStyles.boldLabel);

                // Кнопки «быстрого» добавления языков
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ RU"))
                        AddVariantIfMissing(e, SystemLanguage.Russian);
                    if (GUILayout.Button("+ EN"))
                        AddVariantIfMissing(e, SystemLanguage.English);
                    if (GUILayout.Button("+ Current (" + Application.systemLanguage + ")"))
                        AddVariantIfMissing(e, Application.systemLanguage);
                    GUILayout.FlexibleSpace();
                }

                // Список вариантов
                for (int i = 0; i < e.variants.Count; i++)
                {
                    var v = e.variants[i];
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // Язык
                        EditorGUI.BeginChangeCheck();
                        var lang = (SystemLanguage)EditorGUILayout.EnumPopup("Language", v.language);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(catalog, "Change Language");
                            v.language = lang;
                            EditorUtility.SetDirty(catalog);
                        }

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("×", GUILayout.Width(24)))
                        {
                            Undo.RecordObject(catalog, "Remove Variant");
                            e.variants.RemoveAt(i);
                            EditorUtility.SetDirty(catalog);
                            EditorGUILayout.EndVertical();
                            break;
                        }
                    }

                    // Заголовок / Текст
                    EditorGUI.BeginChangeCheck();
                    v.title = EditorGUILayout.TextField("Title", v.title);
                    EditorGUILayout.LabelField("Description");
                    v.description = EditorGUILayout.TextArea(v.description, GUILayout.MinHeight(48), GUILayout.MaxWidth(900), GUILayout.ExpandWidth(false));
                    v.imageOverride = (Sprite)EditorGUILayout.ObjectField("Image Override", v.imageOverride, typeof(Sprite), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(catalog, "Edit Variant");
                        EditorUtility.SetDirty(catalog);
                    }

                    EditorGUILayout.EndVertical();
                }
            }
        }
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            _previewLanguage = (SystemLanguage)EditorGUILayout.EnumPopup("Language", _previewLanguage, GUILayout.MaxWidth(360));
            _previewKind = (ModalKind)EditorGUILayout.EnumPopup("Modal Kind", _previewKind, GUILayout.MaxWidth(360));
            _previewSize = (ModalSize)EditorGUILayout.EnumPopup("Size", _previewSize, GUILayout.MaxWidth(280));

            GUILayout.FlexibleSpace();

            // Кнопка «Показать в рантайме» (нужно быть в Play Mode и иметь ModalManager в сцене)
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Show in Play Mode", GUILayout.Width(180)))
                TryShowInPlayMode();
            GUI.enabled = true;
        }

        var e = SafeSelected();
        if (!catalog || e == null)
        {
            EditorGUILayout.HelpBox("Select an entry to preview.", MessageType.Info);
            return;
        }

        // Разрешаем контент вручную (без провайдера) по выбранному языку
        var (title, desc, image) = ResolveForLanguage(e, _previewLanguage);

        // Габариты
        Vector2 boxSize = _previewSize switch
        {
            ModalSize.Small => PREV_SMALL,
            ModalSize.Medium => PREV_MEDIUM,
            ModalSize.Large => PREV_LARGE,
            _ => PREV_MEDIUM
        };

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            using (new GUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(boxSize.x), GUILayout.Height(boxSize.y)))
            {
                // Имитация разных видов: Confirm / Info / FreeReward / Small
                // В упрощённом виде — картинка сверху, заголовок, описание.
                var styleTitle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
                var styleDesc = new GUIStyle(EditorStyles.wordWrappedLabel) { alignment = TextAnchor.UpperLeft };

                var prevRect = GUILayoutUtility.GetRect(boxSize.x - 24, boxSize.y - 24, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                GUI.BeginGroup(prevRect);
                float pad = 8f;
                float y = pad;

                // Картинка (если есть)
                if (image)
                {
                    var tex = image.texture;
                    float w = prevRect.width - pad * 2;
                    float h = Mathf.Min(160, prevRect.height * 0.45f);
                    GUI.DrawTexture(new Rect(pad, y, w, h), tex, ScaleMode.ScaleToFit, true);
                    y += h + pad;
                }

                // Заголовок
                GUI.Label(new Rect(pad, y, prevRect.width - pad * 2, 24), title, styleTitle);
                y += 26;

                // Описание
                var descRect = new Rect(pad, y, prevRect.width - pad * 2, prevRect.height - y - pad);
                GUI.Label(descRect, desc, styleDesc);

                GUI.EndGroup();
            }
            GUILayout.FlexibleSpace();
        }
    }

    // ── Вспомогательное ─────────────────────────────────────────────
    private ModalContentCatalog.Entry SafeSelected()
    {
        if (!catalog) return null;
        if (_selectedIndex < 0 || _selectedIndex >= catalog.entries.Count) return null;
        return catalog.entries[_selectedIndex];
    }

    private List<ModalContentCatalog.Entry> FilteredEntries()
    {
        if (!catalog) return new List<ModalContentCatalog.Entry>();
        var q = _search?.Trim();
        return string.IsNullOrEmpty(q)
            ? catalog.entries
            : catalog.entries.Where(e =>
                (!string.IsNullOrEmpty(e.key) && e.key.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                e.variants.Any(v =>
                    (!string.IsNullOrEmpty(v.title) && v.title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(v.description) && v.description.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                )).ToList();
    }

    private void AddVariantIfMissing(ModalContentCatalog.Entry e, SystemLanguage lang)
    {
        if (e.variants.Any(v => v.language == lang)) return;
        Undo.RecordObject(catalog, "Add Variant");
        e.variants.Add(new ModalContentCatalog.Localized { language = lang, title = e.key, description = "" });
        EditorUtility.SetDirty(catalog);
    }

    private (string title, string desc, Sprite image) ResolveForLanguage(ModalContentCatalog.Entry e, SystemLanguage lang)
    {
        var v = e.variants.FirstOrDefault(x => x.language == lang)
             ?? e.variants.FirstOrDefault(x => x.language == SystemLanguage.English)
             ?? e.variants.FirstOrDefault();
        if (v != null)
            return (string.IsNullOrWhiteSpace(v.title) ? e.key : v.title,
                    v.description ?? "",
                    v.imageOverride ? v.imageOverride : e.defaultImage);
        return (e.key, "", e.defaultImage);
    }

    private void TryShowInPlayMode()
    {
        if (!Application.isPlaying) return;
        var mm = ModalManager.Instance;
        if (!mm) { Debug.LogWarning("ModalManager not found in Play Mode."); return; }

        var e = SafeSelected();
        if (e == null) return;

        var rc = ResolveForLanguage(e, _previewLanguage);

        var req = new ModalRequest
        {
            kind = _previewKind,
            size = _previewSize,
            title = rc.title,
            message = rc.desc,
            picture = rc.image,
            canCancel = (_previewKind == ModalKind.Confirm)
        };

        mm.Show(req, _ => { /* nop */ });
    }
}
