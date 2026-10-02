using UnityEngine;
using UnityEngine.Tilemaps;

public static class LevelAppearanceSetup
{
    public static void RebuildAppearance(this LevelLandscape landscape)
    {
        var ground = landscape.Ground;
        var grass = landscape.Grass;
        var earth = landscape.Earth;
        var forest = landscape.Forest;
        var slab = landscape.Slab;
        var slope = landscape.Slope;
        var hazards = landscape.Hazards;
        var spike = landscape.Spike;
        var backdropFill = landscape.BackdropFill;
        var tiles = VisualMap("TerrainVisuals", ground);
        tiles.ClearAllTiles();
        tiles.tileAnchor = ground.tileAnchor;
        var renderer = tiles.GetComponent<TilemapRenderer>();
        var source = ground.GetComponent<TilemapRenderer>();
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder;
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
        renderer.sortingOrder += 1;
        if (hazards != null && spike != null)
        {
            var hazardTiles = VisualMap("HazardVisuals", hazards);
            hazardTiles.ClearAllTiles();
            hazardTiles.tileAnchor = hazards.tileAnchor;
            var hazardRenderer = hazardTiles.GetComponent<TilemapRenderer>();
            hazardRenderer.sortingLayerID = hazards.GetComponent<TilemapRenderer>().sortingLayerID;
            hazardRenderer.sortingOrder = hazards.GetComponent<TilemapRenderer>().sortingOrder + 1;
            foreach (var cell in hazards.cellBounds.allPositionsWithin)
            {
                if (!hazards.HasTile(cell)) continue;
                hazardTiles.SetTile(cell, spike);
                hazardTiles.SetTransformMatrix(cell, hazards.GetTransformMatrix(cell));
            }
            hazards.GetComponent<TilemapRenderer>().enabled = true;
        }
        var camera = Camera.main;
        if (camera == null || forest == null) return;
        var existing = ground.transform.parent.Find("ForestBackdrop");
        var background = existing != null ? existing.gameObject : new GameObject("ForestBackdrop", typeof(ForestParallax));
        background.transform.SetParent(ground.transform.parent, false);
        var parallax = background.GetComponent<ForestParallax>();
        var scenePreview = parallax.ScenePreview;
        if (scenePreview == null)
        {
            var preview = new GameObject("ScenePreview", typeof(SpriteRenderer));
            preview.transform.SetParent(background.transform, false);
            scenePreview = preview.GetComponent<SpriteRenderer>();
        }
        parallax.Configure(camera, forest, scenePreview);
        var bounds = ground.cellBounds;
        if (backdropFill != null)
        {
            var existingFill = background.transform.Find("ForestColor");
            var fill = existingFill != null ? existingFill.gameObject : new GameObject("ForestColor", typeof(SpriteRenderer));
            fill.transform.SetParent(background.transform, false);
            var fillRenderer = fill.GetComponent<SpriteRenderer>();
            fillRenderer.sprite = backdropFill;
            fillRenderer.drawMode = SpriteDrawMode.Sliced;
            fillRenderer.size = new Vector2(bounds.size.x + 30f, bounds.size.y + 24f);
            fillRenderer.color = new Color(.2f,.34f,.35f);
            fillRenderer.sortingOrder = -200;
            fill.transform.localScale = Vector3.one;
            fill.transform.position = new Vector3(bounds.center.x,bounds.center.y,7f);
        }
        scenePreview.sprite = forest;
        scenePreview.drawMode = SpriteDrawMode.Tiled;
        scenePreview.size = new Vector2(bounds.size.x + 30f, bounds.size.y + 24f);
        scenePreview.transform.position = new Vector3(bounds.center.x, bounds.center.y, 5f);
        scenePreview.sortingOrder = -100;
        scenePreview.color = new Color(.7f,.85f,.8f,1f);
        scenePreview.enabled = !Application.isPlaying;
        camera.backgroundColor = new Color(0.2f, 0.34f, 0.35f);
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
