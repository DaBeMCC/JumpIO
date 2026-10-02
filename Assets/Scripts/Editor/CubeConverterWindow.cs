using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using static GDLevelParser;

public class CubeConverterWindow : EditorWindow
{
    const int MaxValidationRounds = 400;
    const string SettingsPath = "Assets/Data/CubeConversionSettings.asset";
    const string BotPlanPath = "Temp/CubeBotPlan.txt";

    [SerializeField] string filePath = "";
    [SerializeField] GDObjectMap map;
    [SerializeField] CubeConversionSettings settings;
    [SerializeField] string gdResourcesFolder = @"C:\Program Files (x86)\Steam\steamapps\common\Geometry Dash\Resources";

    List<GDLevel> levels;
    int selection;
    string report = "";
    Vector2 scroll;
    Editor settingsEditor;

    [MenuItem("Tools/JumpIO/Convert Level To Cube")]
    static void Open() => GetWindow<CubeConverterWindow>("Convert To Cube");

    void OnEnable()
    {
        if (map == null) map = AssetDatabase.LoadAssetAtPath<GDObjectMap>("Assets/Data/GDObjectMap.asset");
        if (settings == null) settings = AssetDatabase.LoadAssetAtPath<CubeConversionSettings>(SettingsPath);
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Rebuilds the whole level as cube mode on the floor. Accent x positions and speed changes " +
                                "are kept, so the obstacles stay on the song's beats. A bot checks that it can be completed.", MessageType.Info);
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
        settings = (CubeConversionSettings)EditorGUILayout.ObjectField("Settings", settings, typeof(CubeConversionSettings), false);
        if (settings != null)
        {
            Editor.CreateCachedEditor(settings, null, ref settingsEditor);
            settingsEditor.OnInspectorGUI();
        }
        gdResourcesFolder = EditorGUILayout.TextField("GD Resources folder", gdResourcesFolder);

