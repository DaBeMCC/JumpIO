using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using static GDLevelParser;

public class LevelSceneWriter
{
    const string FinishPrefabPath = "Assets/Prefabs/FinishLine.prefab";
    const string LevelDataFolder = "Assets/Data";
    const string MusicFolder = "Assets/Audio/Music";

    public Tilemap Ground { get; private set; }
    public Tilemap Hazards { get; private set; }
    public Tilemap Background { get; private set; }
    public Transform Objects { get; private set; }
    PolygonCollider2D cameraBounds;
    PlayerController player;
    MusicConductor conductor;

    public static LevelSceneWriter FromActiveScene()
    {
        var w = new LevelSceneWriter
        {
            Ground = Find<Tilemap>("Ground_Tilemap"),
            Hazards = Find<Tilemap>("Hazards_Tilemap"),
            Background = Find<Tilemap>("Background_Tilemap"),
            Objects = GameObject.Find("Objects")?.transform,
            cameraBounds = Find<PolygonCollider2D>("CameraBounds"),
            player = UnityEngine.Object.FindAnyObjectByType<PlayerController>(),
            conductor = UnityEngine.Object.FindAnyObjectByType<MusicConductor>(),
        };
        bool complete = w.Ground && w.Hazards && w.Background && w.Objects && w.cameraBounds && w.player && w.conductor;
        if (!complete)
            EditorUtility.DisplayDialog("JumpIO", "The active scene lacks the Level_01 structure " +
                "(Grid with the 3 tilemaps, Objects, CameraBounds, Player and Music).", "OK");
        return complete ? w : null;
    }

