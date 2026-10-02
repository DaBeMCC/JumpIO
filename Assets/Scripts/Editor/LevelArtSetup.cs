using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class LevelArtSetup
{
    public const string Background = "Background", Midground = "Midground", Gameplay = "Gameplay", Foreground = "Foreground";

    const string LevelScene = "Assets/Scenes/Level_01.unity";
    const string MenuScene = "Assets/Scenes/MainMenu.unity";
    const string Sprites = "Assets/Art/Sprites/";
    const string Source = Sprites + "Source/";
    const float PixelsPerUnit = 32f;

    [MenuItem("JumpIO/Setup/Level Art")]
    public static void Configure()
    {
        EnsureSortingLayers();
        SliceScenery();
        ConfigurePrefabs();
        ConfigureSkins();
        var scene = EditorSceneManager.OpenScene(LevelScene);
        string report = ConfigureLevel();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var menu = EditorSceneManager.OpenScene(MenuScene);
        ConfigureMenu();
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        AssetDatabase.SaveAssets();
        Debug.Log("JUMPIO_BLOCK_5_OK " + report);
    }

    static void ConfigureMenu()
    {
        LightAllLayers();
        var canvas = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
            .First(g => g.name == "Canvas").transform;
        var forest = LoadSprites(Sprites + "Landscape/Forest.png");
        var old = canvas.Find("ForestScenery");
        int index = old != null ? old.GetSiblingIndex() : 1;
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        foreach (string name in new[] { "FarForest", "NearForest" })
        {
            var existing = canvas.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }
        TiledStrip(canvas, "FarForest", forest["ForestLight"], 1f, index);
        TiledStrip(canvas, "NearForest", forest["ForestDark"], 1.35f, index + 1);
        var shade = canvas.Find("BackgroundShade").GetComponent<UnityEngine.UI.Image>();
        shade.color = new Color(0.015f, 0.04f, 0.06f, 0.35f);
    }

    static void TiledStrip(Transform canvas, string name, Sprite sprite, float pixelsPerUnitMultiplier, int siblingIndex)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(canvas, false);
        go.transform.SetSiblingIndex(siblingIndex);
        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.type = UnityEngine.UI.Image.Type.Tiled;
        image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        image.raycastTarget = false;
        var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        float tileHeight = sprite.rect.height * scaler.referencePixelsPerUnit / sprite.pixelsPerUnit / pixelsPerUnitMultiplier;
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, tileHeight);
    }

    static void EnsureSortingLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("m_SortingLayers");
        string[] order = { Background, Midground, "Default", Gameplay, Foreground };
        var random = new System.Random(2026);
        foreach (string name in order)
        {
            if (IndexOf(layers, name) >= 0) continue;
            layers.InsertArrayElementAtIndex(layers.arraySize);
            var entry = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            entry.FindPropertyRelative("name").stringValue = name;
            entry.FindPropertyRelative("uniqueID").longValue = random.Next(1, int.MaxValue);
            var locked = entry.FindPropertyRelative("locked");
            if (locked.propertyType == SerializedPropertyType.Boolean) locked.boolValue = false;
            else locked.intValue = 0;
        }
        for (int i = 0; i < order.Length; i++) layers.MoveArrayElement(IndexOf(layers, order[i]), i);
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    static int IndexOf(SerializedProperty layers, string name)
    {
        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) return i;
        return -1;
    }

    struct Slice
    {
        public string name;
        public RectInt hint;
        public Vector2 pivot;
    }

    static Slice S(string name, int x, int yTop, int width, int height, float pivotX = 0.5f, float pivotY = 0f) =>
        new Slice { name = name, hint = new RectInt(x, yTop, width, height), pivot = new Vector2(pivotX, pivotY) };

    static readonly string[] TreeSheets =
    {
        Sprites + "Landscape/Trees.png", Source + "Trees/Golden-Tree.png", Source + "Trees/Yellow-Tree.png",
        Source + "Trees/Red-Tree.png", Source + "Trees/Dark-Tree.png",
    };

    static void SliceScenery()
    {
        SetSlices(Sprites + "Landscape/Forest.png", new[]
        {
            S("ForestDark", 112, 0, 96, 256, 0.5f, 0.5f),
            S("ForestLight", 464, 0, 96, 256, 0.5f, 0.5f),
        }, keepExisting: true, fullRect: true);

        var trees = new[]
        {
            S("Tree_L", 0, 0, 112, 370), S("Tree_M", 0, 388, 112, 320), S("Tree_S", 0, 716, 94, 214),
            S("Tree_XS", 0, 940, 94, 150), S("Tree_XXS", 0, 1088, 78, 112),
            S("Dead_M", 362, 388, 62, 320), S("Dead_S", 305, 716, 62, 214),
        };
        foreach (string sheet in TreeSheets) SetSlices(sheet, trees, keepExisting: sheet.Contains("Landscape"));

        SetSlices(Source + "Assets/Props-Rocks.png", new[]
        {
            S("Rock_Tall", 5, 0, 58, 80), S("Rock_Wide", 65, 32, 63, 48), S("Rock_Mid", 132, 30, 44, 50),
            S("Rock_Pebble", 180, 47, 25, 33), S("Rock_Round", 192, 17, 31, 30), S("Rock_Big", 225, 4, 48, 44),
            S("Rock_Flat", 212, 50, 66, 30),
            S("Moss_Tall", 5, 80, 58, 80), S("Moss_Wide", 65, 110, 63, 50), S("Moss_Mid", 132, 110, 44, 50),
            S("Moss_Big", 225, 82, 48, 46), S("Moss_Flat", 212, 130, 66, 30),
            S("Stone_A", 6, 164, 56, 88), S("Stone_B", 68, 174, 44, 79), S("Stone_C", 114, 190, 44, 63),
            S("Stone_Small", 122, 158, 30, 33), S("Stone_Broken", 162, 210, 58, 43), S("Boulder", 160, 163, 48, 44),
        });
        SetSlices(Source + "Assets/Tree-Assets.png", new[]
        {
            S("Bush_Green", 210, 5, 124, 86), S("Bush_Olive", 210, 101, 124, 86),
            S("Bush_Dark", 210, 197, 124, 86), S("Bush_Night", 210, 293, 124, 86),
        });
        SetSlices(Source + "Assets/Tiles.png", new[]
        {
            S("Shrub_1", 274, 5, 124, 43), S("Shrub_2", 274, 53, 124, 43), S("Shrub_3", 274, 101, 124, 43),
            S("Shrub_4", 274, 149, 124, 43),
            S("Mushroom_Big", 256, 237, 32, 38), S("Mushroom_Wide", 289, 238, 31, 37),
            S("Mushroom_Small", 320, 249, 17, 25), S("Mushroom_Tiny", 240, 240, 15, 17),
            S("Cattail", 256, 292, 17, 28), S("Cattail_Tall", 256, 325, 17, 26), S("Fern", 319, 357, 35, 28),
            S("Hive_Big", 190, 100, 38, 45, 0.5f, 1f), S("Hive_Small", 190, 40, 20, 24, 0.5f, 1f),
            S("Branch", 162, 168, 79, 20),
        });
        SetSlices(Source + "Assets/Buildings.png", new[]
        {
            S("LogPile", 14, 156, 52, 24), S("Mound_1", 30, 205, 82, 36), S("Mound_2", 111, 205, 81, 36),
            S("Mound_3", 191, 205, 84, 36), S("Fence", 161, 173, 46, 36), S("MineEntrance", 270, 206, 130, 164),
        });
    }

    static void SetSlices(string path, IEnumerable<Slice> slices, bool keepExisting = false, bool fullRect = false)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("Missing texture " + path);
        var image = new Texture2D(2, 2);
        image.LoadImage(System.IO.File.ReadAllBytes(path));
        var pixels = image.GetPixels32();
        int width = image.width, height = image.height;
        UnityEngine.Object.DestroyImmediate(image);

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("No sprite data provider for " + path);
        provider.InitSpriteEditorDataProvider();
        var rects = keepExisting ? provider.GetSpriteRects().ToList() : new List<SpriteRect>();
        foreach (var slice in slices)
        {
            var rect = rects.FirstOrDefault(r => r.name == slice.name);
            if (rect == null)
            {
                rect = new SpriteRect { name = slice.name, spriteID = GUID.Generate() };
                rects.Add(rect);
            }
            rect.rect = Tight(pixels, width, height, slice.hint);
            rect.alignment = SpriteAlignment.Custom;
            rect.pivot = slice.pivot;
        }
        provider.SetSpriteRects(rects.ToArray());
        var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        var pairs = keepExisting ? ids.GetNameFileIdPairs().ToList() : new List<SpriteNameFileIdPair>();
        foreach (var rect in rects)
            if (!pairs.Any(p => p.name == rect.name)) pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
        ids.SetNameFileIdPairs(pairs);
        provider.Apply();

        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        if (fullRect)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
        importer.SaveAndReimport();
    }

    static Rect Tight(Color32[] pixels, int width, int height, RectInt hint)
    {
        int yMin = height - (hint.y + hint.height), yMax = height - hint.y - 1;
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
        for (int y = Mathf.Max(0, yMin); y <= Mathf.Min(height - 1, yMax); y++)
            for (int x = Mathf.Max(0, hint.x); x < Mathf.Min(width, hint.x + hint.width); x++)
            {
                if (pixels[y * width + x].a < 10) continue;
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x);
                y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
            }
        if (x1 < 0) throw new InvalidOperationException($"Empty slice hint {hint}");
        return new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    static Dictionary<string, Sprite> LoadSprites(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);

    static void ConfigurePrefabs()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                string layer = renderer is ParticleSystemRenderer || renderer.GetComponent<TMPro.TextMeshPro>() != null
                    ? Foreground : Gameplay;
                if (renderer.sortingLayerName == layer) continue;
                renderer.sortingLayerName = layer;
                changed = true;
            }
            changed |= OutlineHud(root);
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool OutlineHud(GameObject root)
    {
        var hud = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "HUD");
        if (hud == null) return false;
        var outline = HudMaterial();
        bool changed = false;
        foreach (var text in hud.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (text.fontSharedMaterial == outline) continue;
            text.fontSharedMaterial = outline;
            EditorUtility.SetDirty(text);
            changed = true;
        }
        return changed;
    }

    static Material HudMaterial()
    {
        const string path = "Assets/Art/HUDText.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            material = new Material(font.material) { name = "HUDText" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.EnableKeyword("OUTLINE_ON");
        material.EnableKeyword("UNDERLAY_ON");
        material.SetFloat("_FaceDilate", 0.15f);
        material.SetFloat("_OutlineWidth", 0.28f);
        material.SetColor("_OutlineColor", new Color(0.05f, 0.08f, 0.1f, 1f));
        material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
        material.SetFloat("_UnderlayOffsetX", 0.6f);
        material.SetFloat("_UnderlayOffsetY", -0.6f);
        material.SetFloat("_UnderlaySoftness", 0.3f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ConfigureSkins()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterSkin"))
        {
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkin>(AssetDatabase.GUIDToAssetPath(guid));
            skin.showTrail = !skin.uprightRunner;
            EditorUtility.SetDirty(skin);
        }
    }

    static string ConfigureLevel()
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().ToDictionary(g => g.name);
        var grid = roots["Grid"];
        var camera = roots["Main Camera"].GetComponent<Camera>();
        var landscape = grid.GetComponentInChildren<LevelLandscape>(true);
        landscape.RebuildAppearance();

        var ground = landscape.Ground;
        var hazards = landscape.Hazards;
        var earthTile = landscape.Earth;
        var fill = grid.transform.Find("Background_Tilemap").GetComponent<Tilemap>();
        if (fill.GetComponent<Collider2D>() != null) throw new InvalidOperationException("Background_Tilemap has a collider.");
        var placeholder = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Art/Palettes/Tiles/Tile_Block.asset");
        fill.SwapTile(placeholder, earthTile);

        foreach (string name in new[] { "TerrainVisuals", "HazardVisuals", "Background_Tilemap" })
            grid.transform.Find(name).GetComponent<Renderer>().sortingLayerName = Gameplay;
        grid.transform.Find("Background_Tilemap").GetComponent<Renderer>().sortingOrder = 0;
        foreach (var root in roots.Values.Where(r => r != grid && r.name != "Decoration" && r.name != "Backdrop" && r.name != "ForestDetails"))
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sortingLayerName = renderer is ParticleSystemRenderer || renderer.GetComponent<TMPro.TextMeshPro>() != null
                    ? Foreground : Gameplay;

        ConfigureCorridor(roots["Effects"]);
        foreach (var root in roots.Values) OutlineHud(root);

        var oldBackdrop = grid.transform.Find("ForestBackdrop");
        if (oldBackdrop != null) UnityEngine.Object.DestroyImmediate(oldBackdrop.gameObject);
        if (roots.TryGetValue("ForestDetails", out var oldDetails)) UnityEngine.Object.DestroyImmediate(oldDetails);
        if (roots.TryGetValue("Backdrop", out var backdrop)) UnityEngine.Object.DestroyImmediate(backdrop);
        if (roots.TryGetValue("Decoration", out var decoration)) UnityEngine.Object.DestroyImmediate(decoration);

        LightAllLayers();
        BuildBackdrop(camera);
        int placed = Decorate(ground, hazards, fill, roots);
        return $"layers={string.Join(",", SortingLayer.layers.Select(l => l.name))} decoration={placed}";
    }

    public static void LightAllLayers()
    {
        foreach (var light in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>())
        {
            var serialized = new SerializedObject(light);
            var targets = serialized.FindProperty("m_ApplyToSortingLayers");
            targets.arraySize = SortingLayer.layers.Length;
            for (int i = 0; i < SortingLayer.layers.Length; i++)
                targets.GetArrayElementAtIndex(i).intValue = SortingLayer.layers[i].id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void ConfigureCorridor(GameObject effects)
    {
        var view = effects.GetComponentInChildren<CorridorView>(true);
        var serialized = new SerializedObject(view);
        serialized.FindProperty("thickness").floatValue = 0.12f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        foreach (var bar in view.GetComponentsInChildren<SpriteRenderer>(true))
        {
            bar.color = new Color(1f, 0.86f, 0.45f, 0.55f);
            bar.sortingLayerName = Gameplay;
            bar.sortingOrder = 4;
        }
    }

    static void BuildBackdrop(Camera camera)
    {
        var backdrop = new GameObject("Backdrop");
        var forest = LoadSprites(Sprites + "Landscape/Forest.png");
        var sky = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "Interface/MenuBackground.png");

        float skyScale = 7.5f;
        float skyHalf = sky.bounds.size.y * skyScale / 2f;
        Layer(backdrop.transform, "Sky", camera, 0f, camera.orthographicSize + 1f - skyHalf, -30, 1,
            sky, skyScale, SpriteDrawMode.Simple, sky.bounds.size.x);
        Layer(backdrop.transform, "FarForest", camera, 0.08f, Top(camera, 0.78f) - 8f, -20, 3,
            forest["ForestLight"], 2f, SpriteDrawMode.Tiled, 21f);
        Layer(backdrop.transform, "NearForest", camera, 0.2f, Top(camera, 0.58f) - 8f, -10, 3,
            forest["ForestDark"], 2f, SpriteDrawMode.Tiled, 21f);
        camera.backgroundColor = new Color(0.55f, 0.87f, 0.88f);
    }

    static float Top(Camera camera, float viewportY) => camera.orthographicSize * (2f * viewportY - 1f);

    static void Layer(Transform parent, string name, Camera camera, float factor, float offsetY, int order,
        int panelCount, Sprite sprite, float scale, SpriteDrawMode mode, float localWidth)
    {
        var layer = new GameObject(name, typeof(ForestParallax));
        layer.transform.SetParent(parent, false);
        var panels = new SpriteRenderer[panelCount];
        for (int i = 0; i < panelCount; i++)
        {
            var panel = new GameObject(name + "Panel" + i, typeof(SpriteRenderer));
            panel.transform.SetParent(layer.transform, false);
            panel.transform.localScale = Vector3.one * scale;
            var renderer = panel.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = mode;
            if (mode == SpriteDrawMode.Tiled)
            {
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(localWidth, sprite.bounds.size.y);
            }
            renderer.sortingLayerName = Background;
            renderer.sortingOrder = order;
            float width = renderer.bounds.size.x;
            panel.transform.position = new Vector3((i - panelCount / 2) * width, offsetY, 0f);
            panels[i] = renderer;
        }
        layer.GetComponent<ForestParallax>().Configure(camera, factor, offsetY, panels);
    }

    enum Kind { Tree, Bush, Rock, Plant }

    struct Prop
    {
        public Sprite sprite;
        public Kind kind;
        public float weight;
    }

    class Section
    {
        public float start;
        public List<Prop> props = new List<Prop>();
    }

    static int Decorate(Tilemap ground, Tilemap hazards, Tilemap fill, Dictionary<string, GameObject> roots)
    {
        var root = new GameObject("Decoration");
        var midground = new GameObject("Midground").transform;
        var foreground = new GameObject("Foreground").transform;
        midground.SetParent(root.transform, false);
        foreground.SetParent(root.transform, false);

        var obstacles = Obstacles(hazards, roots);
        var sections = Sections();
        var random = new System.Random(1337);
        var taken = new List<Rect>();
        var trees = new List<Vector2>();
        int count = 0;

        if (roots.TryGetValue("Enemies", out var enemies))
            foreach (Transform enemy in enemies.transform)
            {
                if (!enemy.name.StartsWith("Bee")) continue;
                float x = enemy.position.x + 2.5f;
                var floors = Floors(ground, hazards, Mathf.FloorToInt(x)).Where(f => f.top <= enemy.position.y).ToList();
                if (floors.Count == 0) continue;
                float top = floors.Max(f => f.top);
                var tree = LoadSprites(Source + "Trees/Dark-Tree.png")["Tree_S"];
                Place(midground, tree, new Vector2(x, top), 0, TreeTint(tree), ref count);
                trees.Add(new Vector2(x, top));
                var hive = LoadSprites(Source + "Assets/Tiles.png")["Hive_Big"];
                Place(midground, hive, new Vector2(x - 1.1f, enemy.position.y + 2.4f), 40, Color.white, ref count);
            }
        var finish = roots.Values.SelectMany(r => r.GetComponentsInChildren<FinishLine>(true)).FirstOrDefault();
        float end = finish != null ? finish.transform.position.x + 7f : ground.cellBounds.xMax - 8f;
        var mine = LoadSprites(Source + "Assets/Buildings.png")["MineEntrance"];
        var endFloors = Floors(ground, hazards, Mathf.FloorToInt(end));
        if (endFloors.Count > 0)
        {
            float mineTop = endFloors.Min(f => f.top);
            if (Fits(ground, hazards, obstacles, taken, mine, end, mineTop, 0.5f))
                Place(midground, mine, new Vector2(end, mineTop), 3, Tint(0.9f), ref count, taken);
        }

        for (float x = ground.cellBounds.xMin + 2f; x < ground.cellBounds.xMax - 2f; x += Range(random, 1.2f, 2.6f))
        {
            var section = sections.Last(s => s.start <= x);
            foreach (var floor in Floors(ground, hazards, Mathf.FloorToInt(x)))
            {
                if (floor.headroom < 3 || (floor.top < 5f && x < 300f)) continue;
                var prop = Pick(section.props, random);
                if (prop.kind == Kind.Tree)
                {
                    if (trees.Any(t => Mathf.Approximately(t.y, floor.top) && Mathf.Abs(t.x - x) < 3.2f)) continue;
                    var fitting = section.props
                        .Where(p => p.kind == Kind.Tree && p.sprite.bounds.size.y <= floor.headroom - 0.3f).ToList();
                    if (fitting.Count == 0) continue;
                    var tree = fitting[random.Next(fitting.Count)].sprite;
                    if (!Fits(ground, hazards, obstacles, null, tree, x, floor.top, 0.6f, trunkOnly: true)) continue;
                    Place(midground, tree, new Vector2(x, floor.top), random.Next(0, 3), TreeTint(tree), ref count);
                    trees.Add(new Vector2(x, floor.top));
                    continue;
                }
                if (!Fits(ground, hazards, obstacles, taken, prop.sprite, x, floor.top, 1.1f)) continue;
                int order = prop.kind == Kind.Bush ? 10 : prop.kind == Kind.Rock ? 20 : 30;
                float shade = prop.kind == Kind.Bush ? 0.82f : prop.kind == Kind.Rock ? 0.9f : 1f;
                Place(midground, prop.sprite, new Vector2(x, floor.top - 0.06f), order, Tint(shade), ref count, taken);
            }
        }

        var stones = LoadSprites(Source + "Assets/Props-Rocks.png");
        Sprite[] embedded = { stones["Rock_Round"], stones["Rock_Flat"], stones["Moss_Flat"], stones["Rock_Pebble"] };
        for (float x = ground.cellBounds.xMin + 3f; x < ground.cellBounds.xMax - 3f; x += Range(random, 3f, 6f))
            foreach (var floor in Floors(ground, hazards, Mathf.FloorToInt(x)))
            {
                var sprite = embedded[random.Next(embedded.Length)];
                if (sprite.bounds.size.y > 1.05f || !Solid(ground, fill, x, floor.top, sprite.bounds.size.x)) continue;
                Place(foreground, sprite, new Vector2(x, floor.top - 1.3f), 0, Tint(0.8f), ref count, layer: Foreground);
            }
        return count;
    }

    static List<Section> Sections()
    {
        var green = LoadSprites(Sprites + "Landscape/Trees.png");
        var golden = LoadSprites(Source + "Trees/Golden-Tree.png");
        var yellow = LoadSprites(Source + "Trees/Yellow-Tree.png");
        var red = LoadSprites(Source + "Trees/Red-Tree.png");
        var dark = LoadSprites(Source + "Trees/Dark-Tree.png");
        var rocks = LoadSprites(Source + "Assets/Props-Rocks.png");
        var bushes = LoadSprites(Source + "Assets/Tree-Assets.png");
        var tiles = LoadSprites(Source + "Assets/Tiles.png");
        var buildings = LoadSprites(Source + "Assets/Buildings.png");

        Section New(float start) => new Section { start = start };
        void Add(Section s, Kind kind, float weight, params Sprite[] sprites)
        {
            foreach (var sprite in sprites) s.props.Add(new Prop { sprite = sprite, kind = kind, weight = weight / sprites.Length });
        }

        var spring = New(float.NegativeInfinity);
        Add(spring, Kind.Tree, 4f, green["Tree_S"], green["Tree_XS"], green["Tree_XXS"], green["Tree_M"]);
        Add(spring, Kind.Bush, 2f, bushes["Bush_Green"], tiles["Shrub_1"]);
        Add(spring, Kind.Rock, 2f, rocks["Rock_Tall"], rocks["Rock_Wide"], rocks["Moss_Big"], rocks["Rock_Round"]);
        Add(spring, Kind.Plant, 1.5f, tiles["Mushroom_Small"], tiles["Fern"], tiles["Mushroom_Tiny"]);

        var golden1 = New(85f);
        Add(golden1, Kind.Tree, 4f, golden["Tree_S"], golden["Tree_XS"], yellow["Tree_S"], yellow["Tree_XXS"], golden["Tree_M"]);
        Add(golden1, Kind.Bush, 2f, bushes["Bush_Olive"], tiles["Shrub_2"]);
        Add(golden1, Kind.Rock, 2f, rocks["Moss_Tall"], rocks["Moss_Wide"], rocks["Moss_Mid"]);
        Add(golden1, Kind.Plant, 1.5f, tiles["Mushroom_Big"], tiles["Mushroom_Wide"], tiles["Cattail"]);

        var red1 = New(170f);
        Add(red1, Kind.Tree, 4f, red["Tree_S"], red["Tree_XS"], golden["Tree_XXS"], red["Dead_S"], red["Tree_M"]);
        Add(red1, Kind.Bush, 2f, bushes["Bush_Dark"], tiles["Shrub_3"]);
        Add(red1, Kind.Rock, 2f, rocks["Moss_Flat"], rocks["Rock_Flat"], buildings["LogPile"], tiles["Branch"]);
        Add(red1, Kind.Plant, 1.5f, tiles["Mushroom_Tiny"], tiles["Cattail_Tall"], tiles["Fern"]);

        var dark1 = New(255f);
        Add(dark1, Kind.Tree, 4f, dark["Tree_S"], dark["Tree_XS"], dark["Dead_M"], dark["Tree_XXS"]);
        Add(dark1, Kind.Bush, 1.5f, bushes["Bush_Night"], tiles["Shrub_4"]);
        Add(dark1, Kind.Rock, 3f, rocks["Stone_A"], rocks["Stone_B"], rocks["Stone_C"], rocks["Stone_Broken"], rocks["Boulder"],
            buildings["Mound_1"], buildings["Mound_2"], buildings["Fence"]);
        Add(dark1, Kind.Plant, 1f, tiles["Mushroom_Small"], tiles["Cattail"]);
        return new List<Section> { spring, golden1, red1, dark1 };
    }

    static Prop Pick(List<Prop> props, System.Random random)
    {
        double roll = random.NextDouble() * props.Sum(p => p.weight);
        foreach (var prop in props)
        {
            roll -= prop.weight;
            if (roll <= 0) return prop;
        }
        return props[props.Count - 1];
    }

    static List<Rect> Obstacles(Tilemap hazards, Dictionary<string, GameObject> roots)
    {
        var rects = new List<Rect>();
        foreach (var cell in hazards.cellBounds.allPositionsWithin)
            if (hazards.HasTile(cell))
            {
                Vector3 corner = hazards.CellToWorld(cell);
                rects.Add(new Rect(corner.x, corner.y, 1f, 1f));
            }
        int groundLayer = LayerMask.NameToLayer("Ground");
        foreach (string name in new[] { "Objects", "Enemies" })
            if (roots.TryGetValue(name, out var root))
                foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
                    if (collider.gameObject.layer != groundLayer)
                    {
                        var b = collider.bounds;
                        rects.Add(new Rect(b.min.x, b.min.y, b.size.x, b.size.y));
                    }
        return rects;
    }

    struct Floor
    {
        public float top;
        public int headroom;
    }

    static List<Floor> Floors(Tilemap ground, Tilemap hazards, int column)
    {
        var floors = new List<Floor>();
        var bounds = ground.cellBounds;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            var cell = new Vector3Int(column, y, 0);
            var tile = ground.GetTile(cell);
            if (tile == null || tile.name != "Tile_Block") continue;
            int free = 0;
            while (free < 12 && !ground.HasTile(cell + Vector3Int.up * (free + 1))
                   && !hazards.HasTile(cell + Vector3Int.up * (free + 1)))
                free++;
            if (free == 0) continue;
            floors.Add(new Floor { top = ground.CellToWorld(cell).y + 1f, headroom = free });
        }
        return floors;
    }

    static bool HasFloor(Tilemap ground, Tilemap hazards, int column, float top, float height) =>
        Floors(ground, hazards, column).Any(f => Mathf.Approximately(f.top, top) && f.headroom >= height);

    static bool Fits(Tilemap ground, Tilemap hazards, List<Rect> obstacles, List<Rect> taken, Sprite sprite,
        float x, float top, float clearance, bool trunkOnly = false)
    {
        Vector2 size = sprite.bounds.size;
        float half = trunkOnly ? 0.45f : size.x * 0.4f;
        for (int column = Mathf.FloorToInt(x - half); column <= Mathf.FloorToInt(x + half); column++)
            if (!HasFloor(ground, hazards, column, top, Mathf.Min(size.y, 11f))) return false;
        float height = trunkOnly ? Mathf.Min(size.y, 2.5f) : size.y;
        var area = trunkOnly
            ? new Rect(x - 0.6f - clearance, top - 0.5f, 1.2f + 2f * clearance, height + clearance)
            : new Rect(x - size.x / 2f - clearance, top - 0.5f, size.x + 2f * clearance, height + 0.5f + clearance);
        if (obstacles.Any(o => o.Overlaps(area))) return false;
        if (taken != null)
        {
            var own = new Rect(x - size.x / 2f, top, size.x, size.y);
            if (taken.Any(t => t.Overlaps(own))) return false;
        }
        return true;
    }

    static bool Solid(Tilemap ground, Tilemap fill, float x, float top, float width)
    {
        int row = Mathf.FloorToInt(top) - 1;
        for (int column = Mathf.FloorToInt(x - width / 2f); column <= Mathf.FloorToInt(x + width / 2f); column++)
            for (int y = row - 2; y <= row; y++)
            {
                var cell = new Vector3Int(column, y, 0);
                if (!ground.HasTile(cell) && !fill.HasTile(cell)) return false;
            }
        return true;
    }

    static void Place(Transform parent, Sprite sprite, Vector2 position, int order, Color tint, ref int count,
        List<Rect> taken = null, string layer = Midground)
    {
        var go = new GameObject(sprite.name, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = layer;
        renderer.sortingOrder = order;
        renderer.color = tint;
        taken?.Add(new Rect(position.x - sprite.bounds.size.x / 2f, position.y, sprite.bounds.size.x, sprite.bounds.size.y));
        count++;
    }

    static Color Tint(float shade) => new Color(shade, shade, shade, 1f);

    static Color TreeTint(Sprite tree) => Tint(tree.texture.name.StartsWith("Dark") ? 1f : 0.8f);

    static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
}
