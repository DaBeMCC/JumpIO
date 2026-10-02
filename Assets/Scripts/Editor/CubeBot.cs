using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CubeBot
{
    public struct Physics
    {
        public float dt, gravity, jumpVelocity, halfSize, jumpDuration;
        public float padVelocity, orbVelocity, orbRadius;
        public Vector2 padSize;
        public Rect spikeShape;
        public float scheduleLead, snapThreshold, correctionRate;
        public float audioStep;

        public static Physics FromProject(GDObjectMap map, MusicConductor conductor)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            var pc = new SerializedObject(player.GetComponent<PlayerController>());
            float jump = pc.FindProperty("cube.jumpVelocity").floatValue;
            float gravity = Physics2D.gravity.y * pc.FindProperty("gravityScale").floatValue;

            var pad = map.Find(35).prefab;
            var orb = map.Find(36).prefab;
            var spikeSprite = ((UnityEngine.Tilemaps.Tile)map.Find(8).tile).sprite;
            var shape = new List<Vector2>();
            spikeSprite.GetPhysicsShape(0, shape);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var point in shape) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            var music = new SerializedObject(conductor);
            var audio = AudioSettings.GetConfiguration();

            return new Physics
            {
                dt = Time.fixedDeltaTime,
                gravity = gravity,
                jumpVelocity = jump,
                jumpDuration = 2f * jump / -gravity,
                halfSize = player.GetComponent<BoxCollider2D>().size.x / 2f,
                padVelocity = pad.GetComponent<JumpPad>().LaunchVelocity(PlayerModeType.Cube),
                padSize = pad.GetComponent<BoxCollider2D>().size,
                orbVelocity = orb.GetComponent<JumpOrb>().LaunchVelocity(PlayerModeType.Cube),
                orbRadius = orb.GetComponent<CircleCollider2D>().radius,
                spikeShape = new Rect(min + new Vector2(0.5f, 0.5f), max - min),
                scheduleLead = (float)music.FindProperty("scheduleLead").doubleValue,
                snapThreshold = music.FindProperty("snapThreshold").floatValue,
                correctionRate = music.FindProperty("correctionRate").floatValue,
                audioStep = audio.dspBufferSize / (float)audio.sampleRate,
            };
        }
    }

    public class ClockModel
    {
        readonly Physics p;
        readonly double phase, frameInterval, offset, audioRate;
        readonly List<float> clocks = new List<float>();
        double clock, lastReported = -1, lastReportedAt, rate = 1, rateAnchorAudio = double.NaN, rateAnchorTime;

        public ClockModel(Physics physics, double phase = 0, double frameRate = 0, double offset = 0, double audioRate = 1)
        {
            p = physics;
            this.offset = offset;
            this.phase = phase;
            this.audioRate = audioRate;
            frameInterval = frameRate > 0 ? 1.0 / frameRate : 0;
        }

        public float At(int step)
        {
            while (clocks.Count <= step) Advance();
            return clocks[step] + (float)offset;
        }

        void Advance()
        {
            int n = clocks.Count;
            if (n == 0) { clock = -p.scheduleLead; clocks.Add((float)clock); return; }

            clock += p.dt * rate;
            double fixedNow = n * (double)p.dt;
            double real = frameInterval > 0 ? System.Math.Ceiling(fixedNow / frameInterval) * frameInterval : fixedNow;
            double played = (real - p.scheduleLead) * audioRate;
            if (played >= 0)
            {
                double reported = System.Math.Max(0, (System.Math.Floor(played / p.audioStep + phase) - phase) * p.audioStep);
                if (reported != lastReported) { lastReported = reported; lastReportedAt = fixedNow; }
                double estimate = reported + System.Math.Min(fixedNow - lastReportedAt, p.audioStep);
                if (double.IsNaN(rateAnchorAudio)) { rateAnchorAudio = estimate; rateAnchorTime = fixedNow; }
                else if (fixedNow - rateAnchorTime >= 1.0)
                    rate = System.Math.Clamp((estimate - rateAnchorAudio) / (fixedNow - rateAnchorTime), 0.9, 1.1);
                double drift = estimate - clock;
                if (System.Math.Abs(drift) > p.snapThreshold) { clock = estimate; rateAnchorAudio = double.NaN; }
                else clock += drift * p.correctionRate;
            }
            clocks.Add((float)clock);
        }
    }

    const float HazardMargin = 0.12f;
    const float LandTolerance = 0.05f;
    const float SideSkin = 0.15f;
    const int Substeps = 4;
    const float GroundProbe = 0.30f;

    struct State
    {
        public int step;
        public float x, y, vy;
        public bool grounded;
        public int lastPad, lastOrb;
    }

    struct Node
    {
        public State state;
        public int parent;
        public bool jump, click;
    }

    enum Outcome { Alive, Dead, Finished }

    readonly Physics p;
    readonly LevelData timeline;
    readonly ClockModel clockModel;
    readonly float finishX;
    readonly HashSet<Vector2Int> solids = new HashSet<Vector2Int>();
    readonly Dictionary<int, List<Rect>> hazards = new Dictionary<int, List<Rect>>();
    readonly List<Rect> pads = new List<Rect>();
    readonly List<Vector2> orbs = new List<Vector2>();
    readonly Dictionary<int, List<int>> padsByColumn = new Dictionary<int, List<int>>();
    readonly Dictionary<int, List<int>> orbsByColumn = new Dictionary<int, List<int>>();

    public float FurthestX { get; private set; }
    public List<float> JumpXs { get; } = new List<float>();
    public List<float> OrbXs { get; } = new List<float>();

    public CubeBot(Physics physics, ClockModel clock, LevelData timeline, float finishX, IEnumerable<CubeLayout> layouts)
    {
        p = physics;
        clockModel = clock;
        this.timeline = timeline;
        this.finishX = finishX;
        foreach (var layout in layouts)
        {
            foreach (var cell in layout.Solids) solids.Add(cell);
            foreach (var cell in layout.Spikes)
            {
                var r = new Rect(cell.x + p.spikeShape.x - HazardMargin, cell.y + p.spikeShape.y - HazardMargin,
                                 p.spikeShape.width + 2 * HazardMargin, p.spikeShape.height + 2 * HazardMargin);
                Add(hazards, cell.x, r);
            }
            foreach (var pad in layout.Pads)
            {
                pads.Add(new Rect(pad - p.padSize / 2f, p.padSize));
                Add(padsByColumn, Mathf.FloorToInt(pad.x), pads.Count - 1);
            }
            foreach (var orb in layout.Orbs)
            {
                orbs.Add(orb);
                Add(orbsByColumn, Mathf.FloorToInt(orb.x), orbs.Count - 1);
            }
        }
    }

    static void Add<T>(Dictionary<int, List<T>> index, int column, T value)
    {
        if (!index.TryGetValue(column, out var list)) index[column] = list = new List<T>();
        list.Add(value);
    }

    public bool Solve(int maxNodes = 3_000_000)
    {
        var nodes = new List<Node>();
        var stack = new Stack<int>();
        var visited = new HashSet<long>();
        var start = new State { x = timeline.XAtTime(0f), y = 0.5f, lastPad = -1, lastOrb = -1 };
        nodes.Add(new Node { state = start, parent = -1 });
        stack.Push(0);
        FurthestX = start.x;

        while (stack.Count > 0 && nodes.Count < maxNodes)
        {
            int index = stack.Pop();
            var node = nodes[index];
            var s = node.state;
            bool press = node.jump, click = node.click;

            while (true)
            {
                var before = s;
                var outcome = Step(ref s, press, click, out int orbAvailable);
                press = click = false;
                if (outcome == Outcome.Dead) break;
                if (outcome == Outcome.Finished) { Reconstruct(nodes, index); return true; }
                FurthestX = Mathf.Max(FurthestX, s.x);

                if (orbAvailable >= 0)
                {
                    var skipped = before; skipped.lastOrb = orbAvailable;
                    nodes.Add(new Node { state = skipped, parent = index });
                    stack.Push(nodes.Count - 1);
                    nodes.Add(new Node { state = before, parent = index, click = true });
                    stack.Push(nodes.Count - 1);
                    break;
                }
                if (s.grounded)
                {
                    if (!visited.Add(((long)s.step << 20) | (uint)Mathf.RoundToInt(s.y * 64f + 4096f))) break;
                    nodes.Add(new Node { state = s, parent = index, jump = true });
                    stack.Push(nodes.Count - 1);
                    nodes.Add(new Node { state = s, parent = index });
                    stack.Push(nodes.Count - 1);
                    break;
                }
            }
        }
        return false;
    }

    void Reconstruct(List<Node> nodes, int index)
    {
        for (int i = index; i >= 0; i = nodes[i].parent)
        {
            if (nodes[i].jump) JumpXs.Add(nodes[i].state.x);
            if (nodes[i].click) OrbXs.Add(nodes[i].state.x);
        }
        JumpXs.Reverse();
        OrbXs.Reverse();
    }

    public const int HoldSteps = 6;

    public bool Verify(List<float> jumps, List<float> clicks, out float failX, float stopX = float.PositiveInfinity)
    {
        var s = new State { x = timeline.XAtTime(0f), y = 0.5f, lastPad = -1, lastOrb = -1 };
        int nextJump = 0, nextClick = 0, hold = 0;
        while (s.x < stopX)
        {
            float half = timeline.SpeedAtTime(Mathf.Max(0f, clockModel.At(s.step))) * p.dt * 0.5f;
            while (nextJump < jumps.Count && s.x > jumps[nextJump] + 0.5f) nextJump++;
            while (nextClick < clicks.Count && s.x > clicks[nextClick] + 0.5f) nextClick++;
            if (nextJump < jumps.Count && s.x >= jumps[nextJump] - half) { nextJump++; hold = HoldSteps; }
            bool click = nextClick < clicks.Count && s.x >= clicks[nextClick] - half;
            if (click) nextClick++;

            bool press = hold > 0, jumpedNow = press && s.grounded;
            var outcome = Step(ref s, press || click, click, out _);
            hold = jumpedNow ? 0 : Mathf.Max(0, hold - 1);
            if (outcome == Outcome.Finished) { failX = float.NaN; return true; }
            if (outcome == Outcome.Dead) { failX = s.x; return false; }
        }
        failX = float.NaN;
        return true;
    }

    public void CenterJumps(int maxShiftSteps = 24)
    {
        var jumps = JumpXs;
        for (int i = 0; i < jumps.Count; i++)
        {
            float original = jumps[i];
            float step = timeline.SpeedAtTime(timeline.TimeAtX(original)) * p.dt;
            float stopX = (i + 2 < jumps.Count ? jumps[i + 2] : finishX) + 5f;
            int earliest = 0;
            for (int k = 1; k <= maxShiftSteps; k++)
            {
                jumps[i] = original - k * step;
                if (i > 0 && jumps[i] <= jumps[i - 1] + step) break;
                if (!Verify(jumps, OrbXs, out _, stopX)) break;
                earliest = k;
            }
            jumps[i] = original - earliest / 2 * step;
        }
    }

    Outcome Step(ref State s, bool press, bool click, out int orbAvailable)
    {
        orbAvailable = -1;
        s.step++;
        float clock = clockModel.At(s.step);

        if (s.grounded && press) { s.vy = p.jumpVelocity; s.grounded = false; }
        s.vy += p.gravity * p.dt;
        float previousX = s.x, previousY = s.y, previousBottom = s.y - p.halfSize;
        s.y += s.vy * p.dt;
        s.x = timeline.XAtTime(Mathf.Max(0f, clock));
        float h = p.halfSize;

        float landTop = float.NegativeInfinity;
        bool falling = s.vy <= 0f, crashed = false;
        for (int k = 1; k <= Substeps && !crashed; k++)
        {
            float f = k / (float)Substeps;
            float x = Mathf.Lerp(previousX, s.x, f), y = Mathf.Lerp(previousY, s.y, f);
            ForEachSolid(x, y, h + SideSkin, h - 0.005f, cell =>
            {
                float top = cell.y + 1f;
                bool under = cell.x + 1f > x - h && cell.x < x + h;
                if (falling && under && previousBottom >= top - LandTolerance) landTop = Mathf.Max(landTop, top);
                else if (top - (y - h) > LandTolerance) crashed = true;
            });
        }
        if (crashed) return Outcome.Dead;
        bool landed = landTop > float.NegativeInfinity;
        if (landed) { s.y = landTop + h; s.vy = 0f; }

        s.grounded = landed && HasSupport(s.x, landTop);

        var box = new Rect(s.x - h, s.y - h, 2 * h, 2 * h);
        for (int c = Mathf.FloorToInt(box.xMin) - 1; c <= Mathf.FloorToInt(box.xMax) + 1; c++)
        {
            if (hazards.TryGetValue(c, out var list))
                foreach (var r in list) if (r.Overlaps(box)) return Outcome.Dead;

            if (padsByColumn.TryGetValue(c, out var padIds))
                foreach (int id in padIds)
                    if (pads[id].Overlaps(box) && id != s.lastPad) { s.vy = p.padVelocity; s.grounded = false; s.lastPad = id; }

            if (orbsByColumn.TryGetValue(c, out var orbIds))
                foreach (int id in orbIds)
                {
                    if (id == s.lastOrb || !CircleOverlaps(orbs[id], p.orbRadius, box)) continue;
                    if (click) { s.vy = p.orbVelocity; s.grounded = false; s.lastOrb = id; }
                    else orbAvailable = id;
                }
        }
        return s.x >= finishX ? Outcome.Finished : Outcome.Alive;
    }

    void ForEachSolid(float x, float y, float halfWidth, float halfHeight, System.Action<Vector2Int> action)
    {
        const float e = 0.001f;
        for (int cx = Mathf.FloorToInt(x - halfWidth + e); cx <= Mathf.FloorToInt(x + halfWidth - e); cx++)
            for (int cy = Mathf.FloorToInt(y - halfHeight + e); cy <= Mathf.FloorToInt(y + halfHeight - e); cy++)
            {
                var cell = new Vector2Int(cx, cy);
                if (cy == -1 || solids.Contains(cell)) action(cell);
            }
    }

    bool HasSupport(float x, float top)
    {
        int row = Mathf.RoundToInt(top) - 1;
        for (int cx = Mathf.FloorToInt(x - GroundProbe); cx <= Mathf.FloorToInt(x + GroundProbe); cx++)
            if (row == -1 || solids.Contains(new Vector2Int(cx, row))) return true;
        return false;
    }

    static bool CircleOverlaps(Vector2 center, float radius, Rect box)
    {
        float dx = center.x - Mathf.Clamp(center.x, box.xMin, box.xMax);
        float dy = center.y - Mathf.Clamp(center.y, box.yMin, box.yMax);
        return dx * dx + dy * dy <= radius * radius;
    }
}