        using (new EditorGUI.DisabledScope(levels == null || levels.Count == 0 || map == null || settings == null))
        {
            if (GUILayout.Button("Convert into active scene", GUILayout.Height(28)))
                Convert(levels[selection], false);
            if (GUILayout.Button(new GUIContent("Revalidate scene layout",
                    "Keeps the obstacles already in the scene and regenerates only the sections that break the current rules or the bot.")))
                Convert(levels[selection], true);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
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

    void Convert(GDLevel level, bool keepSceneLayout)
    {
        var writer = LevelSceneWriter.FromActiveScene();
        if (writer == null) return;

        var objects = ParseObjects(PlainLevelString(level), out var header);
        var analysis = CubeLevelConverter.Analyze(objects, header, map);
        var timeline = CreateInstance<LevelData>();
        timeline.changeX = new[] { 0f }.Concat(analysis.speedChanges.Select(c => c.x)).ToArray();
        timeline.speeds = new[] { analysis.startSpeed }.Concat(analysis.speedChanges.Select(c => c.speed)).ToArray();
        timeline.RecalculateTimes();
        var physics = CubeBot.Physics.FromProject(map, FindAnyObjectByType<MusicConductor>());

        int n = analysis.Sections;
        var attempts = new int[n];
        CubeLayout[] sections;
        var ruleBroken = new List<int>();
        if (keepSceneLayout)
        {
            sections = CubeLevelConverter.ReadSections(writer, n);
            ruleBroken = CubeLevelConverter.GapViolations(sections, timeline, settings.minGapSeconds);
            foreach (int i in ruleBroken) sections[i] = Regenerate(analysis, sections, i, 0, timeline, physics);
        }
        else
        {
            sections = new CubeLayout[n];
            for (int i = 0; i < n; i++) sections[i] = Regenerate(analysis, sections, i, 0, timeline, physics);
        }

        var clocks = new[]
        {
            new CubeBot.ClockModel(physics), new CubeBot.ClockModel(physics, 1.0 / 3),
            new CubeBot.ClockModel(physics, 2.0 / 3), new CubeBot.ClockModel(physics, 0, 60),
            new CubeBot.ClockModel(physics, 0, 0, physics.dt), new CubeBot.ClockModel(physics, 0, 0, -physics.dt),
            new CubeBot.ClockModel(physics, 0, 0, 0, 0.98), new CubeBot.ClockModel(physics, 0, 0, 0, 1.02),
        };
        CubeBot bot = null;
        bool solved = false;
        int rounds = 0;
        for (; rounds < MaxValidationRounds; rounds++)
        {
            EditorUtility.DisplayProgressBar("Convert To Cube", $"Bot validation, round {rounds + 1}", rounds / (float)MaxValidationRounds);
            bot = new CubeBot(physics, clocks[0], timeline, analysis.finishX, sections);
            solved = bot.Solve();
            float failX = bot.FurthestX;
            if (solved) bot.CenterJumps();
            for (int c = 1; solved && c < clocks.Length; c++)
                solved = new CubeBot(physics, clocks[c], timeline, analysis.finishX, sections).Verify(bot.JumpXs, bot.OrbXs, out failX);
            if (solved) break;

            int stuck = Mathf.Clamp(Mathf.FloorToInt((failX + 1f) / CubeLevelConverter.SectionBlocks), 0, n - 1);
            int target = stuck;
            while (target > 0 && attempts[target] >= 4) target--;
            attempts[target]++;
            sections[target] = Regenerate(analysis, sections, target, attempts[target], timeline, physics);
        }
        EditorUtility.ClearProgressBar();

        var log = new StringBuilder($"Level: {level.name} → cube ({(keepSceneLayout ? "revalidated scene layout" : "full conversion")})\n" +
                                    $"Objects in level string: {objects.Count}\n");
        if (keepSceneLayout)
            log.Append($"Sections breaking the gap rules (doubled for 2 s after speed changes), regenerated: {ruleBroken.Count}" +
                       (ruleBroken.Count > 0 ? " → #" + string.Join(", #", ruleBroken) : "") + "\n");
        log.Append($"Sections: {n} of {CubeLevelConverter.SectionBlocks} blocks  Finish at x = {analysis.finishX:F1}  " +
                   $"Duration: {timeline.TimeAtX(analysis.finishX):F1} s\n");
        log.Append($"Original modes: {string.Join(" → ", analysis.modeChanges.Select(m => $"{m.mode}@{m.x:F0}"))}\n");
        log.Append($"Speed changes kept: {analysis.speedChanges.Count} (start {analysis.startSpeed} blocks/s)\n");
        log.Append($"Obstacles: {sections.Sum(s => s.ObstacleCount)}  (spikes {sections.Sum(s => s.Spikes.Count)}, blocks {sections.Sum(s => s.Solids.Count)}, " +
                   $"pads {sections.Sum(s => s.Pads.Count)}, orbs {sections.Sum(s => s.Orbs.Count)})\n");
        log.Append($"Settings: seed {settings.seed}, min gap {settings.minGapSeconds} s, density {settings.density}\n");
        log.Append($"Bot (synced clock, 3 audio phases + 60 fps + ±1 step + audio ±2 %, 0 hazard touches): {(solved ? "COMPLETED" : "FAILED")} after {rounds + 1} " +
                   $"validation round(s); sections regenerated by the bot: {attempts.Count(a => a > 0)} (total regenerations {attempts.Sum()})\n");
        for (int i = 0; i < n; i++)
        {
            if (attempts[i] == 0) continue;
            float from = i * CubeLevelConverter.SectionBlocks;
            string emptied = attempts[i] >= 4 ? " (emptied)" : "";
            log.Append($"  #{i} [{from:F0}-{from + CubeLevelConverter.SectionBlocks:F0}] originally {CubeLevelConverter.ModeAt(analysis, from)}: " +
                       $"regenerated ×{attempts[i]}{emptied}\n");
        }
        if (solved) log.Append($"Bot plan: {bot.JumpXs.Count} jumps, {bot.OrbXs.Count} orb clicks (saved to {BotPlanPath})\n");

        if (!solved)
        {
            log.Append($"Not written to the scene: the bot could not complete the level (furthest x = {bot.FurthestX:F1}).\n");
            report = log.ToString();
            Debug.LogWarning("[Cube Converter]\n" + report);
            return;
        }

        Write(writer, level, header, analysis, sections, objects, log);
        File.WriteAllText(BotPlanPath, "jumps " + string.Join(" ", bot.JumpXs.Select(x => x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture))) +
                                       "\norbs " + string.Join(" ", bot.OrbXs.Select(x => x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture))));
        report = log.ToString();
        Debug.Log("[Cube Converter]\n" + report);
    }

    CubeLayout Regenerate(CubeLevelConverter.Analysis analysis, CubeLayout[] sections, int i, int attempt,
                          LevelData timeline, CubeBot.Physics physics)
    {
        float previousEnd = float.NegativeInfinity, nextStart = float.PositiveInfinity;
        for (int j = i - 1; j >= 0 && previousEnd == float.NegativeInfinity; j--) previousEnd = sections[j]?.EndX ?? previousEnd;
        for (int j = i + 1; j < sections.Length && nextStart == float.PositiveInfinity; j++) nextStart = sections[j]?.StartX ?? nextStart;
        return CubeLevelConverter.Generate(analysis, i, settings, timeline, physics, previousEnd, nextStart, attempt);
    }

    void Write(LevelSceneWriter writer, GDLevel level, Dictionary<string, string> header, CubeLevelConverter.Analysis analysis,
               CubeLayout[] sections, List<GDObject> objects, StringBuilder log)
    {
        writer.Clear();
        var block = map.Find(1).tile;
        var spike = map.Find(8).tile;
        var pad = map.Find(35).prefab;
        var orb = map.Find(36).prefab;
        foreach (var s in sections)
        {
            foreach (var cell in s.Solids) writer.Ground.SetTile((Vector3Int)cell, block);
            foreach (var cell in s.Spikes) writer.Hazards.SetTile((Vector3Int)cell, spike);
            foreach (var position in s.Pads) writer.Instantiate(pad, position);
            foreach (var position in s.Orbs) writer.Instantiate(orb, position);
        }
        foreach (var o in objects)
        {
            var entry = map.Find(o.id);
            if (entry?.prefab != null && entry.prefab.TryGetComponent<SpeedPortal>(out _))
                writer.Instantiate(entry.prefab, new Vector3(o.x / UnitsPerBlock, 1.5f, 0f));
        }

        writer.PlaceGroundAndFinish(block, analysis.finishX);
        writer.RegenerateColliders(log);
        writer.FitCameraBounds(analysis.finishX, 3f);

        var song = LevelSceneWriter.CopySong(level, gdResourcesFolder, log);
        var data = LevelSceneWriter.SaveLevelData(level.name + " Cube", level.name + " (Cube)", Number(header, "kA13", 0f),
                                                  analysis.startSpeed, analysis.speedChanges, analysis.finishX, song);
        writer.WireAndSave(data);
        log.Append($"LevelData: {AssetDatabase.GetAssetPath(data)} (offset {data.songOffset} s)\n");
    }
}
