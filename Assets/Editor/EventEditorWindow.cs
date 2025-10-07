#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using Unity.VisualScripting; // NEW: ReorderableList

/// <summary>
/// Robinson → Event Editor
/// Удобный редактор EventSO:
///  • слева — каталог с поиском/фильтрами и цветной подсветкой типов (Resource/Combat/Choice)
///  • справа — вкладки для редактирования (Overview / Simple / Choice / Combat / Hints / Preview)
///  • базовая валидация и быстрые кнопки (Create, Duplicate, Ping, Open)
/// Шаг 1: каркас и разметка. На шаге 2 добавим ReorderableList-дроуеры для наград/штрафов/ресторов.
/// </summary>
public class EventEditorWindow : EditorWindow
{
    // ---------- состояние окна ----------
    private Vector2 _leftScroll;
    private Vector2 _rightScroll;
    private string _search = "";
    private bool _filterRes = true;   // показывать Resource
    private bool _filterCom = true;   // показывать Combat
    private bool _filterChc = true;   // показывать Choice

    private List<EventSO> _all = new();
    private EventSO _selected;

    private enum Tab { Overview, Simple, Choice, Combat, Hints, Preview }
    private Tab _tab = Tab.Overview;

    // стили
    private GUIStyle _labelSmallCenter;
    private GUIStyle _tagStyle;

    // ========== ReorderableList состояния для вкладки Simple ==========
    private ReorderableList _rlAdditional;     // Additional costs
    private ReorderableList _rlPenalties;      // Penalties
    private ReorderableList _rlRewards;        // Rewards (простые)
    private ReorderableList _rlAltRewards;     // Alternative rewards (до 2 шт.)

    private SerializedObject _soCached;         // для отслеживания смены выбранного ассета

    // === Choice ===
    private ReorderableList _rlChoices;
    private SerializedObject _soCachedChoices;

    // === Combat ===
    private ReorderableList _rlEnemies;
    private SerializedObject _soCachedEnemies;

    // === Preview (живой HexEventBadgeUI) ===
    private PreviewRenderUtility _preview;       // мини-сцена и камера предпросмотра
    private GameObject _previewGO;               // инстанс префаба бейджа в мини-сцене
    private HexEventBadgeUI _previewBadge;       // ссылка на компонент бейджа
    private GameObject _previewPrefab;           // выбранный в UI префаб HexEventBadgeUI
    private int _previewLastEventID = 0;         // чтобы знать, когда нужно ребиндить
    private int _previewLastPrefabID = 0;        // чтобы знать, когда переинстансить
    private Color _previewBg = new Color(0.15f, 0.15f, 0.15f, 1f); // фон предпросмотра



    // ---------- меню ----------
    [MenuItem("Robinson/Event Editor")]
    private static void Open()
    {
        var w = GetWindow<EventEditorWindow>("Event Editor");
        w.minSize = new Vector2(980, 600);
        w.Show();
    }

