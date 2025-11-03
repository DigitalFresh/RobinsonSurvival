#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static AdventureAsset; // AdventureCell, SpritePickRule

/// Adventure Map Editor (улучшенная версия):
///  • Terrain: цветные кисти + Selection.
///  • Backdrop Sprites: CSV или выбор миниатюр из Sprite Sheet.
///  • Event Catalog: ID + «человеческое имя», автоподгон по высоте.
public class AdventureMapEditorWindow : EditorWindow
{
    [SerializeField] private AdventureAsset asset;

    // Индекс ячеек ассета и временные «просмотры»
    private Dictionary<(int x, int y), AdventureCell> _cells;
    private Dictionary<(int x, int y), AdventureCell> _temp;
    private AdventureAsset _lastAsset;

    // Канвас
    private Vector2 _canvasScroll;
    [SerializeField] private float _hexRadius = 48f;
    [SerializeField] private float _zoom = 1f;
    [SerializeField] private float _padX = 80f, _padY = 80f;

    // Палитры
    private enum BrushMode { Terrain, Event, Visibility, Barriers }
    private BrushMode _brush = BrushMode.Terrain;

    private enum Tool { Paint, Eyedropper, RectFill }
    private Tool _tool = Tool.Paint;

    // Terrain: кисти + Selection
    private enum TerrainPick { Select, Empty, Event, Blocked, Start, Exit }
    private TerrainPick _terrainPick = TerrainPick.Select;

    private bool _paintVisible = true;

    // События
    private List<EventSO> _allEvents;
    private string _eventFilter = "";
    private Vector2 _eventScroll;
    private EventSO _selectedEvent;

    // Барьеры
    private enum BarrierOp { Add, RemoveFirst, Clear }
    private BarrierOp _barOp = BarrierOp.Add;
    [SerializeField] private int _barPaintValue = 1; // 1 или 3
    [SerializeField] private Sprite _barIcon1;
    [SerializeField] private Sprite _barIcon3;

    // Backdrop thumbnails (опционально): укажите разрезанный Sprite Sheet
    [SerializeField] private Texture2D _spriteSheet;
    private Sprite[] _sheetSprites;     // кэш спрайтов из sheet
    private Vector2 _thumbScroll;       // скролл превью

    // Добавьте ссылку на каталог наборов:
    [SerializeField] private SpriteSheetCatalog _catalog; // перетащите сюда в инспекторе окно редактора

    // Стили
    private GUIStyle _centerMiniLabel;
    private readonly Color _outlineColor = new Color(0, 0, 0, 0.9f);
    private readonly Dictionary<HexTerrainType, Color> _terrainColor = new Dictionary<HexTerrainType, Color>
    {
        { HexTerrainType.Empty,   new Color(0.15f,0.15f,0.18f) },
        { HexTerrainType.Event,   new Color(0.96f,0.62f,0.24f) },
        { HexTerrainType.Blocked, new Color(0.40f,0.40f,0.40f) },
        { HexTerrainType.Start,   new Color(0.20f,0.65f,1.00f) },
        { HexTerrainType.Exit,    new Color(0.62f,0.45f,0.95f) },
    };

    [MenuItem("Robinson/Adventure Map Editor")]
    public static void Open()
    {
        var w = GetWindow<AdventureMapEditorWindow>("Adventure Map Editor");
        w.minSize = new Vector2(980, 620);
        w.Show();
    }

    private void OnEnable()
    {
        _centerMiniLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false
        };

