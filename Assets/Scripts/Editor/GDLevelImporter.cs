using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using static GDLevelParser;

// Importa un nivel de Geometry Dash a la escena activa: bloques y peligros a tilemaps, objetos de juego a prefabs, canción y tiempos a LevelData.
public class GDLevelImporter : EditorWindow
{
    const float BlocksAfterLastObject = 12f;
    const int MoveTrigger = 901;

    [SerializeField] string filePath = "";
    [SerializeField] GDObjectMap map;
    [SerializeField] string displayName = "";
    [SerializeField] bool cutAtModePortal = true;
    [SerializeField] bool applyMoveTriggers;
    [SerializeField] string gdResourcesFolder = @"C:\Program Files (x86)\Steam\steamapps\common\Geometry Dash\Resources";

    List<GDLevel> levels;
    int selection;
    string report = "";
    Vector2 scroll;

    [MenuItem("Tools/JumpIO/Import Level")]
    static void Open() => GetWindow<GDLevelImporter>("Import GD Level");

    void OnEnable()
    {
        if (map == null) map = AssetDatabase.LoadAssetAtPath<GDObjectMap>("Assets/Data/GDObjectMap.asset");
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Accepts .gmd, GDBrowser .json (?download), .txt with the level string or a .dat save. " +
                                "Work on copies: never on Geometry Dash's own files.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            filePath = EditorGUILayout.TextField("File", filePath);
            if (GUILayout.Button("Browse…", GUILayout.Width(70)))
            {
                string path = EditorUtility.OpenFilePanelWithFilters("Geometry Dash level", Path.GetDirectoryName(filePath),
                    new[] { "GD levels", "gmd,json,txt,dat", "All", "*" });
                if (!string.IsNullOrEmpty(path)) { filePath = path; levels = null; }
            }
        }
        if (GUILayout.Button("Read file")) ReadLevels();

        if (levels != null && levels.Count > 0)
            selection = EditorGUILayout.Popup("Level (k2)", selection, levels.Select(l => l.name).ToArray());

        map = (GDObjectMap)EditorGUILayout.ObjectField("ID map", map, typeof(GDObjectMap), false);
        cutAtModePortal = EditorGUILayout.Toggle(new GUIContent("Cut at unsupported mode", "Ends the level at the first wave, robot, spider or swing portal."), cutAtModePortal);
        applyMoveTriggers = EditorGUILayout.Toggle(new GUIContent("Apply move triggers", "Places each moved group at its final position (move triggers are not simulated at runtime)."), applyMoveTriggers);
        displayName = EditorGUILayout.TextField(new GUIContent("Display name", "Empty = the level's name in Geometry Dash."), displayName);
        gdResourcesFolder = EditorGUILayout.TextField("GD Resources folder", gdResourcesFolder);

        using (new EditorGUI.DisabledScope(levels == null || levels.Count == 0 || map == null))
            if (GUILayout.Button("Import into active scene", GUILayout.Height(28)))
                Import(levels[selection]);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    public static string ImportIntoActiveScene(GDLevel level, string shownName, bool moveTriggers)
    {
        var importer = CreateInstance<GDLevelImporter>();
        importer.map = AssetDatabase.LoadAssetAtPath<GDObjectMap>("Assets/Data/GDObjectMap.asset");
        importer.displayName = shownName;
        importer.cutAtModePortal = true;
        importer.applyMoveTriggers = moveTriggers;
        importer.Import(level);
        string result = importer.report;
        DestroyImmediate(importer);
        return result;
    }

    void ReadLevels()
    {
        try
        {
            levels = GDLevelParser.ReadFile(filePath);
            selection = 0;
            report = levels.Count + " level(s) in the file.";
        }
        catch (Exception e)
        {
            levels = null;
            report = "Could not read the file: " + e.Message;
        }
    }

