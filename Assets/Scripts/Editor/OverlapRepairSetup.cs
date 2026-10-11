using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class OverlapRepairSetup
{
    public const string OverflowTag = "_Overflow";
    const string Source = "LevelSource/CCLocalLevels_2026-10-10.dat";

    [MenuItem("JumpIO/Setup/Restore Overlapped Pieces (Seven Seas)")]
    public static void RepairSevenSeas()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level_02.unity");
        var level = GDLevelParser.ReadFile(Source).FirstOrDefault(l => l.name == "The Seven Seas  2");
        var landscape = Object.FindAnyObjectByType<LevelLandscape>();
        if (level == null || landscape == null) { Debug.LogWarning("Overlap repair: missing level or landscape"); return; }
        string report = Repair(landscape, GDLevelParser.ParseObjects(GDLevelParser.PlainLevelString(level), out _));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Overlap repair\n" + report);
    }

    // El importador guarda una pieza por celda: en las esquinas la viga lateral pisaba a la superior y quedaban huecos
    public static string Repair(LevelLandscape landscape, List<GDLevelParser.GDObject> objects)
    {
        var map = AssetDatabase.LoadAssetAtPath<GDObjectMap>("Assets/Data/GDObjectMap.asset");
        var offsets = GDLevelImporter.MoveOffsets(objects, float.PositiveInfinity);
        var ground = new Dictionary<Vector3Int, List<(TileBase tile, Matrix4x4 matrix)>>();
        var hazards = new Dictionary<Vector3Int, List<(TileBase tile, Matrix4x4 matrix)>>();
        foreach (var o in objects)
        {
            var entry = map.Find(o.id);
            if (entry == null || entry.category is GDObjectMap.Category.Ignored or GDObjectMap.Category.Object) continue;
            var p = new Vector2(o.x, o.y) / GDLevelParser.UnitsPerBlock + GDLevelImporter.GroupOffset(o, offsets);
            var cell = new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), 0);
            var matrix = Matrix4x4.Translate(new Vector3(p.x - cell.x - 0.5f, p.y - cell.y - 0.5f, 0f))
                * GDLevelImporter.TileMatrix(o.rotation, o.flipX, o.flipY, entry.scale * o.scale, !entry.centered);
            var target = entry.category == GDObjectMap.Category.Solid ? ground : hazards;
            if (!target.TryGetValue(cell, out var list)) target[cell] = list = new();
            list.Add((entry.tile, matrix));
        }
        var log = new StringBuilder();
        log.AppendLine(Fill(landscape, landscape.Ground, ground, false));
        log.AppendLine(Fill(landscape, landscape.Hazards, hazards, true));
        return log.ToString();
    }

    static string Fill(LevelLandscape landscape, Tilemap main, Dictionary<Vector3Int, List<(TileBase tile, Matrix4x4 matrix)>> cells, bool hazard)
    {
        var layers = new List<Tilemap>();
        foreach (Transform child in main.transform.parent)
            if (child.name.StartsWith(main.name + OverflowTag) && !child.name.EndsWith("Visuals")) child.GetComponent<Tilemap>().ClearAllTiles();
        int restored = 0, skipped = 0;
        foreach (var (cell, pieces) in cells)
        {
            if (pieces.Count < 2) continue;
            // La última pieza es la que quedó en el tilemap; si no coincide, la celda se editó a mano y no se toca
            var last = pieces[^1];
            if (main.GetTile(cell) != last.tile || !Same(main.GetTransformMatrix(cell), last.matrix)) { skipped++; continue; }
            for (int i = 0; i < pieces.Count - 1; i++)
            {
                if (pieces.Take(i).Concat(new[] { last }).Any(p => p.tile == pieces[i].tile && Same(p.matrix, pieces[i].matrix))) continue;
                while (layers.Count <= i) layers.Add(Layer(main, layers.Count + 1));
                var layer = layers[i];
                layer.SetTile(cell, pieces[i].tile);
                layer.SetTransformMatrix(cell, pieces[i].matrix);
                restored++;
            }
        }
        var visuals = main.transform.parent.Find((hazard ? LevelAppearanceSetup.HazardVisualsName : LevelAppearanceSetup.TerrainVisualsName)).GetComponent<TilemapRenderer>();
        foreach (var layer in layers)
        {
            landscape.RefreshOverflow(layer, hazard);
            var renderer = main.transform.parent.Find(layer.name + "Visuals").GetComponent<TilemapRenderer>();
            renderer.sortingLayerID = visuals.sortingLayerID;
            renderer.sortingOrder = visuals.sortingOrder;
        }
        return $"{main.name}: {restored} pieces restored in {layers.Count} layers, {skipped} cells changed by hand";
    }

    static bool Same(Matrix4x4 a, Matrix4x4 b)
    {
        for (int i = 0; i < 16; i++)
            if (Mathf.Abs(a[i] - b[i]) > 0.001f) return false;
        return true;
    }

    // Copia de las capas, el cuerpo y los colliders del tilemap original para que el jugador lo trate igual
    static Tilemap Layer(Tilemap main, int index)
    {
        string name = main.name + OverflowTag + index;
        var existing = main.transform.parent.Find(name);
        if (existing != null) return existing.GetComponent<Tilemap>();
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.layer = main.gameObject.layer;
        go.transform.SetParent(main.transform.parent, false);
        go.transform.localPosition = main.transform.localPosition;
        var map = go.GetComponent<Tilemap>();
        map.tileAnchor = main.tileAnchor;
        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        var sourceCollider = main.GetComponent<TilemapCollider2D>();
        var tiles = go.AddComponent<TilemapCollider2D>();
        tiles.isTrigger = sourceCollider.isTrigger;
        tiles.compositeOperation = Collider2D.CompositeOperation.Merge;
        var composite = go.AddComponent<CompositeCollider2D>();
        composite.isTrigger = sourceCollider.isTrigger;
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        go.GetComponent<TilemapRenderer>().enabled = false;
        return map;
    }
}
