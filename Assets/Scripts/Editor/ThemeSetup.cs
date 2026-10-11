using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;
public static class ThemeSetup
{
    const string ThemesFolder = "Assets/Art/Sprites/Themes";
    const string PlaceholderTiles = "Assets/Art/Palettes/Tiles";
    const string ThemeTiles = "Assets/Art/Palettes/Themes";
    const string AnimationsFolder = "Assets/Animations";
    const string SkinsFolder = "Assets/Art/Skins";
    const int ItemPixelsPerUnit = 128;
    const int FramePixels = 160;
    const float FramePixelsPerUnit = 120f;
    const float BackdropPixelsPerUnit = 64f;
    const int MinComponentPixels = 40;
    const float FarOffset = 4.5f;
    const float NearOffset = 3.5f;

    enum Fit { Fill, Bottom, Center }

    class Theme
    {
        public string name, prefix, scene, displayName;
        public Color particleColor;
        public float characterScale = 1f;
        public string Folder => $"{ThemesFolder}/{name}";
        public string Sheet(string part) => $"{Folder}/Sheets/{prefix}_{part}.png";
    }

    class Item
    {
        public string name, sheet;
        public float cellWidth, cellHeight;
        public int col, row;
        public float width, height;
        public Fit fit;
        public Vector2 pivot = new(0.5f, 0.5f);
        public Rect area = new(0f, 0f, 1f, 1f);
    }

    static readonly Theme[] Themes =
    {
        new Theme { name = "Pirates", prefix = "Pirates", scene = "Level_02", displayName = "Pirate", particleColor = new Color(0.85f, 0.65f, 0.3f) },
        new Theme { name = "Cyberpunk", prefix = "Cyber", scene = "Level_03", displayName = "Robot", particleColor = new Color(0.2f, 0.95f, 1f), characterScale = 1.5f },
    };

    static readonly (string tile, string item, bool ignoreScale)[] Swaps =
    {
        ("Tile_Thin", "Thin", true),
        ("Tile_Panel", "Panel", false),
        ("Tile_Saw", "Saw", false),
        ("Tile_SpikeRow", "SpikeRow", true),
        ("Tile_Strip", "Strip", true),
    };

    static readonly (string prefab, string item)[] ObjectSprites =
    {
        ("JumpOrb_Yellow", "OrbYellow"), ("JumpOrb_Pink", "OrbPink"), ("JumpOrb_Red", "OrbRed"), ("JumpOrb_Blue", "OrbBlue"),
        ("JumpOrb_Green", "OrbGreen"), ("JumpOrb_Black", "OrbBlack"), ("JumpOrb_Dash", "OrbDash"), ("JumpOrb_GravityDash", "OrbGravityDash"),
        ("JumpPad_Yellow", "PadYellow"), ("JumpPad_Pink", "PadPink"), ("JumpPad_Red", "PadRed"), ("JumpPad_Blue", "PadBlue"),
        ("ModePortal_Cube", "PortalCube"), ("ModePortal_Ship", "PortalShip"), ("ModePortal_Ball", "PortalBall"), ("ModePortal_Ufo", "PortalUfo"),
        ("GravityPortal_Normal", "GravityNormal"), ("GravityPortal_Inverted", "GravityInverted"),
        ("SpeedPortal_05x", "Speed05x"), ("SpeedPortal_1x", "Speed1x"), ("SpeedPortal_2x", "Speed2x"), ("SpeedPortal_3x", "Speed3x"),
        ("SpeedPortal_4x", "Speed4x"), ("Coin", "Coin"), ("FinishLine", "Finish"),
    };

    static readonly (string clip, int row, int col, int count)[] Animations =
    {
        ("Idle", 0, 0, 4), ("Run", 1, 0, 16), ("Jump", 3, 0, 4), ("Fall", 3, 4, 4), ("Action", 4, 0, 8),
        ("Hurt", 5, 0, 4), ("Death", 5, 4, 4), ("Ship", 6, 0, 4), ("Ball", 6, 4, 4), ("Ufo", 7, 0, 4),
    };