        RefreshEventCatalog();
        RebuildCellIndex();
        LoadSheetSprites();
    }

    private void RefreshEventCatalog()
    {
        var guids = AssetDatabase.FindAssets("t:EventSO");
        _allEvents = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<EventSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(e => e != null)
            .OrderBy(e => e.name)
            .ToList();
    }

    private void RebuildCellIndex()
    {
        _cells = new Dictionary<(int x, int y), AdventureCell>();
        _temp = new Dictionary<(int x, int y), AdventureCell>();
        if (!asset) return;
        asset.cells ??= new List<AdventureCell>();
        asset.cells.RemoveAll(c => c == null);
        foreach (var c in asset.cells) _cells[(c.x, c.y)] = c;
    }

    private void OnGUI()
    {
        if (_lastAsset != asset) { _lastAsset = asset; RebuildCellIndex(); }
        using (new EditorGUILayout.HorizontalScope()) DrawTopBar();
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPalette();
            DrawCanvas();
            DrawInspector();
        }
    }

    private void DrawTopBar()
    {
        asset = (AdventureAsset)EditorGUILayout.ObjectField("Adventure Asset", asset, typeof(AdventureAsset), false);
        if (!asset)
        {
            EditorGUILayout.HelpBox("Назначьте AdventureAsset.", MessageType.Info);
            return;
        }

        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.HorizontalScope())
        {
            asset.displayName = EditorGUILayout.TextField("Display Name", asset.displayName);
            asset.version = EditorGUILayout.IntField("Version", asset.version, GUILayout.MaxWidth(220));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            asset.width = EditorGUILayout.IntSlider("Width", Mathf.Max(1, asset.width), 1, 100);
            asset.height = EditorGUILayout.IntSlider("Height", Mathf.Max(1, asset.height), 1, 100);
        }
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(asset, "Edit asset props");
            EditorUtility.SetDirty(asset);
            RebuildCellIndex();
            Repaint();
        }
        _catalog = (SpriteSheetCatalog)EditorGUILayout.ObjectField("Sheet Catalog", _catalog, typeof(SpriteSheetCatalog), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Ensure Missing (Empty)", GUILayout.Width(170)))
            { Undo.RecordObject(asset, "EnsureMissingEmpty"); EnsureMissingCellsEmpty(asset); EditorUtility.SetDirty(asset); RebuildCellIndex(); }

            if (GUILayout.Button("Reset Map (clear & fill Empty)", GUILayout.Width(230)))
            {
                if (EditorUtility.DisplayDialog("Сбросить карту?",
                    "Удалит все клетки и создаст сетку заново как Empty.", "Да", "Отмена"))
                { Undo.RecordObject(asset, "ResetMap"); ResetAllCellsToEmpty(asset); EditorUtility.SetDirty(asset); RebuildCellIndex(); }
            }

            if (GUILayout.Button("Validate", GUILayout.Width(100))) ValidateAndReport();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Apply to Scene (Builder)", GUILayout.Width(180))) ApplyToScene();
        }
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    private void DrawPalette()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(450)))
        {
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
            _brush = (BrushMode)GUILayout.Toolbar((int)_brush, new[] { "Terrain", "Event", "Visibility", "Barriers" });
            EditorGUILayout.Space(6);
            _tool = (Tool)GUILayout.Toolbar((int)_tool, new[] { "Paint", "Eyedropper", "Rect Fill" });
            EditorGUILayout.Space(8);

            if (_brush == BrushMode.Terrain)
            {
                EditorGUILayout.LabelField("Terrain Brush", EditorStyles.miniBoldLabel);

                // Кисти: Selection + типы
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ToggleBrush(TerrainPick.Select, "Selection", new Color(.85f, .85f, .9f))) _terrainPick = TerrainPick.Select;
                    if (ToggleBrush(TerrainPick.Empty, "Empty", _terrainColor[HexTerrainType.Empty])) _terrainPick = TerrainPick.Empty;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ToggleBrush(TerrainPick.Event, "Event", _terrainColor[HexTerrainType.Event])) _terrainPick = TerrainPick.Event;
                    if (ToggleBrush(TerrainPick.Blocked, "Blocked", _terrainColor[HexTerrainType.Blocked])) _terrainPick = TerrainPick.Blocked;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ToggleBrush(TerrainPick.Start, "Start", _terrainColor[HexTerrainType.Start])) _terrainPick = TerrainPick.Start;
                    if (ToggleBrush(TerrainPick.Exit, "Exit", _terrainColor[HexTerrainType.Exit])) _terrainPick = TerrainPick.Exit;
                }

                EditorGUILayout.Space(8);
                DrawLegend();
            }
            else if (_brush == BrushMode.Event)
            {
                EditorGUILayout.LabelField("Event Catalog", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _eventFilter = EditorGUILayout.TextField(_eventFilter);
                    if (GUILayout.Button("⟳", GUILayout.Width(28))) RefreshEventCatalog();
                }

                // высота подстраивается под окно
                float avail = Mathf.Max(200f, position.height - 360f);
                using (var sv = new EditorGUILayout.ScrollViewScope(_eventScroll, GUILayout.Height(avail)))
                {
                    _eventScroll = sv.scrollPosition;
                    var filtered = string.IsNullOrWhiteSpace(_eventFilter)
                        ? _allEvents
                        : _allEvents.Where(e => NiceEventTitle(e).IndexOf(_eventFilter, StringComparison.OrdinalIgnoreCase) >= 0
                                             || e.name.IndexOf(_eventFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                    if (GUILayout.Button("— None (clear) —")) _selectedEvent = null;

                    foreach (var ev in filtered)
                    {
                        var isSel = _selectedEvent == ev;
                        using (new EditorGUILayout.HorizontalScope(isSel ? "SelectionRect" : GUIStyle.none))
                        {
                            var spr = ev.icon;
                            var iconRect = GUILayoutUtility.GetRect(30, 30, GUILayout.Width(30), GUILayout.Height(30));
                            if (spr && spr.texture) GUI.DrawTextureWithTexCoords(iconRect, spr.texture, GetSpriteUV(spr), true);

                            // ID + красивое имя
                            string label = NiceEventTitle(ev) + " — " + ev.name;
                            var style = new GUIStyle(GUI.skin.button);
                            if (ev.isCombat) style.normal.textColor = new Color(1f, .25f, .25f);
                            else if (ev.isChoice) style.normal.textColor = new Color(.7f, .4f, 1f);
                            else if (ev.isResource) style.normal.textColor = Color.white;

                            if (GUILayout.Button(label, style)) _selectedEvent = ev;
                            if (GUILayout.Button("Open", GUILayout.Width(48))) Selection.activeObject = ev;
                            if (GUILayout.Button("Ping", GUILayout.Width(44))) EditorGUIUtility.PingObject(ev);                        
                        }
                    }
                }
            }
            else if (_brush == BrushMode.Barriers)
            {
                _barOp = (BarrierOp)GUILayout.Toolbar((int)_barOp, new[] { "Add", "+Remove 1st", "Clear" });
                using (new EditorGUI.DisabledScope(_barOp != BarrierOp.Add))
                {
                    EditorGUILayout.LabelField("Value to Add", EditorStyles.miniBoldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Toggle(_barPaintValue == 1, "+1", "Button")) _barPaintValue = 1;
                        if (GUILayout.Toggle(_barPaintValue == 3, "+3", "Button")) _barPaintValue = 3;
                    }
                }
                _barIcon1 = (Sprite)EditorGUILayout.ObjectField("Icon +1 (optional)", _barIcon1, typeof(Sprite), false);
                _barIcon3 = (Sprite)EditorGUILayout.ObjectField("Icon +3 (optional)", _barIcon3, typeof(Sprite), false);
            }
            else
            {
                _paintVisible = EditorGUILayout.ToggleLeft("Visible = true", _paintVisible);
            }

            EditorGUILayout.Space(6);
            _hexRadius = EditorGUILayout.Slider("Hex Radius (px)", _hexRadius, 16, 96);
            _zoom = EditorGUILayout.Slider("Zoom", _zoom, 0.5f, 2f);
            _padX = EditorGUILayout.Slider("Canvas Pad X", _padX, 0, 200);
            _padY = EditorGUILayout.Slider("Canvas Pad Y", _padY, 0, 200);
        }
    }

    private bool ToggleBrush(TerrainPick pick, string caption, Color c)
    {
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = c;
        bool pressed = GUILayout.Toggle(_terrainPick == pick, caption, "Button", GUILayout.Height(24));
        GUI.backgroundColor = prev;
        return pressed;
    }

    private void DrawLegend()
    {
        EditorGUILayout.Space(6);
        foreach (var kv in _terrainColor)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var c = kv.Value; c.a = 1f;
                var rect = GUILayoutUtility.GetRect(18, 14, GUILayout.Width(24));
                EditorGUI.DrawRect(rect, c);
                EditorGUILayout.LabelField(kv.Key.ToString());
            }
        }
    }

    private AdventureCell _hoverCell;
    private (int x, int y)? _hoverCoord;

    private void DrawInspector()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(330)))
        {
            EditorGUILayout.LabelField("Cell Inspector", EditorStyles.boldLabel);

            if (_hoverCoord == null)
            {
                EditorGUILayout.HelpBox("Наведите/кликните на гекс на канвасе.", MessageType.Info);
                return;
            }

            var (hx, hy) = _hoverCoord.Value;
            var exists = _cells.TryGetValue((hx, hy), out var real);
            var view = exists ? real : GetTempViewCell(hx, hy);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Cell: (" + hx + "," + hy + ")", EditorStyles.miniBoldLabel);
            view.visible = EditorGUILayout.ToggleLeft("Visible", view.visible);
            view.terrain = (HexTerrainType)EditorGUILayout.EnumPopup("Terrain", view.terrain);
            view.eventAsset = (EventSO)EditorGUILayout.ObjectField("Event", view.eventAsset, typeof(EventSO), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (!exists) { CommitTempToAsset(hx, hy, view); exists = true; real = _cells[(hx, hy)]; }
                real.visible = view.visible;
                real.terrain = view.terrain;
                real.eventAsset = view.eventAsset;
                if (real.terrain == HexTerrainType.Empty || real.terrain == HexTerrainType.Blocked) real.eventAsset = null;
                if (real.eventAsset != null) real.terrain = HexTerrainType.Event;
                Undo.RecordObject(asset, "Edit Cell"); EditorUtility.SetDirty(asset); Repaint();
            }

            // Barriers
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Barriers", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+1", GUILayout.Width(40)))
                {
                    if (!exists) { CommitTempToAsset(hx, hy, view); exists = true; real = _cells[(hx, hy)]; }
                    real.barriers ??= new List<int>();
                    if (real.barriers.Count < 3) real.barriers.Add(1);
                    Undo.RecordObject(asset, "Add +1"); EditorUtility.SetDirty(asset); Repaint();
                }

                if (GUILayout.Button("+3", GUILayout.Width(40)))
                {
                    if (!exists) { CommitTempToAsset(hx, hy, view); exists = true; real = _cells[(hx, hy)]; }
                    real.barriers ??= new List<int>();
                    if (real.barriers.Count < 3) real.barriers.Add(3);
                    Undo.RecordObject(asset, "Add +3"); EditorUtility.SetDirty(asset); Repaint();
                }

                if (GUILayout.Button("Remove 1st", GUILayout.Width(90)))
                {
                    if (exists && real.barriers != null && real.barriers.Count > 0) real.barriers.RemoveAt(0);
                    Undo.RecordObject(asset, "Remove 1st"); EditorUtility.SetDirty(asset); Repaint();
                }

                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    if (exists && real.barriers != null) real.barriers.Clear();
                    Undo.RecordObject(asset, "Clear"); EditorUtility.SetDirty(asset); Repaint();
                }
            }

            string cur = (exists && real.barriers != null && real.barriers.Count > 0)
                ? string.Join(", ", real.barriers.Select(v => v >= 3 ? "3" : "1"))
                : "—";
            EditorGUILayout.LabelField("Current: " + cur + " (max 3)");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (view.eventAsset != null && GUILayout.Button("Ping Event")) EditorGUIUtility.PingObject(view.eventAsset);
                if (view.eventAsset != null && GUILayout.Button("Open Event")) Selection.activeObject = view.eventAsset;
            }

            if (!view.visible)
                EditorGUILayout.HelpBox("Эта клетка невидима.", MessageType.Warning);

            // Backdrop Sprites (CSV + thumbnails)
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Backdrop Sprites", EditorStyles.boldLabel);

            _spriteSheet = (Texture2D)EditorGUILayout.ObjectField("Sprite Sheet (sliced)", _spriteSheet, typeof(Texture2D), false);
            if (GUI.changed) LoadSheetSprites();

            void DrawPickWithSet(
    string title,
    System.Func<SpriteSheetSet> getSet,         // как прочитать текущее значение
    System.Action<SpriteSheetSet> setSet,       // как записать новое значение
    AdventureAsset.SpritePickRule ruleRef       // правило подложки (класс — менять поля можно напрямую)
)
            {
                if (ruleRef == null) return;

                var setRef = getSet(); // читаем текущее

                using (new EditorGUILayout.VerticalScope("HelpBox"))
                {
                    EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);

                    // 1) Выбор набора напрямую или из каталога
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Set", GUILayout.Width(40));
                        var newSet = (SpriteSheetSet)EditorGUILayout.ObjectField(setRef, typeof(SpriteSheetSet), false);
                        if (newSet != setRef)
                        {
                            Undo.RecordObject(asset, "Pick SpriteSheetSet");
                            setSet(newSet);
                            setRef = newSet;
                            EditorUtility.SetDirty(asset);
                            Repaint();
                        }

                        if (_catalog && GUILayout.Button("Pick from Catalog", GUILayout.Width(150)))
                        {
                            var menu = new GenericMenu();
                            for (int i = 0; i < _catalog.sets.Count; i++)
                            {
                                var index = i;
                                var set = _catalog.sets[index];
                                var label = (set ? set.displayName : "(null)") + $"  [{index}]";
                                bool on = setRef == set;
                                menu.AddItem(new GUIContent(label), on, () =>
                                {
                                    Undo.RecordObject(asset, "Pick from Catalog");
                                    setSet(set);              // <- просто вызываем setter, никаких ref
                                    EditorUtility.SetDirty(asset);
                                    Repaint();
                                });
                            }
                            menu.ShowAsContext();
                        }
                    }

                    // 2) Сетка превью из выбранного Set (если есть)
                    if (setRef && setRef.sprites != null && setRef.sprites.Count > 0)
                    {
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField("Pick from Set:");

                        int cols = 6; float sz = 40f;
                        int total = setRef.sprites.Count;
                        int rows = Mathf.CeilToInt(total / (float)cols);

                        for (int r = 0; r < rows; r++)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                for (int c = 0; c < cols; c++)
                                {
                                    int i = r * cols + c;
                                    if (i >= total) { GUILayout.FlexibleSpace(); continue; }

                                    var s = setRef.sprites[i];
                                    var rect = GUILayoutUtility.GetRect(sz, sz, GUILayout.Width(sz), GUILayout.Height(sz));
                                    if (s && s.texture) GUI.DrawTextureWithTexCoords(rect, s.texture, GetSpriteUV(s), true);

                                    bool isOn = (ruleRef.fixedIndex == i) ||
                                                (ruleRef.fixedIndex < 0 && ruleRef.pool != null && ruleRef.pool.Contains(i));

                                    var tRect = new Rect(rect.xMax - 16, rect.yMin, 16, 16);
                                    bool newOn = GUI.Toggle(tRect, isOn, GUIContent.none);
                                    if (newOn != isOn)
                                    {
                                        Undo.RecordObject(asset, "Edit Backdrop picks");
                                        bool multi = Event.current.control || Event.current.command;

                                        if (multi)
                                        {
                                            ruleRef.fixedIndex = -1;
                                            ruleRef.pool ??= new System.Collections.Generic.List<int>();
                                            if (newOn) { if (!ruleRef.pool.Contains(i)) ruleRef.pool.Add(i); }
                                            else { ruleRef.pool.Remove(i); }
                                        }
                                        else
                                        {
                                            if (newOn) { ruleRef.fixedIndex = i; ruleRef.pool = new System.Collections.Generic.List<int>(); }
                                            else { ruleRef.fixedIndex = -1; ruleRef.pool = new System.Collections.Generic.List<int>(); }
                                        }

                                        EditorUtility.SetDirty(asset);
                                        Repaint();
                                    }
                                }
                            }
                        }

                        EditorGUILayout.HelpBox("Клик — одиночный (Fixed). Ctrl/Cmd — множественный (Pool).", MessageType.None);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Назначьте SpriteSheetSet (или выберите из каталога).", MessageType.Info);
                    }

                    // 3) CSV-пул (по желанию)
                    string csv = string.Join(",", (ruleRef.pool ??= new System.Collections.Generic.List<int>()));
                    string newCsv = EditorGUILayout.TextField(new GUIContent("Pool CSV (если Fixed < 0)"), csv);
                    if (newCsv != csv)
                    {
                        var arr = ParseCsvInts(newCsv);
                        ruleRef.pool = new System.Collections.Generic.List<int>(arr);
                        Undo.RecordObject(asset, "Edit Backdrop CSV");
                        EditorUtility.SetDirty(asset);
                    }

                    // Fixed Index числом
                    ruleRef.fixedIndex = EditorGUILayout.IntField(new GUIContent("Fixed Index (-1 = use pool)"), ruleRef.fixedIndex);
                }
            }

            //void DrawPick(string title, ref SpritePickRule rule)
            //{
            //    if (rule == null) rule = new SpritePickRule();
            //    using (new EditorGUILayout.VerticalScope("HelpBox"))
            //    {
            //        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            //        rule.fixedIndex = EditorGUILayout.IntField(new GUIContent("Fixed Index (-1 = random)"), rule.fixedIndex);

            //        // CSV-пул
            //        string csv = string.Join(",", (rule.pool ??= new List<int>()));
            //        string newCsv = EditorGUILayout.TextField(new GUIContent("Pool CSV (если Fixed < 0)"), csv);
            //        if (newCsv != csv)
            //        {
            //            var arr = ParseCsvInts(newCsv);
            //            rule.pool = arr.ToList();
            //            Undo.RecordObject(asset, "Edit Backdrop CSV");
            //            EditorUtility.SetDirty(asset);
            //        }

            //        // Визуальный выбор (если sheet указан)
            //        if (_sheetSprites != null && _sheetSprites.Length > 0)
            //        {
            //            EditorGUILayout.LabelField("Pick from Sheet:");
            //            using (var sv = new EditorGUILayout.ScrollViewScope(_thumbScroll, GUILayout.Height(140)))
            //            {
            //                _thumbScroll = sv.scrollPosition;

            //                // Сетка превью
            //                int cols = 6;                  // столбцов в ряду
            //                float sz = 40f;                // размер иконки
            //                int total = _sheetSprites.Length;
            //                int rows = Mathf.CeilToInt(total / (float)cols);

            //                for (int r = 0; r < rows; r++)
            //                {
            //                    using (new EditorGUILayout.HorizontalScope())
            //                    {
            //                        for (int c = 0; c < cols; c++)
            //                        {
            //                            int i = r * cols + c;
            //                            if (i >= total) { GUILayout.FlexibleSpace(); continue; }

            //                            var s = _sheetSprites[i];
            //                            var rect = GUILayoutUtility.GetRect(sz, sz, GUILayout.Width(sz), GUILayout.Height(sz));

            //                            // рисуем само превью
            //                            if (s && s.texture)
            //                                GUI.DrawTextureWithTexCoords(rect, s.texture, GetSpriteUV(s), true);

            //                            // текущее состояние выбора берём прямо из rule.* (а не из локального "chosen")
            //                            bool isOn = (rule.fixedIndex == i) ||
            //                                        (rule.fixedIndex < 0 && rule.pool != null && rule.pool.Contains(i));

            //                            // чекбокс поверх иконки
            //                            var tRect = new Rect(rect.xMax - 16, rect.yMin, 16, 16);
            //                            bool newOn = GUI.Toggle(tRect, isOn, GUIContent.none);

            //                            // если кликнули — применяем немедленно
            //                            if (newOn != isOn)
            //                            {
            //                                Undo.RecordObject(asset, "Edit Backdrop Picks");

            //                                bool multiselect = Event.current.control || Event.current.command;

            //                                if (multiselect)
            //                                {
            //                                    // множественный выбор — работаем с пулом
            //                                    rule.fixedIndex = -1;
            //                                    rule.pool ??= new List<int>();
            //                                    if (newOn)
            //                                    {
            //                                        if (!rule.pool.Contains(i)) rule.pool.Add(i);
            //                                    }
            //                                    else
            //                                    {
            //                                        rule.pool.Remove(i);
            //                                    }
            //                                }
            //                                else
            //                                {
            //                                    // одиночный выбор — фиксированным индексом, без пула
            //                                    if (newOn)
            //                                    {
            //                                        rule.fixedIndex = i;
            //                                        rule.pool = new List<int>();
            //                                    }
            //                                    else
            //                                    {
            //                                        // сняли галочку — очищаем всё
            //                                        rule.fixedIndex = -1;
            //                                        rule.pool = new List<int>();
            //                                    }
            //                                }

            //                                EditorUtility.SetDirty(asset);
            //                                Repaint();
            //                            }
            //                        }
            //                    }
            //                }
            //            }

            //            // маленькая подсказка об управлении
            //            EditorGUILayout.HelpBox(
            //                "Клик — одиночный выбор (Fixed Index).\n" +
            //                "Ctrl/Cmd + клик — множественный выбор (Pool).\n" +
            //                "Изменения применяются сразу, кнопка Apply не нужна.",
            //                MessageType.None);
            //        }
            //    }
            //}

            if (exists)
            {
                real.backUnrevealed ??= new AdventureAsset.SpritePickRule();
                real.backBlocked ??= new AdventureAsset.SpritePickRule();
                real.backRevealed ??= new AdventureAsset.SpritePickRule();

                DrawPickWithSet("Unrevealed",
                    () => real.backUnrevealedSet,
                    v => real.backUnrevealedSet = v,
                    real.backUnrevealed);

                DrawPickWithSet("Blocked",
                    () => real.backBlockedSet,
                    v => real.backBlockedSet = v,
                    real.backBlocked);

                DrawPickWithSet("Revealed",
                    () => real.backRevealedSet,
                    v => real.backRevealedSet = v,
                    real.backRevealed);
            }


            //if (exists)
            //{
            //    DrawPick("Unrevealed", ref real.backUnrevealed);
            //    DrawPick("Blocked", ref real.backBlocked);
            //    DrawPick("Revealed", ref real.backRevealed);
            //    Undo.RecordObject(asset, "Edit Backdrop Picks"); EditorUtility.SetDirty(asset);
            //}
            //else
            //{
            //    EditorGUILayout.HelpBox("Сначала кликните по канвасу, чтобы создать реальную клетку в ассете.", MessageType.Info);
            //}
        }
    }


    // ===== КАНВАС =====
    private void DrawCanvas()
    {
        if (!asset) return;
        var area = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        using (var view = new GUI.ScrollViewScope(area, _canvasScroll, GetCanvasRect(), true, true))
        {
            _canvasScroll = view.scrollPosition;

            for (int y = 0; y < asset.height; y++)
                for (int x = 0; x < asset.width; x++)
                {
                    var cell = _cells.TryGetValue((x, y), out var real) ? real : GetTempViewCell(x, y);
                    float cx = HexCenterX(x), cy = HexCenterY(x, y);
                    var poly = BuildHex(cx, cy, _hexRadius * _zoom);

                    Handles.color = CellColor(cell);
                    Handles.DrawAAConvexPolygon(poly);
                    Handles.color = _outlineColor; Handles.DrawAAPolyLine(2f, Close(poly));

                    // event preview
                    if (cell.visible && cell.eventAsset != null)
                    {
                        var spr = cell.eventAsset.icon;
                        float iconSize = 50f * _zoom;
                        var iconRect = new Rect(cx - 25 * _zoom, cy - 35 * _zoom, iconSize, iconSize);
                        var labelRect = new Rect(cx - 60 * _zoom, cy + 15 * _zoom, 120 * _zoom, 16 * _zoom);
                        if (spr && spr.texture) GUI.DrawTextureWithTexCoords(iconRect, spr.texture, GetSpriteUV(spr), true);
                        var name = NiceEventTitle(cell.eventAsset);
                        if (name.Length > 18) name = name.Substring(0, 18);
                        GUI.Label(labelRect, name, _centerMiniLabel);

                        // barriers preview (до 3)
                        if (cell.barriers != null && cell.barriers.Count > 0)
                        {
                            float chip = 14f * _zoom, gap = 2f * _zoom;
                            float totalW = 3 * chip + 2 * gap;
                            float left = cx - totalW * 0.5f, yChips = cy + 30f * _zoom;
                            for (int i = 0; i < 3; i++)
                            {
                                var r = new Rect(left + i * (chip + gap), yChips, chip, chip);
                                if (i < cell.barriers.Count)
                                {
                                    int v = cell.barriers[i] >= 3 ? 3 : 1;
                                    Sprite chipSpr = (v == 3) ? _barIcon3 : _barIcon1;
                                    if (chipSpr && chipSpr.texture) GUI.DrawTextureWithTexCoords(r, chipSpr.texture, GetSpriteUV(chipSpr), true);
                                    else
                                    {
                                        var col = (v == 3) ? new Color(1f, .55f, .1f, 1f) : new Color(.25f, .6f, 1f, 1f);
                                        Handles.color = col; Handles.DrawSolidDisc(r.center, Vector3.forward, r.width * 0.5f);
                                        Handles.color = Color.black; Handles.DrawWireDisc(r.center, Vector3.forward, r.width * 0.5f);
                                    }
                                }
                                else
                                {
                                    Handles.color = new Color(0, 0, 0, 0.6f);
                                    Handles.DrawWireDisc(r.center, Vector3.forward, r.width * 0.5f);
                                }
                            }
                        }
                    }

                    // hover/paint
                    var e = Event.current;
                    if (e.isMouse && (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
                    {
                        if (PointInPoly(e.mousePosition, poly))
                        {
                            _hoverCoord = (x, y);
                            _hoverCell = _cells.TryGetValue((x, y), out var r) ? r : null;

                            if (e.type == EventType.MouseDown && e.button == 0)
                            {
                                // В Terrain + Selection — ничего не меняем, только выбор
                                if (!(_brush == BrushMode.Terrain && _terrainPick == TerrainPick.Select))
                                    OnPaintAt(x, y);
                                e.Use();
                            }
                            else if (e.type == EventType.MouseDrag && e.button == 0 && _tool == Tool.RectFill)
                            {
                                if (!_isRectSelecting) { _isRectSelecting = true; _rectStartCell = (x, y); }
                                PaintRect(_rectStartCell.Value, (x, y));
                                e.Use();
                            }
                        }
                    }
                }

            if (Event.current.type == EventType.MouseUp) { _isRectSelecting = false; _rectStartCell = null; }
        }
    }

    // ===== утилиты канваса =====
    private Rect GetCanvasRect()
    {
        var W = 2f * _hexRadius * _zoom;
        var H = Mathf.Sqrt(3f) * _hexRadius * _zoom;
        var totalW = (asset.width - 1) * (W * 0.75f) + W;
        var totalH = (asset.height + 0.5f) * H;
        return new Rect(0, 0, _padX + totalW + _padX, _padY + totalH + _padY);
    }
    private float HexCenterX(int x) => (_padX + _hexRadius * _zoom) + x * (2f * _hexRadius * 0.75f) * _zoom;
    private float HexCenterY(int x, int y)
    {
        var H = Mathf.Sqrt(3f) * _hexRadius * _zoom;
        return (_padY + 0.5f * H) + (y + (x % 2) * 0.5f) * H;
    }
    private Vector3[] BuildHex(float cx, float cy, float r)
    {
        var pts = new Vector3[6];
        for (int i = 0; i < 6; i++) { float a = Mathf.Deg2Rad * (60f * i); pts[i] = new Vector3(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a), 0); }
        return pts;
    }
    private Vector3[] Close(Vector3[] poly)
    {
        var arr = new Vector3[poly.Length + 1];
        Array.Copy(poly, arr, poly.Length);
        arr[arr.Length - 1] = poly[0];
        return arr;
    }
    private bool PointInPoly(Vector2 p, Vector3[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var pi = (Vector2)poly[i]; var pj = (Vector2)poly[j];
            bool inter = ((pi.y > p.y) != (pj.y > p.y)) &&
                         (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y + 1e-5f) + pi.x);
            if (inter) inside = !inside;
        }
        return inside;
    }
    private Color CellColor(AdventureCell c)
    {
        Color baseCol;
        if (!_terrainColor.TryGetValue(c.terrain, out baseCol)) baseCol = _terrainColor[HexTerrainType.Empty];
        baseCol.a = c.visible ? 1f : 0.25f;
        return baseCol;
    }
    private static Rect GetSpriteUV(Sprite s)
    {
        if (!s || !s.texture) return new Rect(0, 0, 1, 1);
        var tr = s.textureRect; var tex = s.texture;
        return new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
    }

    // ===== редактирование ячеек =====
    private AdventureCell GetTempViewCell(int x, int y)
    {
        AdventureCell c;
        if (_temp.TryGetValue((x, y), out c)) return c;
        c = new AdventureCell { x = x, y = y, visible = true, terrain = HexTerrainType.Empty, eventAsset = null, barriers = new List<int>() };
        _temp[(x, y)] = c; return c;
    }

    private void CommitTempToAsset(int x, int y, AdventureCell view)
    {
        var real = new AdventureCell
        {
            x = x,
            y = y,
            visible = view.visible,
            terrain = view.terrain,
            eventAsset = view.eventAsset,
            barriers = view.barriers != null ? new List<int>(view.barriers) : new List<int>()
        };
        asset.cells.Add(real); _cells[(x, y)] = real; EditorUtility.SetDirty(asset);
    }

    private void OnPaintAt(int x, int y)
    {
        AdventureCell cell;
        var exists = _cells.TryGetValue((x, y), out cell);
        if (!exists) { var temp = GetTempViewCell(x, y); CommitTempToAsset(x, y, temp); cell = _cells[(x, y)]; }
        Undo.RecordObject(asset, "Paint cell");

        switch (_tool)
        {
            case Tool.Eyedropper:
                if (_brush == BrushMode.Terrain)
                {
                    _terrainPick = cell.terrain switch
                    {
                        HexTerrainType.Empty => TerrainPick.Empty,
                        HexTerrainType.Event => TerrainPick.Event,
                        HexTerrainType.Blocked => TerrainPick.Blocked,
                        HexTerrainType.Start => TerrainPick.Start,
                        HexTerrainType.Exit => TerrainPick.Exit,
                        _ => TerrainPick.Select
                    };
                }
                else if (_brush == BrushMode.Event) _selectedEvent = cell.eventAsset;
                else _paintVisible = cell.visible;
                break;

            default:
            case Tool.Paint:
                if (_brush == BrushMode.Terrain)
                {
                    if (_terrainPick == TerrainPick.Select)
                    {
                        // только выбор
                    }
                    else
                    {
                        cell.terrain = _terrainPick switch
                        {
                            TerrainPick.Empty => HexTerrainType.Empty,
                            TerrainPick.Event => HexTerrainType.Event,
                            TerrainPick.Blocked => HexTerrainType.Blocked,
                            TerrainPick.Start => HexTerrainType.Start,
                            TerrainPick.Exit => HexTerrainType.Exit,
                            _ => cell.terrain
                        };
                        if (cell.terrain == HexTerrainType.Empty || cell.terrain == HexTerrainType.Blocked) cell.eventAsset = null;
                    }
                }
                else if (_brush == BrushMode.Event)
                {
                    cell.eventAsset = _selectedEvent;
                    if (cell.eventAsset != null) cell.terrain = HexTerrainType.Event;
                }
                else if (_brush == BrushMode.Barriers)
                {
                    cell.barriers ??= new List<int>();
                    if (_barOp == BarrierOp.Add) { if (cell.barriers.Count < 3) cell.barriers.Add(_barPaintValue >= 3 ? 3 : 1); }
                    else if (_barOp == BarrierOp.RemoveFirst) { if (cell.barriers.Count > 0) cell.barriers.RemoveAt(0); }
                    else { cell.barriers.Clear(); }
                }
                else // Visibility
                {
                    cell.visible = _paintVisible;
                }

                EditorUtility.SetDirty(asset);
                break;

            case Tool.RectFill:
                goto default;
        }
        Repaint();
    }

    private bool _isRectSelecting; private (int x, int y)? _rectStartCell;
    private void PaintRect((int x, int y) from, (int x, int y) to)
    {
        int x0 = Math.Min(from.x, to.x), x1 = Math.Max(from.x, to.x);
        int y0 = Math.Min(from.y, to.y), y1 = Math.Max(from.y, to.y);
        Undo.RecordObject(asset, "Rect fill");
        for (int yy = y0; yy <= y1; yy++)
            for (int xx = x0; xx <= x1; xx++)
            {
                if (xx < 0 || xx >= asset.width || yy < 0 || yy >= asset.height) continue;
                AdventureCell cell;
                var exists = _cells.TryGetValue((xx, yy), out cell);
                if (!exists) { var temp = GetTempViewCell(xx, yy); CommitTempToAsset(xx, yy, temp); cell = _cells[(xx, yy)]; }

                if (_brush == BrushMode.Terrain)
                {
                    if (_terrainPick != TerrainPick.Select)
                    {
                        cell.terrain = _terrainPick switch
                        {
                            TerrainPick.Empty => HexTerrainType.Empty,
                            TerrainPick.Event => HexTerrainType.Event,
                            TerrainPick.Blocked => HexTerrainType.Blocked,
                            TerrainPick.Start => HexTerrainType.Start,
                            TerrainPick.Exit => HexTerrainType.Exit,
                            _ => cell.terrain
                        };
                        if (cell.terrain == HexTerrainType.Empty || cell.terrain == HexTerrainType.Blocked) cell.eventAsset = null;
                    }
                }
                else if (_brush == BrushMode.Event)
                {
                    cell.eventAsset = _selectedEvent;
                    if (cell.eventAsset != null) cell.terrain = HexTerrainType.Event;
                }
                else if (_brush == BrushMode.Barriers)
                {
                    cell.barriers ??= new List<int>();
                    if (_barOp == BarrierOp.Add) { if (cell.barriers.Count < 3) cell.barriers.Add(_barPaintValue >= 3 ? 3 : 1); }
                    else if (_barOp == BarrierOp.RemoveFirst) { if (cell.barriers.Count > 0) cell.barriers.RemoveAt(0); }
                    else { cell.barriers.Clear(); }
                }
                else // Visibility
                {
                    cell.visible = _paintVisible;
                }
            }
        EditorUtility.SetDirty(asset); Repaint();
    }

    private void ValidateAndReport()
    {
        if (!asset) return;
        var cells = asset.cells.Where(c => c != null && c.visible).ToList();
        var starts = cells.Where(c => c.terrain == HexTerrainType.Start).ToList();
        var exits = cells.Where(c => c.terrain == HexTerrainType.Exit).ToList();
        if (starts.Count != 1) Debug.LogWarning("[Adventure Editor] Требуется ровно 1 Start. Сейчас: " + starts.Count);
        if (exits.Count == 0) Debug.LogWarning("[Adventure Editor] Рекомендуется иметь хотя бы 1 Exit.");

        if (starts.Count == 1)
        {
            var start = starts[0];
            var passable = new HashSet<(int, int)>(cells.Where(c => c.terrain != HexTerrainType.Blocked).Select(c => (c.x, c.y)));
            var visited = new HashSet<(int, int)>();
            var q = new Queue<(int, int)>();
            q.Enqueue((start.x, start.y)); visited.Add((start.x, start.y));
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (var n in Neighbors(cur.Item1, cur.Item2))
                {
                    if (n.Item1 < 0 || n.Item1 >= asset.width || n.Item2 < 0 || n.Item2 >= asset.height) continue;
                    if (!passable.Contains(n)) continue;
                    if (visited.Contains(n)) continue;
                    visited.Add(n); q.Enqueue(n);
                }
            }
            var unreachable = passable.Except(visited).ToList();
            if (unreachable.Count > 0) Debug.LogWarning("[Adventure Editor] Недостижимых клеток: " + unreachable.Count + ".");
            else Debug.Log("[Adventure Editor] OK: все проходимые клетки достижимы от Start.");
        }
    }

    private IEnumerable<(int x, int y)> Neighbors(int x, int y)
    {
        var even = (x % 2 == 0);
        var dirs = even
            ? new (int dx, int dy)[] { (+1, 0), (0, +1), (-1, 0), (-1, -1), (0, -1), (+1, -1) }
            : new (int dx, int dy)[] { (+1, +1), (0, +1), (-1, +1), (-1, 0), (0, -1), (+1, 0) };
        foreach (var d in dirs) yield return (x + d.dx, y + d.dy);
    }

    private void ApplyToScene()
    {
        var builder = FindFirstObjectByType<AdventureBuilder>(FindObjectsInactive.Include);
        if (builder)
        {
            Undo.RecordObject(builder, "Assign Adventure");
            var so = new SerializedObject(builder);
            so.FindProperty("adventure").objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            builder.BuildAll();
            return;
        }
        EditorUtility.DisplayDialog("Не найден AdventureBuilder",
            "В сцене нет AdventureBuilder. Добавьте на пустой GameObject и повторите.", "Ок");
    }

    // — утилиты заполнения —
    private void EnsureMissingCellsEmpty(AdventureAsset a)
    {
        a.cells ??= new List<AdventureCell>();
        for (int yy = 0; yy < a.height; yy++)
            for (int xx = 0; xx < a.width; xx++)
                if (_cells.ContainsKey((xx, yy)) == false)
                {
                    var c = new AdventureCell { x = xx, y = yy, visible = true, terrain = HexTerrainType.Empty, eventAsset = null, barriers = new List<int>() };
                    a.cells.Add(c); _cells[(xx, yy)] = c;
                }
    }
    private void ResetAllCellsToEmpty(AdventureAsset a)
    {
        a.cells ??= new List<AdventureCell>(); a.cells.Clear();
        for (int yy = 0; yy < a.height; yy++)
            for (int xx = 0; xx < a.width; xx++)
                a.cells.Add(new AdventureCell { x = xx, y = yy, visible = true, terrain = HexTerrainType.Empty, eventAsset = null, barriers = new List<int>() });
    }

    // ===== хелперы =====
    private void LoadSheetSprites()
    {
        _sheetSprites = null;
        if (_spriteSheet == null) return;
        var path = AssetDatabase.GetAssetPath(_spriteSheet);
        if (string.IsNullOrEmpty(path)) return;
        _sheetSprites = AssetDatabase.LoadAllAssetsAtPath(path)
                                     .OfType<Sprite>()
                                     .OrderBy(s => s.name, StringComparer.Ordinal)
                                     .ToArray();
    }

    private static IEnumerable<int> ParseCsvInts(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) yield break;
        var parts = csv.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts) { int v; if (int.TryParse(p.Trim(), out v)) yield return v; }
    }

    private static string NiceEventTitle(EventSO ev)
    {
        if (ev == null) return "";
        var so = new SerializedObject(ev);
        var titleProp = so.FindProperty("displayName") ??
                        so.FindProperty("title") ??
                        so.FindProperty("eventName") ??
                        so.FindProperty("nameLocalized");
        if (titleProp != null &&
            titleProp.propertyType == SerializedPropertyType.String &&
            !string.IsNullOrEmpty(titleProp.stringValue))
            return titleProp.stringValue;
        return ev.name;
    }
}
#endif






