using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class DecoBlockSetup
{
    public const string DecoBlocksName = "DecoBlocks";
    const string Source = "LevelSource/CCLocalLevels_2026-10-10.dat";

    // En GD el interior de las estructuras son cuadros decorativos sin colisión; sin ellos solo se veían los contornos
    static readonly HashSet<int> SquareIds = new() { 869, 870, 871, 872, 1266, 1267 };

    [MenuItem("JumpIO/Setup/Deco Blocks (Seven Seas)")]
    public static void BuildSevenSeas()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level_02.unity");
        var level = GDLevelParser.ReadFile(Source).FirstOrDefault(l => l.name == "The Seven Seas  2");
        var landscape = Object.FindAnyObjectByType<LevelLandscape>();
        if (level == null || landscape == null) { Debug.LogWarning("Deco blocks: missing level or landscape"); return; }
        int placed = Build(landscape, GDLevelParser.ParseObjects(GDLevelParser.PlainLevelString(level), out _));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Deco blocks: {placed} cells in {scene.name}");
    }

    public static int Build(LevelLandscape landscape, List<GDLevelParser.GDObject> objects)
    {
        var map = DecoMap(landscape);
        map.ClearAllTiles();
        // Más oscuro que el terreno sólido: se ve el bloque pero se distingue lo que se pisa
        map.color = new Color(0.72f, 0.72f, 0.72f, 1f);
        var ground = landscape.Ground;
        int placed = 0;
        // El importador deja los grupos movidos en su posición final; el relleno tiene que seguirlos
        var offsets = GDLevelImporter.MoveOffsets(objects, float.PositiveInfinity);
        foreach (var o in objects)
        {
            if (!SquareIds.Contains(o.id)) continue;
            var center = (Vector3)(new Vector2(o.x, o.y) / GDLevelParser.UnitsPerBlock + GDLevelImporter.GroupOffset(o, offsets));
            var cell = map.WorldToCell(center);
            // Bajo el suelo ya está el relleno oscuro del fondo
            if (cell.y < 0) continue;
            var solid = ground.GetTile(cell);
            // Los bloques sólidos ya tienen su propio arte
            if (solid != null && solid.name != "Tile_Thin") continue;
            if (map.HasTile(cell)) continue;
            map.SetTile(cell, landscape.Earth);
            map.SetTileFlags(cell, TileFlags.None);
            // Los cuadros desplazados respecto a la rejilla se dibujan donde estaban en GD
            map.SetTransformMatrix(cell, Matrix4x4.Translate(center - map.GetCellCenterWorld(cell)));
            placed++;
        }
        return placed;
    }

    static Tilemap DecoMap(LevelLandscape landscape)
    {
        var parent = landscape.Ground.transform.parent;
        var existing = parent.Find(DecoBlocksName);
        if (existing != null) return existing.GetComponent<Tilemap>();
        var go = new GameObject(DecoBlocksName, typeof(Tilemap), typeof(TilemapRenderer));
        Undo.RegisterCreatedObjectUndo(go, "Create " + DecoBlocksName);
        go.transform.SetParent(parent, false);
        var map = go.GetComponent<Tilemap>();
        map.tileAnchor = landscape.Ground.tileAnchor;
        var renderer = go.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName = "Gameplay";
        // Debajo de vigas, peligros y objetos para no tapar nada jugable
        renderer.sortingOrder = 0;
        return map;
    }
}
