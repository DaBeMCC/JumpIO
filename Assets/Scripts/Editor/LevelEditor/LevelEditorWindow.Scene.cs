using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class LevelEditorWindow
{
    static readonly int ControlHint = "JumpIOLevelEditor".GetHashCode();

    bool stroking;
    bool strokeErase;
    Vector3Int rectStart, rectEnd;
    readonly HashSet<Vector3Int> strokeCells = new();
    Vector3 hover;

    void OnSceneGUI(SceneView view)
    {
        if (!Ready) { Tools.hidden = false; return; }
        Tools.hidden = true;
        var e = Event.current;
        if (e.type == EventType.Repaint) DrawGuides(view);

        if (e.type == EventType.KeyDown && HandleSceneKey(e.keyCode)) { e.Use(); return; }
        if (e.type == EventType.MouseDown && e.button == 0 && (e.control || e.command) && !e.alt)
        {
            var point = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
            Seek(level.TimeAtX(point.x));
            spawnY = landscape.Ground.GetCellCenterWorld(landscape.Ground.WorldToCell(point)).y;
            e.Use();
            return;
        }
        if (tool == EditTool.Select)
        {
            HandleSelectTool(view, e);
            return;
        }

        int id = GUIUtility.GetControlID(ControlHint, FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

        hover = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
        hover.z = 0f;
        var cell = landscape.Ground.WorldToCell(hover);
        bool erase = tool == EditTool.Erase || e.shift;

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseMove:
                view.Repaint();
                break;
            case EventType.MouseDown when e.button == 0 && !e.alt:
                GUIUtility.hotControl = id;
                BeginStroke(erase);
                rectStart = rectEnd = cell;
                if (shape == BrushShape.Single || tool == EditTool.Paint && PlacesObject(Current) && !erase) StrokeAt(cell);
                e.Use();
                break;
            case EventType.MouseDrag when GUIUtility.hotControl == id:
                rectEnd = cell;
                if (shape == BrushShape.Single && !(tool == EditTool.Paint && PlacesObject(Current) && !strokeErase)) StrokeAt(cell);
                e.Use();
                view.Repaint();
                break;
            case EventType.MouseUp when GUIUtility.hotControl == id:
                GUIUtility.hotControl = 0;
                if (shape == BrushShape.Rectangle && !(tool == EditTool.Paint && PlacesObject(Current) && !strokeErase))
                {
                    foreach (var c in RectCells(rectStart, rectEnd)) StrokeAt(c);
                    if (strokeErase) EraseObjectsIn(rectStart, rectEnd);
                }
                EndStroke();
                e.Use();
                break;
            case EventType.Repaint:
                DrawCursor(cell, erase);
                break;
        }
    }

    bool HandleSceneKey(KeyCode key)
    {
        if (tool == EditTool.Select && HandleSelectionKey(key)) return true;
        if (tool == EditTool.Select && key != KeyCode.Space && key != KeyCode.M) return false;
        bool handled = HandleSharedKey(key);
        if (handled) Repaint();
        return handled;
    }

    void BeginStroke(bool erase)
    {
        stroking = true;
        strokeErase = erase;
        strokeCells.Clear();
        Undo.IncrementCurrentGroup();
        Undo.RegisterCompleteObjectUndo(VisualMaps(), erase ? "Erase" : tool == EditTool.Texture ? "Texture blocks" : "Paint");
    }

    void EndStroke()
    {
        if (!stroking) return;
        stroking = false;
        if (strokeCells.Count > 0)
        {
            landscape.RefreshCells(strokeCells);
            RegenerateColliders();
            EditorSceneManager.MarkSceneDirty(landscape.gameObject.scene);
        }
        SyncLevelData();
        timelineDirty = true;
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        Repaint();
    }

    void StrokeAt(Vector3Int cell)
    {
        if (tool == EditTool.Texture && !strokeErase)
        {
            PaintTexture(cell);
            return;
        }
        if (strokeErase)
        {
            if (shape == BrushShape.Single && tool != EditTool.Texture)
            {
                var target = ObjectAt(hover);
                if (target == null && !Solid(cell)) target = SceneryAt(hover);
                if (target != null)
                {
                    Undo.DestroyObjectImmediate(target);
                    return;
                }
            }
            if (strokeCells.Add(cell)) EraseCell(cell);
            return;
        }
        var item = Current;
        if (item == null) return;
        if (item.prefab != null) PlaceObject(item);
        else if (item.scenery != null) PlaceScenery(item);
        else if (strokeCells.Add(cell)) PaintTile(item, cell);
    }

    void PaintTile(PaletteItem item, Vector3Int cell)
    {
        var target = item.hazard ? landscape.Hazards : landscape.Ground;
        var other = item.hazard ? landscape.Ground : landscape.Hazards;
        if (target == null) return;
        other?.SetTile(cell, null);
        target.SetTile(cell, item.tile);
        target.SetTransformMatrix(cell, TileMatrix());
        landscape.Textures().SetTile(cell, CurrentTexture);
    }

    void PaintTexture(Vector3Int cell)
    {
        bool solid = landscape.Ground.HasTile(cell) || landscape.Hazards != null && landscape.Hazards.HasTile(cell);
        if (!solid || !strokeCells.Add(cell)) return;
        landscape.Textures().SetTile(cell, CurrentTexture);
    }

    void EraseCell(Vector3Int cell)
    {
        landscape.Ground.SetTile(cell, null);
        landscape.Hazards?.SetTile(cell, null);
        landscape.Textures().SetTile(cell, null);
    }

    void EraseObjectsIn(Vector3Int a, Vector3Int b)
    {
        if (tool == EditTool.Texture) return;
        var area = CellsRect(a, b);
        foreach (Transform child in objects.Cast<Transform>().ToList())
            if (area.Contains(child.position)) Undo.DestroyObjectImmediate(child.gameObject);
        EraseSceneryIn(area);
    }

    void PlaceObject(PaletteItem item)
    {
        var position = ObjectPosition(hover);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(item.prefab, objects);
        Undo.RegisterCreatedObjectUndo(go, "Place " + item.prefab.name);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        if (flip)
        {
            var scale = go.transform.localScale;
            go.transform.localScale = new Vector3(-Mathf.Abs(scale.x), scale.y, scale.z);
        }
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    Vector3 ObjectPosition(Vector3 world)
    {
        float snap = Snap;
        bool speedPortal = Current?.prefab != null && Current.prefab.GetComponent<SpeedPortal>() != null;
        float x = snapToBeat && level.bpm > 0f
            ? speedPortal ? NearestBeatX(world.x - 1f) + 1f : NearestBeatX(world.x)
            : Mathf.Round(world.x / snap) * snap;
        float y = Mathf.Round(world.y / snap) * snap;
        if (Mathf.Approximately(snap, 1f)) y = Mathf.Floor(world.y) + 0.5f;
        if (Mathf.Approximately(snap, 1f) && !snapToBeat) x = Mathf.Floor(world.x) + 0.5f;
        return new Vector3(x, y, 0f);
    }

    Matrix4x4 TileMatrix() =>
        Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, rotation), new Vector3(flip ? -1f : 1f, 1f, 1f));

    Rect CellsRect(Vector3Int a, Vector3Int b)
    {
        var min = landscape.Ground.CellToWorld(Vector3Int.Min(a, b));
        var max = landscape.Ground.CellToWorld(Vector3Int.Max(a, b) + Vector3Int.one);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static IEnumerable<Vector3Int> RectCells(Vector3Int a, Vector3Int b)
    {
        for (int x = Mathf.Min(a.x, b.x); x <= Mathf.Max(a.x, b.x); x++)
            for (int y = Mathf.Min(a.y, b.y); y <= Mathf.Max(a.y, b.y); y++)
                yield return new Vector3Int(x, y, 0);
    }

    void DrawCursor(Vector3Int cell, bool erase)
    {
        var item = Current;
        if (tool == EditTool.Paint && !erase && item?.scenery != null)
        {
            DrawSceneryCursor(item);
            return;
        }
        bool placingObject = tool == EditTool.Paint && !erase && item?.prefab != null;
        Color color = erase ? new Color(1f, 0.25f, 0.25f) : tool == EditTool.Texture ? new Color(0.7f, 0.4f, 1f) : new Color(0.3f, 0.85f, 1f);

        if (placingObject)
        {
            var position = ObjectPosition(hover);
            var renderer = item.prefab.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null && renderer.sprite != null)
            {
                var scale = renderer.transform.lossyScale;
                var offset = renderer.transform.position - item.prefab.transform.position;
                var bounds = renderer.sprite.bounds;
                var center = position + Quaternion.Euler(0f, 0f, rotation) * (offset + Vector3.Scale(bounds.center, scale));
                var size = Vector3.Scale(bounds.size, scale);
                DrawWorldSprite(renderer.sprite, center, size, new Color(renderer.color.r, renderer.color.g, renderer.color.b, 0.65f));
            }
            Handles.color = color;
            Handles.DrawWireDisc(position, Vector3.forward, 0.15f);
            float t = level.TimeAtX(position.x);
            Handles.Label(position + new Vector3(0.3f, 0.6f), $"{Clock(t)}  beat {level.BeatAt(t) / 4 + 1}.{Mod(level.BeatAt(t), 4) + 1}", EditorStyles.whiteMiniLabel);
            return;
        }

        var cells = stroking && shape == BrushShape.Rectangle ? RectCells(rectStart, rectEnd) : new[] { cell };
        var min = landscape.Ground.CellToWorld(cells.Aggregate((a, b) => Vector3Int.Min(a, b)));
        var max = landscape.Ground.CellToWorld(cells.Aggregate((a, b) => Vector3Int.Max(a, b)) + Vector3Int.one);
        var corners = new[] { new Vector3(min.x, min.y), new Vector3(max.x, min.y), new Vector3(max.x, max.y), new Vector3(min.x, max.y) };
        Handles.DrawSolidRectangleWithOutline(corners, new Color(color.r, color.g, color.b, 0.15f), color);
        if (!erase && tool == EditTool.Paint && item?.tile != null && !(stroking && shape == BrushShape.Rectangle))
        {
            var sprite = CurrentTexture != null ? CurrentTexture.sprite : item.icon;
            DrawWorldSprite(sprite, landscape.Ground.GetCellCenterWorld(cell), Vector3.one, new Color(1f, 1f, 1f, 0.7f), rotation, flip);
        }
    }

    void DrawWorldSprite(Sprite sprite, Vector3 center, Vector3 size, Color tint, float angle = 0f, bool flipX = false)
    {
        if (sprite == null) return;
        Vector2 a = HandleUtility.WorldToGUIPoint(center - size * 0.5f);
        Vector2 b = HandleUtility.WorldToGUIPoint(center + size * 0.5f);
        var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        Handles.BeginGUI();
        DrawSprite(rect, sprite, tint, angle, flipX);
        Handles.EndGUI();
    }

    void DrawGuides(SceneView view)
    {
        var camera = view.camera;
        var bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        var topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
        float yMin = bottomLeft.y, yMax = topRight.y;

        if (showBeats && level.bpm > 0f)
        {
            double beat = 60.0 / level.bpm;
            float tMin = level.TimeAtX(bottomLeft.x), tMax = level.TimeAtX(topRight.x);
            long first = (long)System.Math.Ceiling((level.songOffset + tMin - level.firstBeat) / beat);
            float pixels = (float)(beat * level.SpeedAtTime(Mathf.Max(0f, tMin)) / (topRight.x - bottomLeft.x) * camera.pixelWidth);
            if (pixels > 4f)
            {
                for (long k = first; k < first + 2000; k++)
                {
                    float t = (float)(level.firstBeat + k * beat - level.songOffset);
                    if (t > tMax) break;
                    float x = level.XAtTime(t);
                    Handles.color = new Color(1f, 1f, 1f, k % 4 == 0 ? 0.35f : 0.12f);
                    Handles.DrawLine(new Vector3(x, yMin), new Vector3(x, yMax));
                }
            }
        }

        if (level.markers != null)
        {
            Handles.color = new Color(1f, 0.9f, 0.2f, 0.85f);
            foreach (float marker in level.markers)
            {
                float x = level.XAtTime(marker);
                if (x >= bottomLeft.x && x <= topRight.x) Handles.DrawLine(new Vector3(x, yMin), new Vector3(x, yMax), 2f);
            }
        }

        float head = PlayheadX;
        Handles.color = new Color(1f, 0.25f, 0.25f, 0.95f);
        Handles.DrawLine(new Vector3(head, yMin), new Vector3(head, yMax), 3f);
        Handles.Label(new Vector3(head + 0.2f, yMax - 0.4f), Clock(playhead), EditorStyles.whiteBoldLabel);

        if (playing || playhead < 0.0) return;
        var start = StartState();
        var spawn = new Vector3(head, start.y);
        Handles.color = new Color(0.3f, 1f, 0.45f, 0.95f);
        Handles.DrawWireDisc(spawn, Vector3.forward, 0.45f, 2f);
        Handles.DrawLine(spawn + Vector3.left * 0.3f, spawn + Vector3.right * 0.6f, 2f);
        Handles.Label(spawn + new Vector3(0.55f, 0.75f), $"Start · {start.mode}{(start.gravityInverted ? " · inverted" : "")}", EditorStyles.whiteMiniLabel);
    }
}