//#if UNITY_EDITOR
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using UnityEditor;
//using UnityEngine;
//using static AdventureAsset; // даёт короткое имя SpritePickRule

//public class AdventureMapEditorWindow : EditorWindow
//{
//    [SerializeField] private AdventureAsset asset;

//    // кеш ячеек ассета и временных «просмотров»
//    private Dictionary<(int x, int y), AdventureCell> _cells;
//    private Dictionary<(int x, int y), AdventureCell> _temp;
//    private AdventureAsset _lastAsset;

//    // канвас
//    private Vector2 _canvasScroll;
//    [SerializeField] private float _hexRadius = 48f;
//    [SerializeField] private float _zoom = 1f;
//    [SerializeField] private float _padX = 80f, _padY = 80f;

//    // палитры
//    private enum BrushMode { Terrain, Event, Visibility, Barriers }
//    private BrushMode _brush = BrushMode.Terrain;

//    private enum Tool { Paint, Eyedropper, RectFill }
//    private Tool _tool = Tool.Paint;

//    private HexTerrainType _paintTerrain = HexTerrainType.Empty;
//    private bool _paintVisible = true;

//    // события
//    private List<EventSO> _allEvents;
//    private string _eventFilter = "";
//    private Vector2 _eventScroll;
//    private EventSO _selectedEvent;

