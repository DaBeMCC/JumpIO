using UnityEngine;
using UnityEngine.Tilemaps;

public static class LevelAppearanceSetup
{
    public static void RebuildAppearance(this LevelLandscape landscape)
    {
        var ground = landscape.Ground;
        var grass = landscape.Grass;
        var earth = landscape.Earth;
        var slab = landscape.Slab;
        var slope = landscape.Slope;
        var hazards = landscape.Hazards;
        var spike = landscape.Spike;
        var tiles = VisualMap("TerrainVisuals", ground);
        tiles.ClearAllTiles();
        tiles.tileAnchor = ground.tileAnchor;
        var renderer = tiles.GetComponent<TilemapRenderer>();
        var source = ground.GetComponent<TilemapRenderer>();
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder + 1;
        foreach (var cell in ground.cellBounds.allPositionsWithin)
        {
            var sourceTile = ground.GetTile<Tile>(cell);
            if (sourceTile == null) continue;
            TileBase visual = sourceTile.name == "Tile_Block"
                ? (ground.HasTile(cell + Vector3Int.up) ? earth : grass)
                : sourceTile.name == "Tile_Slab" ? slab : sourceTile.name == "Tile_Slope" ? slope : null;
            if (visual == null) continue;
            tiles.SetTile(cell, visual);
            tiles.SetTransformMatrix(cell, ground.GetTransformMatrix(cell));
        }
        source.enabled = false;
        if (hazards != null && spike != null)
        {
            var hazardTiles = VisualMap("HazardVisuals", hazards);
            hazardTiles.ClearAllTiles();
            hazardTiles.tileAnchor = hazards.tileAnchor;
            var hazardRenderer = hazardTiles.GetComponent<TilemapRenderer>();
            var hazardSource = hazards.GetComponent<TilemapRenderer>();
            hazardRenderer.sortingLayerID = hazardSource.sortingLayerID;
            hazardRenderer.sortingOrder = hazardSource.sortingOrder + 1;
            foreach (var cell in hazards.cellBounds.allPositionsWithin)
            {
                if (!hazards.HasTile(cell)) continue;
                hazardTiles.SetTile(cell, spike);
                hazardTiles.SetTransformMatrix(cell, hazards.GetTransformMatrix(cell));
            }
            hazardSource.enabled = false;
        }
    }

    static Tilemap VisualMap(string name, Tilemap source)
    {
        var existing = source.transform.parent.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(source.transform.parent, false);
        go.transform.localPosition = source.transform.localPosition;
        go.transform.localRotation = source.transform.localRotation;
        go.transform.localScale = source.transform.localScale;
        return go.GetComponent<Tilemap>();
    }
}