    private void OnEnable()
    {
        _labelSmallCenter = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter };
        _tagStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight };
        RefreshCatalog();
    }

    // ---------- загрузка каталога ----------
    private void RefreshCatalog()
    {
        var guids = AssetDatabase.FindAssets("t:EventSO");
        _all = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<EventSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(e => e != null)
            .OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // если текущий выбранный ассет удалён/переименован — снимем выбор
        if (_selected != null && !_all.Contains(_selected)) _selected = null;
        Repaint();
    }

    // ---------- GUI ----------
    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawLeftPanel();     // каталог
            DrawRightPanel();    // редактор выбранного события
        }
    }

    // ---------- ЛЕВАЯ ПАНЕЛЬ: каталог событий ----------
    private void DrawLeftPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(320)))
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))  // рисуем тулбар
            {
                _search ??= string.Empty;                                      // страхуемся от null

                // Безопасно получаем стили для поля поиска и кнопки очистки:
                // поддерживаем и старые, и новые имена; если не найдём — берём дефолтные
                var searchStyle = TryStyle("ToolbarSearchTextField", "ToolbarSeachTextField", "SearchTextField")
                                  ?? GUI.skin.textField;                        // фолбэк — обычное текстовое поле
                var cancelStyle = TryStyle("ToolbarSearchCancelButton", "ToolbarSeachCancelButton", "SearchCancelButton")
                                  ?? GUI.skin.button;                           // фолбэк — обычная кнопка

                // Само поле ввода — участвует в лэйауте, NRE не будет
                var newSearch = GUILayout.TextField(_search, searchStyle, GUILayout.ExpandWidth(true));

                // Кнопка "очистить" — активна только если есть текст
                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(newSearch));
                if (GUILayout.Button("×", cancelStyle, GUILayout.Width(18)))   // символ «×» на случай простого стиля
                {
                    newSearch = string.Empty;                                  // очищаем строку
                    GUI.FocusControl(null);                                    // снимаем фокус, чтобы курсор пропал
                }
                EditorGUI.EndDisabledGroup();

                // Кнопка «обновить каталог»
                if (GUILayout.Button("⟳", EditorStyles.toolbarButton, GUILayout.Width(28)))
                    RefreshCatalog();

                // Применяем изменения и перерисовываем окно
                if (newSearch != _search)
                {
                    _search = newSearch;
                    Repaint();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _filterRes = GUILayout.Toggle(_filterRes, "Resource", "Button");
                _filterCom = GUILayout.Toggle(_filterCom, "Combat", "Button");
                _filterChc = GUILayout.Toggle(_filterChc, "Choice", "Button");
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_leftScroll))
            {
                _leftScroll = scroll.scrollPosition;

                // фильтрация списка
                IEnumerable<EventSO> view = _all;
                if (!string.IsNullOrWhiteSpace(_search))
                    view = view.Where(e => e.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                                        || (!string.IsNullOrEmpty(e.eventName) &&
                                            e.eventName.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0));

                view = view.Where(e =>
                    (e.isResource && _filterRes) ||
                    (e.isCombat && _filterCom) ||
                    (e.isChoice && _filterChc) ||
                    (!e.isResource && !e.isCombat && !e.isChoice) // «прочие» — если все фильтры выкл., покажем тоже
                );

                // рендер элементов списка
                foreach (var ev in view)
                {
                    var isSel = _selected == ev;
                    using (new EditorGUILayout.HorizontalScope(isSel ? "SelectionRect" : GUIStyle.none))
                    {
                        // иконка из EventSO.icon (Sprite)
                        var spr = ev.icon;
                        var icRect = GUILayoutUtility.GetRect(28, 28, GUILayout.Width(28), GUILayout.Height(28));
                        if (spr)
                        {
                            var tex = AssetPreview.GetAssetPreview(spr) ?? AssetPreview.GetMiniThumbnail(spr);
                            if (tex) GUI.DrawTexture(icRect, tex, ScaleMode.ScaleToFit, true);
                            else if (spr.texture) GUI.DrawTextureWithTexCoords(icRect, spr.texture, GetSpriteUV(spr), true);
                        }
                        else
                        {
                            var mini = AssetPreview.GetMiniThumbnail(ev);
                            if (mini) GUI.DrawTexture(icRect, mini, ScaleMode.ScaleToFit, true);
                        }

                        // цвет имени по типу
                        var nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Normal };
                        nameStyle.normal.textColor = NameColorFor(ev);

                        // кликабельное имя
                        if (GUILayout.Button(string.IsNullOrEmpty(ev.eventName) ? ev.name : ev.eventName, nameStyle))
                            _selected = ev;

                        // маленькие теги справа
                        GUILayout.FlexibleSpace();
                        DrawTypeTag(ev);
                    }
                }
            }

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create"))
                {
                    var path = EditorUtility.SaveFilePanelInProject("Create Event", "NewEvent", "asset", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var ne = ScriptableObject.CreateInstance<EventSO>();
                        ne.eventName = "New Event";
                        AssetDatabase.CreateAsset(ne, path);
                        AssetDatabase.SaveAssets();
                        RefreshCatalog();
                        _selected = ne;
                        Selection.activeObject = ne;
                    }
                }
                using (new EditorGUI.DisabledScope(_selected == null))
                {
                    if (GUILayout.Button("Duplicate"))
                    {
                        var src = _selected;
                        var path = AssetDatabase.GetAssetPath(src);
                        var newPath = AssetDatabase.GenerateUniqueAssetPath(path);
                        var copy = Instantiate(src);
                        copy.name = src.name;
                        AssetDatabase.CreateAsset(copy, newPath);
                        AssetDatabase.SaveAssets();
                        RefreshCatalog();
                        _selected = copy;
                        Selection.activeObject = copy;
                    }
                    if (GUILayout.Button("Ping")) EditorGUIUtility.PingObject(_selected);
                    if (GUILayout.Button("Open")) Selection.activeObject = _selected;
                }
            }
        }
    }

    // цветная «плашка» типа события справа от имени
    private void DrawTypeTag(EventSO ev)
    {
        string tag = ev.isCombat ? "COMBAT" : ev.isChoice ? "CHOICE" : ev.isResource ? "RESOURCE" : "";
        if (string.IsNullOrEmpty(tag)) return;

        var col = NameColorFor(ev);
        var old = GUI.color;
        GUI.color = col;
        GUILayout.Label(tag, _tagStyle, GUILayout.Width(74));
        GUI.color = old;
    }

    // ---------- ПРАВАЯ ПАНЕЛЬ: вкладки и редактирование ----------
    private void DrawRightPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
        {
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Выбери EventSO слева (или создай новый), чтобы редактировать.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var newTab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Overview", "Simple", "Choice", "Combat", "Hints", "Preview" }, EditorStyles.toolbarButton);
                if (newTab != _tab) { _tab = newTab; GUI.FocusControl(null); }
                GUILayout.FlexibleSpace();
                // быстрые кнопки
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60))) SaveSelected();
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_rightScroll))
            {
                _rightScroll = scroll.scrollPosition;

                // SerializedObject — надёжно редактируем поля даже если их станет больше/меньше
                var so = new SerializedObject(_selected);
                so.Update();

                switch (_tab)
                {
                    case Tab.Overview: DrawTabOverview(so); break;
                    case Tab.Simple: DrawTabSimple(so); break;
                    case Tab.Choice: DrawTabChoice(so); break;
                    case Tab.Combat: DrawTabCombat(so); break;
                    case Tab.Hints: DrawTabHints(so); break;
                    case Tab.Preview: DrawTabPreview(so); break;
                }

                // сохраняем изменения (Undo + SetDirty)
                if (GUI.changed) SaveSerialized(so);
            }
        }
    }

    // ---------- вкладка: Overview ----------
    private void DrawTabOverview(SerializedObject so)
    {
        EditorGUILayout.LabelField("Overview", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        Prop(so, "eventId", "Event ID");
        Prop(so, "eventName", "Display Name");
        Prop(so, "description", "Description");
        Prop(so, "icon", "Icon (Sprite)");
       // Prop(so, "hexType", "Hex Type");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Type Flags", EditorStyles.miniBoldLabel);

        Prop(so, "isResource", "Resource Event");
        Prop(so, "isChoice", "Choice Event");
        Prop(so, "isCombat", "Combat Event");

        EditorGUILayout.Space(10);
        DrawValidationSummary(_selected);
    }

    // ---------- вкладка: Simple (обычное событие) ----------
    private void DrawTabSimple(SerializedObject so)
    {
        EditorGUILayout.LabelField("Simple Event", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Обычное событие без ветвлений. Здесь задаём главную стоимость, доп.стоимость (опц.), пенальти и награды.", MessageType.None);
        EditorGUILayout.Space(6);

        // Main cost
        Prop(so, "mainCostType", "Main Cost Type");
        Prop(so, "mainCostAmount", "Main Cost Amount");

        // Создаём списки (один раз на текущий выбранный so)
        if (_soCached != so || _rlAdditional == null || _rlPenalties == null || _rlRewards == null || _rlAltRewards == null)
            BuildSimpleLists(so);

        EditorGUILayout.Space(6);
        // Additional costs mandatory
        Prop(so, "additionalMandatory", "Additional Costs Mandatory");

        // Additional costs list
        _rlAdditional?.DoLayoutList();

        EditorGUILayout.Space(6);
        // Penalties list
        _rlPenalties?.DoLayoutList();

        EditorGUILayout.Space(6);
        // Rewards mode: normal or alternatives
        var alt = GetProp(so, "rewardsAreAlternative");
        if (alt != null)
        {
            EditorGUILayout.PropertyField(alt, new GUIContent("Rewards are Alternative (two-choice)"));
            EditorGUILayout.Space(2);
            if (alt.boolValue)
            {
                EditorGUILayout.HelpBox("В этом режиме игрок выбирает одну из двух альтернатив награды.", MessageType.None);
                _rlAltRewards?.DoLayoutList();
            }
            else
            {
                _rlRewards?.DoLayoutList();
            }
        }
    }

    // ---------- вкладка: Choice ----------
    // Отрисовка одной карточки ChoiceOption
    private void DrawTabChoice(SerializedObject so)
    {
        EditorGUILayout.LabelField("Choice Event", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Событие с выбором. У каждого варианта — свой набор полей: описание, стоимость, доп.стоимости, пенальти, а также либо видимые награды, либо скрытые исходы.", MessageType.None);
        EditorGUILayout.Space(6);

        // Пересобрать список при смене ассета/первом входе
        if (_soCachedChoices != so || _rlChoices == null)
            BuildChoiceList(so);

        // Отрисовать список вариантов
        _rlChoices?.DoLayoutList();

        // Мини-валидация
        var choices = so.FindProperty("choices");
        if (choices != null)
        {
            int n = choices.arraySize;
            if (n < 2 || n > 3)
                EditorGUILayout.HelpBox("Ожидается 2–3 варианта выбора.", MessageType.Warning);
        }
    }


    // Отрисовка одной карточки ChoiceOption (с встраиваемым редактором наград)
    private void DrawChoiceElement(Rect rect, SerializedProperty choiceProp, int index)
    {
        float y = rect.y + 2f;
        float line = EditorGUIUtility.singleLineHeight;
        float w = rect.width;

        // === Заголовок Option N ===
        EditorGUI.LabelField(new Rect(rect.x, y, w, line), $"Option {index + 1}", EditorStyles.boldLabel);
        y += line + 2f;

        // -- Description (TextArea через PropertyField учтёт [TextArea] атрибут)
        var desc = choiceProp.FindPropertyRelative("description");
        float descH = EditorGUI.GetPropertyHeight(desc, new GUIContent("Description"), true);
        EditorGUI.PropertyField(new Rect(rect.x, y, w, descH), desc, new GUIContent("Description"), true);
        y += descH + 4f;

        // -- Main cost
        var mct = choiceProp.FindPropertyRelative("mainCostType");
        var mca = choiceProp.FindPropertyRelative("mainCostAmount");
        EditorGUI.PropertyField(new Rect(rect.x, y, w, line), mct, new GUIContent("Main Cost Type")); y += line + 4f;
        EditorGUI.PropertyField(new Rect(rect.x, y, w, line), mca, new GUIContent("Main Cost Amount")); y += line + 8f;

        // -- Additional costs
        var add = choiceProp.FindPropertyRelative("additionalCosts");
        var addH = EditorGUI.GetPropertyHeight(add, true);
        EditorGUI.PropertyField(new Rect(rect.x, y, w, addH), add, new GUIContent("Additional Costs"), true);
        y += addH + 6f;

        // -- Penalties
        var pen = choiceProp.FindPropertyRelative("penalties");
        var penH = EditorGUI.GetPropertyHeight(pen, true);
        EditorGUI.PropertyField(new Rect(rect.x, y, w, penH), pen, new GUIContent("Penalties"), true);
        y += penH + 6f;

        // -- Toggles: showRewards / showHiddenOutcomes
        var showRw = choiceProp.FindPropertyRelative("showRewards");
        var showHo = choiceProp.FindPropertyRelative("showHiddenOutcomes");
        EditorGUI.PropertyField(new Rect(rect.x, y, w, line), showRw, new GUIContent("Show Rewards")); y += line + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, y, w, line), showHo, new GUIContent("Show Hidden Outcomes")); y += line + 4f;

        // === ВСТРОЕННЫЙ РЕДАКТОР Наград (тот же интерфейс, что и в Simple) ===
        var rewards = choiceProp.FindPropertyRelative("rewards");
        string rewardsTitle = showRw.boolValue ? "Rewards (shown)" : "Rewards (hidden in UI)";
        float hRw = RewardsInlineHeight(rewards);
        DrawRewardsInline(new Rect(rect.x, y, w, hRw), rewards, rewardsTitle);   // см. патч ниже
        y += hRw + 6f;

        // === HiddenOutcomes — если включено ===
        if (showHo.boolValue)
        {
            var hidd = choiceProp.FindPropertyRelative("hiddenOutcomes");
            var hH = EditorGUI.GetPropertyHeight(hidd, true);
            EditorGUI.PropertyField(new Rect(rect.x, y, w, hH), hidd, new GUIContent("Hidden Outcomes (icons)"), true);
            y += hH + 6f;
        }
    }
    // Высота встроенного списка наград (заголовок + элементы + строка кнопок)
    private float RewardsInlineHeight(SerializedProperty rewards)
    {
        float h = 0f;
        float line = EditorGUIUtility.singleLineHeight;

        // Заголовок
        h += line + 4f;

        // Элементы
        if (rewards != null)
        {
            for (int i = 0; i < rewards.arraySize; i++)
            {
                var el = rewards.GetArrayElementAtIndex(i);
                h += RewardElementHeight(el) + 4f; // используем уже имеющийся хелпер
            }
        }

        // Строка кнопок (+, возможно подсказка)
        h += line + 4f;

        return h;
    }

    // Отрисовка встроенного списка наград с меню "+" (типы как в Simple)
    private void DrawRewardsInline(Rect rect, SerializedProperty rewards, string title)
    {
        float y = rect.y;
        float line = EditorGUIUtility.singleLineHeight;
        float w = rect.width;

        // Заголовок блока
        EditorGUI.LabelField(new Rect(rect.x, y, w, line), title, EditorStyles.miniBoldLabel);
        y += line + 4f;

        if (rewards != null)
        {
            // ЭЛЕМЕНТЫ
            for (int i = 0; i < rewards.arraySize; i++)
            {
                var el = rewards.GetArrayElementAtIndex(i);
                float hEl = RewardElementHeight(el); // тот же хелпер, что и для Simple
                var area = new Rect(rect.x, y, w, hEl);

                // Рисуем карточку награды
                DrawRewardElement(area, el);

                // Кнопка удалить (в правом верхнем углу элемента)
                var btnRect = new Rect(area.xMax - 22f, area.y + 2f, 20f, line);
                if (GUI.Button(btnRect, "×"))
                {
                    rewards.DeleteArrayElementAtIndex(i);
                    break; // прерываем, чтобы не рисовать «сместившиеся» индексы
                }

                y += hEl + 4f;
            }

            // НИЖНЯЯ ПАНЕЛЬ: кнопка «+» с выбором типа
            var addRect = new Rect(rect.x, y, 80f, line);
            if (GUI.Button(addRect, "+ Add"))
            {
                var menu = new GenericMenu();

                void Add(EventSO.RewardType t)
                {
                    rewards.arraySize++;
                    var el = rewards.GetArrayElementAtIndex(rewards.arraySize - 1);
                    InitRewardOfType(el, t); // ставим тип и дефолты
                    rewards.serializedObject.ApplyModifiedProperties();
                }

                menu.AddItem(new GUIContent("Resource"), false, () => Add(EventSO.RewardType.Resource));
                menu.AddItem(new GUIContent("RestoreStat"), false, () => Add(EventSO.RewardType.RestoreStat));
                menu.AddItem(new GUIContent("NewCard"), false, () => Add(EventSO.RewardType.NewCard));
                menu.AddItem(new GUIContent("FreeReward"), false, () => Add(EventSO.RewardType.FreeReward));
                menu.ShowAsContext();
            }

            // Подпись справа
            EditorGUI.LabelField(new Rect(addRect.xMax + 8, y, w - addRect.width - 8, line),
                "Выбери тип награды через «+ Add», далее заполни поля.", EditorStyles.miniLabel);

            y += line + 4f;
        }
    }



    // ---------- вкладка: Combat ----------
    private void DrawTabCombat(SerializedObject so)
    {
        EditorGUILayout.LabelField("Combat Event", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // Флаги и настройки
        Prop(so, "isAggressiveCombat", "Aggressive Combat");

        // Пересобрать список врагов при смене ассета/первом входе
        if (_soCachedEnemies != so || _rlEnemies == null)
            BuildEnemyList(so);

        // Список врагов
        _rlEnemies?.DoLayoutList();

        // Предпросмотр врага на бейдже
        var prev = so.FindProperty("previewEnemyIndex");
        if (prev != null)
        {
            EditorGUILayout.PropertyField(prev, new GUIContent("Preview Enemy Index"));
        }

        // Компактный превью текущего выбранного врага
        var ev = (EventSO)so.targetObject;
        var enemy = ev != null ? ev.GetPreviewEnemy() : null;
        if (enemy)
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                GUILayout.Label("Preview:", GUILayout.Width(70));
                var tex = AssetPreview.GetAssetPreview(enemy) ?? AssetPreview.GetMiniThumbnail(enemy);
                var r = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
                if (tex) GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
                EditorGUILayout.LabelField(enemy.name, EditorStyles.boldLabel);
            }
        }

        // Валидация
        var enemies = so.FindProperty("combatEnemies");
        if (enemies != null)
        {
            int n = enemies.arraySize;
            if (n == 0) EditorGUILayout.HelpBox("Добавь 1–3 врагов.", MessageType.Warning);
            if (prev != null && n > 0 && (prev.intValue < 0 || prev.intValue > n - 1))
                EditorGUILayout.HelpBox("Preview Enemy Index выходит за пределы списка.", MessageType.Warning);
        }
    }

    // Создание/обновление ReorderableList для списка combatEnemies
    private void BuildEnemyList(SerializedObject so)
    {
        _soCachedEnemies = so;

        // Ищем SerializedProperty списка врагов в EventSO
        var p = so.FindProperty("combatEnemies"); // List<EnemySO> combatEnemies
        if (p == null)
        {
            EditorGUILayout.HelpBox("Поле 'combatEnemies' не найдено в EventSO.", MessageType.Error);
            return;
        }

        // Создаём ReorderableList
        _rlEnemies = new ReorderableList(so, p, /*draggable*/ true, /*displayHeader*/ true, /*displayAddButton*/ true, /*displayRemoveButton*/ true);

        // Заголовок списка
        _rlEnemies.drawHeaderCallback = rect =>
            EditorGUI.LabelField(rect, "Combat Enemies (1–3)");

        // Высота элемента — одна строка
        _rlEnemies.elementHeight = EditorGUIUtility.singleLineHeight + 6f;

        // Как рисуем один элемент: просто объект EnemySO (drag&drop работает из коробки)
        _rlEnemies.drawElementCallback = (rect, index, active, focused) =>
        {
            var el = p.GetArrayElementAtIndex(index); // это элемент массива (ObjectReference)
            var r = new Rect(rect.x, rect.y + 2f, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(r, el, GUIContent.none);
        };

        // Добавление нового врага (ограничим максимум 3)
        _rlEnemies.onAddCallback = list =>
        {
            if (p.arraySize >= 3)
            {
                EditorApplication.Beep();
                return;
            }
            p.arraySize++;
            so.ApplyModifiedProperties();
        };

        // Разрешим удалять всегда (даже до 0), но валидировать будем в UI
        _rlEnemies.onCanRemoveCallback = list => true;
    }



    // ---------- вкладка: Hints ----------
    private void DrawTabHints(SerializedObject so)
    {
        EditorGUILayout.LabelField("Hex Hint (default)", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);
        Prop(so, "defaultHint", "Default Hint on hidden hex");
    }

    // ---------- вкладка: Preview ----------
    private void DrawTabPreview(SerializedObject so)
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Живой предпросмотр префаба HexEventBadgeUI. Объект создаётся в скрытой mini-scene редактора и НИКОГДА не попадает в вашу сцену.", MessageType.Info);
        EditorGUILayout.Space(6);

        _previewPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Badge Prefab (HexEventBadgeUI)"),
            _previewPrefab, typeof(GameObject), false);

        _previewBg = EditorGUILayout.ColorField(new GUIContent("Background"), _previewBg);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rebuild")) DestroyPreviewGO();
            if (GUILayout.Button("Rebind")) _previewLastEventID = 0;
            GUILayout.FlexibleSpace();
        }

        var rect = GUILayoutUtility.GetRect(10, 260, GUILayout.ExpandWidth(true));
        var ev = (EventSO)so.targetObject;

        if (ev != null && _previewPrefab != null)
        {
            if (_preview == null || _previewGO == null || _previewLastPrefabID != (_previewPrefab ? _previewPrefab.GetInstanceID() : 0))
                DestroyPreviewGO();

            if (_preview == null || _previewGO == null || _previewLastEventID != ev.GetInstanceID())
                BuildOrRebindBadgePreview(ev);

            if (_preview != null)
            {
                _preview.camera.backgroundColor = _previewBg;
                if (Event.current.type == EventType.Repaint)
                {
                    _preview.BeginPreview(rect, GUIStyle.none);
                    _preview.camera.Render();
                    var tex = _preview.EndPreview();
                    if (tex) GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, false);
                }
            }
        }
        else
        {
            if (ev == null) EditorGUILayout.HelpBox("Выбери EventSO слева.", MessageType.Warning);
            if (_previewPrefab == null) EditorGUILayout.HelpBox("Укажи префаб HexEventBadgeUI для предпросмотра.", MessageType.Warning);
        }
    }


    // ---------- валидация (краткая) ----------
    private void DrawValidationSummary(EventSO ev)
    {
        var messages = new List<string>();

        if (string.IsNullOrWhiteSpace(ev.eventName))
            messages.Add("Нет Display Name.");
        if (!ev.icon)
            messages.Add("Не задана Icon (Sprite).");
        if (ev.isChoice)
        {
            if (ev.choices == null || ev.choices.Count < 2 || ev.choices.Count > 3)
                messages.Add("Choice: ожидаем 2..3 варианта.");
        }
        if (ev.isCombat)
        {
            if (ev.combatEnemies == null || ev.combatEnemies.Count == 0)
                messages.Add("Combat: нет врагов (1..3).");
        }

        if (messages.Count == 0)
        {
            EditorGUILayout.HelpBox("✔ Всё выглядит корректно.", MessageType.Info);
        }
        else
        {
            foreach (var m in messages)
                EditorGUILayout.HelpBox(m, MessageType.Warning);
        }
    }

    // ---------- utils: SerializedObject helpers ----------
    private static SerializedProperty GetProp(SerializedObject so, string path)
    {
        var p = so.FindProperty(path);
        if (p == null)
            EditorGUILayout.HelpBox($"Поле '{path}' не найдено в EventSO.", MessageType.Error);
        return p;
    }

    private static void Prop(SerializedObject so, string path, string label = null)
    {
        var p = GetProp(so, path);
        if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label ?? p.displayName));
    }

    private static void PropArray(SerializedObject so, string path, string label = null)
    {
        var p = GetProp(so, path);
        if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label ?? p.displayName), true);
    }

    private static void SaveSerialized(SerializedObject so)
    {
        Undo.RecordObject(so.targetObject, "Edit EventSO");
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
    }

    private void SaveSelected()
    {
        if (_selected == null) return;
        Undo.RecordObject(_selected, "Save EventSO");
        EditorUtility.SetDirty(_selected);
        AssetDatabase.SaveAssets();
    }

    // ---------- визуальные утилиты ----------
    private static Color NameColorFor(EventSO ev)
    {
        // По твоему правилу:
        // Resource — чёрный, Combat — красный, Choice — фиолетовый.
        if (ev.isCombat) return new Color(0.90f, 0.25f, 0.25f);
        if (ev.isChoice) return new Color(0.65f, 0.40f, 1.00f);
        if (ev.isResource) return Color.white;
        // Прочие — нейтральный (учтём тему редактора)
        return EditorGUIUtility.isProSkin ? new Color(0.85f, 0.85f, 0.85f) : Color.black;
    }

    private static Rect GetSpriteUV(Sprite s)
    {
        if (!s || !s.texture) return new Rect(0, 0, 1, 1);
        var tr = s.textureRect;
        var tex = s.texture;
        return new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
    }

    private static GUIStyle TryStyle(params string[] names)
    {
        foreach (var n in names)
        {
            var s = GUI.skin.FindStyle(n);     // пробуем найти стиль в текущей скине
            if (s != null) return s;           // нашли — возвращаем
        }
        return null;                           // не нашли — вернём null, выше подставим фолбэк
    }

    // Создаём/пересоздаём все списки для текущего выбранного EventSO
    private void BuildSimpleLists(SerializedObject so)
    {
        _soCached = so;
        // --- Additional costs ---
        {
            var p = so.FindProperty("additionalCosts");
            _rlAdditional = new ReorderableList(so, p, true, true, true, true);
            _rlAdditional.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Additional Costs (Brain/Power/Speed)");
            _rlAdditional.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
            _rlAdditional.drawElementCallback = (rect, index, active, focused) =>
            {
                var el = p.GetArrayElementAtIndex(index);
                var tag = el.FindPropertyRelative("tag");
                var amt = el.FindPropertyRelative("amount");
                var r = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
                var col = r.width;
                var wTag = 140f;
                EditorGUI.PropertyField(new Rect(r.x, r.y, wTag, r.height), tag, GUIContent.none);
                EditorGUI.LabelField(new Rect(r.x + wTag + 6, r.y, 64, r.height), "Amount:");
                EditorGUI.PropertyField(new Rect(r.x + wTag + 6 + 64, r.y, col - wTag - 6 - 64, r.height), amt, GUIContent.none);
            };
            _rlAdditional.onAddDropdownCallback = (rect, list) =>
            {
                p.arraySize++;
                var el = p.GetArrayElementAtIndex(p.arraySize - 1);
                el.FindPropertyRelative("tag").enumValueIndex = 0;
                el.FindPropertyRelative("amount").intValue = 1;
                so.ApplyModifiedProperties();
            };
        }

        // --- Penalties ---
        {
            var p = so.FindProperty("penalties");
            _rlPenalties = new ReorderableList(so, p, true, true, true, true);
            _rlPenalties.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Penalties (Hunger/Thirst/Energy/Health)");
            _rlPenalties.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
            _rlPenalties.drawElementCallback = (rect, index, active, focused) =>
            {
                var el = p.GetArrayElementAtIndex(index);
                var stat = el.FindPropertyRelative("stat");
                var amt = el.FindPropertyRelative("amount");
                var r = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
                var wStat = 160f;
                EditorGUI.PropertyField(new Rect(r.x, r.y, wStat, r.height), stat, GUIContent.none);
                EditorGUI.LabelField(new Rect(r.x + wStat + 6, r.y, 64, r.height), "Amount:");
                EditorGUI.PropertyField(new Rect(r.x + wStat + 6 + 64, r.y, r.width - wStat - 6 - 64, r.height), amt, GUIContent.none);
            };
            _rlPenalties.onAddDropdownCallback = (rect, list) =>
            {
                p.arraySize++;
                var el = p.GetArrayElementAtIndex(p.arraySize - 1);
                el.FindPropertyRelative("stat").enumValueIndex = 0;
                el.FindPropertyRelative("amount").intValue = 1;
                so.ApplyModifiedProperties();
            };
        }

        // --- Rewards (обычные) ---
        {
            var p = so.FindProperty("rewards");
            _rlRewards = new ReorderableList(so, p, true, true, true, true);
            _rlRewards.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Rewards (up to 4)");
            _rlRewards.elementHeightCallback = idx => RewardElementHeight(p.GetArrayElementAtIndex(idx));
            _rlRewards.drawElementCallback = (rect, index, active, focused) =>
            {
                DrawRewardElement(rect, p.GetArrayElementAtIndex(index));
            };
            _rlRewards.onAddDropdownCallback = (r, list) =>
            {
                var p = list.serializedProperty; // это so.FindProperty("rewards")
                var menu = new GenericMenu();

                void Add(EventSO.RewardType t)
                {
                    p.arraySize++;
                    var el = p.GetArrayElementAtIndex(p.arraySize - 1);
                    InitRewardOfType(el, t);
                    list.serializedProperty.serializedObject.ApplyModifiedProperties();
                }

                menu.AddItem(new GUIContent("Resource"), false, () => Add(EventSO.RewardType.Resource));
                menu.AddItem(new GUIContent("RestoreStat"), false, () => Add(EventSO.RewardType.RestoreStat));
                menu.AddItem(new GUIContent("NewCard"), false, () => Add(EventSO.RewardType.NewCard));
                menu.AddItem(new GUIContent("FreeReward"), false, () => Add(EventSO.RewardType.FreeReward));

                menu.ShowAsContext();
            };
        }

        // --- Alternative rewards (до 2 шт.) ---
        {
            var p = so.FindProperty("alternativeRewards");
            _rlAltRewards = new ReorderableList(so, p, true, true, true, true);
            _rlAltRewards.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Alternative Rewards (two-choice)");
            _rlAltRewards.elementHeightCallback = idx => RewardElementHeight(p.GetArrayElementAtIndex(idx));
            _rlAltRewards.drawElementCallback = (rect, index, active, focused) =>
            {
                DrawRewardElement(rect, p.GetArrayElementAtIndex(index));
            };
            _rlAltRewards.onAddDropdownCallback = (r, list) =>
            {
                var p = list.serializedProperty; // so.FindProperty("alternativeRewards")
                if (p.arraySize >= 2) { EditorApplication.Beep(); return; }

                var menu = new GenericMenu();

                void Add(EventSO.RewardType t)
                {
                    p.arraySize++;
                    var el = p.GetArrayElementAtIndex(p.arraySize - 1);
                    InitRewardOfType(el, t);
                    list.serializedProperty.serializedObject.ApplyModifiedProperties();
                }

                menu.AddItem(new GUIContent("Resource"), false, () => Add(EventSO.RewardType.Resource));
                menu.AddItem(new GUIContent("RestoreStat"), false, () => Add(EventSO.RewardType.RestoreStat));
                menu.AddItem(new GUIContent("NewCard"), false, () => Add(EventSO.RewardType.NewCard));
                menu.AddItem(new GUIContent("FreeReward"), false, () => Add(EventSO.RewardType.FreeReward));

                menu.ShowAsContext();
            };
        }
    }

    // Хелпер: цветная шапка строки риворда
    private static void RewardHeader(Rect rect, SerializedProperty typeProp, string titleRight = null)
    {
        var typeName = typeProp.enumDisplayNames[typeProp.enumValueIndex];
        var label = $"Type: {typeName}";
        EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width * 0.66f, EditorGUIUtility.singleLineHeight), label, EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(titleRight))
            EditorGUI.LabelField(new Rect(rect.x + rect.width * 0.66f, rect.y, rect.width * 0.34f, EditorGUIUtility.singleLineHeight), titleRight, EditorStyles.miniLabel);
    }

    // Возвращает высоту карточки награды, исходя из выбранного типа и наличия gating
    private static float RewardElementHeight(SerializedProperty rewardProp)
    {
        float h = EditorGUIUtility.singleLineHeight + 6f;                   // строка "Type"

        var typeProp = rewardProp.FindPropertyRelative("type");             // enum RewardType
        if (typeProp == null) return h + 10f;

        var t = (EventSO.RewardType)typeProp.enumValueIndex;

        // Поля по типу
        switch (t)
        {
            case EventSO.RewardType.Resource: h += (EditorGUIUtility.singleLineHeight + 4f) * 2; break; // resource + amount
            case EventSO.RewardType.RestoreStat: h += (EditorGUIUtility.singleLineHeight + 4f) * 2; break; // stat + restoreAmount
            case EventSO.RewardType.NewCard: h += (EditorGUIUtility.singleLineHeight + 4f) * 3; break; // cardDef + cardCount + knownPreview
            case EventSO.RewardType.FreeReward: h += (EditorGUIUtility.singleLineHeight + 4f) * 1; break; // freeReward
        }

        // Gating
        var gated = rewardProp.FindPropertyRelative("gatedByAdditional");
        if (gated != null && gated.boolValue)
            h += (EditorGUIUtility.singleLineHeight + 4f) * 3;             // checkbox уже учтён выше, здесь: tooltip + tag + amount
        else
            h += (EditorGUIUtility.singleLineHeight + 4f) * 1;             // только чекбокс "gated?"

        return h + 6f;                                                     // финальный отступ
    }


    private static void InitRewardDefaults(SerializedProperty r)
    {
        r.FindPropertyRelative("type").enumValueIndex = (int)EventSO.RewardType.Resource;
        var amt = r.FindPropertyRelative("amount"); if (amt != null) amt.intValue = 1;
        var rc = r.FindPropertyRelative("cardCount"); if (rc != null) rc.intValue = 1;
        var kp = r.FindPropertyRelative("knownPreview"); if (kp != null) kp.boolValue = true;
        var gat = r.FindPropertyRelative("gatedByAdditional"); if (gat != null) gat.boolValue = false;
        var tip = r.FindPropertyRelative("tooltip"); if (tip != null) tip.stringValue = string.Empty;
        var reqA = r.FindPropertyRelative("requiredAmount"); if (reqA != null) reqA.intValue = 1;
    }

    // Рисует ОДНУ карточку награды в списке (Rewards/AlternativeRewards)
    private static void DrawRewardElement(Rect rect, SerializedProperty rewardProp)
    {
        // Y-координата курсора рисования и стандартная высота строки
        float y = rect.y + 2f;
        float line = EditorGUIUtility.singleLineHeight;

        // Достаём SerializedProperty типа награды
        var typeProp = rewardProp.FindPropertyRelative("type");              // enum RewardType
        var curType = (EventSO.RewardType)typeProp.enumValueIndex;          // текущее значение enum

        // === Переключатель типа награды (EnumPopup) ===
        var newType = (EventSO.RewardType)EditorGUI.EnumPopup(
            new Rect(rect.x, y, rect.width, line),
            new GUIContent("Type"),
            curType
        );
        y += line + 4f;

        // Если тип поменялся — очищаем лишние поля и инициализируем дефолты нового типа
        if (newType != curType)
        {
            ClearRewardIrrelevant(rewardProp);                               // убрать мусор из «чужих» полей
            typeProp.enumValueIndex = (int)newType;                          // установить новый тип
            InitRewardOfType(rewardProp, newType);                           // задать дефолты под тип
            curType = newType;                                               // обновить локальное значение
        }

        // === Поля в зависимости от типа ===
        switch (curType)
        {
            case EventSO.RewardType.Resource:
                {
                    var res = rewardProp.FindPropertyRelative("resource");       // ссылка на ресурс
                    var amt = rewardProp.FindPropertyRelative("amount");         // его количество
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, line), res, new GUIContent("Resource")); y += line + 4f;
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, line), amt, new GUIContent("Amount")); y += line + 8f;
                    break;
                }
            case EventSO.RewardType.RestoreStat:
                {
                    var stat = rewardProp.FindPropertyRelative("stat");          // какой стат восстанавливаем
                    var amt = rewardProp.FindPropertyRelative("restoreAmount"); // на сколько
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, line), stat, new GUIContent("Stat")); y += line + 4f;
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, line), amt, new GUIContent("Restore Amount")); y += line + 8f;
                    break;
                }
            case EventSO.RewardType.NewCard:
                {
                    var card = rewardProp.FindPropertyRelative("cardDef");       // какая карта
                    var cnt = rewardProp.FindPropertyRelative("cardCount");     // сколько копий
                    var kp = rewardProp.FindPropertyRelative("knownPreview");  // известна ли заранее
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, line), card, new GUIContent("Card")); y += line + 4f;
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, line), cnt, new GUIContent("Count")); y += line + 4f;
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, line), kp, new GUIContent("Known Preview")); y += line + 8f;
                    break;
                }
            case EventSO.RewardType.FreeReward:
                {
                    var fr = rewardProp.FindPropertyRelative("freeReward");      // ссылка на заготовленную «свободную награду»
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, line), fr, new GUIContent("Free Reward Def")); y += line + 8f;
                    break;
                }
        }

        // === Gating (опционально): награда доступна только при выполнении доп.требования ===
        var gated = rewardProp.FindPropertyRelative("gatedByAdditional");   // включить gating?
        var tip = rewardProp.FindPropertyRelative("tooltip");             // поясняющий текст (UI)
        var reqTag = rewardProp.FindPropertyRelative("requiredTag");         // какой «тэг/скилл» требуется
        var reqAmt = rewardProp.FindPropertyRelative("requiredAmount");      // и сколько

        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, line), gated, new GUIContent("Gated by Additional?")); y += line + 4f;

        if (gated.boolValue)
        {
            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, line), tip, new GUIContent("Tooltip")); y += line + 4f;

            float wTag = 220f;                                               // ширина колонки «Required Tag»
            EditorGUI.PropertyField(new Rect(rect.x, y, wTag, line), reqTag, new GUIContent("Required Tag"));
            EditorGUI.PropertyField(new Rect(rect.x + wTag + 8, y, rect.width - wTag - 8, line), reqAmt, new GUIContent("Amount"));
            // y += line + 4f;                                                // (если ниже нет полей — можно не инкрементить)
        }
    }

    // Создание/обновление ReorderableList для списка ChoiceOption
    private void BuildChoiceList(SerializedObject so)
    {
        _soCachedChoices = so;
        var p = so.FindProperty("choices");                // List<EventSO.ChoiceOption>
        _rlChoices = new ReorderableList(so, p, true, true, true, true);

        _rlChoices.drawHeaderCallback = rect =>
            EditorGUI.LabelField(rect, "Choices (2–3 options)");

        // Динамическая высота элемента — по содержимому
        _rlChoices.elementHeightCallback = idx =>
        {
            var el = p.GetArrayElementAtIndex(idx);
            return ChoiceElementHeight(el);               // см. патч ниже
        };

        // Отрисовка элемента — теперь с индексом (для заголовка Option N)
        _rlChoices.drawElementCallback = (rect, index, active, focused) =>
        {
            var el = p.GetArrayElementAtIndex(index);
            DrawChoiceElement(rect, el, index);          // см. патч ниже
        };

        // Добавление нового варианта (инициируем дефолты)
        _rlChoices.onAddDropdownCallback = (r, list) =>
        {
            if (p.arraySize >= 3) { EditorApplication.Beep(); return; } // максимум 3
            p.arraySize++;
            var el = p.GetArrayElementAtIndex(p.arraySize - 1);
            el.FindPropertyRelative("description").stringValue = "New choice...";
            el.FindPropertyRelative("showRewards").boolValue = true;
            el.FindPropertyRelative("showHiddenOutcomes").boolValue = false;
            el.FindPropertyRelative("mainCostType").enumValueIndex = (int)CostType.Hands;
            el.FindPropertyRelative("mainCostAmount").intValue = 1;
            // пустые списки создадутся сами
            so.ApplyModifiedProperties();
        };

        // Ограничим минимум 2 пункта
        _rlChoices.onCanRemoveCallback = list => p.arraySize > 2;
    }


    // Высота одного ChoiceOption с учётом описания, списков и ВСТРОЕННОГО редактора Rewards
    private float ChoiceElementHeight(SerializedProperty choiceProp)
    {
        float h = 2f; // верхний отступ
        float line = EditorGUIUtility.singleLineHeight;

        // Заголовок Option N
        h += line + 2f;

        // description (учитываем [TextArea])
        var desc = choiceProp.FindPropertyRelative("description");
        h += EditorGUI.GetPropertyHeight(desc, new GUIContent("Description"), true) + 4f;

        // main cost
        h += line + 4f; // mainCostType
        h += line + 8f; // mainCostAmount

        // additionalCosts (список)
        var add = choiceProp.FindPropertyRelative("additionalCosts");
        h += EditorGUI.GetPropertyHeight(add, true) + 6f;

        // penalties (список)
        var pen = choiceProp.FindPropertyRelative("penalties");
        h += EditorGUI.GetPropertyHeight(pen, true) + 6f;

        // toggles
        var showRw = choiceProp.FindPropertyRelative("showRewards");
        var showHo = choiceProp.FindPropertyRelative("showHiddenOutcomes");
        h += line + 2f; // showRewards
        h += line + 4f; // showHiddenOutcomes

        // === ВСТРОЕННЫЙ редактор Rewards (всегда доступен!) ===
        var rewards = choiceProp.FindPropertyRelative("rewards");
        h += RewardsInlineHeight(rewards) + 6f;           // см. патч ниже

        // HiddenOutcomes (иконки) — если включено
        if (showHo.boolValue)
        {
            var hidd = choiceProp.FindPropertyRelative("hiddenOutcomes");
            h += EditorGUI.GetPropertyHeight(hidd, true) + 6f;
        }

        return h + 6f; // нижний отступ
    }



    // Инициализация полей под конкретный тип награды
    private static void InitRewardOfType(SerializedProperty r, EventSO.RewardType t)
    {
        var typeProp = r.FindPropertyRelative("type");
        if (typeProp != null) typeProp.enumValueIndex = (int)t;

        // Общие безопасные дефолты
        var amt = r.FindPropertyRelative("amount"); if (amt != null) amt.intValue = 1;
        var rc = r.FindPropertyRelative("cardCount"); if (rc != null) rc.intValue = 1;
        var kp = r.FindPropertyRelative("knownPreview"); if (kp != null) kp.boolValue = true;
        var gat = r.FindPropertyRelative("gatedByAdditional"); if (gat != null) gat.boolValue = false;
        var tip = r.FindPropertyRelative("tooltip"); if (tip != null) tip.stringValue = string.Empty;
        var reqA = r.FindPropertyRelative("requiredAmount"); if (reqA != null) reqA.intValue = 1;

        var reqT = r.FindPropertyRelative("requiredTag"); if (reqT != null) reqT.enumValueIndex = 0;

        // Сброс ссылок (чтобы не тянулись от прежнего типа)
        var res = r.FindPropertyRelative("resource"); if (res != null) res.objectReferenceValue = null;
        var stat = r.FindPropertyRelative("stat"); if (stat != null) stat.enumValueIndex = 0;
        var rstA = r.FindPropertyRelative("restoreAmount"); if (rstA != null) rstA.intValue = 1;
        var card = r.FindPropertyRelative("cardDef"); if (card != null) card.objectReferenceValue = null;
        var fr = r.FindPropertyRelative("freeReward"); if (fr != null) fr.objectReferenceValue = null;
    }

    // Удаление несоответствующих полей при смене типа
    private static void ClearRewardIrrelevant(SerializedProperty r)
    {
        var res = r.FindPropertyRelative("resource"); if (res != null) res.objectReferenceValue = null;
        var amt = r.FindPropertyRelative("amount"); if (amt != null) amt.intValue = 1;

        var stat = r.FindPropertyRelative("stat"); if (stat != null) stat.enumValueIndex = 0;
        var rstA = r.FindPropertyRelative("restoreAmount"); if (rstA != null) rstA.intValue = 1;

        var card = r.FindPropertyRelative("cardDef"); if (card != null) card.objectReferenceValue = null;
        var cnt = r.FindPropertyRelative("cardCount"); if (cnt != null) cnt.intValue = 1;
        var kp = r.FindPropertyRelative("knownPreview"); if (kp != null) kp.boolValue = true;

        var fr = r.FindPropertyRelative("freeReward"); if (fr != null) fr.objectReferenceValue = null;
    }

    // Создаёт PreviewRenderUtility и настраивает камеру
    private void EnsurePreview()
    {
        if (_preview != null) return;                               // уже есть — выходим

        _preview = new PreviewRenderUtility();                      // мини-сцена + своя камера
        _preview.camera.orthographic = true;                        // 2D-просмотр
        _preview.camera.nearClipPlane = 0.01f;                      // близкий клип — UI рядом с камерой
        _preview.camera.farClipPlane = 100f;                       // хватит с запасом
        _preview.camera.transform.position = new Vector3(0, 0, -10);// смотрим вдоль +Z
        _preview.camera.transform.rotation = Quaternion.identity;   // без поворота
        _preview.camera.clearFlags = CameraClearFlags.SolidColor;   // сплошной фон
        _preview.camera.backgroundColor = _previewBg;               // цвет фона
    }

    // Полная зачистка предпросмотра (на закрытие окна/смену префаба)
    private void DestroyPreviewGO()
    {
        if (_previewGO != null)
        {
            _preview.Cleanup();                                     // зачистка всей мини-сцены
            _preview = null;
            _previewGO = null;
            _previewBadge = null;
            _previewLastEventID = 0;
            _previewLastPrefabID = 0;
        }
    }

    // Unity вызовет при выгрузке окна — почистимся
    private void OnDisable()
    {
        DestroyPreviewGO();
    }

    private void AddToPreviewScene(GameObject go)
    {
        if (_preview == null || go == null) return;
        go.hideFlags = HideFlags.HideAndDontSave;     // не светится в Hierarchy и не сохраняется
                                                      // Разные версии Unity: пробуем AddManagedGO, иначе AddSingleGO
        var mi = typeof(PreviewRenderUtility).GetMethod("AddManagedGO",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (mi != null) mi.Invoke(_preview, new object[] { go });
        else _preview.AddSingleGO(go);
    }
    private void BuildOrRebindBadgePreview(EventSO ev)
    {
        EnsurePreview();
        if (_preview == null) return;

        int prefabID = _previewPrefab ? _previewPrefab.GetInstanceID() : 0;

        // Если префаб сменился — пересоздаём mini-scene
        if (_previewGO == null || _previewLastPrefabID != prefabID)
        {
            DestroyPreviewGO();
            EnsurePreview();
            if (_previewPrefab == null) return;

            // ВАЖНО: инстансим в редакторе, но тут же переносим в preview-сцену и скрываем
            _previewGO = (GameObject)PrefabUtility.InstantiatePrefab(_previewPrefab);
            AddToPreviewScene(_previewGO);

            _previewBadge = _previewGO ? _previewGO.GetComponentInChildren<HexEventBadgeUI>(true) : null;

            if (_previewGO)
            {
                _previewGO.transform.position = Vector3.zero;
                _previewGO.transform.rotation = Quaternion.identity;
                _previewGO.transform.localScale = Vector3.one;
            }

            // Настроим Canvas префаба, чтобы UGUI корректно рисовался в превью
            var canvas = _previewGO ? _previewGO.GetComponentInChildren<Canvas>(true) : null;
            if (canvas)
            {
                // Для ScreenSpace — укажем камеру; для WorldSpace — тоже не повредит
                canvas.renderMode = canvas.renderMode == RenderMode.WorldSpace
                                     ? RenderMode.WorldSpace
                                     : RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = _preview.camera;
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    canvas.planeDistance = 1f; // рядом с камерой превью
                var gr = _previewGO.GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>(true);
                if (gr) gr.enabled = false; // в превью события мыши не нужны
            }

            _previewLastPrefabID = prefabID;
        }

        // Привяжем EventSO — это заполняет иконки/надписи/панели, как в игре
        if (_previewBadge != null) _previewBadge.Bind(ev);

        FramePreviewToBadge();                     // подстроим камеру под размер
        _previewLastEventID = ev ? ev.GetInstanceID() : 0;
    }


    // Вычисляет границы UI и настраивает orthographicSize камеры, чтобы всё уместилось
    private void FramePreviewToBadge()
    {
        if (_preview == null || _previewGO == null) return;

        var rt = _previewGO.transform as RectTransform;
        float halfW = 1f, halfH = 1f;

        if (rt)
        {
            var size = rt.rect.size;
            var scale = rt.lossyScale;
            halfW = Mathf.Max(0.1f, size.x * scale.x * 0.5f);
            halfH = Mathf.Max(0.1f, size.y * scale.y * 0.5f);
        }

        float pad = 1.12f;
        _preview.camera.orthographicSize = halfH * pad;
        _preview.camera.transform.position = new Vector3(0, 0, -10);
    }



}
#endif