//    // барьеры
//    private enum BarrierOp { Add, RemoveFirst, Clear }
//    private BarrierOp _barOp = BarrierOp.Add;
//    [SerializeField] private int _barPaintValue = 1; // 1 или 3
//    [SerializeField] private Sprite _barIcon1;
//    [SerializeField] private Sprite _barIcon3;

//    // стили
//    private GUIStyle _centerMiniLabel;
//    private readonly Color _outlineColor = new(0, 0, 0, 0.9f);
//    private readonly Dictionary<HexTerrainType, Color> _terrainColor = new()
//    {
//        { HexTerrainType.Empty,   new Color(0.15f,0.15f,0.18f) },
//        { HexTerrainType.Event,   new Color(0.96f,0.62f,0.24f) },
//        { HexTerrainType.Blocked, new Color(0.40f,0.40f,0.40f) },
//        { HexTerrainType.Start,   new Color(0.20f,0.65f,1.00f) },
//        { HexTerrainType.Exit,    new Color(0.62f,0.45f,0.95f) },
//    };

//    [MenuItem("Robinson/Adventure Map Editor")]
//    public static void Open()
//    {
//        var w = GetWindow<AdventureMapEditorWindow>("Adventure Map Editor");
//        w.minSize = new Vector2(900, 580);
//        w.Show();
//    }

//    private void OnEnable()
//    {
//        _centerMiniLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
//        { alignment = TextAnchor.MiddleCenter, wordWrap = false };

//        RefreshEventCatalog();
//        RebuildCellIndex();
//    }

//    private void RefreshEventCatalog()
//    {
//        var guids = AssetDatabase.FindAssets("t:EventSO");
//        _allEvents = guids
//            .Select(g => AssetDatabase.LoadAssetAtPath<EventSO>(AssetDatabase.GUIDToAssetPath(g)))
//            .Where(e => e != null)
//            .OrderBy(e => e.name)
//            .ToList();
//    }

//    private void RebuildCellIndex()
//    {
//        _cells = new(); _temp = new();
//        if (!asset) return;
//        asset.cells ??= new List<AdventureCell>();
//        asset.cells.RemoveAll(c => c == null);
//        foreach (var c in asset.cells) _cells[(c.x, c.y)] = c;
//    }

//    private void OnGUI()
//    {
//        if (_lastAsset != asset) { _lastAsset = asset; RebuildCellIndex(); }

//        using (new EditorGUILayout.HorizontalScope()) DrawTopBar();
//        using (new EditorGUILayout.HorizontalScope())
//        {
//            DrawPalette();
//            DrawCanvas();
//            DrawInspector();
//        }
//    }

//    private void DrawTopBar()
//    {
//        asset = (AdventureAsset)EditorGUILayout.ObjectField("Adventure Asset", asset, typeof(AdventureAsset), false);
//        if (!asset)
//        {
//            EditorGUILayout.HelpBox("Назначьте AdventureAsset.", MessageType.Info);
//            return;
//        }

//        EditorGUI.BeginChangeCheck();
//        using (new EditorGUILayout.HorizontalScope())
//        {
//            asset.displayName = EditorGUILayout.TextField("Display Name", asset.displayName);
//            asset.version = EditorGUILayout.IntField("Version", asset.version, GUILayout.MaxWidth(220));
//        }
//        using (new EditorGUILayout.HorizontalScope())
//        {
//            asset.width = EditorGUILayout.IntSlider("Width", Mathf.Max(1, asset.width), 1, 100);
//            asset.height = EditorGUILayout.IntSlider("Height", Mathf.Max(1, asset.height), 1, 100);
//        }
//        if (EditorGUI.EndChangeCheck())
//        {
//            Undo.RecordObject(asset, "Edit asset props");
//            EditorUtility.SetDirty(asset);
//            RebuildCellIndex();
//            Repaint();
//        }

//        using (new EditorGUILayout.HorizontalScope())
//        {
//            if (GUILayout.Button("Ensure Missing (Empty)", GUILayout.Width(170)))
//            { Undo.RecordObject(asset, "EnsureMissingEmpty"); EnsureMissingCellsEmpty(asset); EditorUtility.SetDirty(asset); RebuildCellIndex(); }

//            if (GUILayout.Button("Reset Map (clear & fill Empty)", GUILayout.Width(230)))
//            {
//                if (EditorUtility.DisplayDialog("Сбросить карту?",
//                    "Удалит все клетки и создаст сетку заново как Empty.", "Да", "Отмена"))
//                { Undo.RecordObject(asset, "ResetMap"); ResetAllCellsToEmpty(asset); EditorUtility.SetDirty(asset); RebuildCellIndex(); }
//            }

//            if (GUILayout.Button("Validate", GUILayout.Width(100))) ValidateAndReport();
//            GUILayout.FlexibleSpace();
//            if (GUILayout.Button("Apply to Scene (Builder)", GUILayout.Width(180))) ApplyToScene();
//        }
//        EditorGUILayout.Space(4);
//        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
//    }

//    private void DrawPalette()
//    {
//        using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
//        {
//            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
//            _brush = (BrushMode)GUILayout.Toolbar((int)_brush, new[] { "Terrain", "Event", "Visibility", "Barriers" });
//            EditorGUILayout.Space(6);
//            _tool = (Tool)GUILayout.Toolbar((int)_tool, new[] { "Paint", "Eyedropper", "Rect Fill" });
//            EditorGUILayout.Space(8);

//            if (_brush == BrushMode.Terrain)
//            {
//                _paintTerrain = (HexTerrainType)EditorGUILayout.EnumPopup("Terrain", _paintTerrain);
//                DrawLegend();
//            }
//            else if (_brush == BrushMode.Event)
//            {
//                EditorGUILayout.LabelField("Event Catalog", EditorStyles.miniBoldLabel);
//                using (new EditorGUILayout.HorizontalScope())
//                {
//                    _eventFilter = EditorGUILayout.TextField(_eventFilter);
//                    if (GUILayout.Button("⟳", GUILayout.Width(28))) RefreshEventCatalog();
//                }

//                using (var sv = new EditorGUILayout.ScrollViewScope(_eventScroll, GUILayout.Height(340)))
//                {
//                    _eventScroll = sv.scrollPosition;
//                    var filtered = string.IsNullOrWhiteSpace(_eventFilter)
//                        ? _allEvents
//                        : _allEvents.Where(e => e.name.IndexOf(_eventFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

//                    if (GUILayout.Button("— None (clear) —")) _selectedEvent = null;

//                    foreach (var ev in filtered)
//                    {
//                        var isSel = _selectedEvent == ev;
//                        using (new EditorGUILayout.HorizontalScope(isSel ? "SelectionRect" : GUIStyle.none))
//                        {
//                            var spr = ev.icon;
//                            var iconRect = GUILayoutUtility.GetRect(30, 30, GUILayout.Width(30), GUILayout.Height(30));
//                            if (spr && spr.texture) GUI.DrawTextureWithTexCoords(iconRect, spr.texture, GetSpriteUV(spr), true);

//                            var style = new GUIStyle(GUI.skin.button);
//                            if (ev.isCombat) style.normal.textColor = new Color(1f, .25f, .25f);
//                            else if (ev.isChoice) style.normal.textColor = new Color(.7f, .4f, 1f);
//                            else if (ev.isResource) style.normal.textColor = Color.white;

//                            if (GUILayout.Button(ev.name, style)) _selectedEvent = ev;
//                            if (GUILayout.Button("Ping", GUILayout.Width(44))) EditorGUIUtility.PingObject(ev);
//                            if (GUILayout.Button("Open", GUILayout.Width(48))) Selection.activeObject = ev;
//                        }
//                    }
//                }
//            }
//            else if (_brush == BrushMode.Barriers)
//            {
//                _barOp = (BarrierOp)GUILayout.Toolbar((int)_barOp, new[] { "Add", "+Remove 1st", "Clear" });
//                using (new EditorGUI.DisabledScope(_barOp != BarrierOp.Add))
//                {
//                    EditorGUILayout.LabelField("Value to Add", EditorStyles.miniBoldLabel);
//                    using (new EditorGUILayout.HorizontalScope())
//                    {
//                        if (GUILayout.Toggle(_barPaintValue == 1, "+1", "Button")) _barPaintValue = 1;
//                        if (GUILayout.Toggle(_barPaintValue == 3, "+3", "Button")) _barPaintValue = 3;
//                    }
//                }
//                _barIcon1 = (Sprite)EditorGUILayout.ObjectField("Icon +1 (optional)", _barIcon1, typeof(Sprite), false);
//                _barIcon3 = (Sprite)EditorGUILayout.ObjectField("Icon +3 (optional)", _barIcon3, typeof(Sprite), false);
//            }
//            else
//            {
//                _paintVisible = EditorGUILayout.ToggleLeft("Visible = true", _paintVisible);
//            }

//            EditorGUILayout.Space(6);
//            _hexRadius = EditorGUILayout.Slider("Hex Radius (px)", _hexRadius, 16, 96);
//            _zoom = EditorGUILayout.Slider("Zoom", _zoom, 0.5f, 2f);
//            _padX = EditorGUILayout.Slider("Canvas Pad X", _padX, 0, 200);
//            _padY = EditorGUILayout.Slider("Canvas Pad Y", _padY, 0, 200);
//        }
//    }

//    private void DrawLegend()
//    {
//        EditorGUILayout.Space(6);
//        foreach (var kv in _terrainColor)
//        {
//            using (new EditorGUILayout.HorizontalScope())
//            {
//                var c = kv.Value; c.a = 1f;
//                var rect = GUILayoutUtility.GetRect(18, 14, GUILayout.Width(24));
//                EditorGUI.DrawRect(rect, c);
//                EditorGUILayout.LabelField(kv.Key.ToString());
//            }
//        }
//    }

//    private AdventureCell _hoverCell;
//    private (int x, int y)? _hoverCoord;

//    private void DrawInspector()
//    {
//        using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
//        {
//            EditorGUILayout.LabelField("Cell Inspector", EditorStyles.boldLabel);

//            if (_hoverCoord == null)
//            { EditorGUILayout.HelpBox("Наведите/кликните на гекс на канвасе.", MessageType.Info); return; }

//            var (hx, hy) = _hoverCoord.Value;
//            var exists = _cells.TryGetValue((hx, hy), out var real);
//            var view = exists ? real : GetTempViewCell(hx, hy);

//            EditorGUI.BeginChangeCheck();
//            EditorGUILayout.LabelField($"Cell: ({hx},{hy})", EditorStyles.miniBoldLabel);
//            view.visible = EditorGUILayout.ToggleLeft("Visible", view.visible);
//            view.terrain = (HexTerrainType)EditorGUILayout.EnumPopup("Terrain", view.terrain);
//            view.eventAsset = (EventSO)EditorGUILayout.ObjectField("Event", view.eventAsset, typeof(EventSO), false);
//            if (EditorGUI.EndChangeCheck())
//            {
//                if (!exists) { CommitTempToAsset(hx, hy, view); exists = true; real = _cells[(hx, hy)]; }
//                real.visible = view.visible;
//                real.terrain = view.terrain;
//                real.eventAsset = view.eventAsset;
//                if (real.terrain == HexTerrainType.Empty || real.terrain == HexTerrainType.Blocked) real.eventAsset = null;
//                if (real.eventAsset != null) real.terrain = HexTerrainType.Event;
//                Undo.RecordObject(asset, "Edit Cell"); EditorUtility.SetDirty(asset); Repaint();
//            }

//            // --- Barriers ---
//            EditorGUILayout.Space(8);
//            EditorGUILayout.LabelField("Barriers", EditorStyles.boldLabel);
//            using (new EditorGUILayout.HorizontalScope())
//            {
//                if (GUILayout.Button("+1", GUILayout.Width(40)))
//                {
//                    if (!exists) { CommitTempToAsset(hx, hy, view); exists = true; real = _cells[(hx, hy)]; }
//                    real.barriers ??= new List<int>(); if (real.barriers.Count < 3) real.barriers.Add(1);
//                    Undo.RecordObject(asset, "Add +1"); EditorUtility.SetDirty(asset); Repaint();
//                }

