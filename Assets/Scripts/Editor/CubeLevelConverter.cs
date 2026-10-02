using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GDLevelParser;

public static class CubeLevelConverter
{
    public const float SectionBlocks = 10f;
    const float BlocksAfterLastObject = 12f;
    const float QuietStartSeconds = 1.5f;
    const float SettleAfterSpeedChange = 2f;

    public enum AccentKind { Solid, Hazard, Pad, Orb }

    public class Accent
    {
        public float x;
        public float score;
        public AccentKind kind;
    }

    public class Analysis
    {
        public List<Accent>[] accents;
        public int[] playableCount;
        public float finishX;
        public List<(float x, float speed)> speedChanges;
        public float startSpeed;
        public List<(float x, string mode)> modeChanges;
        public int Sections => accents.Length;
    }

    static readonly Dictionary<int, string> ModeNames = new Dictionary<int, string>
    {
        { CubePortal, "cube" }, { 13, "ship" }, { 47, "ball" }, { 111, "ufo" }, { 660, "wave" }, { 745, "robot" }, { 1331, "spider" }, { 1933, "swing" },
    };

    public static Analysis Analyze(List<GDObject> objects, Dictionary<string, string> header, GDObjectMap map)
    {
        var bins = new Dictionary<float, Accent>();
        float lastX = 0f;
        foreach (var o in objects)
        {
            var kind = Classify(map.Find(o.id));
            if (kind == null) continue;
            float x = o.x / UnitsPerBlock;
            lastX = Mathf.Max(lastX, x);
            float key = Mathf.Round(x * 2f) / 2f;
            if (!bins.TryGetValue(key, out var accent)) bins[key] = accent = new Accent { x = key, kind = AccentKind.Solid };
            accent.score += kind == AccentKind.Orb || kind == AccentKind.Pad ? 4f : kind == AccentKind.Hazard ? 2f : 0.5f;
            if (kind.Value > accent.kind) accent.kind = kind.Value;
        }

        float finishX = lastX + BlocksAfterLastObject;
        int sections = Mathf.CeilToInt(finishX / SectionBlocks);
        var analysis = new Analysis
        {
            accents = Enumerable.Range(0, sections).Select(_ => new List<Accent>()).ToArray(),
            playableCount = new int[sections],
            finishX = finishX,
            speedChanges = LevelSceneWriter.SpeedChanges(objects, map),
            startSpeed = StartSpeed(header),
            modeChanges = objects.Where(o => ModeNames.ContainsKey(o.id))
                                 .OrderBy(o => Mathf.Round(o.x)).ThenBy(o => o.id == CubePortal ? 1 : 0)
                                 .Select(o => (o.x / UnitsPerBlock, ModeNames[o.id])).ToList(),
        };
        foreach (var accent in bins.Values)
        {
            int section = Mathf.Clamp(Mathf.FloorToInt(accent.x / SectionBlocks), 0, sections - 1);
            analysis.accents[section].Add(accent);
        }
        foreach (var o in objects)
            if (Classify(map.Find(o.id)) != null)
                analysis.playableCount[Mathf.Clamp(Mathf.FloorToInt(o.x / UnitsPerBlock / SectionBlocks), 0, sections - 1)]++;
        return analysis;
    }

    static AccentKind? Classify(GDObjectMap.Entry entry)
    {
        if (entry == null) return null;
        switch (entry.category)
        {
            case GDObjectMap.Category.Solid: return AccentKind.Solid;
            case GDObjectMap.Category.Hazard: return AccentKind.Hazard;
            case GDObjectMap.Category.Object:
                if (entry.prefab.TryGetComponent<JumpOrb>(out _)) return AccentKind.Orb;
                if (entry.prefab.TryGetComponent<JumpPad>(out _)) return AccentKind.Pad;
                return null;
            default: return null;
        }
    }

