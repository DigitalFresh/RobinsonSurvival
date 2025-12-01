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
    private enum BrushMode { Terrain, Event, Visibility, Barriers, Appearance }
    private BrushMode _brush = BrushMode.Terrain;

    private enum Tool { Paint, Eyedropper, RectFill }
    private Tool _tool = Tool.Paint;

    // Terrain: кисти + Selection
    private enum TerrainPick { Select, Empty, Event, Blocked, Start, Exit }
    private TerrainPick _terrainPick = TerrainPick.Select;

    private bool _paintVisible = true;
    private bool _paintRevealed = false;

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

    [System.Serializable]
    private struct StyleClipboard
    {
        public SpriteSheetSet unrevSet;   // набор для закрытого
        public SpriteSheetSet blockedSet; // набор для Blocked
        public SpriteSheetSet revSet;     // набор для Revealed

        public AdventureAsset.SpritePickRule unrev;   // правило выбора кадра для закрытого
        public AdventureAsset.SpritePickRule blocked; // ... для Blocked
        public AdventureAsset.SpritePickRule rev;     // ... для Revealed
    }
    private StyleClipboard? _styleClip; // null = пусто

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
            _brush = (BrushMode)GUILayout.Toolbar((int)_brush, new[] { "Terrain", "Event", "Visibility", "Barriers", "Appearance" });
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
            else if (_brush == BrushMode.Appearance)
            {
                EditorGUILayout.LabelField("Appearance Brush", EditorStyles.miniBoldLabel);
                EditorGUILayout.HelpBox(
                    "Eyedropper — клик по гексу копирует его наборы и правила (3 состояния). " +
                    "Paint — клик/прямоугольная заливка вставляет стиль. " +
                    "Rect Fill — тяните мышью прямоугольник для массового применения.",
                    MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Clear Clipboard", GUILayout.Width(140))) _styleClip = null;
                    GUILayout.FlexibleSpace();
                }

                // Превью клипборда (если есть)
                if (_styleClip.HasValue)
                {
                    var clip = _styleClip.Value;
                    using (new EditorGUILayout.VerticalScope("HelpBox"))
                    {
                        EditorGUILayout.LabelField("Clipboard:", EditorStyles.miniBoldLabel);
                        EditorGUILayout.ObjectField("Unrevealed Set", clip.unrevSet, typeof(SpriteSheetSet), false);
                        EditorGUILayout.IntField("Unrev Fixed", clip.unrev.fixedIndex);
                        EditorGUILayout.LabelField("Unrev Pool", string.Join(",", clip.unrev.pool ?? new System.Collections.Generic.List<int>()));

                        EditorGUILayout.Space(4);
                        EditorGUILayout.ObjectField("Blocked Set", clip.blockedSet, typeof(SpriteSheetSet), false);
                        EditorGUILayout.IntField("Blocked Fixed", clip.blocked.fixedIndex);
                        EditorGUILayout.LabelField("Blocked Pool", string.Join(",", clip.blocked.pool ?? new System.Collections.Generic.List<int>()));

                        EditorGUILayout.Space(4);
                        EditorGUILayout.ObjectField("Revealed Set", clip.revSet, typeof(SpriteSheetSet), false);
                        EditorGUILayout.IntField("Rev Fixed", clip.rev.fixedIndex);
                        EditorGUILayout.LabelField("Rev Pool", string.Join(",", clip.rev.pool ?? new System.Collections.Generic.List<int>()));
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Клипборд пуст. Включите Eyedropper и кликните по гексу, чтобы скопировать стиль.", MessageType.None);
                }
            }
            else
            {
                //_paintVisible = EditorGUILayout.ToggleLeft("Visible = true", _paintVisible);

                EditorGUILayout.LabelField("Visibility Brush", EditorStyles.miniBoldLabel);

                // Кисть «видимость»
                _paintVisible = EditorGUILayout.ToggleLeft("Visible", _paintVisible);

                // Кисть «раскрыт?» — логично разрешать только если клетка видима
                using (new EditorGUI.DisabledScope(!_paintVisible))
                {
                    _paintRevealed = EditorGUILayout.ToggleLeft("Revealed", _paintRevealed);
                }

                EditorGUILayout.HelpBox(
                    "Paint: клик — установить Visible/Revealed на клетке.\n" +
                    "Eyedropper: забрать значения Visible/Revealed с клетки.\n" +
                    "Rect Fill: заливка прямоугольника текущими значениями.",
                    MessageType.None);
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
            view.revealed = EditorGUILayout.ToggleLeft("Reveal", view.revealed);
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
        c = new AdventureCell
        {
            x = x,
            y = y,
            visible = true,
            revealed = false,         
            terrain = HexTerrainType.Empty,
            eventAsset = null,
            barriers = new List<int>()
        };
        _temp[(x, y)] = c; return c;
    }

    private void CommitTempToAsset(int x, int y, AdventureCell view)
    {
        var real = new AdventureCell
        {
            x = x,
            y = y,
            visible = view.visible,
            revealed = view.revealed,   
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

                else if (_brush == BrushMode.Appearance)
                {
                    // скопировать стиль с клетки
                    AdventureCell src;
                    var ok = _cells.TryGetValue((x, y), out src);
                    if (!ok) { var temp = GetTempViewCell(x, y); CommitTempToAsset(x, y, temp); src = _cells[(x, y)]; }

                    _styleClip = MakeClipFromCell(src);
                    EditorUtility.DisplayDialog("Eyedropper", $"Стиль с клетки ({x},{y}) скопирован.", "OK");
                    Repaint();
                }
                else
                {     
                    _paintVisible = cell.visible;
                    _paintRevealed = cell.revealed;
                }
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
                else if (_brush == BrushMode.Appearance)
                {
                    if (_styleClip.HasValue)
                    {
                        ApplyClipToCell(cell, _styleClip.Value);
                        EditorUtility.SetDirty(asset);
                    }
                    Repaint();
                }
                else // Visibility
                {
                    cell.visible = _paintVisible;
                    cell.revealed = _paintRevealed;
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
                else if (_styleClip.HasValue)
                {
                    ApplyClipToCell(cell, _styleClip.Value);
                }
                else // Visibility
                {
                    cell.visible = _paintVisible;
                    cell.revealed = _paintRevealed;
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

    // Глубокая копия SpritePickRule
    private static AdventureAsset.SpritePickRule CloneRule(AdventureAsset.SpritePickRule src)
    {
        var dst = new AdventureAsset.SpritePickRule();
        if (src != null)
        {
            dst.fixedIndex = src.fixedIndex;
            dst.pool = (src.pool != null) ? new System.Collections.Generic.List<int>(src.pool)
                                          : new System.Collections.Generic.List<int>();
        }
        else
        {
            dst.fixedIndex = -1;
            dst.pool = new System.Collections.Generic.List<int>();
        }
        return dst;
    }

    // Забрать стиль с ячейки в клипборд
    private static StyleClipboard MakeClipFromCell(AdventureCell c) // MakeClipFromCell(AdventureAsset.AdventureCell c)
    {
        return new StyleClipboard
        {
            unrevSet = c.backUnrevealedSet,
            blockedSet = c.backBlockedSet,
            revSet = c.backRevealedSet,
            unrev = CloneRule(c.backUnrevealed),
            blocked = CloneRule(c.backBlocked),
            rev = CloneRule(c.backRevealed)
        };
    }

    // Применить стиль к ячейке (без ref — класс по ссылке)
    private static void ApplyClipToCell(AdventureCell c, StyleClipboard clip) // ApplyClipToCell(AdventureAsset.AdventureCell c, StyleClipboard clip)
    {
        c.backUnrevealedSet = clip.unrevSet;
        c.backBlockedSet = clip.blockedSet;
        c.backRevealedSet = clip.revSet;

        c.backUnrevealed = CloneRule(clip.unrev);
        c.backBlocked = CloneRule(clip.blocked);
        c.backRevealed = CloneRule(clip.rev);
    }

}
#endif