//                if (GUILayout.Button("+3", GUILayout.Width(40)))
//                {
//                    if (!exists) { CommitTempToAsset(hx, hy, view); exists = true; real = _cells[(hx, hy)]; }
//                    real.barriers ??= new List<int>(); if (real.barriers.Count < 3) real.barriers.Add(3);
//                    Undo.RecordObject(asset, "Add +3"); EditorUtility.SetDirty(asset); Repaint();
//                }

//                if (GUILayout.Button("Remove 1st", GUILayout.Width(90)))
//                {
//                    if (exists && real.barriers != null && real.barriers.Count > 0) real.barriers.RemoveAt(0);
//                    Undo.RecordObject(asset, "Remove 1st"); EditorUtility.SetDirty(asset); Repaint();
//                }

//                if (GUILayout.Button("Clear", GUILayout.Width(60)))
//                {
//                    if (exists && real.barriers != null) real.barriers.Clear();
//                    Undo.RecordObject(asset, "Clear"); EditorUtility.SetDirty(asset); Repaint();
//                }
//            }

//            string cur = (exists && real.barriers != null && real.barriers.Count > 0)
//                ? string.Join(", ", real.barriers.Select(v => v >= 3 ? "3" : "1")) : "—";
//            EditorGUILayout.LabelField($"Current: {cur} (max 3)");

//            using (new EditorGUILayout.HorizontalScope())
//            {
//                if (view.eventAsset != null && GUILayout.Button("Ping Event")) EditorGUIUtility.PingObject(view.eventAsset);
//                if (view.eventAsset != null && GUILayout.Button("Open Event")) Selection.activeObject = view.eventAsset;
//            }

//            if (!view.visible)
//                EditorGUILayout.HelpBox("Эта клетка невидима.", MessageType.Warning);

//            // --- Backdrop Sprites ---
//            EditorGUILayout.Space(8);
//            EditorGUILayout.LabelField("Backdrop Sprites", EditorStyles.boldLabel);

//            void DrawPick(string title, ref SpritePickRule rule)
//            {
//                if (rule == null) rule = new SpritePickRule();
//                using (new EditorGUILayout.VerticalScope("HelpBox"))
//                {
//                    EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
//                    rule.fixedIndex = EditorGUILayout.IntField(new GUIContent("Fixed Index (-1 = random)"), rule.fixedIndex);

//                    EditorGUILayout.LabelField("Pool (used if Fixed < 0)");
//                    int removeAt = -1; rule.pool ??= new List<int>();
//                    for (int i = 0; i < rule.pool.Count; i++)
//                    {
//                        using (new EditorGUILayout.HorizontalScope())
//                        {
//                            rule.pool[i] = EditorGUILayout.IntField($"[{i}]", rule.pool[i]);
//                            if (GUILayout.Button("X", GUILayout.Width(22))) removeAt = i;
//                        }
//                    }
//                    if (removeAt >= 0) rule.pool.RemoveAt(removeAt);
//                    if (GUILayout.Button("+ Add Pool Index")) rule.pool.Add(0);
//                }
//            }

//            if (exists)
//            {
//                DrawPick("Unrevealed", ref real.backUnrevealed);
//                DrawPick("Blocked", ref real.backBlocked);
//                DrawPick("Revealed", ref real.backRevealed);
//                Undo.RecordObject(asset, "Edit Backdrop Picks"); EditorUtility.SetDirty(asset);
//            }
//            else
//            {
//                EditorGUILayout.HelpBox("Сперва кликните по канвасу, чтобы создать реальную клетку в ассете.", MessageType.Info);
//            }
//        }
//    }

//    // ===== КАНВАС =====

//    private void DrawCanvas()
//    {
//        if (!asset) return;
//        var area = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
//        using (var view = new GUI.ScrollViewScope(area, _canvasScroll, GetCanvasRect(), true, true))
//        {
//            _canvasScroll = view.scrollPosition;

//            for (int y = 0; y < asset.height; y++)
//                for (int x = 0; x < asset.width; x++)
//                {
//                    var cell = _cells.TryGetValue((x, y), out var real) ? real : GetTempViewCell(x, y);
//                    float cx = HexCenterX(x), cy = HexCenterY(x, y);
//                    var poly = BuildHex(cx, cy, _hexRadius * _zoom);

//                    Handles.color = CellColor(cell);
//                    Handles.DrawAAConvexPolygon(poly);
//                    Handles.color = _outlineColor; Handles.DrawAAPolyLine(2f, Close(poly));

//                    // event preview
//                    if (cell.visible && cell.eventAsset != null)
//                    {
//                        var spr = cell.eventAsset.icon;
//                        float iconSize = 50f * _zoom;
//                        var iconRect = new Rect(cx - 25 * _zoom, cy - 35 * _zoom, iconSize, iconSize);
//                        var labelRect = new Rect(cx - 48 * _zoom, cy + 15 * _zoom, 96 * _zoom, 16 * _zoom);
//                        if (spr && spr.texture) GUI.DrawTextureWithTexCoords(iconRect, spr.texture, GetSpriteUV(spr), true);
//                        var name = cell.eventAsset.name;
//                        if (name.Length > 18) name = name[..18];
//                        GUI.Label(labelRect, name, _centerMiniLabel);

//                        // barriers preview (до 3)
//                        if (cell.barriers != null && cell.barriers.Count > 0)
//                        {
//                            float chip = 14f * _zoom, gap = 2f * _zoom;
//                            float totalW = 3 * chip + 2 * gap;
//                            float left = cx - totalW * 0.5f, yChips = cy + 30f * _zoom;
//                            for (int i = 0; i < 3; i++)
//                            {
//                                var r = new Rect(left + i * (chip + gap), yChips, chip, chip);
//                                if (i < cell.barriers.Count)
//                                {
//                                    int v = cell.barriers[i] >= 3 ? 3 : 1;
//                                    Sprite chipSpr = (v == 3) ? _barIcon3 : _barIcon1;
//                                    if (chipSpr && chipSpr.texture) GUI.DrawTextureWithTexCoords(r, chipSpr.texture, GetSpriteUV(chipSpr), true);
//                                    else
//                                    {
//                                        var col = (v == 3) ? new Color(1f, .55f, .1f, 1f) : new Color(.25f, .6f, 1f, 1f);
//                                        Handles.color = col; Handles.DrawSolidDisc(r.center, Vector3.forward, r.width * 0.5f);
//                                        Handles.color = Color.black; Handles.DrawWireDisc(r.center, Vector3.forward, r.width * 0.5f);
//                                    }
//                                }
//                                else { Handles.color = new Color(0, 0, 0, 0.6f); Handles.DrawWireDisc(r.center, Vector3.forward, r.width * 0.5f); }
//                            }
//                        }
//                    }

//                    // hover/paint
//                    var e = Event.current;
//                    if (e.isMouse && (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
//                    {
//                        if (PointInPoly(e.mousePosition, poly))
//                        {
//                            _hoverCoord = (x, y);
//                            _hoverCell = _cells.TryGetValue((x, y), out var r) ? r : null;

//                            if (e.type == EventType.MouseDown && e.button == 0) { OnPaintAt(x, y); e.Use(); }
//                            else if (e.type == EventType.MouseDrag && e.button == 0 && _tool == Tool.RectFill)
//                            { if (!_isRectSelecting) { _isRectSelecting = true; _rectStartCell = (x, y); } PaintRect(_rectStartCell.Value, (x, y)); e.Use(); }
//                        }
//                    }
//                }

//            if (Event.current.type == EventType.MouseUp) { _isRectSelecting = false; _rectStartCell = null; }
//        }
//    }

//    // ===== утилиты канваса =====
//    private Rect GetCanvasRect()
//    {
//        var W = 2f * _hexRadius * _zoom;
//        var H = Mathf.Sqrt(3f) * _hexRadius * _zoom;
//        var totalW = (asset.width - 1) * (W * 0.75f) + W;
//        var totalH = (asset.height + 0.5f) * H;
//        return new Rect(0, 0, _padX + totalW + _padX, _padY + totalH + _padY);
//    }
//    private float HexCenterX(int x) => (_padX + _hexRadius * _zoom) + x * (2f * _hexRadius * 0.75f) * _zoom;
//    private float HexCenterY(int x, int y) { var H = Mathf.Sqrt(3f) * _hexRadius * _zoom; return (_padY + 0.5f * H) + (y + (x % 2) * 0.5f) * H; }
//    private Vector3[] BuildHex(float cx, float cy, float r) { var pts = new Vector3[6]; for (int i = 0; i < 6; i++) { float a = Mathf.Deg2Rad * (60f * i); pts[i] = new(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a), 0); } return pts; }
//    private Vector3[] Close(Vector3[] poly) { var arr = new Vector3[poly.Length + 1]; Array.Copy(poly, arr, poly.Length); arr[^1] = poly[0]; return arr; }
//    private bool PointInPoly(Vector2 p, Vector3[] poly)
//    {
//        bool inside = false; for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
//        {
//            var pi = (Vector2)poly[i]; var pj = (Vector2)poly[j];
//            bool inter = ((pi.y > p.y) != (pj.y > p.y)) && (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y + 1e-5f) + pi.x); if (inter) inside = !inside;
//        }
//        return inside;
//    }
//    private Color CellColor(AdventureCell c) { if (!_terrainColor.TryGetValue(c.terrain, out var baseCol)) baseCol = _terrainColor[HexTerrainType.Empty]; baseCol.a = c.visible ? 1f : 0.25f; return baseCol; }
//    private static Rect GetSpriteUV(Sprite s)
//    {
//        if (!s || !s.texture) return new Rect(0, 0, 1, 1); var tr = s.textureRect; var tex = s.texture;
//        return new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
//    }

//    // ===== редактирование ячеек =====
//    private AdventureCell GetTempViewCell(int x, int y)
//    { if (_temp.TryGetValue((x, y), out var c)) return c; c = new AdventureCell { x = x, y = y, visible = true, terrain = HexTerrainType.Empty, eventAsset = null, barriers = new List<int>() }; _temp[(x, y)] = c; return c; }

//    private void CommitTempToAsset(int x, int y, AdventureCell view)
//    {
//        var real = new AdventureCell
//        {
//            x = x,
//            y = y,
//            visible = view.visible,
//            terrain = view.terrain,
//            eventAsset = view.eventAsset,
//            barriers = view.barriers != null ? new List<int>(view.barriers) : new List<int>()
//        };
//        asset.cells.Add(real); _cells[(x, y)] = real; EditorUtility.SetDirty(asset);
//    }

//    private void OnPaintAt(int x, int y)
//    {
//        var exists = _cells.TryGetValue((x, y), out var cell);
//        if (!exists) { var temp = GetTempViewCell(x, y); CommitTempToAsset(x, y, temp); cell = _cells[(x, y)]; }
//        Undo.RecordObject(asset, "Paint cell");

//        switch (_tool)
//        {
//            case Tool.Eyedropper:
//                if (_brush == BrushMode.Terrain) _paintTerrain = cell.terrain;
//                else if (_brush == BrushMode.Event) _selectedEvent = cell.eventAsset;
//                else _paintVisible = cell.visible;
//                break;

//            default:
//            case Tool.Paint:
//                if (_brush == BrushMode.Terrain)
//                {
//                    cell.terrain = _paintTerrain;
//                    if (cell.terrain == HexTerrainType.Empty || cell.terrain == HexTerrainType.Blocked) cell.eventAsset = null;
//                }
//                else if (_brush == BrushMode.Event)
//                {
//                    cell.eventAsset = _selectedEvent;
//                    if (cell.eventAsset != null) cell.terrain = HexTerrainType.Event;
//                }
//                else if (_brush == BrushMode.Barriers)
//                {
//                    cell.barriers ??= new List<int>();
//                    if (_barOp == BarrierOp.Add) { if (cell.barriers.Count < 3) cell.barriers.Add(_barPaintValue >= 3 ? 3 : 1); }
//                    else if (_barOp == BarrierOp.RemoveFirst) { if (cell.barriers.Count > 0) cell.barriers.RemoveAt(0); }
//                    else /*Clear*/                      { cell.barriers.Clear(); }
//                }
//                else /*Visibility*/ cell.visible = _paintVisible;

//                EditorUtility.SetDirty(asset);
//                break;

//            case Tool.RectFill: goto default;
//        }
//        Repaint();
//    }

//    private bool _isRectSelecting; private (int x, int y)? _rectStartCell;
//    private void PaintRect((int x, int y) from, (int x, int y) to)
//    {
//        int x0 = Math.Min(from.x, to.x), x1 = Math.Max(from.x, to.x);
//        int y0 = Math.Min(from.y, to.y), y1 = Math.Max(from.y, to.y);
//        Undo.RecordObject(asset, "Rect fill");
//        for (int yy = y0; yy <= y1; yy++)
//            for (int xx = x0; xx <= x1; xx++)
//            {
//                if (xx < 0 || xx >= asset.width || yy < 0 || yy >= asset.height) continue;
//                var exists = _cells.TryGetValue((xx, yy), out var cell);
//                if (!exists) { var temp = GetTempViewCell(xx, yy); CommitTempToAsset(xx, yy, temp); cell = _cells[(xx, yy)]; }
//                if (_brush == BrushMode.Terrain) { cell.terrain = _paintTerrain; if (cell.terrain == HexTerrainType.Empty || cell.terrain == HexTerrainType.Blocked) cell.eventAsset = null; }
//                else if (_brush == BrushMode.Event) { cell.eventAsset = _selectedEvent; if (cell.eventAsset != null) cell.terrain = HexTerrainType.Event; }
//                else if (_brush == BrushMode.Barriers)
//                {
//                    cell.barriers ??= new List<int>(); if (_barOp == BarrierOp.Add) { if (cell.barriers.Count < 3) cell.barriers.Add(_barPaintValue >= 3 ? 3 : 1); }
//                    else if (_barOp == BarrierOp.RemoveFirst) { if (cell.barriers.Count > 0) cell.barriers.RemoveAt(0); } else cell.barriers.Clear();
//                }
//                else cell.visible = _paintVisible;
//            }
//        EditorUtility.SetDirty(asset); Repaint();
//    }

