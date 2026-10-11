using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class LevelAppearanceSetup
{
    public const string TerrainVisualsName = "TerrainVisuals";
    public const string HazardVisualsName = "HazardVisuals";
    public const string TexturesName = "TerrainTextures";

    public static void RebuildAppearance(this LevelLandscape landscape)
    {
        var ground = landscape.Ground;
        var tiles = landscape.TerrainVisuals();
        tiles.ClearAllTiles();
        foreach (var cell in ground.cellBounds.allPositionsWithin) RefreshGround(landscape, tiles, cell);
        ground.GetComponent<TilemapRenderer>().enabled = false;
        PlatformFillSetup.Rebuild(landscape);


        var hazards = landscape.Hazards;
        if (hazards == null || landscape.Spike == null) return;
        var hazardTiles = landscape.HazardVisuals();
        hazardTiles.ClearAllTiles();
        foreach (var cell in hazards.cellBounds.allPositionsWithin) RefreshHazard(landscape, hazardTiles, cell);
        hazards.GetComponent<TilemapRenderer>().enabled = false;

        foreach (Transform child in ground.transform.parent)
            if (child.name.Contains(OverlapRepairSetup.OverflowTag) && !child.name.EndsWith("Visuals"))
                landscape.RefreshOverflow(child.GetComponent<Tilemap>(), child.name.StartsWith(hazards.name));
    }

    public static void RefreshCells(this LevelLandscape landscape, IEnumerable<Vector3Int> cells)
    {
        var tiles = landscape.TerrainVisuals();
        var hazardTiles = landscape.Hazards != null ? landscape.HazardVisuals() : null;
        foreach (var cell in cells)
        {
            RefreshGround(landscape, tiles, cell);
            RefreshGround(landscape, tiles, cell + Vector3Int.down);
            if (hazardTiles != null) RefreshHazard(landscape, hazardTiles, cell);
        }
    }

    // Copia el arte de un tilemap de desbordamiento (piezas que el importador pisó en la misma celda)
    public static void RefreshOverflow(this LevelLandscape landscape, Tilemap source, bool hazard)
    {
        var tiles = VisualMap(source.name + "Visuals", source, true);
        tiles.ClearAllTiles();
        foreach (var cell in source.cellBounds.allPositionsWithin)
            if (hazard) RefreshHazard(landscape, source, tiles, cell);
            else RefreshGround(landscape, source, tiles, cell);
        source.GetComponent<TilemapRenderer>().enabled = false;
    }

    public static Tilemap TerrainVisuals(this LevelLandscape landscape) =>
        VisualMap(TerrainVisualsName, landscape.Ground, true);

    public static Tilemap HazardVisuals(this LevelLandscape landscape) =>
        VisualMap(HazardVisualsName, landscape.Hazards, true);

    public static Tilemap Textures(this LevelLandscape landscape) =>
        VisualMap(TexturesName, landscape.Ground, false);

    public static TileBase TextureAt(this LevelLandscape landscape, Vector3Int cell)
    {
        var existing = landscape.Ground.transform.parent.Find(TexturesName);
        return existing != null ? existing.GetComponent<Tilemap>().GetTile(cell) : null;
    }

    static void RefreshGround(LevelLandscape landscape, Tilemap tiles, Vector3Int cell) => RefreshGround(landscape, landscape.Ground, tiles, cell);

    static void RefreshGround(LevelLandscape landscape, Tilemap ground, Tilemap tiles, Vector3Int cell)
    {
        var source = ground.GetTile<Tile>(cell);
        var swap = Swap(landscape, source);
        TileBase visual = source == null ? null : landscape.TextureAt(cell) ?? swap?.to ?? DefaultGround(landscape, source, cell);
        tiles.SetTile(cell, visual);
        if (visual == null) return;
        var matrix = Visual(ground.GetTransformMatrix(cell), swap);
        if (source.name == "Tile_Thin" && visual is Tile thin && thin.sprite != null)
        {
            var original = ground.GetTransformMatrix(cell);
            float thickness = original.MultiplyVector(Vector3.up).magnitude;
            float visibleThickness = Mathf.Max(thickness, 0.15f);
            var normal = original.MultiplyVector(Vector3.up).normalized;
            Vector3 offset = original.GetColumn(3);
            float side = Mathf.Sign(Vector3.Dot(offset, normal));
            matrix = original * Matrix4x4.Scale(new Vector3(1f / thin.sprite.bounds.size.x, visibleThickness / Mathf.Max(thickness, 0.001f) / thin.sprite.bounds.size.y, 1f));
            var position = offset - normal * side * (visibleThickness - thickness) * 0.5f;
            matrix.SetColumn(3, new Vector4(position.x, position.y, position.z, 1f));
        }
        tiles.SetTransformMatrix(cell, matrix);
    }

    static TileBase DefaultGround(LevelLandscape landscape, Tile source, Vector3Int cell) =>
        source.name == "Tile_Block" ? (Covered(landscape.Ground, cell) ? landscape.Earth : landscape.Grass)
        : source.name == "Tile_Slab" ? landscape.Slab
        : source.name == "Tile_Slope" ? landscape.Slope
        : source;
    static bool Covered(Tilemap ground, Vector3Int cell)
    {
        var above = ground.GetTile(cell + Vector3Int.up);
        return above != null && above.name != "Tile_Thin";
    }

    static void RefreshHazard(LevelLandscape landscape, Tilemap tiles, Vector3Int cell) => RefreshHazard(landscape, landscape.Hazards, tiles, cell);

    static void RefreshHazard(LevelLandscape landscape, Tilemap hazards, Tilemap tiles, Vector3Int cell)
    {
        var source = hazards.GetTile(cell);
        var swap = Swap(landscape, source);
        TileBase visual = source == null ? null : landscape.TextureAt(cell) ?? swap?.to ?? (source.name == "Tile_Spike" ? landscape.Spike : source);
        tiles.SetTile(cell, visual);
        if (visual != null) tiles.SetTransformMatrix(cell, Visual(hazards.GetTransformMatrix(cell), swap));
    }

    static LevelLandscape.TileSwap? Swap(LevelLandscape landscape, TileBase source)
    {
        if (source == null || landscape.Swaps == null) return null;
        foreach (var swap in landscape.Swaps)
            if (swap.from == source && swap.to != null) return swap;
        return null;
    }

    static Matrix4x4 Visual(Matrix4x4 matrix, LevelLandscape.TileSwap? swap)
    {
        if (swap == null || !swap.Value.ignoreScale) return matrix;
        var scale = matrix.lossyScale;
        return Matrix4x4.TRS(matrix.GetColumn(3), matrix.rotation, new Vector3(Mathf.Sign(scale.x), Mathf.Sign(scale.y), 1f));
    }

    static Tilemap VisualMap(string name, Tilemap source, bool rendered)
    {
        var existing = source.transform.parent.Find(name);
        if (existing != null) return existing.GetComponent<Tilemap>();
        var go = rendered ? new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)) : new GameObject(name, typeof(Tilemap));
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(source.transform.parent, false);
        go.transform.localPosition = source.transform.localPosition;
        go.transform.localRotation = source.transform.localRotation;
        go.transform.localScale = source.transform.localScale;
        var map = go.GetComponent<Tilemap>();
        map.tileAnchor = source.tileAnchor;
        if (rendered)
        {
            var renderer = go.GetComponent<TilemapRenderer>();
            var sourceRenderer = source.GetComponent<TilemapRenderer>();
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder + 1;
        }
        return map;
    }
}

