#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adventure Map Editor — интерактивное окно разметки карт AdventureAsset:
///  • слева палитра (Terrain / Event / Visibility),
///  • в центре — канвас гекс‑сетки (клик/заливка, предпросмотр),
///  • справа — инспектор выбранной клетки,
///  • Validate и Apply to Scene (только через AdventureBuilder).
/// Окно НЕ создаёт новые AdventureCell при простом отображении — записи появляются только при клике/заливке.
/// </summary>
public class AdventureMapEditorWindow : EditorWindow
{
    // ---------- СОСТОЯНИЕ ----------
    [SerializeField] private AdventureAsset asset;                  // редактируемый ассет карты  :contentReference[oaicite:3]{index=3}
    private Dictionary<(int x, int y), AdventureCell> _cells;      // реальные клетки ассета по координатам
    private Dictionary<(int x, int y), AdventureCell> _temp;       // «виртуальные» клетки для предпросмотра
    private AdventureAsset _lastAsset;                              // чтобы понять, что ассет сменили

    private Vector2 _canvasScroll;
    [SerializeField] private float _hexRadius = 48f;
    [SerializeField] private float _zoom = 1f;

    // Отступы, чтобы крупные гексы не резались краями
    [SerializeField] private float _padX = 80f;
    [SerializeField] private float _padY = 80f;

    // ---------- ПАЛИТРА / ИНСТРУМЕНТЫ ----------
    private enum BrushMode { Terrain, Event, Visibility }
    private BrushMode _brush = BrushMode.Terrain;

    private enum Tool { Paint, Eyedropper, RectFill }
    private Tool _tool = Tool.Paint;

    // По умолчанию создаём Empty
    private HexTerrainType _paintTerrain = HexTerrainType.Empty;
    private bool _paintVisible = true;

    // Каталог событий
    private string _eventFilter = "";
    private Vector2 _eventScroll;
    private EventSO _selectedEvent;
    private List<EventSO> _allEvents;

    // Визуальные стили/цвета
    private GUIStyle _centerMiniLabel;
    private readonly Color _outlineColor = new(0, 0, 0, 0.9f);
    private readonly Dictionary<HexTerrainType, Color> _terrainColor = new()
    {
        { HexTerrainType.Empty,   new Color(0.15f, 0.15f, 0.18f) },
        { HexTerrainType.Event,   new Color(0.96f, 0.62f, 0.24f) },
        { HexTerrainType.Blocked, new Color(0.40f, 0.40f, 0.40f) },
        { HexTerrainType.Start,   new Color(0.20f, 0.65f, 1.00f) },
        { HexTerrainType.Exit,    new Color(0.62f, 0.45f, 0.95f) },
    };

    // ---------- МЕНЮ ----------
    [MenuItem("Robinson/Adventure Map Editor")]
    public static void Open()
    {
        var w = GetWindow<AdventureMapEditorWindow>("Adventure Map Editor");
        w.minSize = new Vector2(900, 580);
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
    }

    // ---------- КАТАЛОГ СОБЫТИЙ ----------
    private void RefreshEventCatalog()
    {
        var guids = AssetDatabase.FindAssets("t:EventSO");
        _allEvents = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<EventSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(e => e != null)
            .OrderBy(e => e.name)
            .ToList();
    }

    // ---------- ИНДЕКС/КЕШ ----------
    private void RebuildCellIndex()
    {
        _cells = new Dictionary<(int, int), AdventureCell>();
        _temp = new Dictionary<(int, int), AdventureCell>();

        if (!asset) return;
        asset.cells ??= new List<AdventureCell>();
        asset.cells.RemoveAll(c => c == null);
        foreach (var c in asset.cells)
            _cells[(c.x, c.y)] = c;
    }

    // ---------- GUI ----------
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
        EditorGUILayout.Space(4);
        asset = (AdventureAsset)EditorGUILayout.ObjectField("Adventure Asset", asset, typeof(AdventureAsset), false);
        if (!asset)
        {
            EditorGUILayout.HelpBox("Назначь AdventureAsset (Create → Robinson → Adventure → Adventure Asset).", MessageType.Info);
            return;
        }