//    private void ValidateAndReport()
//    {
//        if (!asset) return;
//        var cells = asset.cells.Where(c => c != null && c.visible).ToList();
//        var starts = cells.Where(c => c.terrain == HexTerrainType.Start).ToList();
//        var exits = cells.Where(c => c.terrain == HexTerrainType.Exit).ToList();
//        if (starts.Count != 1) Debug.LogWarning($"[Adventure Editor] Требуется ровно 1 Start. Сейчас: {starts.Count}");
//        if (exits.Count == 0) Debug.LogWarning("[Adventure Editor] Рекомендуется иметь хотя бы 1 Exit.");

//        if (starts.Count == 1)
//        {
//            var start = starts[0];
//            var passable = new HashSet<(int, int)>(cells.Where(c => c.terrain != HexTerrainType.Blocked).Select(c => (c.x, c.y)));
//            var visited = new HashSet<(int, int)>(); var q = new Queue<(int, int)>(); q.Enqueue((start.x, start.y)); visited.Add((start.x, start.y));
//            while (q.Count > 0)
//            {
//                var (x, y) = q.Dequeue();
//                foreach (var (nx, ny) in Neighbors(x, y))
//                { if (nx < 0 || nx >= asset.width || ny < 0 || ny >= asset.height) continue; if (!passable.Contains((nx, ny))) continue; if (visited.Contains((nx, ny))) continue; visited.Add((nx, ny)); q.Enqueue((nx, ny)); }
//            }
//            var unreachable = passable.Except(visited).ToList();
//            if (unreachable.Count > 0) Debug.LogWarning($"[Adventure Editor] Недостижимых клеток: {unreachable.Count}.");
//            else Debug.Log("[Adventure Editor] OK: все проходимые клетки достижимы от Start.");
//        }
//    }

//    private IEnumerable<(int x, int y)> Neighbors(int x, int y)
//    {
//        var dirs = (x % 2 == 0)
//            ? new (int dx, int dy)[] { (+1, 0), (0, +1), (-1, 0), (-1, -1), (0, -1), (+1, -1) }
//            : new (int dx, int dy)[] { (+1, +1), (0, +1), (-1, +1), (-1, 0), (0, -1), (+1, 0) };
//        foreach (var d in dirs) yield return (x + d.dx, y + d.dy);
//    }

//    private void ApplyToScene()
//    {
//        var builder = FindFirstObjectByType<AdventureBuilder>(FindObjectsInactive.Include);
//        if (builder)
//        {
//            Undo.RecordObject(builder, "Assign Adventure");
//            var so = new SerializedObject(builder);
//            so.FindProperty("adventure").objectReferenceValue = asset;
//            so.ApplyModifiedPropertiesWithoutUndo();
//            builder.BuildAll();
//            return;
//        }
//        EditorUtility.DisplayDialog("Не найден AdventureBuilder",
//            "В сцене нет AdventureBuilder. Добавьте на пустой GameObject и повторите.", "Ок");
//    }

//    // — вспомогательные заполнители —
//    private void EnsureMissingCellsEmpty(AdventureAsset a)
//    {
//        a.cells ??= new List<AdventureCell>();
//        for (int yy = 0; yy < a.height; yy++)
//            for (int xx = 0; xx < a.width; xx++)
//                if (_cells.ContainsKey((xx, yy)) == false)
//                {
//                    var c = new AdventureCell { x = xx, y = yy, visible = true, terrain = HexTerrainType.Empty, eventAsset = null, barriers = new List<int>() };
//                    a.cells.Add(c); _cells[(xx, yy)] = c;
//                }
//    }
//    private void ResetAllCellsToEmpty(AdventureAsset a)
//    {
//        a.cells ??= new List<AdventureCell>(); a.cells.Clear();
//        for (int yy = 0; yy < a.height; yy++)
//            for (int xx = 0; xx < a.width; xx++)
//                a.cells.Add(new AdventureCell { x = xx, y = yy, visible = true, terrain = HexTerrainType.Empty, eventAsset = null, barriers = new List<int>() });
//    }
//}
//#endif


////#if UNITY_EDITOR
////using System;
////using System.Collections.Generic;
////using System.Linq;
////using UnityEditor;
////using UnityEngine;

/////// <summary>
/////// Adventure Map Editor — интерактивное окно разметки карт AdventureAsset:
///////  • слева палитра (Terrain / Event / Visibility),
///////  • в центре — канвас гекс‑сетки (клик/заливка, предпросмотр),
///////  • справа — инспектор выбранной клетки,
///////  • Validate и Apply to Scene (только через AdventureBuilder).
/////// Окно НЕ создаёт новые AdventureCell при простом отображении — записи появляются только при клике/заливке.
/////// </summary>
////public class AdventureMapEditorWindow : EditorWindow
////{
////    // ---------- СОСТОЯНИЕ ----------
////    [SerializeField] private AdventureAsset asset;                  // редактируемый ассет карты  :contentReference[oaicite:3]{index=3}
////    private Dictionary<(int x, int y), AdventureCell> _cells;      // реальные клетки ассета по координатам
////    private Dictionary<(int x, int y), AdventureCell> _temp;       // «виртуальные» клетки для предпросмотра
////    private AdventureAsset _lastAsset;                              // чтобы понять, что ассет сменили

////    private Vector2 _canvasScroll;
////    [SerializeField] private float _hexRadius = 48f;
////    [SerializeField] private float _zoom = 1f;

////    // Отступы, чтобы крупные гексы не резались краями
////    [SerializeField] private float _padX = 80f;
////    [SerializeField] private float _padY = 80f;

////    // --- ПАЛИТРА БАРЬЕРОВ ---
////    private enum BarrierOp { Add, RemoveFirst, Clear } // операция: добавить, снять первый, очистить все
////    private BarrierOp _barOp = BarrierOp.Add;         // текущая операция
////    [SerializeField] private int _barPaintValue = 1;  // что добавляем: 1 (синяя) или 3 (оранжевая)
////    [SerializeField] private Sprite _barIcon1;        // (необязательно) иконка предпросмотра +1
////    [SerializeField] private Sprite _barIcon3;        // (необязательно) иконка предпросмотра +3


////    // ---------- ПАЛИТРА / ИНСТРУМЕНТЫ ----------
////    private enum BrushMode { Terrain, Event, Visibility, Barriers }
////    private BrushMode _brush = BrushMode.Terrain;

////    private enum Tool { Paint, Eyedropper, RectFill }
////    private Tool _tool = Tool.Paint;

////    // По умолчанию создаём Empty
////    private HexTerrainType _paintTerrain = HexTerrainType.Empty;
////    private bool _paintVisible = true;

////    // Каталог событий
////    private string _eventFilter = "";
////    private Vector2 _eventScroll;
////    private EventSO _selectedEvent;
////    private List<EventSO> _allEvents;

////    // Визуальные стили/цвета
////    private GUIStyle _centerMiniLabel;
////    private readonly Color _outlineColor = new(0, 0, 0, 0.9f);
////    private readonly Dictionary<HexTerrainType, Color> _terrainColor = new()
////    {
////        { HexTerrainType.Empty,   new Color(0.15f, 0.15f, 0.18f) },
////        { HexTerrainType.Event,   new Color(0.96f, 0.62f, 0.24f) },
////        { HexTerrainType.Blocked, new Color(0.40f, 0.40f, 0.40f) },
////        { HexTerrainType.Start,   new Color(0.20f, 0.65f, 1.00f) },
////        { HexTerrainType.Exit,    new Color(0.62f, 0.45f, 0.95f) },
////    };

////    // ---------- МЕНЮ ----------
////    [MenuItem("Robinson/Adventure Map Editor")]
////    public static void Open()
////    {
////        var w = GetWindow<AdventureMapEditorWindow>("Adventure Map Editor");
////        w.minSize = new Vector2(900, 580);
////        w.Show();
////    }

////    private void OnEnable()
////    {
////        _centerMiniLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
////        {
////            alignment = TextAnchor.MiddleCenter,
////            wordWrap = false
////        };

////        RefreshEventCatalog();
////        RebuildCellIndex();
////    }

////    // ---------- КАТАЛОГ СОБЫТИЙ ----------
////    private void RefreshEventCatalog()
////    {
////        var guids = AssetDatabase.FindAssets("t:EventSO");
////        _allEvents = guids
////            .Select(g => AssetDatabase.LoadAssetAtPath<EventSO>(AssetDatabase.GUIDToAssetPath(g)))
////            .Where(e => e != null)
////            .OrderBy(e => e.name)
////            .ToList();
////    }

////    // ---------- ИНДЕКС/КЕШ ----------
////    private void RebuildCellIndex()
////    {
////        _cells = new Dictionary<(int, int), AdventureCell>();
////        _temp = new Dictionary<(int, int), AdventureCell>();

////        if (!asset) return;
////        asset.cells ??= new List<AdventureCell>();
////        asset.cells.RemoveAll(c => c == null);
////        foreach (var c in asset.cells)
////            _cells[(c.x, c.y)] = c;
////    }

////    // ---------- GUI ----------
////    private void OnGUI()
////    {
////        if (_lastAsset != asset) { _lastAsset = asset; RebuildCellIndex(); }

////        using (new EditorGUILayout.HorizontalScope()) DrawTopBar();
////        using (new EditorGUILayout.HorizontalScope())
////        {
////            DrawPalette();
////            DrawCanvas();
////            DrawInspector();
////        }
////    }

////    private void DrawTopBar()
////    {
////        EditorGUILayout.Space(4);
////        asset = (AdventureAsset)EditorGUILayout.ObjectField("Adventure Asset", asset, typeof(AdventureAsset), false);
////        if (!asset)
////        {
////            EditorGUILayout.HelpBox("Назначь AdventureAsset (Create → Robinson → Adventure → Adventure Asset).", MessageType.Info);
////            return;
////        }

////        // Метаданные/размеры (ничего не создаём автоматически)
////        EditorGUI.BeginChangeCheck();
////        using (new EditorGUILayout.HorizontalScope())
////        {
////            asset.displayName = EditorGUILayout.TextField("Display Name", asset.displayName, GUILayout.MinWidth(120));
////            asset.version = EditorGUILayout.IntField("Version", asset.version, GUILayout.MaxWidth(200));
////        }
////        using (new EditorGUILayout.HorizontalScope())
////        {
////            asset.width = EditorGUILayout.IntSlider("Width", Mathf.Max(1, asset.width), 1, 100);
////            asset.height = EditorGUILayout.IntSlider("Height", Mathf.Max(1, asset.height), 1, 100);
////        }
////        if (EditorGUI.EndChangeCheck())
////        {
////            Undo.RecordObject(asset, "Edit asset props");
////            EditorUtility.SetDirty(asset);
////            RebuildCellIndex();
////            Repaint();
////        }

////        using (new EditorGUILayout.HorizontalScope())
////        {
////            if (GUILayout.Button("Ensure Missing (Empty)", GUILayout.Width(170)))
////            {
////                Undo.RecordObject(asset, "EnsureMissingEmpty");
////                EnsureMissingCellsEmpty(asset);
////                EditorUtility.SetDirty(asset);
////                RebuildCellIndex();
////            }

////            if (GUILayout.Button("Reset Map (clear & fill Empty)", GUILayout.Width(230)))
////            {
////                if (EditorUtility.DisplayDialog("Сбросить карту?",
////                    "Это удалит все текущие клетки и события в ассете и создаст сетку заново как Empty.",
////                    "Да, сбросить", "Отмена"))
////                {
////                    Undo.RecordObject(asset, "ResetMap");
////                    ResetAllCellsToEmpty(asset);
////                    EditorUtility.SetDirty(asset);
////                    RebuildCellIndex();
////                }
////            }

////            if (GUILayout.Button("Validate", GUILayout.Width(100)))
////                ValidateAndReport();

////            GUILayout.FlexibleSpace();

////            if (GUILayout.Button("Apply to Scene (Builder)", GUILayout.Width(180)))
////                ApplyToScene(); // только через AdventureBuilder
////        }

////        EditorGUILayout.Space(4);
////        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
////    }

////    private void DrawPalette()
////    {
////        using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
////        {
////            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);

////            _brush = (BrushMode)GUILayout.Toolbar((int)_brush, new[] { "Terrain", "Event", "Visibility", "Barriers" });
////            EditorGUILayout.Space(6);
////            _tool = (Tool)GUILayout.Toolbar((int)_tool, new[] { "Paint", "Eyedropper", "Rect Fill" });

////            EditorGUILayout.Space(10);
////            if (_brush == BrushMode.Terrain)
////            {
////                EditorGUILayout.LabelField("Terrain Type", EditorStyles.miniBoldLabel);
////                _paintTerrain = (HexTerrainType)EditorGUILayout.EnumPopup(_paintTerrain);
////                DrawLegend();
////            }
////            else if (_brush == BrushMode.Event)
////            {
////                EditorGUILayout.LabelField("Event Catalog", EditorStyles.miniBoldLabel);
////                using (new EditorGUILayout.HorizontalScope())
////                {
////                    _eventFilter = EditorGUILayout.TextField(_eventFilter);
////                    if (GUILayout.Button("⟳", GUILayout.Width(28))) RefreshEventCatalog();
////                }

////                using (var scroll = new EditorGUILayout.ScrollViewScope(_eventScroll, GUILayout.Height(340)))
////                {
////                    _eventScroll = scroll.scrollPosition;

////                    var filtered = string.IsNullOrWhiteSpace(_eventFilter)
////                        ? _allEvents
////                        : _allEvents.Where(e => e.name.IndexOf(_eventFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

////                    if (GUILayout.Button("— None (clear event) —")) _selectedEvent = null;

////                    foreach (var ev in filtered)
////                    {
////                        var isSel = _selectedEvent == ev;                                 // выделяем выбранную строку визуально
////                        using (new EditorGUILayout.HorizontalScope(isSel ? "SelectionRect" : GUIStyle.none))
////                        {
////                            // --- ИКОНКА события из поля EventSO.icon ---
////                            var spr = ev.icon;                                            // спрайт-иконка из ассета
////                            var iconRect = GUILayoutUtility.GetRect(30, 30,               // зарезервируем место слева
////                                              GUILayout.Width(30), GUILayout.Height(30));

