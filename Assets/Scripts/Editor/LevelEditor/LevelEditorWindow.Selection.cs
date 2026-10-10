using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class LevelEditorWindow
{
    static readonly int SelectHint = "JumpIOLevelEditorSelect".GetHashCode();
    static readonly Color SelectionColor = new(0.25f, 0.95f, 1f);

    readonly HashSet<Vector3Int> selectedCells = new();

    enum DragMode { None, Pending, Move }

    DragMode drag;
    Vector3 dragOrigin;
    Vector3 dragDelta;
    GameObject pressedObject;
    Vector3Int pressedCell;
    bool pressedOnCell;

    struct CellContent
    {
        public TileBase ground, hazard, texture;
        public Matrix4x4 groundMatrix, hazardMatrix;
    }

    IEnumerable<GameObject> SelectedObjects
    {
        get
        {
            var active = Selection.activeGameObject;
            if (selectedCells.Count == 0 && IsEditable(active)) yield return active;
        }
    }

    bool HasSelection => selectedCells.Count > 0 || SelectedObjects.Any();

    bool Solid(Vector3Int cell) =>
        landscape.Ground.HasTile(cell) || landscape.Hazards != null && landscape.Hazards.HasTile(cell);

    GameObject ObjectAt(Vector3 point)
    {
        GameObject best = null;
        float bestArea = float.MaxValue;
        foreach (Transform child in objects)
        {
            var bounds = ObjectBounds(child.gameObject);
            if (point.x < bounds.min.x || point.x > bounds.max.x || point.y < bounds.min.y || point.y > bounds.max.y) continue;
            float area = bounds.size.x * bounds.size.y;
            if (area >= bestArea) continue;
            bestArea = area;
            best = child.gameObject;
        }
        return best;
    }

    static Bounds ObjectBounds(GameObject go)
    {
        var renderer = go.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null && renderer.sprite != null) return renderer.bounds;
        return new Bounds(go.transform.position, new Vector3(0.8f, 0.8f, 0f));
    }

    void HandleSelectTool(SceneView view, Event e)
    {
        if (HandleSelectionCommand(e)) return;

        int id = GUIUtility.GetControlID(SelectHint, FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);
        hover = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
        hover.z = 0f;

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseMove:
                view.Repaint();
                break;
            case EventType.MouseDown when e.button == 0 && !e.alt:
                GUIUtility.hotControl = id;
                PressSelection();
                e.Use();
                break;
            case EventType.MouseDrag when GUIUtility.hotControl == id:
                DragSelection();
                e.Use();
                view.Repaint();
                break;
            case EventType.MouseUp when GUIUtility.hotControl == id:
                GUIUtility.hotControl = 0;
                ReleaseSelection();
                e.Use();
                view.Repaint();
                Repaint();
                break;
            case EventType.Repaint:
                DrawSelection();
                break;
        }
    }

    void PressSelection()
    {
        dragOrigin = hover;
        dragDelta = Vector3.zero;
        pressedObject = ObjectAt(hover);
        pressedCell = landscape.Ground.WorldToCell(hover);
        pressedOnCell = pressedObject == null && Solid(pressedCell);
        if (pressedObject == null && !pressedOnCell) pressedObject = SceneryAt(hover);
        if (pressedObject == null && !pressedOnCell)
        {
            ClearSelection();
            drag = DragMode.None;
            return;
        }
        SelectOnly(pressedObject, pressedCell);
        drag = DragMode.Pending;
    }

    void SelectOnly(GameObject go, Vector3Int cell)
    {
        selectedCells.Clear();
        if (go != null) Selection.objects = new Object[] { go };
        else
        {
            if (Selection.objects.Length > 0) Selection.objects = new Object[0];
            if (Solid(cell)) selectedCells.Add(cell);
        }
    }

    void ClearSelection()
    {
        selectedCells.Clear();
        if (Selection.objects.Length > 0) Selection.objects = new Object[0];
    }

    void DragSelection()
    {
        if (drag == DragMode.Pending && (hover - dragOrigin).magnitude > 0.2f) drag = DragMode.Move;
        if (drag == DragMode.Move) dragDelta = MoveDelta(hover - dragOrigin);
    }

    void ReleaseSelection()
    {
        if (drag == DragMode.Move && dragDelta != Vector3.zero) MoveSelection(dragDelta);
        drag = DragMode.None;
        dragDelta = Vector3.zero;
    }

    Vector3 MoveDelta(Vector3 raw)
    {
        if (selectedCells.Count > 0) return new Vector3(Mathf.Round(raw.x), Mathf.Round(raw.y), 0f);
        float snap = Snap;
        float dy = Mathf.Round(raw.y / snap) * snap;
        var anchor = pressedObject != null ? pressedObject.transform.position : SelectedObjects.FirstOrDefault()?.transform.position ?? dragOrigin;
        float dx = snapToBeat && level.bpm > 0f && SelectedObjects.All(g => g.transform.parent == objects && !g.GetComponent<SpeedPortal>())
            ? NearestBeatX(anchor.x + raw.x) - anchor.x
            : Mathf.Round(raw.x / snap) * snap;
        return new Vector3(dx, dy, 0f);
    }

    void MoveSelection(Vector3 delta)
    {
        Undo.IncrementCurrentGroup();
        var moving = SelectedObjects.ToList();
        if (selectedCells.Count > 0)
        {
            Undo.RegisterCompleteObjectUndo(VisualMaps(), "Move blocks");
            var offset = new Vector3Int(Mathf.RoundToInt(delta.x), Mathf.RoundToInt(delta.y), 0);
            var contents = selectedCells.ToDictionary(c => c, Read);
            foreach (var cell in contents.Keys) EraseCell(cell);
            foreach (var (cell, content) in contents) Write(cell + offset, content);
            var touched = contents.Keys.Concat(contents.Keys.Select(c => c + offset)).ToList();
            selectedCells.Clear();
            foreach (var cell in contents.Keys) selectedCells.Add(cell + offset);
            landscape.RefreshCells(touched);
            RegenerateColliders();
        }
        foreach (var go in moving)
        {
            Undo.RecordObject(go.transform, "Move objects");
            go.transform.position += delta;
        }
        Undo.SetCurrentGroupName("Move selection");
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        AfterSelectionEdit();
    }

    void DeleteSelection()
    {
        Undo.IncrementCurrentGroup();
        if (selectedCells.Count > 0)
        {
            Undo.RegisterCompleteObjectUndo(VisualMaps(), "Delete blocks");
            foreach (var cell in selectedCells) EraseCell(cell);
            landscape.RefreshCells(selectedCells.ToList());
            RegenerateColliders();
        }
        foreach (var go in SelectedObjects.ToList()) Undo.DestroyObjectImmediate(go);
        selectedCells.Clear();
        Selection.objects = new Object[0];
        Undo.SetCurrentGroupName("Delete selection");
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        AfterSelectionEdit();
    }

    void RotateSelection()
    {
        Undo.IncrementCurrentGroup();
        var turn = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -90f));
        if (selectedCells.Count > 0)
        {
            Undo.RegisterCompleteObjectUndo(VisualMaps(), "Rotate blocks");
            foreach (var cell in selectedCells)
            {
                foreach (var map in new[] { landscape.Ground, landscape.Hazards })
                    if (map != null && map.HasTile(cell)) map.SetTransformMatrix(cell, turn * map.GetTransformMatrix(cell));
            }
            landscape.RefreshCells(selectedCells.ToList());
            RegenerateColliders();
        }
        foreach (var go in SelectedObjects)
        {
            Undo.RecordObject(go.transform, "Rotate objects");
            go.transform.rotation = Quaternion.Euler(0f, 0f, -90f) * go.transform.rotation;
        }
        Undo.SetCurrentGroupName("Rotate selection");
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        AfterSelectionEdit();
    }

    void DuplicateSelection()
    {
        var sources = SelectedObjects.ToList();
        float minX = float.MaxValue, maxX = float.MinValue;
        foreach (var cell in selectedCells) { minX = Mathf.Min(minX, cell.x); maxX = Mathf.Max(maxX, cell.x + 1); }
        foreach (var go in sources) { minX = Mathf.Min(minX, go.transform.position.x); maxX = Mathf.Max(maxX, go.transform.position.x); }
        if (minX > maxX) return;
        int width = Mathf.Max(1, Mathf.CeilToInt(maxX - minX));

        Undo.IncrementCurrentGroup();
        var offset = new Vector3Int(width, 0, 0);
        if (selectedCells.Count > 0)
        {
            Undo.RegisterCompleteObjectUndo(VisualMaps(), "Duplicate blocks");
            var contents = selectedCells.ToDictionary(c => c, Read);
            foreach (var (cell, content) in contents) Write(cell + offset, content);
            selectedCells.Clear();
            foreach (var cell in contents.Keys) selectedCells.Add(cell + offset);
            landscape.RefreshCells(selectedCells.ToList());
            RegenerateColliders();
        }
        var copies = new List<Object>();
        foreach (var go in sources)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            var copy = source != null ? (GameObject)PrefabUtility.InstantiatePrefab(source, go.transform.parent) : Instantiate(go, go.transform.parent);
            if (source != null) PrefabUtility.SetPropertyModifications(copy, PrefabUtility.GetPropertyModifications(go));
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate objects");
            copy.transform.SetPositionAndRotation(go.transform.position + offset, go.transform.rotation);
            copy.transform.localScale = go.transform.localScale;
            copies.Add(copy);
        }
        Selection.objects = copies.ToArray();
        Undo.SetCurrentGroupName("Duplicate selection");
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        AfterSelectionEdit();
    }

    CellContent Read(Vector3Int cell)
    {
        var hazards = landscape.Hazards;
        return new CellContent
        {
            ground = landscape.Ground.GetTile(cell),
            groundMatrix = landscape.Ground.GetTransformMatrix(cell),
            hazard = hazards != null ? hazards.GetTile(cell) : null,
            hazardMatrix = hazards != null ? hazards.GetTransformMatrix(cell) : Matrix4x4.identity,
            texture = landscape.TextureAt(cell),
        };
    }

    void Write(Vector3Int cell, CellContent content)
    {
        EraseCell(cell);
        if (content.ground != null)
        {
            landscape.Ground.SetTile(cell, content.ground);
            landscape.Ground.SetTransformMatrix(cell, content.groundMatrix);
        }
        if (content.hazard != null && landscape.Hazards != null)
        {
            landscape.Hazards.SetTile(cell, content.hazard);
            landscape.Hazards.SetTransformMatrix(cell, content.hazardMatrix);
        }
        if (content.texture != null) landscape.Textures().SetTile(cell, content.texture);
    }

    void AfterSelectionEdit()
    {
        EditorSceneManager.MarkSceneDirty(landscape.gameObject.scene);
        SyncLevelData();
        timelineDirty = true;
        SceneView.RepaintAll();
        Repaint();
    }

    bool HandleSelectionCommand(Event e)
    {
        if (e.type != EventType.ValidateCommand && e.type != EventType.ExecuteCommand) return false;
        bool delete = e.commandName is "SoftDelete" or "Delete";
        bool duplicate = e.commandName is "Duplicate";
        if (!(delete || duplicate) || !HasSelection) return false;
        if (e.type == EventType.ExecuteCommand)
        {
            if (delete) DeleteSelection();
            else DuplicateSelection();
        }
        e.Use();
        return true;
    }

    bool HandleSelectionKey(KeyCode key)
    {
        if (!HasSelection) return false;
        switch (key)
        {
            case KeyCode.Escape: ClearSelection(); SceneView.RepaintAll(); return true;
            case KeyCode.Delete:
            case KeyCode.Backspace: DeleteSelection(); return true;
            case KeyCode.R: RotateSelection(); return true;
            case KeyCode.LeftArrow: MoveSelection(Vector3.left * StepX()); return true;
            case KeyCode.RightArrow: MoveSelection(Vector3.right * StepX()); return true;
            case KeyCode.UpArrow: MoveSelection(Vector3.up * StepY()); return true;
            case KeyCode.DownArrow: MoveSelection(Vector3.down * StepY()); return true;
        }
        return false;
    }

    float StepX() => selectedCells.Count > 0 ? 1f : Snap;
    float StepY() => selectedCells.Count > 0 ? 1f : Snap;

    void DrawSelection()
    {
        var hoverCell = landscape.Ground.WorldToCell(hover);
        var hoverObject = drag == DragMode.None ? ObjectAt(hover) ?? (Solid(hoverCell) ? null : SceneryAt(hover)) : null;
        var faint = new Color(1f, 1f, 1f, 0.5f);
        if (hoverObject != null) DrawBox(ObjectBounds(hoverObject), faint, 0f);
        else if (drag == DragMode.None && Solid(hoverCell)) DrawCells(new[] { hoverCell }, Vector3.zero, faint, 0f);

        var offset = drag == DragMode.Move ? dragDelta : Vector3.zero;
        DrawCells(selectedCells, offset, SelectionColor, 0.22f);
        foreach (var go in SelectedObjects)
        {
            var bounds = ObjectBounds(go);
            bounds.center += offset;
            DrawBox(bounds, SelectionColor, 0.15f);
        }

    }

    void DrawCells(IEnumerable<Vector3Int> cells, Vector3 offset, Color color, float fill)
    {
        foreach (var cell in cells)
        {
            var min = landscape.Ground.CellToWorld(cell) + offset;
            DrawBox(new Bounds(min + new Vector3(0.5f, 0.5f), Vector3.one), color, fill);
        }
    }

    static void DrawBox(Bounds bounds, Color color, float fill)
    {
        var a = bounds.min;
        var b = bounds.max;
        var corners = new[] { new Vector3(a.x, a.y), new Vector3(b.x, a.y), new Vector3(b.x, b.y), new Vector3(a.x, b.y) };
        Handles.DrawSolidRectangleWithOutline(corners, new Color(color.r, color.g, color.b, fill), color);
    }

    void ProtectTilemaps()
    {
        if (landscape == null) return;
        var grid = landscape.Ground.transform.parent;
        foreach (Transform child in grid)
            if (child.GetComponent<Tilemap>() != null) SceneVisibilityManager.instance.DisablePicking(child.gameObject, true);
    }
}
