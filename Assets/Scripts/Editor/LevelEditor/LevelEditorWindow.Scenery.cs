using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public partial class LevelEditorWindow
{
    const string SceneryRoot = "Scenery";
    const string AutoDecorationRoot = "Decoration";
    const string BehindLayer = "Behind";
    const string FarLayer = "Far";
    const string SpriteFolder = "Assets/Art/Sprites/";
    const string SourceFolder = SpriteFolder + "Source/";
    const int FarOrder = 100;
    const float FarHaze = 0.45f;
    static readonly Color Haze = new(0.55f, 0.66f, 0.7f, 1f);

    static readonly (string category, string path, float shade, int order)[] ScenerySheets =
    {
        ("Trees · Green", SpriteFolder + "Landscape/Trees.png", 0.8f, 0),
        ("Trees · Golden", SourceFolder + "Trees/Golden-Tree.png", 0.8f, 0),
        ("Trees · Yellow", SourceFolder + "Trees/Yellow-Tree.png", 0.8f, 0),
        ("Trees · Red", SourceFolder + "Trees/Red-Tree.png", 0.8f, 0),
        ("Trees · Dark", SourceFolder + "Trees/Dark-Tree.png", 1f, 0),
        ("Bushes", SourceFolder + "Assets/Tree-Assets.png", 0.82f, 10),
        ("Rocks", SourceFolder + "Assets/Props-Rocks.png", 0.9f, 20),
        ("Plants & props", SourceFolder + "Assets/Tiles.png", 1f, 30),
        ("Plants & props", SourceFolder + "Assets/Buildings.png", 0.9f, 20),
    };

    [SerializeField] bool sceneryFar;

    static bool PlacesObject(PaletteItem item) => item != null && (item.prefab != null || item.scenery != null);

    void LoadScenery()
    {
        foreach (var (category, path, shade, order) in ScenerySheets)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name);
            foreach (var sprite in sprites)
            {
                bool shrub = sprite.name.StartsWith("Shrub");
                palette.Add(new PaletteItem
                {
                    name = sprite.name.Replace('_', ' '),
                    category = shrub ? "Bushes" : category,
                    scenery = sprite,
                    icon = sprite,
                    tint = new Color(shrub ? 0.82f : shade, shrub ? 0.82f : shade, shrub ? 0.82f : shade, 1f),
                    order = shrub ? 10 : order,
                });
            }
        }
    }

    void DrawSceneryOptions()
    {
        if (Current?.scenery == null) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Scenery layer", GUILayout.Width(84f));
            sceneryFar = GUILayout.Toolbar(sceneryFar ? 1 : 0, new[] { "Behind gameplay", "Far background" }, GUILayout.Width(240f)) == 1;
        }
    }

    Vector3 SceneryPosition(Vector3 world)
    {
        float snap = Snap;
        return new Vector3(Mathf.Round(world.x / snap) * snap, Mathf.Round(world.y / snap) * snap, 0f);
    }

    void PlaceScenery(PaletteItem item)
    {
        var parent = SceneryLayer(sceneryFar);
        var go = new GameObject(item.scenery.name, typeof(SpriteRenderer));
        Undo.RegisterCreatedObjectUndo(go, "Place scenery");
        go.transform.SetParent(parent, false);
        go.transform.position = SceneryPosition(hover);
        if (flip) go.transform.localScale = new Vector3(-1f, 1f, 1f);
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = item.scenery;
        renderer.sortingLayerName = sceneryFar ? LevelArtSetup.Background : LevelArtSetup.Midground;
        renderer.sortingOrder = sceneryFar ? FarOrder + item.order : item.order;
        renderer.color = SceneryTint(item);
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    Color SceneryTint(PaletteItem item)
    {
        var tint = item.tint;
        return sceneryFar ? Color.Lerp(tint, Haze, FarHaze) : tint;
    }

    Transform SceneryLayer(bool far)
    {
        var scene = landscape.gameObject.scene;
        var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == SceneryRoot);
        if (root == null)
        {
            root = new GameObject(SceneryRoot);
            SceneManagerMove(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create scenery");
        }
        string name = far ? FarLayer : BehindLayer;
        var layer = root.transform.Find(name);
        if (layer != null) return layer;
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create scenery layer");
        go.transform.SetParent(root.transform, false);
        return go.transform;
    }

    static void SceneManagerMove(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }

    bool IsScenery(GameObject go)
    {
        var layer = go.transform.parent;
        var root = layer != null ? layer.parent : null;
        return root != null && root.parent == null && (root.name == SceneryRoot || root.name == AutoDecorationRoot)
               && go.GetComponent<SpriteRenderer>() != null;
    }

    bool IsEditable(GameObject go) => go != null && (objects != null && go.transform.parent == objects || IsScenery(go));

    GameObject SceneryAt(Vector3 point)
    {
        GameObject best = null;
        float bestArea = float.MaxValue;
        foreach (var root in landscape.gameObject.scene.GetRootGameObjects())
        {
            if (root.name != SceneryRoot && root.name != AutoDecorationRoot) continue;
            foreach (Transform layer in root.transform)
            foreach (Transform child in layer)
            {
                if (!child.TryGetComponent(out SpriteRenderer renderer) || renderer.sprite == null) continue;
                var bounds = renderer.bounds;
                if (point.x < bounds.min.x || point.x > bounds.max.x || point.y < bounds.min.y || point.y > bounds.max.y) continue;
                float area = bounds.size.x * bounds.size.y;
                if (area >= bestArea) continue;
                bestArea = area;
                best = child.gameObject;
            }
        }
        return best;
    }

    void EraseSceneryIn(Rect area)
    {
        foreach (var root in landscape.gameObject.scene.GetRootGameObjects())
        {
            if (root.name != SceneryRoot && root.name != AutoDecorationRoot) continue;
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>().ToList())
                if (IsScenery(renderer.gameObject) && area.Contains(renderer.transform.position))
                    Undo.DestroyObjectImmediate(renderer.gameObject);
        }
    }

    void DrawSceneryCursor(PaletteItem item)
    {
        var position = SceneryPosition(hover);
        var bounds = item.scenery.bounds;
        var center = position + new Vector3(flip ? -bounds.center.x : bounds.center.x, bounds.center.y);
        var tint = SceneryTint(item);
        DrawWorldSprite(item.scenery, center, bounds.size, new Color(tint.r, tint.g, tint.b, 0.7f), 0f, flip);
        Handles.color = new Color(0.3f, 0.85f, 1f);
        Handles.DrawWireDisc(position, Vector3.forward, 0.12f);
    }
}