////                            if (spr != null)
////                            {
////                                // быстрый предпросмотр (если уже прогрет), иначе рисуем саму текстуру спрайта с корректными UV
////                                var tex = AssetPreview.GetAssetPreview(spr) ?? AssetPreview.GetMiniThumbnail(spr);
////                                if (tex != null) GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit, true);
////                                else if (spr.texture != null) GUI.DrawTextureWithTexCoords(iconRect, spr.texture, GetSpriteUV(spr), true);
////                            }
////                            else
////                            {
////                                // запасной вариант — миниатюра ScriptableObject
////                                var mini = AssetPreview.GetMiniThumbnail(ev);
////                                if (mini) GUI.DrawTexture(iconRect, mini, ScaleMode.ScaleToFit, true);
////                            }

////                            // --- СТИЛЬ ДЛЯ НАЗВАНИЯ: цвет по типу события ---
////                            var nameStyle = new GUIStyle(GUI.skin.label);                 // копия базового стиля «label»
////                                                                                          // Combat = красный, Choice = фиолетовый, Resource = чёрный, иначе — стандартный
////                            if (ev.isCombat) nameStyle.normal.textColor = new Color(1f, 0.25f, 0.25f);
////                            else if (ev.isChoice) nameStyle.normal.textColor = new Color(0.7f, 0.4f, 1f);
////                            else if (ev.isResource) nameStyle.normal.textColor = Color.white;

////                            // Клик по имени — выбираем событие для кисти «Event»
////                            if (GUILayout.Button(ev.name, nameStyle)) _selectedEvent = ev;

////                            // Служебные кнопки — «подсветить» ассет в Project и открыть в инспекторе
////                            if (GUILayout.Button("Ping", GUILayout.Width(44))) EditorGUIUtility.PingObject(ev);
////                            if (GUILayout.Button("Open", GUILayout.Width(48))) Selection.activeObject = ev;
////                        }
////                    }

////                }
////            }
////            else if (_brush == BrushMode.Barriers)
////            {
////                EditorGUILayout.LabelField("Barriers Tool", EditorStyles.miniBoldLabel);

////                // Операция
////                _barOp = (BarrierOp)GUILayout.Toolbar((int)_barOp, new[] { "Add", "Remove 1st", "Clear" });

////                // Что добавляем (актуально только для Add)
////                using (new EditorGUI.DisabledScope(_barOp != BarrierOp.Add))
////                {
////                    EditorGUILayout.Space(4);
////                    EditorGUILayout.LabelField("Value to Add", EditorStyles.miniBoldLabel);
////                    using (new EditorGUILayout.HorizontalScope())
////                    {
////                        if (GUILayout.Toggle(_barPaintValue == 1, "+1", "Button")) _barPaintValue = 1;
////                        if (GUILayout.Toggle(_barPaintValue == 3, "+3", "Button")) _barPaintValue = 3;
////                    }
////                }

////                // (Необязательно) спрайты-иконки для предпросмотра на канвасе
////                EditorGUILayout.Space(6);
////                _barIcon1 = (Sprite)EditorGUILayout.ObjectField("Icon +1 (optional)", _barIcon1, typeof(Sprite), false);
////                _barIcon3 = (Sprite)EditorGUILayout.ObjectField("Icon +3 (optional)", _barIcon3, typeof(Sprite), false);

////                EditorGUILayout.HelpBox(
////                    "На клетке можно держать до трёх фишек. Add — добавляет (+1/+3), Remove 1st — снимает первую, Clear — очищает все. " +
////                    "Барьеры учитываются только для simple-событий (увеличивают Main Cost Amount).",
////                    MessageType.None);
////            }

////            else
////            {
////                EditorGUILayout.LabelField("Visibility Brush", EditorStyles.miniBoldLabel);
////                _paintVisible = EditorGUILayout.ToggleLeft("Visible = true", _paintVisible);
////                EditorGUILayout.HelpBox("Кисть Visibility ставит Visible=true/false.", MessageType.None);
////            }

////            EditorGUILayout.Space(6);
////            _hexRadius = EditorGUILayout.Slider(new GUIContent("Hex Radius (px)"), _hexRadius, 16, 96);
////            _zoom = EditorGUILayout.Slider(new GUIContent("Zoom"), _zoom, 0.5f, 2.0f);
////            _padX = EditorGUILayout.Slider(new GUIContent("Canvas Pad X"), _padX, 0f, 200f);
////            _padY = EditorGUILayout.Slider(new GUIContent("Canvas Pad Y"), _padY, 0f, 200f);
////        }
////    }

////    private void DrawLegend()
////    {
////        EditorGUILayout.Space(6);
////        EditorGUILayout.LabelField("Legend", EditorStyles.miniBoldLabel);
////        foreach (var kv in _terrainColor)
////        {
////            using (new EditorGUILayout.HorizontalScope())
////            {
////                var c = kv.Value; c.a = 1f;
////                var rect = GUILayoutUtility.GetRect(18, 14, GUILayout.Width(24));
////                EditorGUI.DrawRect(rect, c);
////                EditorGUILayout.LabelField(kv.Key.ToString());
////            }
////        }
////    }

////    private AdventureCell _hoverCell;        // реальная клетка (если есть)
////    private (int x, int y)? _hoverCoord;     // координаты под курсором

////    private void DrawInspector()
////    {
////        using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
////        {
////            EditorGUILayout.LabelField("Cell Inspector", EditorStyles.boldLabel);

////            if (_hoverCoord == null)
////            {
////                EditorGUILayout.HelpBox("Наведись/кликни на гекс на канвасе, чтобы редактировать свойства клетки.", MessageType.Info);
////                return;
////            }

////            var (hx, hy) = _hoverCoord.Value;
////            var exists = _cells.TryGetValue((hx, hy), out var real);
////            var view = exists ? real : GetTempViewCell(hx, hy);

////            EditorGUI.BeginChangeCheck();
////            EditorGUILayout.LabelField($"Cell: ({hx}, {hy})", EditorStyles.miniBoldLabel);
////            view.visible = EditorGUILayout.ToggleLeft("Visible", view.visible);
////            view.terrain = (HexTerrainType)EditorGUILayout.EnumPopup("Terrain", view.terrain);
////            view.eventAsset = (EventSO)EditorGUILayout.ObjectField("Event", view.eventAsset, typeof(EventSO), false);
////            if (EditorGUI.EndChangeCheck())
////            {
////                if (!exists) { CommitTempToAsset(hx, hy, view); exists = true; real = _cells[(hx, hy)]; }
////                real.visible = view.visible;
////                real.terrain = view.terrain;
////                real.eventAsset = view.eventAsset;

////                // Если вручную поставили Terrain = Empty — обязательно сбрасываем событие (твоя просьба)
////                if (real.terrain == HexTerrainType.Empty)
////                    real.eventAsset = null;

////                // Если назначили событие — выставим тип Event (как удобно)
////                if (real.eventAsset != null)
////                    real.terrain = HexTerrainType.Event;

////                Undo.RecordObject(asset, "Edit Cell");
////                EditorUtility.SetDirty(asset);
////                Repaint();
////            }

////            // --- ПАНЕЛЬ БАРЬЕРОВ ДЛЯ ВЫБРАННОЙ КЛЕТКИ ---
////            EditorGUILayout.Space(8);
////            EditorGUILayout.LabelField("Barriers", EditorStyles.boldLabel);

////            // Пояснение, если это не Event или нет события
////            if (view.terrain != HexTerrainType.Event || view.eventAsset == null)
////            {
////                EditorGUILayout.HelpBox("Барьеры влияют только на клетки с простым событием (Simple). " +
////                                        "Тем не менее их можно задать заранее — они будут учтены, если здесь появится Simple-событие.",
////                                        MessageType.Info);
////            }

////            // Кнопки управления
////            using (new EditorGUILayout.HorizontalScope())
////            {
////                if (GUILayout.Button("+1", GUILayout.Width(40)))
////                {
////                    if (!exists) { CommitTempToAsset(hx, hy, view); exists = true; real = _cells[(hx, hy)]; }
////                    real.barriers ??= new List<int>();
////                    if (real.barriers.Count < 3) real.barriers.Add(1);
////                    Undo.RecordObject(asset, "Add +1 Barrier");
////                    EditorUtility.SetDirty(asset);
////                    Repaint();
////                }

////                if (GUILayout.Button("+3", GUILayout.Width(40)))
////                {
////                    if (!exists) { CommitTempToAsset(hx, hy, view); exists = true; real = _cells[(hx, hy)]; }
////                    real.barriers ??= new List<int>();
////                    if (real.barriers.Count < 3) real.barriers.Add(3);
////                    Undo.RecordObject(asset, "Add +3 Barrier");
////                    EditorUtility.SetDirty(asset);
////                    Repaint();
////                }

////                if (GUILayout.Button("Remove 1st", GUILayout.Width(90)))
////                {
////                    if (exists && real.barriers != null && real.barriers.Count > 0)
////                    {
////                        real.barriers.RemoveAt(0);
////                        Undo.RecordObject(asset, "Remove First Barrier");
////                        EditorUtility.SetDirty(asset);
////                        Repaint();
////                    }
////                }

////                if (GUILayout.Button("Clear", GUILayout.Width(60)))
////                {
////                    if (exists && real.barriers != null)
////                    {
////                        real.barriers.Clear();
////                        Undo.RecordObject(asset, "Clear Barriers");
////                        EditorUtility.SetDirty(asset);
////                        Repaint();
////                    }
////                }
////            }

////            // Текущее содержимое
////            string cur = (exists && real.barriers != null && real.barriers.Count > 0)
////                ? string.Join(", ", real.barriers.Select(v => v >= 3 ? "3" : "1"))
////                : "—";
////            EditorGUILayout.LabelField($"Current: {cur} (max 3)");


////            using (new EditorGUILayout.HorizontalScope())
////            {
////                if (view.eventAsset != null && GUILayout.Button("Ping Event")) EditorGUIUtility.PingObject(view.eventAsset);
////                if (view.eventAsset != null && GUILayout.Button("Open Event")) Selection.activeObject = view.eventAsset;
////            }

////            if (!view.visible)
////                EditorGUILayout.HelpBox("Эта клетка невидима (будет скрыта в сцене).", MessageType.Warning);
////        }
////    }



////    private void DrawCanvas()
////    {
////        if (!asset) return;

////        var area = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
////        using (var view = new GUI.ScrollViewScope(area, _canvasScroll, GetCanvasRect(), true, true))
////        {
////            _canvasScroll = view.scrollPosition;

////            // Геометрия совпадает с твоим генератором (flat‑top, шаг X = 0.75 ширины, Y = √3/2 от ширины). :contentReference[oaicite:4]{index=4}
////            for (int y = 0; y < asset.height; y++)
////                for (int x = 0; x < asset.width; x++)
////                {
////                    var cell = _cells.TryGetValue((x, y), out var real) ? real : GetTempViewCell(x, y);

////                    float cx = HexCenterX(x);
////                    float cy = HexCenterY(x, y);

////                    var poly = BuildHex(cx, cy, _hexRadius * _zoom);
////                    var fill = CellColor(cell);

////                    // 1) заливка
////                    Handles.color = fill;
////                    Handles.DrawAAConvexPolygon(poly);

////                    // 2) контур
////                    Handles.color = _outlineColor;
////                    Handles.DrawAAPolyLine(2f, Close(poly));

////                    // 3) иконка + имя события (если есть EventSO и клетка видимая)
////                    if (cell.visible && cell.eventAsset != null)
////                    {
////                        // Сначала пытаемся взять ИМЕННО ИКОНКУ события (поле icon у EventSO). :contentReference[oaicite:5]{index=5}
////                        var spr = cell.eventAsset.icon;
////                        float iconSize = 50f * _zoom;

////                        // Иконка слева от текста (в одну строку)
////                        var iconRect = new Rect(cx - 25 * _zoom, cy - 35 * _zoom, iconSize, iconSize);
////                        var labelRect = new Rect(cx - 48 * _zoom, cy + 15 * _zoom, 96 * _zoom, 16 * _zoom);

////                        // Рисуем спрайт‑иконку: пробуем быстрый превью; если null — рисуем текстуру спрайта с UV
////                        if (spr != null)
////                        {
////                            var texPreview = AssetPreview.GetAssetPreview(spr) ?? AssetPreview.GetMiniThumbnail(spr);
////                            if (texPreview != null)
////                                GUI.DrawTexture(iconRect, texPreview, ScaleMode.ScaleToFit, true);
////                            else if (spr.texture != null)
////                                GUI.DrawTextureWithTexCoords(iconRect, spr.texture, GetSpriteUV(spr), true);
////                        }
////                        else
////                        {
////                            // Фолбэк: если у события нет icon — можно показать миниатюру самого EventSO
////                            var mini = AssetPreview.GetMiniThumbnail(cell.eventAsset);
////                            if (mini) GUI.DrawTexture(iconRect, mini, ScaleMode.ScaleToFit, true);
////                        }

////                        // Подпись события
////                        var name = cell.eventAsset.name;
////                        if (name.Length > 18) name = name[..18];
////                        GUI.Label(labelRect, name, _centerMiniLabel);

////                        // 3.1) предпросмотр барьеров: рисуем до 3 штук под подписью
////                        if (cell.barriers != null && cell.barriers.Count > 0)
////                        {
////                            float chip = 14f * _zoom;       // размер «фишки» в пикселях
////                            float gap = 2f * _zoom;        // зазор
////                                                           // центрируем три слота по низу гекса
////                            float totalW = 3 * chip + 2 * gap;
////                            float left = cx - totalW * 0.5f;
////                            float yChips = cy + 30f * _zoom;

////                            for (int i = 0; i < 3; i++)
////                            {
////                                var r = new Rect(left + i * (chip + gap), yChips, chip, chip);

////                                if (i < cell.barriers.Count)
////                                {
////                                    int v = cell.barriers[i] >= 3 ? 3 : 1;