    [MenuItem("JumpIO/Setup/Level Themes")]
    public static void Build()
    {
        var log = new StringBuilder();
        foreach (var theme in Themes)
        {
            if (!File.Exists(theme.Sheet("Tiles"))) { log.AppendLine($"{theme.name}: missing {theme.Sheet("Tiles")}"); continue; }
            var sprites = BuildItems(theme, log);
            var tiles = BuildTiles(theme, sprites);
            var backdrop = BuildBackdrop(theme);
            var skin = BuildSkin(theme, log);
            ApplyToScene(theme, sprites, tiles, backdrop, skin, log);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("JUMPIO_THEMES_OK\n" + log);
    }

    const string BonusSource = "LevelSource/CCLocalLevels_2026-10-10.dat";

    static readonly (string gdName, string scene, string shownName, string data, string song)[] BonusLevels =
    {
        ("The Seven Seas  2", "Level_02", "The Seven Seas", "TheSevenSeas", "TheSevenSeas"),
        ("Airborne Robots cop", "Level_03", "Airborne Robots", "AirborneRobots", "AirborneRobots"),
    };

    static readonly (string tile, string sprite, string[] entries)[] ShapedTiles =
    {
        ("Tile_Thin", "Block", new[] { "Thin platform (30x1.5)", "Thin slab (30x3)", "Thin slab (30x6)" }),
        ("Tile_Panel", "Block", new[] { "Slab (30x15)", "Slab (30x16)", "Slab (30x14)", "Block (30x21)", "Half block (15x15)" }),
        ("Tile_SpikeRow", "Spike", new[] { "Ground spikes 2.0" }),
        ("Tile_Strip", "Spike", new[] { "Wide hazard (25x6)" }),
    };

    [MenuItem("JumpIO/Setup/Rebuild Bonus Levels")]
    public static void RebuildBonusLevels()
    {
        var log = new StringBuilder();
        EnsureShapedTiles(log);
        var levels = GDLevelParser.ReadFile(BonusSource);
        foreach (var bonus in BonusLevels)
        {
            var level = levels.FirstOrDefault(l => l.name == bonus.gdName);
            string scenePath = $"Assets/Scenes/{bonus.scene}.unity";
            if (level == null || !File.Exists(scenePath)) { log.AppendLine($"{bonus.shownName}: missing level or scene"); continue; }
            var scene = EditorSceneManager.OpenScene(scenePath);
            var textures = UnityEngine.Object.FindAnyObjectByType<LevelLandscape>().Ground.transform.parent.Find(LevelAppearanceSetup.TexturesName);
            if (textures != null) textures.GetComponent<Tilemap>().ClearAllTiles();

            string kept = $"Assets/Data/{bonus.data}_LevelData.asset";
            string imported = $"Assets/Data/{new string(bonus.gdName.Where(char.IsLetterOrDigit).ToArray())}_LevelData.asset";
            if (kept != imported && File.Exists(kept) && !File.Exists(imported)) AssetDatabase.MoveAsset(kept, imported);
            log.AppendLine(GDLevelImporter.ImportIntoActiveScene(level, bonus.shownName, true).Split('\n').FirstOrDefault(l => l.StartsWith("Solids")));
            if (kept != imported && File.Exists(imported) && !File.Exists(kept)) AssetDatabase.MoveAsset(imported, kept);

            var data = AssetDatabase.LoadAssetAtPath<LevelData>(kept);
            var song = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Music/{bonus.song}.mp3");
            if (data != null && song != null && data.song != song)
            {
                string copied = AssetDatabase.GetAssetPath(data.song);
                data.song = song;
                EditorUtility.SetDirty(data);
                if (!string.IsNullOrEmpty(copied) && copied != AssetDatabase.GetAssetPath(song)) AssetDatabase.DeleteAsset(copied);
            }
            var conductor = UnityEngine.Object.FindAnyObjectByType<MusicConductor>();
            if (conductor != null && data != null)
            {
                var cs = new SerializedObject(conductor);
                cs.FindProperty("level").objectReferenceValue = data;
                cs.ApplyModifiedPropertiesWithoutUndo();
                conductor.GetComponent<AudioSource>().clip = data.song;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("JUMPIO_BONUS_REIMPORTED\n" + log);
        Build();
    }

    static void EnsureShapedTiles(StringBuilder log)
    {
        var sheet = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Sprites/Placeholders/Placeholders_Sheet.png").OfType<Sprite>().ToDictionary(s => s.name);
        var map = AssetDatabase.LoadAssetAtPath<GDObjectMap>("Assets/Data/GDObjectMap.asset");
        foreach (var (name, sprite, entries) in ShapedTiles)
        {
            string path = $"{PlaceholderTiles}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sheet[sprite];
            tile.colliderType = Tile.ColliderType.Sprite;
            EditorUtility.SetDirty(tile);
            foreach (var entry in map.entries.Where(e => entries.Contains(e.name))) entry.tile = tile;
        }
        EditorUtility.SetDirty(map);
        AssetDatabase.SaveAssets();
        typeof(GDObjectMap).GetField("lookup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(map, null);
        log.AppendLine($"Shaped tiles: {string.Join(", ", ShapedTiles.Select(s => s.tile))} (block physics shapes {sheet["Block"].GetPhysicsShapeCount()})");
    }

    [MenuItem("JumpIO/Setup/Apply Level Themes")]
    public static void ApplyOnly()
    {
        var log = new StringBuilder();
        foreach (var theme in Themes)
        {
            var sprites = LoadItemSprites(theme);
            if (sprites.Count == 0) { log.AppendLine($"{theme.name}: run Level Themes first"); continue; }
            var tiles = Directory.GetFiles($"{ThemeTiles}/{theme.name}", "*.asset")
                .Select(p => AssetDatabase.LoadAssetAtPath<Tile>(p.Replace('\\', '/'))).Where(t => t != null)
                .ToDictionary(t => t.name.Replace(theme.prefix + "_", ""));
            var backdrop = new[] { "Sky", "Far", "Near" }.Select(n => AssetDatabase.LoadAssetAtPath<Sprite>($"{theme.Folder}/Backdrop/{n}.png")).ToArray();
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkin>($"{SkinsFolder}/Skin_{theme.name}.asset");
            ApplyToScene(theme, sprites, tiles, backdrop, skin, log);
        }
        Debug.Log("JUMPIO_THEMES_APPLIED\n" + log);
    }

    static List<Item> Items(Theme theme)
    {
        var items = new List<Item>();
        void Add(string name, string part, float cols, float rows, int col, int row, float w, float h, Fit fit, Rect? area = null, Vector2? pivot = null) =>
            items.Add(new Item
            {
                name = name, sheet = theme.Sheet(part), cellWidth = 1f / cols, cellHeight = 1f / rows, col = col, row = row,
                width = w, height = h, fit = fit, area = area ?? new Rect(0f, 0f, 1f, 1f), pivot = pivot ?? new Vector2(0.5f, 0.5f),
            });

        Add("Top", "Tiles", 4, 4, 0, 0, 1f, 1f, Fit.Fill);
        Add("Fill", "Tiles", 4, 4, 1, 0, 1f, 1f, Fit.Fill);
        Add("FillAlt", "Tiles", 4, 4, 2, 0, 1f, 1f, Fit.Fill);
        Add("Pillar", "Tiles", 4, 4, 3, 0, 1f, 1f, Fit.Fill);
        Add("Thin", "Tiles", 4, 4, 0, 1, 1f, 0.2f, Fit.Fill);
        Add("Slab", "Tiles", 4, 4, 1, 1, 1f, 1f, Fit.Fill, new Rect(0f, 0f, 1f, 0.5f));
        Add("Slope", "Tiles", 4, 4, 2, 1, 1f, 1f, Fit.Fill);
        Add("Panel", "Tiles", 4, 4, 3, 1, 1f, 1f, Fit.Fill);
        Add("Spike", "Tiles", 4, 4, 0, 2, 1f, 1f, Fit.Bottom);
        Add("SpikeSmall", "Tiles", 4, 4, 1, 2, 1f, 1f, Fit.Bottom);
        Add("SpikeRow", "Tiles", 4, 4, 2, 2, 1f, 1f, Fit.Bottom);
        Add("Strip", "Tiles", 4, 4, 3, 2, 1f, 0.3f, Fit.Fill);
        Add("Saw", "Tiles", 4, 4, 0, 3, 1f, 1f, Fit.Center);
        Add("SawSmall", "Tiles", 4, 4, 1, 3, 1f, 1f, Fit.Center);
        Add("Coin", "Tiles", 4, 4, 2, 3, 1f, 1f, Fit.Center);

        string[] orbs = { "OrbYellow", "OrbPink", "OrbRed", "OrbBlue", "OrbGreen", "OrbBlack", "OrbDash", "OrbGravityDash" };
        for (int i = 0; i < orbs.Length; i++) Add(orbs[i], "Orbs", 4, 4, i % 4, i / 4, 1f, 1f, Fit.Center);
        string[] pads = { "PadYellow", "PadPink", "PadRed", "PadBlue" };
        for (int i = 0; i < pads.Length; i++) Add(pads[i], "Orbs", 4, 4, i, 2, 1f, 0.45f, Fit.Bottom, null, new Vector2(0.5f, 0.095f / 0.45f));

        string[] portals =
        {
            "PortalCube", "PortalShip", "PortalBall", "PortalUfo", "GravityNormal", "GravityInverted", "Speed05x", "Speed1x",
            "Speed2x", "Speed3x", "Speed4x", "Finish",
        };
        for (int i = 0; i < portals.Length; i++) Add(portals[i], "Portals", 8, 1024f / 384f, i % 8, i / 8, 1f, 3f, Fit.Center);
        return items;
    }

    static Dictionary<string, Sprite> BuildItems(Theme theme, StringBuilder log)
    {
        string folder = $"{theme.Folder}/Items";
        Directory.CreateDirectory(folder);
        var sheets = new Dictionary<string, Sheet>();
        int built = 0;
        foreach (var item in Items(theme))
        {
            if (!sheets.TryGetValue(item.sheet, out var sheet))
            {
                if (!File.Exists(item.sheet)) { log.AppendLine($"  missing {item.sheet}"); continue; }
                sheets[item.sheet] = sheet = Sheet.Load(item.sheet);
            }
            var bounds = sheet.ItemBounds(item);
            if (bounds.width <= 0) { log.AppendLine($"  {theme.name}/{item.name}: empty cell"); continue; }
            int w = Mathf.RoundToInt(item.width * ItemPixelsPerUnit), h = Mathf.RoundToInt(item.height * ItemPixelsPerUnit);
            var dest = new RectInt(Mathf.RoundToInt(item.area.x * w), Mathf.RoundToInt(item.area.y * h),
                Mathf.RoundToInt(item.area.width * w), Mathf.RoundToInt(item.area.height * h));
            var pixels = sheet.Render(bounds, w, h, Place(bounds, dest, item.fit));
            if (SolidItems.Contains(item.name)) Seal(pixels, w, h);
            string path = $"{folder}/{item.name}.png";
            WritePng(path, pixels, w, h);
            ImportSprite(path, ItemPixelsPerUnit, item.pivot, SpriteMeshType.FullRect, false);
            built++;
        }
        log.AppendLine($"{theme.name}: {built} item sprites");
        return LoadItemSprites(theme);
    }
    static readonly HashSet<string> SolidItems = new() { "Top", "Fill", "FillAlt", "Pillar", "Panel" };
    public static void Seal(Color32[] pixels, int w, int h)
    {
        var filled = new bool[h];
        for (int y = 0; y < h; y++)
        {
            int left = -1, right = -1;
            for (int x = 0; x < w; x++)
                if (pixels[y * w + x].a >= 200) { if (left < 0) left = x; right = x; }
            if (left < 0) continue;
            filled[y] = true;
            for (int x = 0; x < w; x++)
            {
                ref var p = ref pixels[y * w + x];
                if (x < left) p = pixels[y * w + left];
                else if (x > right) p = pixels[y * w + right];
                p.a = 255;
            }
        }
        for (int y = 0; y < h; y++)
        {
            if (filled[y]) continue;
            int source = -1;
            for (int d = 1; d < h && source < 0; d++)
            {
                if (y - d >= 0 && filled[y - d]) source = y - d;
                else if (y + d < h && filled[y + d]) source = y + d;
            }
            if (source >= 0) System.Array.Copy(pixels, source * w, pixels, y * w, w);
        }
    }

    static Dictionary<string, Sprite> LoadItemSprites(Theme theme)
    {
        string folder = $"{theme.Folder}/Items";
        if (!Directory.Exists(folder)) return new Dictionary<string, Sprite>();
        return Directory.GetFiles(folder, "*.png")
            .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/'))).Where(s => s != null)
            .ToDictionary(s => s.name);
    }

    static RectInt Place(RectInt source, RectInt area, Fit fit)
    {
        if (fit == Fit.Fill) return area;
        float scale = Mathf.Min(area.width / (float)source.width, area.height / (float)source.height);
        int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale)), h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
        int x = area.x + (area.width - w) / 2;
        int y = fit == Fit.Bottom ? area.y : area.y + (area.height - h) / 2;
        return new RectInt(x, y, w, h);
    }

    static Dictionary<string, Tile> BuildTiles(Theme theme, Dictionary<string, Sprite> sprites)
    {
        string folder = $"{ThemeTiles}/{theme.name}";
        Directory.CreateDirectory(folder);
        var tiles = new Dictionary<string, Tile>();
        foreach (string name in new[] { "Top", "Fill", "Slab", "Slope", "Panel", "Thin", "Spike", "SpikeRow", "Strip", "Saw" })
        {
            if (!sprites.TryGetValue(name, out var sprite)) continue;



            string path = $"{folder}/{theme.prefix}_{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            tile.color = Color.white;
            EditorUtility.SetDirty(tile);
            tiles[name] = tile;
        }
        return tiles;
    }

    static Sprite[] BuildBackdrop(Theme theme)
    {
        string folder = $"{theme.Folder}/Backdrop";
        Directory.CreateDirectory(folder);
        var result = new List<Sprite>();
        foreach (string part in new[] { "Sky", "Far", "Near" })
        {
            string source = theme.Sheet(part), path = $"{folder}/{part}.png";
            if (!File.Exists(source)) { result.Add(null); continue; }
            File.Copy(source, path, true);
            AssetDatabase.ImportAsset(path);
            ImportSprite(path, BackdropPixelsPerUnit, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect, false);
            result.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }
        return result.ToArray();
    }

    static CharacterSkin BuildSkin(Theme theme, StringBuilder log)
    {
        string source = theme.Sheet("Character");
        if (!File.Exists(source)) { log.AppendLine($"  {theme.name}: missing character sheet"); return null; }
        var sheet = Sheet.Load(source);
        const int grid = 8;
        int size = FramePixels * grid;
        var output = new Color32[size * size];
        for (int row = 0; row < grid; row++)
            for (int col = 0; col < grid; col++)
            {
                var cell = sheet.Cell(1f / grid, 1f / grid, col, row);
                var pixels = sheet.Render(cell, FramePixels, FramePixels, new RectInt(0, 0, FramePixels, FramePixels));
                int ox = col * FramePixels, oy = (grid - 1 - row) * FramePixels;
                for (int y = 0; y < FramePixels; y++)
                    Array.Copy(pixels, y * FramePixels, output, (oy + y) * size + ox, FramePixels);
            }
        string path = $"{theme.Folder}/Character.png";
        WritePng(path, output, size, size);
        AssetDatabase.ImportAsset(path);

        var rects = new List<SpriteRect>();
        foreach (var (clip, row, col, count) in Animations)
            for (int i = 0; i < count; i++)
            {
                int c = col + i, r = row + c / grid;
                c %= grid;
                rects.Add(new SpriteRect
                {
                    name = $"{clip}_{i}", spriteID = GUID.Generate(), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f),
                    rect = new Rect(c * FramePixels, (grid - 1 - r) * FramePixels, FramePixels, FramePixels),
                });
            }
        Slice(path, rects, FramePixelsPerUnit);
        var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);

        var knight = AssetDatabase.LoadAssetAtPath<CharacterSkin>($"{SkinsFolder}/Skin_Knight.asset");
        var baseOverride = knight.animationOverride;
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        baseOverride.GetOverrides(pairs);
        string clipFolder = $"{AnimationsFolder}/{theme.name}";
        Directory.CreateDirectory(clipFolder);
        var controller = new AnimatorOverrideController(baseOverride.runtimeAnimatorController) { name = theme.name };
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        foreach (var pair in pairs)
        {
            var anim = Animations.FirstOrDefault(a => a.clip == pair.Key.name);
            if (anim.clip == null || pair.Value == null) { overrides.Add(pair); continue; }
            var clip = BuildClip(pair.Value, Enumerable.Range(0, anim.count).Select(i => frames[$"{anim.clip}_{i}"]).ToArray());
            clip.name = $"{theme.name}_{anim.clip}";
            string clipPath = $"{clipFolder}/{clip.name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null) { EditorUtility.CopySerialized(clip, existing); clip = existing; }
            else AssetDatabase.CreateAsset(clip, clipPath);
            overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(pair.Key, clip));
        }
        controller.ApplyOverrides(overrides);
        string controllerPath = $"{clipFolder}/{theme.name}.overrideController";
        var savedController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        if (savedController != null) { EditorUtility.CopySerialized(controller, savedController); controller = savedController; }
        else AssetDatabase.CreateAsset(controller, controllerPath);