        // Метаданные/размеры (ничего не создаём автоматически)
        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.HorizontalScope())
        {
            asset.displayName = EditorGUILayout.TextField("Display Name", asset.displayName, GUILayout.MinWidth(120));
            asset.version = EditorGUILayout.IntField("Version", asset.version, GUILayout.MaxWidth(200));
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

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Ensure Missing (Empty)", GUILayout.Width(170)))
            {
                Undo.RecordObject(asset, "EnsureMissingEmpty");
                EnsureMissingCellsEmpty(asset);
                EditorUtility.SetDirty(asset);
                RebuildCellIndex();
            }

            if (GUILayout.Button("Reset Map (clear & fill Empty)", GUILayout.Width(230)))
            {
                if (EditorUtility.DisplayDialog("Сбросить карту?",
                    "Это удалит все текущие клетки и события в ассете и создаст сетку заново как Empty.",
                    "Да, сбросить", "Отмена"))
                {
                    Undo.RecordObject(asset, "ResetMap");
                    ResetAllCellsToEmpty(asset);
                    EditorUtility.SetDirty(asset);
                    RebuildCellIndex();
                }
            }

            if (GUILayout.Button("Validate", GUILayout.Width(100)))
                ValidateAndReport();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Apply to Scene (Builder)", GUILayout.Width(180)))
                ApplyToScene(); // только через AdventureBuilder
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    private void DrawPalette()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
        {
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);

            _brush = (BrushMode)GUILayout.Toolbar((int)_brush, new[] { "Terrain", "Event", "Visibility" });
            EditorGUILayout.Space(6);
            _tool = (Tool)GUILayout.Toolbar((int)_tool, new[] { "Paint", "Eyedropper", "Rect Fill" });

            EditorGUILayout.Space(10);
            if (_brush == BrushMode.Terrain)
            {
                EditorGUILayout.LabelField("Terrain Type", EditorStyles.miniBoldLabel);
                _paintTerrain = (HexTerrainType)EditorGUILayout.EnumPopup(_paintTerrain);
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

                using (var scroll = new EditorGUILayout.ScrollViewScope(_eventScroll, GUILayout.Height(340)))
                {
                    _eventScroll = scroll.scrollPosition;

                    var filtered = string.IsNullOrWhiteSpace(_eventFilter)
                        ? _allEvents
                        : _allEvents.Where(e => e.name.IndexOf(_eventFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                    if (GUILayout.Button("— None (clear event) —")) _selectedEvent = null;

                    foreach (var ev in filtered)
                    {
                        var isSel = _selectedEvent == ev;                                 // выделяем выбранную строку визуально
                        using (new EditorGUILayout.HorizontalScope(isSel ? "SelectionRect" : GUIStyle.none))
                        {
                            // --- ИКОНКА события из поля EventSO.icon ---
                            var spr = ev.icon;                                            // спрайт-иконка из ассета
                            var iconRect = GUILayoutUtility.GetRect(30, 30,               // зарезервируем место слева
                                              GUILayout.Width(30), GUILayout.Height(30));

                            if (spr != null)
                            {
                                // быстрый предпросмотр (если уже прогрет), иначе рисуем саму текстуру спрайта с корректными UV
                                var tex = AssetPreview.GetAssetPreview(spr) ?? AssetPreview.GetMiniThumbnail(spr);
                                if (tex != null) GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit, true);
                                else if (spr.texture != null) GUI.DrawTextureWithTexCoords(iconRect, spr.texture, GetSpriteUV(spr), true);
                            }
                            else
                            {
                                // запасной вариант — миниатюра ScriptableObject
                                var mini = AssetPreview.GetMiniThumbnail(ev);
                                if (mini) GUI.DrawTexture(iconRect, mini, ScaleMode.ScaleToFit, true);
                            }

                            // --- СТИЛЬ ДЛЯ НАЗВАНИЯ: цвет по типу события ---
                            var nameStyle = new GUIStyle(GUI.skin.label);                 // копия базового стиля «label»
                                                                                          // Combat = красный, Choice = фиолетовый, Resource = чёрный, иначе — стандартный
                            if (ev.isCombat) nameStyle.normal.textColor = new Color(1f, 0.25f, 0.25f);
                            else if (ev.isChoice) nameStyle.normal.textColor = new Color(0.7f, 0.4f, 1f);
                            else if (ev.isResource) nameStyle.normal.textColor = Color.white;

                            // Клик по имени — выбираем событие для кисти «Event»
                            if (GUILayout.Button(ev.name, nameStyle)) _selectedEvent = ev;

                            // Служебные кнопки — «подсветить» ассет в Project и открыть в инспекторе
                            if (GUILayout.Button("Ping", GUILayout.Width(44))) EditorGUIUtility.PingObject(ev);
                            if (GUILayout.Button("Open", GUILayout.Width(48))) Selection.activeObject = ev;
                        }
                    }

                }
            }
            else
            {
                EditorGUILayout.LabelField("Visibility Brush", EditorStyles.miniBoldLabel);
                _paintVisible = EditorGUILayout.ToggleLeft("Visible = true", _paintVisible);
                EditorGUILayout.HelpBox("Кисть Visibility ставит Visible=true/false.", MessageType.None);
            }

            EditorGUILayout.Space(6);
            _hexRadius = EditorGUILayout.Slider(new GUIContent("Hex Radius (px)"), _hexRadius, 16, 96);
            _zoom = EditorGUILayout.Slider(new GUIContent("Zoom"), _zoom, 0.5f, 2.0f);
            _padX = EditorGUILayout.Slider(new GUIContent("Canvas Pad X"), _padX, 0f, 200f);
            _padY = EditorGUILayout.Slider(new GUIContent("Canvas Pad Y"), _padY, 0f, 200f);
        }
    }

    private void DrawLegend()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Legend", EditorStyles.miniBoldLabel);
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

    private AdventureCell _hoverCell;        // реальная клетка (если есть)
    private (int x, int y)? _hoverCoord;     // координаты под курсором

    private void DrawInspector()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
        {
            EditorGUILayout.LabelField("Cell Inspector", EditorStyles.boldLabel);

            if (_hoverCoord == null)
            {
                EditorGUILayout.HelpBox("Наведись/кликни на гекс на канвасе, чтобы редактировать свойства клетки.", MessageType.Info);
                return;
            }

            var (hx, hy) = _hoverCoord.Value;
            var exists = _cells.TryGetValue((hx, hy), out var real);
            var view = exists ? real : GetTempViewCell(hx, hy);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField($"Cell: ({hx}, {hy})", EditorStyles.miniBoldLabel);
            view.visible = EditorGUILayout.ToggleLeft("Visible", view.visible);
            view.terrain = (HexTerrainType)EditorGUILayout.EnumPopup("Terrain", view.terrain);
            view.eventAsset = (EventSO)EditorGUILayout.ObjectField("Event", view.eventAsset, typeof(EventSO), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (!exists) { CommitTempToAsset(hx, hy, view); exists = true; real = _cells[(hx, hy)]; }
                real.visible = view.visible;
                real.terrain = view.terrain;
                real.eventAsset = view.eventAsset;

                // Если вручную поставили Terrain = Empty — обязательно сбрасываем событие (твоя просьба)
                if (real.terrain == HexTerrainType.Empty)
                    real.eventAsset = null;

                // Если назначили событие — выставим тип Event (как удобно)
                if (real.eventAsset != null)
                    real.terrain = HexTerrainType.Event;

                Undo.RecordObject(asset, "Edit Cell");
                EditorUtility.SetDirty(asset);
                Repaint();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (view.eventAsset != null && GUILayout.Button("Ping Event")) EditorGUIUtility.PingObject(view.eventAsset);
                if (view.eventAsset != null && GUILayout.Button("Open Event")) Selection.activeObject = view.eventAsset;
            }

            if (!view.visible)
                EditorGUILayout.HelpBox("Эта клетка невидима (будет скрыта в сцене).", MessageType.Warning);
        }
    }

    private void DrawCanvas()
    {
        if (!asset) return;

        var area = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        using (var view = new GUI.ScrollViewScope(area, _canvasScroll, GetCanvasRect(), true, true))
        {
            _canvasScroll = view.scrollPosition;

            // Геометрия совпадает с твоим генератором (flat‑top, шаг X = 0.75 ширины, Y = √3/2 от ширины). :contentReference[oaicite:4]{index=4}
            for (int y = 0; y < asset.height; y++)
                for (int x = 0; x < asset.width; x++)
                {
                    var cell = _cells.TryGetValue((x, y), out var real) ? real : GetTempViewCell(x, y);

                    float cx = HexCenterX(x);
                    float cy = HexCenterY(x, y);

                    var poly = BuildHex(cx, cy, _hexRadius * _zoom);
                    var fill = CellColor(cell);

                    // 1) заливка
                    Handles.color = fill;
                    Handles.DrawAAConvexPolygon(poly);

                    // 2) контур
                    Handles.color = _outlineColor;
                    Handles.DrawAAPolyLine(2f, Close(poly));

                    // 3) иконка + имя события (если есть EventSO и клетка видимая)
                    if (cell.visible && cell.eventAsset != null)
                    {
                        // Сначала пытаемся взять ИМЕННО ИКОНКУ события (поле icon у EventSO). :contentReference[oaicite:5]{index=5}
                        var spr = cell.eventAsset.icon;
                        float iconSize = 50f * _zoom;

                        // Иконка слева от текста (в одну строку)
                        var iconRect = new Rect(cx - 25 * _zoom, cy - 35 * _zoom, iconSize, iconSize);
                        var labelRect = new Rect(cx - 48 * _zoom, cy + 15 * _zoom, 96 * _zoom, 16 * _zoom);

                        // Рисуем спрайт‑иконку: пробуем быстрый превью; если null — рисуем текстуру спрайта с UV
                        if (spr != null)
                        {
                            var texPreview = AssetPreview.GetAssetPreview(spr) ?? AssetPreview.GetMiniThumbnail(spr);
                            if (texPreview != null)
                                GUI.DrawTexture(iconRect, texPreview, ScaleMode.ScaleToFit, true);
                            else if (spr.texture != null)
                                GUI.DrawTextureWithTexCoords(iconRect, spr.texture, GetSpriteUV(spr), true);
                        }
                        else
                        {
                            // Фолбэк: если у события нет icon — можно показать миниатюру самого EventSO
                            var mini = AssetPreview.GetMiniThumbnail(cell.eventAsset);
                            if (mini) GUI.DrawTexture(iconRect, mini, ScaleMode.ScaleToFit, true);
                        }

                        // Подпись события
                        var name = cell.eventAsset.name;
                        if (name.Length > 18) name = name[..18];
                        GUI.Label(labelRect, name, _centerMiniLabel);
                    }

                    // 4) hover/клик
                    var e = Event.current;
                    if (e.isMouse && (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
                    {
                        if (PointInPoly(e.mousePosition, poly))
                        {
                            _hoverCoord = (x, y);
                            _hoverCell = _cells.TryGetValue((x, y), out var r) ? r : null;

                            if (e.type == EventType.MouseDown && e.button == 0)
                            {
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

            if (Event.current.type == EventType.MouseUp)
            {
                _isRectSelecting = false;
                _rectStartCell = null;
            }
        }
    }

    // ---------- Геометрия/утилиты рисования ----------
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
        var half = 0.5f * H;
        return (_padY + half) + (y + (x % 2) * 0.5f) * H;
    }

    private Vector3[] BuildHex(float cx, float cy, float r)
    {
        var pts = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float ang = Mathf.Deg2Rad * (60f * i);
            pts[i] = new Vector3(cx + r * Mathf.Cos(ang), cy + r * Mathf.Sin(ang), 0);
        }
        return pts;
    }

    private Vector3[] Close(Vector3[] poly)
    {
        var arr = new Vector3[poly.Length + 1];
        Array.Copy(poly, arr, poly.Length);
        arr[^1] = poly[0];
        return arr;
    }

    private bool PointInPoly(Vector2 p, Vector3[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var pi = (Vector2)poly[i];
            var pj = (Vector2)poly[j];
            bool inter = ((pi.y > p.y) != (pj.y > p.y)) &&
                         (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y + 1e-5f) + pi.x);
            if (inter) inside = !inside;
        }
        return inside;
    }

    // Цвет предпросмотра
    private Color CellColor(AdventureCell c)
    {
        if (!_terrainColor.TryGetValue(c.terrain, out var baseCol))
            baseCol = _terrainColor[HexTerrainType.Empty];
        baseCol.a = c.visible ? 1f : 0.25f;
        return baseCol;
    }

    // Нормализованные UV белого прямоугольника спрайта (если рисуем через DrawTextureWithTexCoords)
    private static Rect GetSpriteUV(Sprite s)
    {
        if (!s || !s.texture) return new Rect(0, 0, 1, 1);
        var tr = s.textureRect;
        var tex = s.texture;
        return new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
    }

    // ---------- РЕДАКТИРОВАНИЕ КЛЕТОК ----------
    private AdventureCell GetTempViewCell(int x, int y)
    {
        if (_temp.TryGetValue((x, y), out var c)) return c;
        c = new AdventureCell { x = x, y = y, visible = true, terrain = HexTerrainType.Empty, eventAsset = null };
        _temp[(x, y)] = c;
        return c;
    }

    private void CommitTempToAsset(int x, int y, AdventureCell view)
    {
        var real = new AdventureCell
        {
            x = x,
            y = y,
            visible = view.visible,
            terrain = view.terrain,
            eventAsset = view.eventAsset
        };
        asset.cells.Add(real);
        _cells[(x, y)] = real;
        EditorUtility.SetDirty(asset);
    }

    private void OnPaintAt(int x, int y)
    {
        var exists = _cells.TryGetValue((x, y), out var cell);
        if (!exists) { var temp = GetTempViewCell(x, y); CommitTempToAsset(x, y, temp); cell = _cells[(x, y)]; }

        Undo.RecordObject(asset, "Paint cell");

        switch (_tool)
        {
            case Tool.Eyedropper:
                if (_brush == BrushMode.Terrain) _paintTerrain = cell.terrain;
                else if (_brush == BrushMode.Event) _selectedEvent = cell.eventAsset;
                else _paintVisible = cell.visible;
                break;

            case Tool.Paint:
                if (_brush == BrushMode.Terrain)
                {
                    cell.terrain = _paintTerrain;
                    // ТВОЁ ПРАВИЛО: если переводим в Empty (или Blocked) — обязательно очищаем событие.
                    if (cell.terrain == HexTerrainType.Empty || cell.terrain == HexTerrainType.Blocked)
                        cell.eventAsset = null;
                }
                else if (_brush == BrushMode.Event)
                {
                    cell.eventAsset = _selectedEvent;
                    if (cell.eventAsset != null)
                        cell.terrain = HexTerrainType.Event;
                }
                else
                {
                    cell.visible = _paintVisible;
                }
                EditorUtility.SetDirty(asset);
                break;

            case Tool.RectFill:
                goto case Tool.Paint;
        }

        Repaint();
    }

    private bool _isRectSelecting;
    private (int x, int y)? _rectStartCell;

    private void PaintRect((int x, int y) from, (int x, int y) to)
    {
        int x0 = Math.Min(from.x, to.x);
        int x1 = Math.Max(from.x, to.x);
        int y0 = Math.Min(from.y, to.y);
        int y1 = Math.Max(from.y, to.y);

        Undo.RecordObject(asset, "Rect fill");

        for (int yy = y0; yy <= y1; yy++)
            for (int xx = x0; xx <= x1; xx++)
            {
                if (xx < 0 || xx >= asset.width || yy < 0 || yy >= asset.height) continue;

                var exists = _cells.TryGetValue((xx, yy), out var cell);
                if (!exists) { var temp = GetTempViewCell(xx, yy); CommitTempToAsset(xx, yy, temp); cell = _cells[(xx, yy)]; }

                if (_brush == BrushMode.Terrain)
                {
                    cell.terrain = _paintTerrain;
                    if (cell.terrain == HexTerrainType.Empty || cell.terrain == HexTerrainType.Blocked)
                        cell.eventAsset = null; // ← тоже чистим при заливке
                }
                else if (_brush == BrushMode.Event)
                {
                    cell.eventAsset = _selectedEvent;
                    if (cell.eventAsset != null)
                        cell.terrain = HexTerrainType.Event;
                }
                else
                {
                    cell.visible = _paintVisible;
                }
            }

        EditorUtility.SetDirty(asset);
        Repaint();
    }

    // ---------- ВАЛИДАЦИЯ ----------
    private void ValidateAndReport()
    {
        if (!asset) return;

        var cells = asset.cells.Where(c => c != null && c.visible).ToList();
        var starts = cells.Where(c => c.terrain == HexTerrainType.Start).ToList();
        var exits = cells.Where(c => c.terrain == HexTerrainType.Exit).ToList();

        if (starts.Count != 1) Debug.LogWarning($"[Adventure Editor] Требуется ровно 1 Start. Сейчас: {starts.Count}");
        if (exits.Count == 0) Debug.LogWarning("[Adventure Editor] Рекомендуется иметь хотя бы 1 Exit.");

        if (starts.Count == 1)
        {
            var start = starts[0];
            var passable = new HashSet<(int, int)>(cells.Where(c => c.terrain != HexTerrainType.Blocked).Select(c => (c.x, c.y)));

            var visited = new HashSet<(int, int)>();
            var q = new Queue<(int, int)>();
            q.Enqueue((start.x, start.y));
            visited.Add((start.x, start.y));

            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
                foreach (var (nx, ny) in Neighbors(x, y))
                {
                    if (nx < 0 || nx >= asset.width || ny < 0 || ny >= asset.height) continue;
                    if (!passable.Contains((nx, ny))) continue;
                    if (visited.Contains((nx, ny))) continue;
                    visited.Add((nx, ny));
                    q.Enqueue((nx, ny));
                }
            }

            var unreachable = passable.Except(visited).ToList();
            if (unreachable.Count > 0) Debug.LogWarning($"[Adventure Editor] Недостижимых клеток: {unreachable.Count}.");
            else Debug.Log("[Adventure Editor] OK: все проходимые клетки достижимы от Start.");
        }
    }

    private IEnumerable<(int x, int y)> Neighbors(int x, int y)
    {
        var dirs = (x % 2 == 0)
            ? new (int dx, int dy)[] { (+1, 0), (0, +1), (-1, 0), (-1, -1), (0, -1), (+1, -1) }
            : new (int dx, int dy)[] { (+1, +1), (0, +1), (-1, +1), (-1, 0), (0, -1), (+1, 0) };
        foreach (var d in dirs) yield return (x + d.dx, y + d.dy);
    }

    // ---------- ПРИМЕНЕНИЕ К СЦЕНЕ ----------
    private void ApplyToScene()
    {
        if (!asset) return;

        // ТОЛЬКО AdventureBuilder: он пересоберёт сетку и вызовет BindEvent у HexTile (бейдж и т.п.). :contentReference[oaicite:6]{index=6}
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

        EditorUtility.DisplayDialog(
            "Не найден AdventureBuilder",
            "В сцене нет AdventureBuilder. Добавь его на пустой GameObject, назначь Hex Prefab / Grid Root и повтори.",
            "Ок");
    }

    // ---------- ХЕЛПЕРЫ ЗАПОЛНЕНИЯ ----------
    private void EnsureMissingCellsEmpty(AdventureAsset a)
    {
        if (a.cells == null) a.cells = new List<AdventureCell>();
        for (int yy = 0; yy < a.height; yy++)
            for (int xx = 0; xx < a.width; xx++)
            {
                if (_cells.ContainsKey((xx, yy))) continue;
                var c = new AdventureCell { x = xx, y = yy, visible = true, terrain = HexTerrainType.Empty, eventAsset = null };
                a.cells.Add(c);
                _cells[(xx, yy)] = c;
            }
    }

    private void ResetAllCellsToEmpty(AdventureAsset a)
    {
        a.cells ??= new List<AdventureCell>();
        a.cells.Clear();
        for (int yy = 0; yy < a.height; yy++)
            for (int xx = 0; xx < a.width; xx++)
                a.cells.Add(new AdventureCell { x = xx, y = yy, visible = true, terrain = HexTerrainType.Empty, eventAsset = null });
    }
}
#endif