////                                    // если заданы спрайты — рисуем их; иначе — цветные кружочки
////                                    Sprite chipSpr = (v == 3) ? _barIcon3 : _barIcon1;
////                                    if (chipSpr && chipSpr.texture)
////                                        GUI.DrawTextureWithTexCoords(r, chipSpr.texture, GetSpriteUV(chipSpr), true);
////                                    else
////                                    {
////                                        var col = (v == 3) ? new Color(1f, 0.55f, 0.1f, 1f) : new Color(0.25f, 0.6f, 1f, 1f);
////                                        Handles.color = col;
////                                        Handles.DrawSolidDisc(r.center, Vector3.forward, r.width * 0.5f);
////                                        Handles.color = Color.black;
////                                        Handles.DrawWireDisc(r.center, Vector3.forward, r.width * 0.5f);
////                                    }
////                                }
////                                else
////                                {
////                                    // пустой слот — тонкий контур
////                                    Handles.color = new Color(0, 0, 0, 0.6f);
////                                    Handles.DrawWireDisc(r.center, Vector3.forward, r.width * 0.5f);
////                                }
////                            }
////                        }
////                    }

////                    // 4) hover/клик
////                    var e = Event.current;
////                    if (e.isMouse && (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
////                    {
////                        if (PointInPoly(e.mousePosition, poly))
////                        {
////                            _hoverCoord = (x, y);
////                            _hoverCell = _cells.TryGetValue((x, y), out var r) ? r : null;

////                            if (e.type == EventType.MouseDown && e.button == 0)
////                            {
////                                OnPaintAt(x, y);
////                                e.Use();
////                            }
////                            else if (e.type == EventType.MouseDrag && e.button == 0 && _tool == Tool.RectFill)
////                            {
////                                if (!_isRectSelecting) { _isRectSelecting = true; _rectStartCell = (x, y); }
////                                PaintRect(_rectStartCell.Value, (x, y));
////                                e.Use();
////                            }
////                        }
////                    }
////                }

////            if (Event.current.type == EventType.MouseUp)
////            {
////                _isRectSelecting = false;
////                _rectStartCell = null;
////            }
////        }
////    }

////    // ---------- Геометрия/утилиты рисования ----------
////    private Rect GetCanvasRect()
////    {
////        var W = 2f * _hexRadius * _zoom;
////        var H = Mathf.Sqrt(3f) * _hexRadius * _zoom;
////        var totalW = (asset.width - 1) * (W * 0.75f) + W;
////        var totalH = (asset.height + 0.5f) * H;
////        return new Rect(0, 0, _padX + totalW + _padX, _padY + totalH + _padY);
////    }

////    private float HexCenterX(int x) => (_padX + _hexRadius * _zoom) + x * (2f * _hexRadius * 0.75f) * _zoom;
////    private float HexCenterY(int x, int y)
////    {
////        var H = Mathf.Sqrt(3f) * _hexRadius * _zoom;
////        var half = 0.5f * H;
////        return (_padY + half) + (y + (x % 2) * 0.5f) * H;
////    }

////    private Vector3[] BuildHex(float cx, float cy, float r)
////    {
////        var pts = new Vector3[6];
////        for (int i = 0; i < 6; i++)
////        {
////            float ang = Mathf.Deg2Rad * (60f * i);
////            pts[i] = new Vector3(cx + r * Mathf.Cos(ang), cy + r * Mathf.Sin(ang), 0);
////        }
////        return pts;
////    }

////    private Vector3[] Close(Vector3[] poly)
////    {
////        var arr = new Vector3[poly.Length + 1];
////        Array.Copy(poly, arr, poly.Length);
////        arr[^1] = poly[0];
////        return arr;
////    }

////    private bool PointInPoly(Vector2 p, Vector3[] poly)
////    {
////        bool inside = false;
////        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
////        {
////            var pi = (Vector2)poly[i];
////            var pj = (Vector2)poly[j];
////            bool inter = ((pi.y > p.y) != (pj.y > p.y)) &&
////                         (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y + 1e-5f) + pi.x);
////            if (inter) inside = !inside;
////        }
////        return inside;
////    }

////    // Цвет предпросмотра
////    private Color CellColor(AdventureCell c)
////    {
////        if (!_terrainColor.TryGetValue(c.terrain, out var baseCol))
////            baseCol = _terrainColor[HexTerrainType.Empty];
////        baseCol.a = c.visible ? 1f : 0.25f;
////        return baseCol;
////    }

////    // Нормализованные UV белого прямоугольника спрайта (если рисуем через DrawTextureWithTexCoords)
////    private static Rect GetSpriteUV(Sprite s)
////    {
////        if (!s || !s.texture) return new Rect(0, 0, 1, 1);
////        var tr = s.textureRect;
////        var tex = s.texture;
////        return new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
////    }

////    // ---------- РЕДАКТИРОВАНИЕ КЛЕТОК ----------
////    private AdventureCell GetTempViewCell(int x, int y)
////    {
////        if (_temp.TryGetValue((x, y), out var c)) return c;
////        c = new AdventureCell { x = x, y = y, visible = true, terrain = HexTerrainType.Empty, eventAsset = null, barriers = new List<int>() };
////        _temp[(x, y)] = c;
////        return c;
////    }

////    private void CommitTempToAsset(int x, int y, AdventureCell view)
////    {
////        var real = new AdventureCell
////        {
////            x = x,
////            y = y,
////            visible = view.visible,
////            terrain = view.terrain,
////            eventAsset = view.eventAsset,
////            barriers = (view.barriers != null) ? new List<int>(view.barriers) : new List<int>()
////        };
////        asset.cells.Add(real);
////        _cells[(x, y)] = real;
////        EditorUtility.SetDirty(asset);
////    }

////    private void OnPaintAt(int x, int y)
////    {
////        var exists = _cells.TryGetValue((x, y), out var cell);
////        if (!exists) { var temp = GetTempViewCell(x, y); CommitTempToAsset(x, y, temp); cell = _cells[(x, y)]; }

////        Undo.RecordObject(asset, "Paint cell");

////        switch (_tool)
////        {
////            case Tool.Eyedropper:
////                if (_brush == BrushMode.Terrain) _paintTerrain = cell.terrain;
////                else if (_brush == BrushMode.Event) _selectedEvent = cell.eventAsset;
////                else _paintVisible = cell.visible;
////                break;

////            case Tool.Paint:
////                if (_brush == BrushMode.Terrain)
////                {
////                    cell.terrain = _paintTerrain;
////                    // ТВОЁ ПРАВИЛО: если переводим в Empty (или Blocked) — обязательно очищаем событие.
////                    if (cell.terrain == HexTerrainType.Empty || cell.terrain == HexTerrainType.Blocked)
////                        cell.eventAsset = null;
////                }
////                else if (_brush == BrushMode.Event)
////                {
////                    cell.eventAsset = _selectedEvent;
////                    if (cell.eventAsset != null)
////                        cell.terrain = HexTerrainType.Event;
////                }
////                else if (_brush == BrushMode.Barriers)
////                {
////                    cell.barriers ??= new List<int>();

////                    if (_barOp == BarrierOp.Add)
////                    {
////                        if (cell.barriers.Count < 3)
////                            cell.barriers.Add(_barPaintValue >= 3 ? 3 : 1);
////                    }
////                    else if (_barOp == BarrierOp.RemoveFirst)
////                    {
////                        if (cell.barriers.Count > 0) cell.barriers.RemoveAt(0);
////                    }
////                    else // Clear
////                    {
////                        cell.barriers.Clear();
////                    }
////                }
////                else
////                {
////                    cell.visible = _paintVisible;
////                }
////                EditorUtility.SetDirty(asset);
////                break;

////            case Tool.RectFill:
////                goto case Tool.Paint;
////        }

////        Repaint();
////    }

////    private bool _isRectSelecting;
////    private (int x, int y)? _rectStartCell;

////    private void PaintRect((int x, int y) from, (int x, int y) to)
////    {
////        int x0 = Math.Min(from.x, to.x);
////        int x1 = Math.Max(from.x, to.x);
////        int y0 = Math.Min(from.y, to.y);
////        int y1 = Math.Max(from.y, to.y);

////        Undo.RecordObject(asset, "Rect fill");

////        for (int yy = y0; yy <= y1; yy++)
////            for (int xx = x0; xx <= x1; xx++)
////            {
////                if (xx < 0 || xx >= asset.width || yy < 0 || yy >= asset.height) continue;

////                var exists = _cells.TryGetValue((xx, yy), out var cell);
////                if (!exists) { var temp = GetTempViewCell(xx, yy); CommitTempToAsset(xx, yy, temp); cell = _cells[(xx, yy)]; }

////                if (_brush == BrushMode.Terrain)
////                {
////                    cell.terrain = _paintTerrain;
////                    if (cell.terrain == HexTerrainType.Empty || cell.terrain == HexTerrainType.Blocked)
////                        cell.eventAsset = null; // ← тоже чистим при заливке
////                }
////                else if (_brush == BrushMode.Event)
////                {
////                    cell.eventAsset = _selectedEvent;
////                    if (cell.eventAsset != null)
////                        cell.terrain = HexTerrainType.Event;
////                }
////                else if (_brush == BrushMode.Barriers)
////                {
////                    cell.barriers ??= new List<int>();

////                    if (_barOp == BarrierOp.Add)
////                    {
////                        if (cell.barriers.Count < 3)
////                            cell.barriers.Add(_barPaintValue >= 3 ? 3 : 1);
////                    }
////                    else if (_barOp == BarrierOp.RemoveFirst)
////                    {
////                        if (cell.barriers.Count > 0) cell.barriers.RemoveAt(0);
////                    }
////                    else // Clear
////                    {
////                        cell.barriers.Clear();
////                    }
////                }
////                else
////                {
////                    cell.visible = _paintVisible;
////                }
////            }

////        EditorUtility.SetDirty(asset);
////        Repaint();
////    }

////    // ---------- ВАЛИДАЦИЯ ----------
////    private void ValidateAndReport()
////    {
////        if (!asset) return;

////        var cells = asset.cells.Where(c => c != null && c.visible).ToList();
////        var starts = cells.Where(c => c.terrain == HexTerrainType.Start).ToList();
////        var exits = cells.Where(c => c.terrain == HexTerrainType.Exit).ToList();

////        if (starts.Count != 1) Debug.LogWarning($"[Adventure Editor] Требуется ровно 1 Start. Сейчас: {starts.Count}");
////        if (exits.Count == 0) Debug.LogWarning("[Adventure Editor] Рекомендуется иметь хотя бы 1 Exit.");

////        if (starts.Count == 1)
////        {
////            var start = starts[0];
////            var passable = new HashSet<(int, int)>(cells.Where(c => c.terrain != HexTerrainType.Blocked).Select(c => (c.x, c.y)));

////            var visited = new HashSet<(int, int)>();
////            var q = new Queue<(int, int)>();
////            q.Enqueue((start.x, start.y));
////            visited.Add((start.x, start.y));

////            while (q.Count > 0)
////            {
////                var (x, y) = q.Dequeue();
////                foreach (var (nx, ny) in Neighbors(x, y))
////                {
////                    if (nx < 0 || nx >= asset.width || ny < 0 || ny >= asset.height) continue;
////                    if (!passable.Contains((nx, ny))) continue;
////                    if (visited.Contains((nx, ny))) continue;
////                    visited.Add((nx, ny));
////                    q.Enqueue((nx, ny));
////                }
////            }

////            var unreachable = passable.Except(visited).ToList();
////            if (unreachable.Count > 0) Debug.LogWarning($"[Adventure Editor] Недостижимых клеток: {unreachable.Count}.");
////            else Debug.Log("[Adventure Editor] OK: все проходимые клетки достижимы от Start.");
////        }
////    }

////    private IEnumerable<(int x, int y)> Neighbors(int x, int y)
////    {
////        var dirs = (x % 2 == 0)
////            ? new (int dx, int dy)[] { (+1, 0), (0, +1), (-1, 0), (-1, -1), (0, -1), (+1, -1) }
////            : new (int dx, int dy)[] { (+1, +1), (0, +1), (-1, +1), (-1, 0), (0, -1), (+1, 0) };
////        foreach (var d in dirs) yield return (x + d.dx, y + d.dy);
////    }

////    // ---------- ПРИМЕНЕНИЕ К СЦЕНЕ ----------
////    private void ApplyToScene()
////    {
////        if (!asset) return;

////        // ТОЛЬКО AdventureBuilder: он пересоберёт сетку и вызовет BindEvent у HexTile (бейдж и т.п.). :contentReference[oaicite:6]{index=6}
////        var builder = FindFirstObjectByType<AdventureBuilder>(FindObjectsInactive.Include);
////        if (builder)
////        {
////            Undo.RecordObject(builder, "Assign Adventure");
////            var so = new SerializedObject(builder);
////            so.FindProperty("adventure").objectReferenceValue = asset;
////            so.ApplyModifiedPropertiesWithoutUndo();
////            builder.BuildAll();
////            return;
////        }

////        EditorUtility.DisplayDialog(
////            "Не найден AdventureBuilder",
////            "В сцене нет AdventureBuilder. Добавь его на пустой GameObject, назначь Hex Prefab / Grid Root и повтори.",
////            "Ок");
////    }

////    // ---------- ХЕЛПЕРЫ ЗАПОЛНЕНИЯ ----------
////    private void EnsureMissingCellsEmpty(AdventureAsset a)
////    {
////        if (a.cells == null) a.cells = new List<AdventureCell>();
////        for (int yy = 0; yy < a.height; yy++)
////            for (int xx = 0; xx < a.width; xx++)
////            {
////                if (_cells.ContainsKey((xx, yy))) continue;
////                var c = new AdventureCell { x = xx, y = yy, visible = true, terrain = HexTerrainType.Empty, eventAsset = null, barriers = new List<int>() };
////                a.cells.Add(c);
////                _cells[(xx, yy)] = c;
////            }
////    }

////    private void ResetAllCellsToEmpty(AdventureAsset a)
////    {
////        a.cells ??= new List<AdventureCell>();
////        a.cells.Clear();
////        for (int yy = 0; yy < a.height; yy++)
////            for (int xx = 0; xx < a.width; xx++)
////                a.cells.Add(new AdventureCell { x = xx, y = yy, visible = true, terrain = HexTerrainType.Empty, eventAsset = null, barriers = new List<int>() });
////    }
////}
////#endif