    void Import(GDLevel level)
    {
        var writer = LevelSceneWriter.FromActiveScene();
        if (writer == null) return;

        var gdObjects = ParseObjects(PlainLevelString(level), out var header);
        var log = new StringBuilder($"Level: {level.name}\nObjects in level string: {gdObjects.Count}\n");

        int startMode = StartModeIndex(header);
        if (startMode > (int)PlayerModeType.Ufo)
        {
            EditorUtility.DisplayDialog("Import level", $"The level starts in an unsupported mode (kA2 = {startMode}).", "OK");
            return;
        }

        var portal = FirstUnsupportedPortal(gdObjects);
        float cutX = float.PositiveInfinity;
        if (portal != null)
        {
            if (cutAtModePortal)
            {
                cutX = portal.x;
                log.Append($"Cut at unsupported mode portal {portal.id} (x = {portal.x / UnitsPerBlock:F1} blocks).\n");
            }
            else if (!EditorUtility.DisplayDialog("Import level", $"The level has unsupported mode portals (first: ID {portal.id}). " +
                         "Those parts will be unplayable.", "Import anyway", "Cancel"))
                return;
        }

        writer.Clear();
        var groupOffsets = applyMoveTriggers ? MoveOffsets(gdObjects, cutX) : new Dictionary<string, Vector2>();
        var unmapped = new Dictionary<int, int>();
        int solids = 0, spikes = 0, prefabs = 0, ignored = 0, overlaps = 0, startPortals = 0;
        float startCorridorY = 0.5f;
        float lastX = 0f, maxY = 0f;

        foreach (var o in gdObjects)
        {
            if (o.x >= cutX) continue;
            var entry = map.Find(o.id);
            if (entry == null) { unmapped[o.id] = unmapped.GetValueOrDefault(o.id) + 1; continue; }
            if (entry.category == GDObjectMap.Category.Ignored) { ignored++; continue; }
            var startPortal = entry.prefab != null ? entry.prefab.GetComponent<ModePortal>() : null;
            bool modePortal = startPortal != null;
            if (o.x <= 0f && (modePortal || entry.prefab != null && entry.prefab.TryGetComponent<GravityPortal>(out _)))
            {
                if (modePortal && (int)startPortal.Mode == startMode) startCorridorY = o.y / UnitsPerBlock;
                startPortals++;
                continue;
            }

            var moved = GroupOffset(o, groupOffsets);
            float x = o.x / UnitsPerBlock + moved.x, y = o.y / UnitsPerBlock + moved.y;
            lastX = Mathf.Max(lastX, x);
            maxY = Mathf.Max(maxY, y);

            if (entry.category == GDObjectMap.Category.Object)
            {
                var go = writer.Instantiate(entry.prefab, new Vector3(x, y, 0f));
                go.transform.rotation = Quaternion.Euler(0f, 0f, -o.rotation);
                go.transform.localScale = new Vector3((o.flipX ? -1f : 1f) * o.scale * entry.scale.x,
                                                      (o.flipY ? -1f : 1f) * o.scale * entry.scale.y, 1f);
                prefabs++;
                continue;
            }

            bool solid = entry.category == GDObjectMap.Category.Solid;
            var tilemap = solid ? writer.Ground : writer.Hazards;
            var cell = new Vector3Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y), 0);
            if (tilemap.HasTile(cell)) overlaps++;
            tilemap.SetTile(cell, entry.tile);
            var offset = new Vector3(x - cell.x - 0.5f, y - cell.y - 0.5f, 0f);
            tilemap.SetTransformMatrix(cell, Matrix4x4.Translate(offset)
                * TileMatrix(o.rotation, o.flipX, o.flipY, entry.scale * o.scale, !entry.centered));
            if (solid) solids++; else spikes++;
        }

        float finishX = Mathf.Min(lastX + BlocksAfterLastObject, cutX / UnitsPerBlock);
        writer.PlaceGroundAndFinish(map.Find(1).tile, finishX);
        writer.RegenerateColliders(log);
        writer.FitCameraBounds(finishX, maxY);

        var speedChanges = LevelSceneWriter.SpeedChanges(gdObjects, map, cutX);
        var song = LevelSceneWriter.CopySong(level, gdResourcesFolder, log);
        string shownName = string.IsNullOrWhiteSpace(displayName) ? level.name : displayName.Trim();
        var data = LevelSceneWriter.SaveLevelData(level.name, shownName, Number(header, "kA13", 0f), StartSpeed(header),
                                                  speedChanges, finishX, song);
        data.startMode = (PlayerModeType)startMode;
        data.startCorridorY = startCorridorY;
        data.startGravityInverted = StartsInverted(header);
        writer.WireAndSave(data);

        log.Append($"LevelData: {AssetDatabase.GetAssetPath(data)} (offset {data.songOffset} s, latency {data.latencyMs} ms kept)\n");
        log.Append($"Solids: {solids}  Hazards: {spikes}  Prefabs: {prefabs}  Ignored (triggers): {ignored}  Overlapping cells: {overlaps}  " +
                   $"Portals behind the start skipped: {startPortals}\n");
        log.Append($"Name: {data.levelName}  Start corridor y: {data.startCorridorY:F2}\n");
        log.Append($"Start: {data.startMode}, gravity {(data.startGravityInverted ? "inverted" : "normal")}, {data.speeds[0]} blocks/s  Speed portals: {speedChanges.Count}\n");
        log.Append($"Finish at x = {finishX:F1}  Duration: {data.TimeAtX(finishX):F1} s\n");
        ReportMoveTriggers(gdObjects, cutX, data, log);
        ReportUnmapped(unmapped, log);

        report = log.ToString();
        Debug.Log("[GD Importer]\n" + report);
    }

    public static Matrix4x4 TileMatrix(float rotation, bool flipX, bool flipY, Vector2 scale, bool anchorBase = true) =>
        Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -rotation))
        * Matrix4x4.Scale(new Vector3(flipX ? -1f : 1f, flipY ? -1f : 1f, 1f))
        * Matrix4x4.Translate(new Vector3(0f, anchorBase ? -(1f - scale.y) / 2f : 0f, 0f))
        * Matrix4x4.Scale(new Vector3(scale.x, scale.y, 1f));

    public static Dictionary<string, Vector2> MoveOffsets(List<GDObject> objects, float cutX)
    {
        var offsets = new Dictionary<string, Vector2>();
        foreach (var t in objects)
        {
            if (t.id != MoveTrigger || t.x >= cutX || !t.fields.TryGetValue("51", out string group)) continue;
            var delta = new Vector2(Number(t.fields, "28", 0f), Number(t.fields, "29", 0f)) / UnitsPerBlock;
            offsets[group] = offsets.GetValueOrDefault(group) + delta;
        }
        return offsets;
    }

    public static Vector2 GroupOffset(GDObject o, Dictionary<string, Vector2> offsets)
    {
        if (offsets.Count == 0 || !o.fields.TryGetValue("57", out string groups)) return Vector2.zero;
        var total = Vector2.zero;
        foreach (string g in groups.Split('.'))
            if (offsets.TryGetValue(g, out var delta)) total += delta;
        return total;
    }

    void ReportMoveTriggers(List<GDObject> objects, float cutX, LevelData data, StringBuilder log)
    {
        var gameplayGroups = new HashSet<string>();
        foreach (var o in objects)
        {
            var entry = map.Find(o.id);
            if (entry == null || entry.category == GDObjectMap.Category.Ignored || !o.fields.TryGetValue("57", out string groups)) continue;
            foreach (string g in groups.Split('.')) gameplayGroups.Add(g);
        }
        var triggers = objects.Where(o => o.id == MoveTrigger && o.x < cutX).OrderBy(o => o.x).ToList();
        var gameplay = triggers.Where(o => o.fields.TryGetValue("51", out string g) && gameplayGroups.Contains(g)).ToList();
        log.Append($"Move triggers in the playable part: {triggers.Count}, moving gameplay objects: {gameplay.Count} (not implemented)\n");
        foreach (var t in gameplay)
        {
            float x = t.x / UnitsPerBlock;
            log.Append($"  x = {x:F1} (t = {data.TimeAtX(x):F1} s) group {t.fields["51"]} move ({Number(t.fields, "28", 0f) / UnitsPerBlock:F1}, " +
                       $"{Number(t.fields, "29", 0f) / UnitsPerBlock:F1}) blocks in {Number(t.fields, "10", 0f):F2} s\n");
        }
    }

    void ReportUnmapped(Dictionary<int, int> unmapped, StringBuilder log)
    {
        if (unmapped.Count == 0) { log.Append("Every ID is in the map.\n"); return; }
        log.Append("Most frequent unmapped IDs (* = has collision per the reference: possibly playable):\n");
        foreach (var pair in unmapped.OrderByDescending(p => p.Value).Take(15))
            log.Append($"  {(map.HasCollisionPerReference(pair.Key) ? "*" : " ")} ID {pair.Key}: {pair.Value}\n");
    }
}
