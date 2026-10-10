using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

public static class PlatformFillSetup
{
    const int Resolution = 16;
    const string RootName = "PlatformFill";
    const string AssetFolder = "Assets/Art/PlatformFill";

    sealed class Region
    {
        public readonly List<int> pixels = new();
        public bool exterior;
        public bool protectedArea;
        public Vector2 min = new(float.MaxValue, float.MaxValue);
        public Vector2 max = new(float.MinValue, float.MinValue);
    }

    sealed class Raster
    {
        public int width, height;
        public Vector2 origin;
        public bool[] walls;
        public int[] labels;
        public readonly List<Region> regions = new();
        public Vector2 Point(int x, int y) => origin + new Vector2((x + 0.5f) / Resolution, (y + 0.5f) / Resolution);
        public int X(float x) => Mathf.FloorToInt((x - origin.x) * Resolution);
        public int Y(float y) => Mathf.FloorToInt((y - origin.y) * Resolution);
    }

    public static string Audit(LevelLandscape landscape)
    {
        var raster = Analyse(landscape);
        var regions = raster.regions.Where(Eligible).ToArray();
        return $"{landscape.gameObject.scene.name}: {regions.Length} enclosed fills, " +
            $"{regions.Sum(r => r.pixels.Count) / (float)(Resolution * Resolution):F2} square units\n" +
            string.Join("\n", regions.Select(r => $"{r.min} to {r.max}; area {r.pixels.Count / (float)(Resolution * Resolution):F2}"));
    }

    public static void Rebuild(LevelLandscape landscape)
    {
        if (landscape == null || landscape.Earth is not Tile fill || fill.sprite == null) return;
        if (!fill.name.StartsWith("Pirates_") && !fill.name.StartsWith("Cyber_")) return;
        var raster = Analyse(landscape);
        if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets/Art", "PlatformFill");
        var parent = landscape.Ground.transform;
        var root = parent.Find(RootName);
        if (root == null)
        {
            var go = new GameObject(RootName, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(go, "Add platform fill");
            go.transform.SetParent(parent, false);
            root = go.transform;
        }
        var mesh = BuildMesh(raster);
        string meshPath = $"{AssetFolder}/{landscape.gameObject.scene.name}_Fill.asset";
        mesh.name = Path.GetFileNameWithoutExtension(meshPath);
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existingMesh == null) AssetDatabase.CreateAsset(mesh, meshPath);
        else
        {
            EditorUtility.CopySerialized(mesh, existingMesh);
            UnityEngine.Object.DestroyImmediate(mesh);
            mesh = existingMesh;
            EditorUtility.SetDirty(mesh);
        }
        string materialPath = $"{AssetFolder}/{fill.name}_Interior.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        string texturePath = $"{AssetFolder}/{fill.name}_Interior.png";
        if (!File.Exists(texturePath))
        {
            File.Copy(AssetDatabase.GetAssetPath(fill.sprite.texture), texturePath);
            AssetDatabase.ImportAsset(texturePath);
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        material.SetTexture("_MainTex", texture);
        material.SetColor("_Color", Color.white);
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null && (importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Repeat || importer.mipmapEnabled))
        {
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        }
        root.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = "Gameplay";
        renderer.sortingOrder = 0;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(root.gameObject);
    }

    static bool Eligible(Region region) => !region.exterior && !region.protectedArea && region.pixels.Count >= Resolution * Resolution / 2;