        string skinPath = $"{SkinsFolder}/Skin_{theme.name}.asset";
        var skin = AssetDatabase.LoadAssetAtPath<CharacterSkin>(skinPath);
        if (skin == null)
        {
            skin = ScriptableObject.CreateInstance<CharacterSkin>();
            AssetDatabase.CreateAsset(skin, skinPath);
        }
        skin.displayName = theme.displayName;
        skin.sprite = frames["Idle_0"];
        skin.shipSprite = frames["Ship_0"];
        skin.ballSprite = frames["Ball_0"];
        skin.ufoSprite = frames["Ufo_0"];
        skin.animationOverride = controller;
        skin.tint = Color.white;
        skin.trailColor = theme.particleColor;
        skin.particleColor = theme.particleColor;
        skin.scale = knight.scale * theme.characterScale;
        skin.uprightRunner = knight.uprightRunner;
        skin.showTrail = false;
        EditorUtility.SetDirty(skin);
        log.AppendLine($"{theme.name}: {frames.Count} frames, {overrides.Count} clips, skin {skinPath}");
        return skin;
    }

    static AnimationClip BuildClip(AnimationClip template, Sprite[] frames)
    {
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var templateKeys = AnimationUtility.GetObjectReferenceCurveBindings(template)
            .Select(b => AnimationUtility.GetObjectReferenceCurve(template, b)).FirstOrDefault();
        float length = Mathf.Max(template.length, 1f / 60f);
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            float time = templateKeys != null && templateKeys.Length == frames.Length ? templateKeys[i].time : length * i / frames.Length;
            keys[i] = new ObjectReferenceKeyframe { time = time, value = frames[i] };
        }
        var clip = new AnimationClip { frameRate = template.frameRate };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(template);
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        if (templateKeys == null || templateKeys.Length != frames.Length || template.length > keys[^1].time)
        {
            var hold = keys.Append(new ObjectReferenceKeyframe { time = length, value = frames[^1] }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip, binding, hold);
        }
        return clip;
    }

    static void ApplyToScene(Theme theme, Dictionary<string, Sprite> sprites, Dictionary<string, Tile> tiles, Sprite[] backdrop,
        CharacterSkin skin, StringBuilder log)
    {
        string scenePath = $"Assets/Scenes/{theme.scene}.unity";
        if (!File.Exists(scenePath)) { log.AppendLine($"  missing scene {scenePath}"); return; }
        var scene = EditorSceneManager.OpenScene(scenePath);
        var landscape = UnityEngine.Object.FindAnyObjectByType<LevelLandscape>();
        var so = new SerializedObject(landscape);
        void Set(string field, string tile) { if (tiles.TryGetValue(tile, out var t)) so.FindProperty(field).objectReferenceValue = t; }
        Set("grass", "Top");
        Set("earth", "Fill");
        Set("slab", "Slab");
        Set("slope", "Slope");
        Set("spike", "Spike");
        var swaps = so.FindProperty("swaps");
        var valid = Swaps.Where(s => tiles.ContainsKey(s.item) && AssetDatabase.LoadAssetAtPath<TileBase>($"{PlaceholderTiles}/{s.tile}.asset") != null).ToList();
        swaps.arraySize = valid.Count;
        for (int i = 0; i < valid.Count; i++)
        {
            var element = swaps.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("from").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TileBase>($"{PlaceholderTiles}/{valid[i].tile}.asset");
            element.FindPropertyRelative("to").objectReferenceValue = tiles[valid[i].item];
            element.FindPropertyRelative("ignoreScale").boolValue = valid[i].ignoreScale;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        var fill = landscape.Ground.transform.parent.Find("Background_Tilemap");
        var placeholder = AssetDatabase.LoadAssetAtPath<TileBase>($"{PlaceholderTiles}/Tile_Block.asset");
        if (fill != null && tiles.TryGetValue("Fill", out var fillTile)) fill.GetComponent<Tilemap>().SwapTile(placeholder, fillTile);
        landscape.RebuildAppearance();
        landscape.Ground.GetComponent<TilemapRenderer>().enabled = false;
        if (landscape.Hazards != null) landscape.Hazards.GetComponent<TilemapRenderer>().enabled = false;

        int themed = 0;
        var objects = GameObject.Find("Objects")?.transform;
        if (objects != null)
            foreach (Transform child in objects)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                string prefab = source != null ? source.name : child.name;
                var entry = ObjectSprites.FirstOrDefault(o => o.prefab == prefab);
                if (entry.item == null || !sprites.TryGetValue(entry.item, out var sprite)) continue;
                var renderer = child.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer == null) continue;
                renderer.sprite = sprite;
                renderer.color = Color.white;
                if (renderer.TryGetComponent(out SpriteLoop loop)) loop.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                themed++;
            }

        var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.name == "Main Camera");
        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Backdrop");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        if (camera != null && backdrop.Length == 3 && backdrop.All(s => s != null))
        {
            var root = new GameObject("Backdrop");
            SceneManagerMove(root, scene);
            Layer(root.transform, "Sky", camera, 0f, 0f, -30, backdrop[0]);
            Layer(root.transform, "Far", camera, 0.08f, FarOffset, -20, backdrop[1], 0.85f);
            Layer(root.transform, "Near", camera, 0.2f, NearOffset, -10, backdrop[2], 0.6f);
            camera.backgroundColor = SkyColor(backdrop[0]);
        }

        if (skin != null)
        {
            var manager = UnityEngine.Object.FindAnyObjectByType<SkinManager>();
            if (manager != null)
            {
                var ms = new SerializedObject(manager);
                var skins = ms.FindProperty("skins");
                skins.arraySize = 1;
                skins.GetArrayElementAtIndex(0).objectReferenceValue = skin;
                ms.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine($"{theme.name}: {theme.scene} themed ({themed} objects, {valid.Count} tile swaps, backdrop {(camera != null)})");
    }

    static void SceneManagerMove(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }

    static void Layer(Transform parent, string name, Camera camera, float factor, float offsetY, int order, Sprite sprite, float shade = 1f)
    {
        const int panelCount = 3;
        var layer = new GameObject(name, typeof(ForestParallax));
        layer.transform.SetParent(parent, false);
        var panels = new SpriteRenderer[panelCount];
        for (int i = 0; i < panelCount; i++)
        {
            var panel = new GameObject(name + "Panel" + i, typeof(SpriteRenderer));
            panel.transform.SetParent(layer.transform, false);
            var renderer = panel.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = LevelArtSetup.Background;
            renderer.sortingOrder = order;
            renderer.color = new Color(shade, shade, shade, 1f);
            float width = renderer.bounds.size.x;
            panel.transform.position = new Vector3((i - panelCount / 2) * width, offsetY, 0f);
            panels[i] = renderer;
        }
        layer.GetComponent<ForestParallax>().Configure(camera, factor, offsetY, panels);
    }

    static Color SkyColor(Sprite sky)
    {
        var sheet = Sheet.Load(AssetDatabase.GetAssetPath(sky));
        return sheet.Pixel(sheet.width / 2, sheet.height - 4);
    }

    static void WritePng(string path, Color32[] pixels, int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
    }

    static void ImportSprite(string path, float pixelsPerUnit, Vector2 pivot, SpriteMeshType mesh, bool point)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = mesh;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    static void Slice(string path, List<SpriteRect> rects, float pixelsPerUnit)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects().ToDictionary(r => r.name);
        foreach (var rect in rects)
            if (existing.TryGetValue(rect.name, out var old)) rect.spriteID = old.spriteID;
        provider.SetSpriteRects(rects.ToArray());
        var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        ids.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        provider.Apply();
        importer.SaveAndReimport();
    }

    class Sheet
    {
        public int width, height;
        Color32[] pixels;
        int[] labels;
        List<(RectInt bounds, Vector2 centroid, int count)> components;

        public static Sheet Load(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            var sheet = new Sheet { width = texture.width, height = texture.height, pixels = texture.GetPixels32() };
            UnityEngine.Object.DestroyImmediate(texture);
            return sheet;
        }

        public Color Pixel(int x, int y) => pixels[Mathf.Clamp(y, 0, height - 1) * width + Mathf.Clamp(x, 0, width - 1)];

        public RectInt Cell(float cellWidth, float cellHeight, int col, int row)
        {
            int x0 = Mathf.RoundToInt(col * cellWidth * width), x1 = Mathf.RoundToInt((col + 1) * cellWidth * width);
            int top = Mathf.RoundToInt(row * cellHeight * height), bottom = Mathf.RoundToInt((row + 1) * cellHeight * height);
            return new RectInt(x0, height - bottom, x1 - x0, bottom - top);
        }

        public RectInt ItemBounds(Item item)
        {
            if (components == null) Label();
            var cell = Cell(item.cellWidth, item.cellHeight, item.col, item.row);
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            for (int i = 0; i < components.Count; i++)
            {
                var c = components[i];
                if (c.count < MinComponentPixels) continue;
                var bounds = c.bounds;
                bool spans = bounds.width > cell.width * 1.4f || bounds.height > cell.height * 1.4f;
                if (spans) bounds = Clip(i + 1, cell, bounds);
                else if (c.centroid.x < cell.xMin || c.centroid.x >= cell.xMax || c.centroid.y < cell.yMin || c.centroid.y >= cell.yMax) continue;
                if (bounds.width <= 0) continue;
                x0 = Mathf.Min(x0, bounds.xMin); y0 = Mathf.Min(y0, bounds.yMin);
                x1 = Mathf.Max(x1, bounds.xMax); y1 = Mathf.Max(y1, bounds.yMax);
            }
            return x1 < x0 ? new RectInt(0, 0, 0, 0) : new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        RectInt Clip(int id, RectInt cell, RectInt bounds)
        {
            int xa = Mathf.Max(cell.xMin, bounds.xMin), xb = Mathf.Min(cell.xMax, bounds.xMax);
            int ya = Mathf.Max(cell.yMin, bounds.yMin), yb = Mathf.Min(cell.yMax, bounds.yMax);
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            for (int y = ya; y < yb; y++)
                for (int x = xa; x < xb; x++)
                {
                    if (labels[y * width + x] != id) continue;
                    x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x);
                    y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                }
            return x1 < x0 ? new RectInt(0, 0, 0, 0) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        void Label()
        {
            labels = new int[pixels.Length];
            components = new List<(RectInt, Vector2, int)>();
            var stack = new Stack<int>();
            for (int start = 0; start < pixels.Length; start++)
            {
                if (labels[start] != 0 || pixels[start].a < 128) continue;
                int id = components.Count + 1;
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue, count = 0;
                long sx = 0, sy = 0;
                labels[start] = id;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int p = stack.Pop(), x = p % width, y = p / width;
                    count++; sx += x; sy += y;
                    x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                    if (x > 0) Visit(p - 1, id, stack);
                    if (x < width - 1) Visit(p + 1, id, stack);
                    if (y > 0) Visit(p - width, id, stack);
                    if (y < height - 1) Visit(p + width, id, stack);
                }
                components.Add((new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1), new Vector2(sx / (float)count, sy / (float)count), count));
            }
        }

        void Visit(int p, int id, Stack<int> stack)
        {
            if (labels[p] != 0 || pixels[p].a < 128) return;
            labels[p] = id;
            stack.Push(p);
        }

        public Color32[] Render(RectInt source, int width, int height, RectInt dest)
        {
            var output = new Color32[width * height];
            for (int dy = Mathf.Max(0, dest.yMin); dy < Mathf.Min(height, dest.yMax); dy++)
                for (int dx = Mathf.Max(0, dest.xMin); dx < Mathf.Min(width, dest.xMax); dx++)
                {
                    float u0 = (dx - dest.x) / (float)dest.width, u1 = (dx - dest.x + 1) / (float)dest.width;
                    float v0 = (dy - dest.y) / (float)dest.height, v1 = (dy - dest.y + 1) / (float)dest.height;
                    int sx0 = source.x + Mathf.FloorToInt(u0 * source.width), sx1 = Mathf.Max(sx0 + 1, source.x + Mathf.FloorToInt(u1 * source.width));
                    int sy0 = source.y + Mathf.FloorToInt(v0 * source.height), sy1 = Mathf.Max(sy0 + 1, source.y + Mathf.FloorToInt(v1 * source.height));
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    int n = 0;
                    for (int sy = sy0; sy < sy1; sy++)
                        for (int sx = sx0; sx < sx1; sx++)
                        {
                            var c = pixels[Mathf.Clamp(sy, 0, this.height - 1) * this.width + Mathf.Clamp(sx, 0, this.width - 1)];
                            float alpha = c.a / 255f;
                            r += c.r * alpha; g += c.g * alpha; b += c.b * alpha; a += alpha;
                            n++;
                        }
                    if (a <= 0.0001f) continue;
                    float coverage = a / n;
                    if (coverage < 0.04f) continue;
                    output[dy * width + dx] = new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)Mathf.RoundToInt(coverage * 255f));
                }
            return output;
        }
    }
}