    public static CubeLayout Generate(Analysis a, int section, CubeConversionSettings settings, LevelData timeline,
                                      CubeBot.Physics physics, float previousEndX, float nextStartX, int attempt)
    {
        var layout = new CubeLayout();
        int count = a.playableCount[section];
        if (count == 0 || attempt >= 4) return layout;

        var rng = new System.Random(unchecked(settings.seed * 73856093 ^ section * 19349663 ^ attempt * 83492791));
        int target = Mathf.RoundToInt(settings.density * Mathf.Sqrt(count) * 0.5f * (1f - 0.25f * attempt));
        var candidates = a.accents[section].OrderByDescending(c => c.score + rng.NextDouble() * 0.01).ToList();
        var placed = new List<(float start, float end)>();

        foreach (var accent in candidates)
        {
            if (layout.ObstacleCount >= target) break;
            if (timeline.TimeAtX(accent.x) < QuietStartSeconds) continue;

            var piece = new CubeLayout();
            Build(piece, accent, timeline, physics, rng, attempt);
            float start = piece.StartX, end = piece.EndX;
            if (end > a.finishX - 2f) continue;
            if (!FarEnough(timeline, settings.minGapSeconds, start, end, previousEndX, nextStartX, placed)) continue;

            placed.Add((start, end));
            layout.Solids.AddRange(piece.Solids);
            layout.Spikes.AddRange(piece.Spikes);
            layout.Pads.AddRange(piece.Pads);
            layout.Orbs.AddRange(piece.Orbs);
            layout.Extend(start, end);
            layout.ObstacleCount++;
        }
        return layout;
    }

    public static float RequiredGap(LevelData timeline, float minGap, float t)
    {
        for (int i = 1; i < timeline.changeTimes.Length; i++)
        {
            float since = t - timeline.changeTimes[i];
            if (since >= 0f && since <= SettleAfterSpeedChange) return minGap * 2f;
        }
        return minGap;
    }

    static bool FarEnough(LevelData timeline, float gap, float start, float end, float previousEndX, float nextStartX,
                          List<(float start, float end)> placed)
    {
        float tStart = timeline.TimeAtX(start), tEnd = timeline.TimeAtX(end);
        if (previousEndX > float.NegativeInfinity && tStart - timeline.TimeAtX(previousEndX) < RequiredGap(timeline, gap, tStart)) return false;
        if (nextStartX < float.PositiveInfinity)
        {
            float tNext = timeline.TimeAtX(nextStartX);
            if (tNext - tEnd < RequiredGap(timeline, gap, tNext)) return false;
        }
        foreach (var (s, e) in placed)
        {
            float ts = timeline.TimeAtX(s), te = timeline.TimeAtX(e);
            bool before = te <= tStart;
            if (before ? tStart - te < RequiredGap(timeline, gap, tStart) : ts - tEnd < RequiredGap(timeline, gap, ts)) return false;
        }
        return true;
    }

    public static CubeLayout[] ReadSections(LevelSceneWriter writer, int count)
    {
        var sections = Enumerable.Range(0, count).Select(_ => new CubeLayout()).ToArray();
        CubeLayout At(float x) => sections[Mathf.Clamp(Mathf.FloorToInt(x / SectionBlocks), 0, count - 1)];

        foreach (var cell in writer.Ground.cellBounds.allPositionsWithin)
            if (cell.y >= 0 && writer.Ground.HasTile(cell)) { var s = At(cell.x); s.Solids.Add((Vector2Int)cell); s.Extend(cell.x, cell.x + 1); }
        foreach (var cell in writer.Hazards.cellBounds.allPositionsWithin)
            if (writer.Hazards.HasTile(cell)) { var s = At(cell.x); s.Spikes.Add((Vector2Int)cell); s.Extend(cell.x, cell.x + 1); }
        foreach (Transform child in writer.Objects)
        {
            Vector2 position = child.position;
            if (child.TryGetComponent<JumpPad>(out _)) { var s = At(position.x); s.Pads.Add(position); s.Extend(position.x - 0.5f, position.x + 0.5f); }
            if (child.TryGetComponent<JumpOrb>(out _)) { var s = At(position.x); s.Orbs.Add(position); s.Extend(position.x - 0.5f, position.x + 0.5f); }
        }
        foreach (var s in sections) s.ObstacleCount = Patterns(new[] { s }).Count;
        return sections;
    }

