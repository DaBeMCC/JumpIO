using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class LevelEditorWindow : EditorWindow
{
    const string TilesFolder = "Assets/Art/Palettes/Tiles";
    const string TexturesFolder = "Assets/Art/Palettes/Tiles/Textures";
    const string LevelScene = "Assets/Scenes/Level_01.unity";
    const float TimelineHeight = 150f;
    static readonly float[] Snaps = { 1f, 0.5f, 0.25f };
    static readonly string[] SnapLabels = { "1", "1/2", "1/4" };
    static readonly int[] Divisions = { 1, 2, 4 };
    static readonly string[] DivisionLabels = { "1 beat", "1/2", "1/4" };

    enum EditTool { Select, Paint, Erase, Texture }
    enum BrushShape { Single, Rectangle }

    class PaletteItem
    {
        public string name;
        public string category;
        public TileBase tile;
        public bool hazard;
        public GameObject prefab;
        public Sprite icon;
        public Color tint = Color.white;
        public Color timelineColor = Color.white;
        public Sprite scenery;
        public int order;
    }

    [SerializeField] EditTool tool = EditTool.Paint;
    [SerializeField] BrushShape shape;
    [SerializeField] int selected;
    [SerializeField] int textureIndex = -1;
    [SerializeField] float rotation;
    [SerializeField] bool flip;
    [SerializeField] int snapIndex = 1;
    [SerializeField] bool snapToBeat = true;
    [SerializeField] int divisionIndex;
    [SerializeField] Sprite newTextureSprite;
    [SerializeField] Color newTextureTint = Color.white;
    [SerializeField] Vector2 paletteScroll;

    readonly List<PaletteItem> palette = new();
    readonly List<Tile> textures = new();
    readonly HashSet<string> collapsed = new();

    LevelLandscape landscape;
    Transform objects;
    MusicConductor conductor;
    LevelData level;

    [MenuItem("JumpIO/Level Editor %#l")]
    static void Open() => GetWindow<LevelEditorWindow>("Level Editor");

    void OnEnable()
    {
        minSize = new Vector2(420f, 520f);
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += OnUpdate;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        Selection.selectionChanged += SceneView.RepaintAll;
        LoadScene();
        LoadPalette();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= OnUpdate;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
        Selection.selectionChanged -= SceneView.RepaintAll;
        Tools.hidden = false;
        StopPreview();
    }

    void OnSceneChanged(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to)
    {
        LoadScene();
        LoadPalette();
    }

    void OnHierarchyChanged()
    {
        if (landscape == null || objects == null) LoadScene();
        timelineDirty = true;
    }

    void OnUndoRedo()
    {
        if (!Ready) return;
        selectedCells.Clear();
        RegenerateColliders();
        timelineDirty = true;
        Repaint();
    }

    bool Ready => landscape != null && objects != null && level != null && !EditorApplication.isPlayingOrWillChangePlaymode;

    void LoadScene()
    {
        landscape = FindAnyObjectByType<LevelLandscape>();
        conductor = FindAnyObjectByType<MusicConductor>();
        level = conductor != null ? conductor.Level : null;
        objects = GameObject.Find("Objects")?.transform;
        selectedCells.Clear();
        ProtectTilemaps();
        envelope = null;
        timelineDirty = true;
    }

    void LoadPalette()
    {
        palette.Clear();
        if (landscape != null)
        {
            AddTile("Block", "Tile_Block", landscape.Grass);
            AddTile("Slab", "Tile_Slab", landscape.Slab);
            AddTile("Slope", "Tile_Slope", landscape.Slope);
            AddTile("Spike", "Tile_Spike", landscape.Spike, true);
        }
        foreach (var prefab in Prefabs("Assets/Prefabs/Objects").Concat(Prefabs("Assets/Prefabs/Enemies"))
                     .Append(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FinishLine.prefab")))
        {
            if (prefab == null) continue;
            var (category, color) = Category(prefab);
            var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            palette.Add(new PaletteItem
            {
                name = prefab.name.Replace("JumpOrb_", "").Replace("JumpPad_", "").Replace("GravityPortal_", "")
                    .Replace("ModePortal_", "").Replace("SpeedPortal_", "").Replace("05x", "0.5x"),
                category = category,
                prefab = prefab,
                icon = renderer != null ? renderer.sprite : null,
                tint = renderer != null ? renderer.color : Color.white,
                timelineColor = color,
            });
        }
        LoadScenery();
        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, palette.Count - 1));
        LoadTextures();
    }

    void AddTile(string name, string asset, TileBase visual, bool hazard = false)
    {
        var tile = AssetDatabase.LoadAssetAtPath<TileBase>($"{TilesFolder}/{asset}.asset");
        if (tile == null) return;
        palette.Add(new PaletteItem
        {
            name = name, category = "Blocks", tile = tile, hazard = hazard,
            icon = (visual as Tile)?.sprite ?? (tile as Tile)?.sprite,
        });
    }

    static IEnumerable<GameObject> Prefabs(string folder) =>
        AssetDatabase.FindAssets("t:Prefab", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>);

    static (string, Color) Category(GameObject prefab)
    {
        if (prefab.GetComponent<JumpOrb>()) return ("Orbs", new Color(1f, 0.85f, 0.2f));
        if (prefab.GetComponent<JumpPad>()) return ("Pads", new Color(1f, 0.45f, 0.75f));
        if (prefab.GetComponent<ModePortal>()) return ("Mode portals", new Color(0.35f, 0.9f, 0.4f));
        if (prefab.GetComponent<GravityPortal>()) return ("Gravity portals", new Color(0.4f, 0.7f, 1f));
        if (prefab.GetComponent<SpeedPortal>()) return ("Speed portals", new Color(1f, 0.55f, 0.15f));
        if (prefab.GetComponent<EnemyDamage>() || prefab.GetComponentInChildren<EnemyDamage>()) return ("Enemies", new Color(1f, 0.3f, 0.3f));
        return ("Items", Color.white);
    }

    void LoadTextures()
    {
        textures.Clear();
        textures.AddRange(AssetDatabase.FindAssets("t:Tile", new[] { TilesFolder })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<Tile>)
            .Where(t => t != null && t.sprite != null && t.colliderType == Tile.ColliderType.None));
        textureIndex = Mathf.Clamp(textureIndex, -1, textures.Count - 1);
    }

    PaletteItem Current => selected >= 0 && selected < palette.Count ? palette[selected] : null;
    Tile CurrentTexture => textureIndex >= 0 && textureIndex < textures.Count ? textures[textureIndex] : null;
    float Snap => Snaps[Mathf.Clamp(snapIndex, 0, Snaps.Length - 1)];
    int Division => Divisions[Mathf.Clamp(divisionIndex, 0, Divisions.Length - 1)];

    void OnGUI()
    {
        HandleWindowKeys(Event.current);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Play Mode: editing is paused. Stop to keep editing.", MessageType.Info);
            return;
        }
        if (landscape == null || objects == null || level == null)
        {
            EditorGUILayout.HelpBox("Open a level scene (Grid with Ground_Tilemap + LevelLandscape, Objects and a MusicConductor with Level Data).", MessageType.Warning);
            if (GUILayout.Button("Open Level_01")) EditorSceneManager.OpenScene(LevelScene);
            if (GUILayout.Button("Reload")) { LoadScene(); LoadPalette(); }
            return;
        }

        var top = new Rect(0f, 0f, position.width, position.height - TimelineHeight);
        GUILayout.BeginArea(top);
        DrawTools();
        paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll);
        DrawPalette();
        DrawTextures();
        DrawLevel();
        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();

        DrawTimeline(new Rect(0f, position.height - TimelineHeight, position.width, TimelineHeight));
    }

    void DrawTools()
    {
        EditorGUILayout.Space(4f);
        tool = (EditTool)GUILayout.Toolbar((int)tool, new[] { "Select (Esc)", "Paint", "Erase", "Texture" });
        using (new EditorGUILayout.HorizontalScope())
        {
            shape = (BrushShape)GUILayout.Toolbar((int)shape, new[] { "Brush", "Rectangle" }, GUILayout.Width(150f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("-90°", GUILayout.Width(40f))) rotation = Mathf.Repeat(rotation - 90f, 360f);
            if (GUILayout.Button("+90°", GUILayout.Width(40f))) rotation = Mathf.Repeat(rotation + 90f, 360f);
            flip = GUILayout.Toggle(flip, "Flip", "Button", GUILayout.Width(40f));
            GUILayout.Label($"{rotation:0}°", GUILayout.Width(36f));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Object grid", GUILayout.Width(72f));
            snapIndex = GUILayout.Toolbar(snapIndex, SnapLabels, GUILayout.Width(120f));
            GUILayout.FlexibleSpace();
            snapToBeat = GUILayout.Toggle(snapToBeat, "Snap X to beat", GUILayout.Width(110f));
            using (new EditorGUI.DisabledScope(!snapToBeat))
                divisionIndex = EditorGUILayout.Popup(divisionIndex, DivisionLabels, GUILayout.Width(64f));
        }
        DrawSceneryOptions();
        EditorGUILayout.LabelField(tool == EditTool.Select
                ? "Click: select one block or object · Drag: move · Del · Arrows · R: rotate · Ctrl+D: duplicate · Esc: deselect"
                : "Click: place · Shift+click: erase one · Ctrl+click: test start · R: rotate · F: flip · Space: play · M: marker",
            EditorStyles.miniLabel);
    }

    void DrawPalette()
    {
        foreach (var group in palette.Select((item, index) => (item, index)).GroupBy(p => p.item.category))
        {
            bool open = !collapsed.Contains(group.Key);
            bool now = EditorGUILayout.Foldout(open, group.Key, true, EditorStyles.foldoutHeader);
            if (now != open) { if (now) collapsed.Remove(group.Key); else collapsed.Add(group.Key); }
            if (!now) continue;
            DrawGrid(group.Select(p => (p.item.name, p.item.icon, p.item.tint, p.index == selected, p.index)).ToList(), index =>
            {
                selected = index;
                if (tool != EditTool.Paint) tool = EditTool.Paint;
            });
        }
    }

    void DrawTextures()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Applied to new blocks and spikes. Use the Texture tool to repaint existing ones. Auto = grass on top, earth below.",
            EditorStyles.wordWrappedMiniLabel);
        var cells = new List<(string, Sprite, Color, bool, int)> { ("Auto", null, Color.white, textureIndex < 0, -1) };
        cells.AddRange(textures.Select((t, i) => (t.name.Replace("Tile_", "").Replace("Texture_", "").Replace("Visual", ""), t.sprite, t.color, i == textureIndex, i)));
        DrawGrid(cells, index => textureIndex = index);
        using (new EditorGUILayout.HorizontalScope())
        {
            newTextureSprite = (Sprite)EditorGUILayout.ObjectField(newTextureSprite, typeof(Sprite), false, GUILayout.Width(170f));
            newTextureTint = EditorGUILayout.ColorField(newTextureTint, GUILayout.Width(60f));
            using (new EditorGUI.DisabledScope(newTextureSprite == null))
                if (GUILayout.Button("Add texture")) CreateTexture();
        }
    }

    void DrawLevel()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        float startSpeed = EditorGUILayout.FloatField("Start speed", level.speeds.Length > 0 ? level.speeds[0] : 10.386f);
        if (EditorGUI.EndChangeCheck() && level.speeds.Length > 0)
        {
            Undo.RecordObject(level, "Start speed");
            level.speeds[0] = Mathf.Max(1f, startSpeed);
            level.RecalculateTimes();
            EditorUtility.SetDirty(level);
            timelineDirty = true;
        }
        EditorGUILayout.LabelField($"Length {level.length:0.0} blocks · {level.TimeAtX(level.length):0.00} s · {level.bpm:0.##} BPM · markers {level.markers?.Length ?? 0}",
            EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("▶ Play from playhead", "Starts Play Mode at the playhead with the mode and gravity of the last portals"))) PlayFromPlayhead();
            if (GUILayout.Button("▶ Play from start")) { StopPreview(); SyncLevelData(); LevelEditorPlaytest.StartFromBeginning(); }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Apply", "Speeds from portals, length from the finish line, camera bounds, colliders and visuals"))) ApplyLevel();
            if (GUILayout.Button("Clear markers")) ClearMarkers();
            if (GUILayout.Button("Save scene")) { ApplyLevel(); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets(); }
        }
    }

    void DrawGrid(List<(string name, Sprite icon, Color tint, bool on, int index)> cells, System.Action<int> pick)
    {
        const float size = 64f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / (size + 4f)));
        for (int start = 0; start < cells.Count; start += columns)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = start; i < Mathf.Min(start + columns, cells.Count); i++)
                {
                    var cell = cells[i];
                    var rect = GUILayoutUtility.GetRect(size, size + 14f, GUILayout.Width(size), GUILayout.Height(size + 14f));
                    if (Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.DrawRect(rect, cell.on ? new Color(0.24f, 0.48f, 0.9f, 0.55f) : new Color(0f, 0f, 0f, 0.18f));
                        DrawSprite(new Rect(rect.x + 6f, rect.y + 4f, size - 12f, size - 12f), cell.icon, cell.tint, 0f, false);
                        GUI.Label(new Rect(rect.x, rect.yMax - 16f, rect.width, 16f), cell.name, CenteredMini);
                    }
                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        pick(cell.index);
                        Event.current.Use();
                        SceneView.RepaintAll();
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }
    }

    static GUIStyle centeredMini;
    static GUIStyle CenteredMini => centeredMini ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };

    static void DrawSprite(Rect area, Sprite sprite, Color tint, float angle, bool flipX)
    {
        if (sprite == null || sprite.texture == null) return;
        var tex = sprite.texture;
        var tr = sprite.textureRect;
        var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        if (flipX) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
        float scale = Mathf.Min(area.width / tr.width, area.height / tr.height);
        var fit = new Rect(0f, 0f, tr.width * scale, tr.height * scale) { center = area.center };
        var matrix = GUI.matrix;
        var color = GUI.color;
        if (angle != 0f) GUIUtility.RotateAroundPivot(-angle, area.center);
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(fit, tex, uv, true);
        GUI.color = color;
        GUI.matrix = matrix;
    }

    void CreateTexture()
    {
        if (!AssetDatabase.IsValidFolder(TexturesFolder)) AssetDatabase.CreateFolder(TilesFolder, "Textures");
        var sprite = newTextureSprite;
        var tile = CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = newTextureTint;
        tile.colliderType = Tile.ColliderType.None;
        var bounds = sprite.bounds;
        var scale = new Vector3(1f / Mathf.Max(0.01f, bounds.size.x), 1f / Mathf.Max(0.01f, bounds.size.y), 1f);
        tile.transform = Matrix4x4.TRS(-Vector3.Scale(bounds.center, scale), Quaternion.identity, scale);
        tile.flags = TileFlags.LockTransform | TileFlags.LockColor;
        string path = AssetDatabase.GenerateUniqueAssetPath($"{TexturesFolder}/Texture_{sprite.name}.asset");
        AssetDatabase.CreateAsset(tile, path);
        AssetDatabase.SaveAssets();
        LoadTextures();
        textureIndex = textures.IndexOf(AssetDatabase.LoadAssetAtPath<Tile>(path));
    }

    void ClearMarkers()
    {
        Undo.RecordObject(level, "Clear markers");
        level.markers = new float[0];
        EditorUtility.SetDirty(level);
        SceneView.RepaintAll();
    }

    void AddMarker()
    {
        Undo.RecordObject(level, "Add marker");
        float t = (float)(snapToBeat ? NearestBeatTime(playhead) : playhead);
        level.markers = (level.markers ?? new float[0]).Append(t).OrderBy(m => m).ToArray();
        EditorUtility.SetDirty(level);
        SceneView.RepaintAll();
        Repaint();
    }

    void SyncLevelData()
    {
        if (level == null || objects == null) return;
        var portals = objects.GetComponentsInChildren<SpeedPortal>().OrderBy(p => p.transform.position.x).ToList();
        var finish = objects.GetComponentInChildren<FinishLine>();
        float origin = level.changeX.Length > 0 ? level.changeX[0] : 0f;
        float startSpeed = level.speeds.Length > 0 ? level.speeds[0] : 10.386f;
        var changeX = new[] { origin }.Concat(portals.Select(p => p.transform.position.x - 1f)).ToArray();
        var speeds = new[] { startSpeed }.Concat(portals.Select(p => p.Speed)).ToArray();
        float length = finish != null ? finish.transform.position.x : level.length;
        if (changeX.SequenceEqual(level.changeX) && speeds.SequenceEqual(level.speeds) && Mathf.Approximately(length, level.length)) return;
        Undo.RecordObject(level, "Sync level data");
        level.changeX = changeX;
        level.speeds = speeds;
        level.length = length;
        level.RecalculateTimes();
        EditorUtility.SetDirty(level);
        timelineDirty = true;
    }

    void ApplyLevel()
    {
        SyncLevelData();
        RegenerateColliders();
        Undo.RegisterCompleteObjectUndo(VisualMaps(), "Rebuild visuals");
        landscape.RebuildAppearance();
        var writer = LevelSceneWriter.FromActiveScene();
        if (writer != null)
        {
            landscape.Ground.CompressBounds();
            writer.FitCameraBounds(level.length, landscape.Ground.cellBounds.yMax);
        }
        EditorSceneManager.MarkSceneDirty(landscape.gameObject.scene);
    }

    Object[] VisualMaps()
    {
        var maps = new List<Object> { landscape.Ground, landscape.TerrainVisuals(), landscape.Textures() };
        if (landscape.Hazards != null) { maps.Add(landscape.Hazards); maps.Add(landscape.HazardVisuals()); }
        return maps.ToArray();
    }

    void RegenerateColliders()
    {
        foreach (var map in new[] { landscape.Ground, landscape.Hazards })
        {
            if (map == null) continue;
            if (map.TryGetComponent(out TilemapCollider2D collider)) collider.ProcessTilemapChanges();
            if (map.TryGetComponent(out CompositeCollider2D composite)) composite.GenerateGeometry();
        }
    }

    void PlayFromPlayhead()
    {
        StopPreview();
        SyncLevelData();
        LevelEditorPlaytest.Start(StartState());
    }

    PlaytestStart.Data StartState()
    {
        float t = Mathf.Max(0f, (float)playhead);
        float x = level.XAtTime(t);
        var data = new PlaytestStart.Data
        {
            time = t, mode = level.startMode, corridorY = level.startCorridorY,
            y = level.startY, gravityInverted = level.startGravityInverted,
        };
        var portal = objects.GetComponentsInChildren<ModePortal>()
            .Where(p => p.transform.position.x <= x).OrderBy(p => p.transform.position.x).LastOrDefault();
        if (portal != null)
        {
            data.mode = portal.Mode;
            data.corridorY = portal.transform.position.y;
            data.y = SurfaceY(x, portal.transform.position.y);
        }
        var gravity = objects.GetComponentsInChildren<GravityPortal>()
            .Where(p => p.transform.position.x <= x).OrderBy(p => p.transform.position.x).LastOrDefault();
        if (gravity != null) data.gravityInverted = new SerializedObject(gravity).FindProperty("invert").boolValue;
        if (spawnY.HasValue) data.y = spawnY.Value;
        return data;
    }

    float SurfaceY(float x, float y)
    {
        var ground = landscape.Ground;
        var hazards = landscape.Hazards;
        var cell = ground.WorldToCell(new Vector3(x, y, 0f));
        for (int i = 0; i < 64 && (ground.HasTile(cell) || hazards != null && hazards.HasTile(cell)); i++) cell.y++;
        for (var below = cell + Vector3Int.down; below.y > cell.y - 64; below.y--)
        {
            if (hazards != null && hazards.HasTile(below)) break;
            if (ground.HasTile(below)) return ground.GetCellCenterWorld(below + Vector3Int.up).y;
        }
        return ground.GetCellCenterWorld(cell).y;
    }

    void HandleWindowKeys(Event e)
    {
        if (e.type != EventType.KeyDown || !Ready) return;
        if (GUIUtility.keyboardControl != 0) return;
        if (HandleSharedKey(e.keyCode)) e.Use();
    }

    bool HandleSharedKey(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Space: TogglePlay(); return true;
            case KeyCode.M: AddMarker(); return true;
            case KeyCode.Escape when tool != EditTool.Select: tool = EditTool.Select; Repaint(); return true;
            case KeyCode.R when tool == EditTool.Paint: rotation = Mathf.Repeat(rotation - 90f, 360f); Repaint(); return true;
            case KeyCode.F when tool == EditTool.Paint: flip = !flip; Repaint(); return true;
        }
        return false;
    }
}