    static Raster Analyse(LevelLandscape landscape)
    {
        var ground = landscape.Ground;
        var polygons = Polygons(ground);
        if (polygons.Count == 0) return new Raster { width = 1, height = 1, walls = new bool[1], labels = new[] { -1 } };
        var low = new Vector2(float.MaxValue, float.MaxValue);
        var high = new Vector2(float.MinValue, float.MinValue);
        foreach (var polygon in polygons)
            foreach (var point in polygon)
            {
                low = Vector2.Min(low, point);
                high = Vector2.Max(high, point);
            }
        var origin = new Vector2(Mathf.Floor(low.x) - 1, Mathf.Floor(low.y) - 1);
        var raster = new Raster
        {
            origin = origin,
            width = Mathf.CeilToInt((high.x + 1 - origin.x) * Resolution),
            height = Mathf.CeilToInt((high.y + 1 - origin.y) * Resolution)
        };
        raster.walls = new bool[raster.width * raster.height];
        raster.labels = new int[raster.walls.Length];
        Array.Fill(raster.labels, -1);
        foreach (var polygon in polygons) PaintWalls(raster, polygon);
        int ceiling = Mathf.Clamp(raster.Y(high.y - 0.02f), 0, raster.height - 1);
        for (int x = 0; x < raster.width; x++) raster.walls[ceiling * raster.width + x] = true;
        CapOpenTops(raster, polygons);
        var queue = new Queue<int>();
        for (int y = 0; y < raster.height; y++)
            for (int x = 0; x < raster.width; x++)
            {
                int start = y * raster.width + x;
                if (raster.walls[start] || raster.labels[start] >= 0) continue;
                int label = raster.regions.Count;
                var region = new Region();
                raster.regions.Add(region);
                raster.labels[start] = label;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue(), px = index % raster.width, py = index / raster.width;
                    region.pixels.Add(index);
                    var point = raster.Point(px, py);
                    region.min = Vector2.Min(region.min, point);
                    region.max = Vector2.Max(region.max, point);
                    if (px == 0 || py == 0 || px == raster.width - 1 || py == raster.height - 1) region.exterior = true;
                    Visit(px - 1, py);
                    Visit(px + 1, py);
                    Visit(px, py - 1);
                    Visit(px, py + 1);
                }
                void Visit(int px, int py)
                {
                    if (px < 0 || py < 0 || px >= raster.width || py >= raster.height) return;
                    int index = py * raster.width + px;
                    if (raster.walls[index] || raster.labels[index] >= 0) return;
                    raster.labels[index] = label;
                    queue.Enqueue(index);
                }
            }
        if (landscape.Hazards != null)
            foreach (var polygon in Polygons(landscape.Hazards)) Protect(raster, polygon.Min(p => p.x), polygon.Min(p => p.y), polygon.Max(p => p.x), polygon.Max(p => p.y));
        var scene = landscape.gameObject.scene;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
            {
                if (collider.GetComponentInParent<PlayerController>() == null &&
                    collider.GetComponentInParent<JumpOrb>() == null && collider.GetComponentInParent<JumpPad>() == null &&
                    collider.GetComponentInParent<ModePortal>() == null && collider.GetComponentInParent<GravityPortal>() == null &&
                    collider.GetComponentInParent<SpeedPortal>() == null && collider.GetComponentInParent<Coin>() == null &&
                    collider.GetComponentInParent<FinishLine>() == null && collider.GetComponentInParent<EnemyDamage>() == null) continue;
                var min = ground.transform.InverseTransformPoint(collider.bounds.min);
                var max = ground.transform.InverseTransformPoint(collider.bounds.max);
                Protect(raster, min.x, min.y, max.x, max.y);
            }
        return raster;
    }

    static List<Vector2[]> Polygons(Tilemap map)
    {
        var result = new List<Vector2[]>();
        var cache = new Dictionary<Sprite, List<Vector2[]>>();
        foreach (var cell in map.cellBounds.allPositionsWithin)
        {
            var tile = map.GetTile<Tile>(cell);
            if (tile == null || tile.sprite == null) continue;
            if (!cache.TryGetValue(tile.sprite, out var shapes))
            {
                shapes = new List<Vector2[]>();
                for (int i = 0; i < tile.sprite.GetPhysicsShapeCount(); i++)
                {
                    var points = new List<Vector2>();
                    tile.sprite.GetPhysicsShape(i, points);
                    if (points.Count >= 3) shapes.Add(points.ToArray());
                }
                cache.Add(tile.sprite, shapes);
            }
            var matrix = Matrix4x4.Translate(map.GetCellCenterLocal(cell)) * map.GetTransformMatrix(cell);
            foreach (var shape in shapes) result.Add(shape.Select(p => (Vector2)matrix.MultiplyPoint3x4(p)).ToArray());
        }
        return result;
    }

    static void PaintWalls(Raster raster, Vector2[] polygon)
    {
        const float padding = 0.045f;
        int x0 = Mathf.Max(0, raster.X(polygon.Min(p => p.x) - padding));
        int y0 = Mathf.Max(0, raster.Y(polygon.Min(p => p.y) - padding));
        int x1 = Mathf.Min(raster.width - 1, raster.X(polygon.Max(p => p.x) + padding));
        int y1 = Mathf.Min(raster.height - 1, raster.Y(polygon.Max(p => p.y) + padding));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var point = raster.Point(x, y);
                bool inside = false, near = false;
                for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                {
                    var a = polygon[j];
                    var b = polygon[i];
                    if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                    var delta = b - a;
                    float t = delta.sqrMagnitude == 0 ? 0 : Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
                    if ((point - (a + t * delta)).sqrMagnitude <= padding * padding) near = true;
                }
                if (inside || near) raster.walls[y * raster.width + x] = true;
            }
    }

    static void CapOpenTops(Raster raster, List<Vector2[]> polygons)
    {
        var strips = polygons.Select(p => new Rect(p.Min(v => v.x), p.Min(v => v.y), p.Max(v => v.x) - p.Min(v => v.x), p.Max(v => v.y) - p.Min(v => v.y)))
            .Where(r => r.width <= 0.2f && r.height >= 0.5f)
            .GroupBy(r => Mathf.RoundToInt(r.center.x * Resolution));
        var runs = new List<Rect>();
        foreach (var column in strips)
        {
            var ordered = column.OrderBy(r => r.yMin).ToArray();
            var run = ordered[0];
            for (int i = 1; i < ordered.Length; i++)
            {
                if (ordered[i].yMin <= run.yMax + 0.1f) run.yMax = Mathf.Max(run.yMax, ordered[i].yMax);
                else { runs.Add(run); run = ordered[i]; }
            }
            runs.Add(run);
        }
        foreach (var row in runs.Where(r => r.height >= 1.5f).GroupBy(r => Mathf.RoundToInt(r.yMax * Resolution)))
        {
            var ordered = row.OrderBy(r => r.xMin).ToArray();
            for (int i = 0; i + 1 < ordered.Length; i++)
            {
                var left = ordered[i];
                var right = ordered[i + 1];
                if (Mathf.Abs(left.yMin - right.yMin) > 0.1f || right.xMin - left.xMax > 64f) continue;
                int y = Mathf.Clamp(raster.Y(left.yMax - 0.025f), 0, raster.height - 1);
                for (int x = Mathf.Max(0, raster.X(left.xMin)); x <= Mathf.Min(raster.width - 1, raster.X(right.xMax)); x++) raster.walls[y * raster.width + x] = true;
            }
        }
    }

    static void Protect(Raster raster, float minX, float minY, float maxX, float maxY)
    {
        for (int y = Mathf.Max(0, raster.Y(minY)); y <= Mathf.Min(raster.height - 1, raster.Y(maxY)); y++)
            for (int x = Mathf.Max(0, raster.X(minX)); x <= Mathf.Min(raster.width - 1, raster.X(maxX)); x++)
            {
                int label = raster.labels[y * raster.width + x];
                if (label >= 0) raster.regions[label].protectedArea = true;
            }
    }

    static Mesh BuildMesh(Raster raster)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var colors = new List<Color32>();
        var triangles = new List<int>();
        var eligible = raster.regions.Select(Eligible).ToArray();
        for (int y = 0; y < raster.height; y++)
            for (int x = 0; x < raster.width; x++)
            {
                int label = raster.labels[y * raster.width + x];
                if (label < 0 || !eligible[label]) continue;
                int start = x;
                while (x + 1 < raster.width && raster.labels[y * raster.width + x + 1] == label) x++;
                float x0 = raster.origin.x + start / (float)Resolution, x1 = raster.origin.x + (x + 1) / (float)Resolution;
                float y0 = raster.origin.y + y / (float)Resolution, y1 = y0 + 1f / Resolution;
                int index = vertices.Count;
                foreach (var point in new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) })
                {
                    vertices.Add(point);
                    uv.Add(point);
                    colors.Add(new Color32(205, 205, 205, 255));
                }
                triangles.AddRange(new[] { index, index + 2, index + 1, index, index + 3, index + 2 });
            }
        var mesh = new Mesh { name = "PlatformInterior", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