    public static List<int> GapViolations(CubeLayout[] sections, LevelData timeline, float minGap)
    {
        var violations = new SortedSet<int>();
        var patterns = Patterns(sections);
        for (int i = 1; i < patterns.Count; i++)
        {
            float tStart = timeline.TimeAtX(patterns[i].start);
            if (tStart - timeline.TimeAtX(patterns[i - 1].end) < RequiredGap(timeline, minGap, tStart))
                violations.Add(Mathf.Clamp(Mathf.FloorToInt(patterns[i].start / SectionBlocks), 0, sections.Length - 1));
        }
        return violations.ToList();
    }

    static List<(float start, float end)> Patterns(IEnumerable<CubeLayout> sections)
    {
        var elements = new List<(float start, float end)>();
        foreach (var s in sections)
        {
            elements.AddRange(s.Solids.Concat(s.Spikes).Select(c => ((float)c.x, c.x + 1f)));
            elements.AddRange(s.Pads.Concat(s.Orbs).Select(p => (p.x - 0.5f, p.x + 0.5f)));
        }
        elements.Sort((a, b) => a.start.CompareTo(b.start));
        var patterns = new List<(float start, float end)>();
        foreach (var e in elements)
        {
            if (patterns.Count > 0 && e.start - patterns[^1].end <= 1f)
                patterns[^1] = (patterns[^1].start, Mathf.Max(patterns[^1].end, e.end));
            else patterns.Add(e);
        }
        return patterns;
    }

    static void Build(CubeLayout piece, Accent accent, LevelData timeline, CubeBot.Physics physics, System.Random rng, int attempt)
    {
        int c = Mathf.FloorToInt(accent.x);
        float speed = timeline.SpeedAtTime(timeline.TimeAtX(accent.x));
        float jumpDistance = speed * physics.jumpDuration;

        switch (accent.kind)
        {
            case AccentKind.Orb:
            {
                int length = Mathf.Clamp(Mathf.CeilToInt(jumpDistance * (1.2f - 0.1f * attempt)), 3, 8);
                for (int i = 0; i < length; i++) piece.Spikes.Add(new Vector2Int(c + i, 0));
                piece.Orbs.Add(new Vector2(c + length * 0.5f, 2.5f));
                piece.Extend(c, c + length);
                return;
            }
            case AccentKind.Pad:
            {
                float padDistance = speed * 2f * physics.padVelocity / -physics.gravity;
                int length = Mathf.Clamp(Mathf.RoundToInt(padDistance * 0.5f), 2, 5);
                piece.Pads.Add(new Vector2(c + 0.5f, 0.15f));
                for (int i = 0; i < length; i++) piece.Spikes.Add(new Vector2Int(c + 2 + i, 0));
                piece.Extend(c, c + 2 + length);
                return;
            }
        }

        double roll = rng.NextDouble();
        if (accent.kind == AccentKind.Solid) roll = 0.6 + roll * 0.4;
        if (roll < 0.35) { piece.Spikes.Add(new Vector2Int(c, 0)); piece.Extend(c, c + 1); }
        else if (roll < 0.55) { piece.Spikes.Add(new Vector2Int(c, 0)); piece.Spikes.Add(new Vector2Int(c + 1, 0)); piece.Extend(c, c + 2); }
        else if (roll < 0.7) { piece.Solids.Add(new Vector2Int(c, 0)); piece.Spikes.Add(new Vector2Int(c + 2, 0)); piece.Extend(c, c + 3); }
        else if (roll < 0.85) { piece.Solids.Add(new Vector2Int(c, 0)); piece.Extend(c, c + 1); }
        else
        {
            int length = 3 + rng.Next(3);
            for (int i = 0; i < length; i++) piece.Solids.Add(new Vector2Int(c + i, 0));
            piece.Extend(c, c + length);
        }
    }

    public static string ModeAt(Analysis a, float x)
    {
        string mode = "cube";
        foreach (var (mx, name) in a.modeChanges)
        {
            if (mx > x) break;
            mode = name;
        }
        return mode;
    }
}