    public void Clear()
    {
        foreach (var tm in new[] { Ground, Hazards, Background }) tm.ClearAllTiles();
        for (int i = Objects.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(Objects.GetChild(i).gameObject);
    }

    public GameObject Instantiate(GameObject prefab, Vector3 position)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Objects);
        go.transform.position = position;
        return go;
    }

    public void PlaceGroundAndFinish(TileBase block, float finishX)
    {
        for (int x = -20; x <= Mathf.CeilToInt(finishX) + 20; x++)
        {
            Ground.SetTile(new Vector3Int(x, -1, 0), block);
            // Se reinicia la matriz: SetTile conserva la anterior y dejaba casillas del suelo escaladas como plataformas finas.
            Ground.SetTransformMatrix(new Vector3Int(x, -1, 0), Matrix4x4.identity);
            for (int y = -8; y <= -2; y++)
            {
                Background.SetTile(new Vector3Int(x, y, 0), block);
                Background.SetTransformMatrix(new Vector3Int(x, y, 0), Matrix4x4.identity);
            }
        }
        Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FinishPrefabPath), new Vector3(finishX, 1.5f, 0f));
    }

    public void RegenerateColliders(StringBuilder log)
    {
        foreach (var tm in new[] { Ground, Hazards })
        {
            if (tm.TryGetComponent(out TilemapCollider2D collider)) collider.ProcessTilemapChanges();
            if (!tm.TryGetComponent(out CompositeCollider2D composite)) continue;
            composite.GenerateGeometry();
            if (composite.shapeCount == 0)
                log.Append($"WARNING: {tm.name} has no composite shapes; check Composite Operation = Merge.\n");
        }
    }

    public void FitCameraBounds(float finishX, float maxY)
    {
        float top = Mathf.Max(maxY + 5f, 8f);
        cameraBounds.pathCount = 1;
        cameraBounds.SetPath(0, new[]
        {
            new Vector2(-10f, -8f), new Vector2(finishX + 10f, -8f),
            new Vector2(finishX + 10f, top), new Vector2(-10f, top),
        });
        EditorUtility.SetDirty(cameraBounds);
    }

    public static List<(float x, float speed)> SpeedChanges(List<GDObject> objects, GDObjectMap map, float maxX = float.PositiveInfinity)
    {
        var changes = new List<(float x, float speed)>();
        foreach (var o in objects)
        {
            if (o.x >= maxX) continue;
            var entry = map.Find(o.id);
            if (entry?.prefab != null && entry.prefab.TryGetComponent(out SpeedPortal portal))
                changes.Add((o.x / UnitsPerBlock - 1f, portal.Speed));
        }
        changes.Sort((a, b) => a.x.CompareTo(b.x));
        return changes;
    }

    public static LevelData SaveLevelData(string fileName, string levelName, float songOffset, float startSpeed,
                                          List<(float x, float speed)> speedChanges, float finishX, AudioClip song)
    {
        Directory.CreateDirectory(LevelDataFolder);
        string path = $"{LevelDataFolder}/{FileName(fileName)}_LevelData.asset";
        var data = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.levelName = levelName;
        data.songOffset = songOffset;
        data.length = finishX;
        data.changeX = new[] { 0f }.Concat(speedChanges.Select(c => c.x)).ToArray();
        data.speeds = new[] { startSpeed }.Concat(speedChanges.Select(c => c.speed)).ToArray();
        data.RecalculateTimes();
        data.song = song;
        EditorUtility.SetDirty(data);
        return data;
    }

    public static AudioClip CopySong(GDLevel level, string gdResourcesFolder, StringBuilder log)
    {
        string gdData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GeometryDash");
        string[] candidates;
        string description;
        if (level.customSong > 0)
        {
            int id = level.customSong;
            description = "custom song " + id;
            candidates = new[]
            {
                Path.Combine(gdData, id + ".mp3"), Path.Combine(gdData, id + ".ogg"),
                Path.Combine(gdResourcesFolder, "songs", id + ".ogg"), Path.Combine(gdResourcesFolder, "songs", id + ".mp3"),
            };
        }
        else if (level.officialTrack >= 0 && level.officialTrack < OfficialTracks.Length)
        {
            description = $"official track {level.officialTrack} ({OfficialTracks[level.officialTrack]})";
            candidates = new[] { Path.Combine(gdResourcesFolder, OfficialTracks[level.officialTrack] + ".mp3") };
        }
        else
        {
            log.Append("Song: the level names none; AudioSource left empty.\n");
            return null;
        }

        string source = candidates.FirstOrDefault(File.Exists);
        if (source == null)
        {
            log.Append($"Song: MISSING {description}. Looked in:\n  {string.Join("\n  ", candidates)}\nAudioSource left empty.\n");
            return null;
        }

        Directory.CreateDirectory(MusicFolder);
        string destination = $"{MusicFolder}/{Path.GetFileName(source)}";
        File.Copy(source, destination, true);
        AssetDatabase.ImportAsset(destination);
        var importer = (AudioImporter)AssetImporter.GetAtPath(destination);
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
        log.Append($"Song: {description} copied to {destination} (Streaming, Vorbis).\n");
        return AssetDatabase.LoadAssetAtPath<AudioClip>(destination);
    }

    public void WireAndSave(LevelData data)
    {
        player.transform.position = new Vector3(data.changeX[0], 0.5f, 0f);
        var sp = new SerializedObject(player);
        sp.FindProperty("speed").floatValue = data.speeds[0];
        sp.FindProperty("conductor").objectReferenceValue = conductor;
        sp.ApplyModifiedProperties();

        var sc = new SerializedObject(conductor);
        sc.FindProperty("level").objectReferenceValue = data;
        sc.ApplyModifiedProperties();
        var source = conductor.GetComponent<AudioSource>();
        source.clip = data.song;
        EditorUtility.SetDirty(source);

        UnityEngine.Object.FindAnyObjectByType<LevelLandscape>()?.RebuildAppearance();
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    static T Find<T>(string name) where T : Component => GameObject.Find(name)?.GetComponent<T>();

    static string FileName(string name) =>
        new string(name.Where(char.IsLetterOrDigit).ToArray()) is { Length: > 0 } s ? s : "Level";
}
