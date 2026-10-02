using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using UnityEngine;

public static class GDLevelParser
{
    public class GDLevel
    {
        public string name;
        public string levelString;
        public int officialTrack = -1;
        public int customSong;
    }

    public class GDObject
    {
        public int id;
        public float x, y, rotation, scale = 1f;
        public bool flipX, flipY;
        public Dictionary<string, string> fields;
    }

    public const float UnitsPerBlock = 30f;
    public const int CubePortal = 12;

    public static readonly int[] UnsupportedModePortals = { 660, 745, 1331, 1933, 2751 };

    static readonly float[] SpeedByKA4 = { 10.386f, 8.372f, 12.914f, 15.6f, 19.2f };

    public static readonly string[] OfficialTracks =
    {
        "StereoMadness", "BackOnTrack", "Polargeist", "DryOut", "BaseAfterBase", "CantLetGo", "Jumper",
        "TimeMachine", "Cycles", "xStep", "Clutterfunk", "TheoryOfEverything", "Electroman", "Clubstep",
        "Electrodynamix", "HexagonForce", "BlastProcessing", "TheoryOfEverything2", "GeometricalDominator",
        "Deadlocked", "Fingerdash", "Dash",
    };

    public static float StartSpeed(Dictionary<string, string> header) =>
        SpeedByKA4[Mathf.Clamp((int)Number(header, "kA4", 0f), 0, SpeedByKA4.Length - 1)];

    public static GDObject FirstUnsupportedPortal(List<GDObject> objects)
    {
        GDObject first = null;
        foreach (var o in objects)
            if (Array.IndexOf(UnsupportedModePortals, o.id) >= 0 && (first == null || o.x < first.x)) first = o;
        return first;
    }

    public static int StartModeIndex(Dictionary<string, string> header) => (int)Number(header, "kA2", 0f);

    public static bool StartsInverted(Dictionary<string, string> header) => Number(header, "kA11", 0f) == 1f;

    static readonly string[] OfficialLevelNames =
    {
        "Stereo Madness", "Back On Track", "Polargeist", "Dry Out", "Base After Base", "Cant Let Go", "Jumper",
        "Time Machine", "Cycles", "xStep", "Clutterfunk", "Theory of Everything", "Electroman Adventures", "Clubstep",
        "Electrodynamix", "Hexagon Force", "Blast Processing", "Theory of Everything 2", "Geometrical Dominator",
        "Deadlocked", "Fingerdash", "Dash",
    };

    public static List<GDLevel> ReadFile(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".dat": return ReadPlist(DecryptSave(File.ReadAllBytes(path)));
            case ".gmd": return ReadPlist(File.ReadAllText(path));
            case ".json": return new List<GDLevel> { ReadGDBrowserJson(File.ReadAllText(path)) };
            case ".txt": return new List<GDLevel> { ReadTxt(path) };
            default: throw new NotSupportedException("Unsupported format: " + Path.GetExtension(path));
        }
    }

    public static string DecryptSave(byte[] data)
    {
        var copy = (byte[])data.Clone();
        for (int i = 0; i < copy.Length; i++) copy[i] ^= 11;
        return Decompress(Encoding.ASCII.GetString(copy));
    }

    public static string Decompress(string base64)
    {
        string s = base64.Trim().TrimEnd('\0').Replace('-', '+').Replace('_', '/');
        while (s.Length % 4 != 0) s += "=";
        byte[] bytes = Convert.FromBase64String(s);

        using var input = new MemoryStream(bytes);
        bool gzip = bytes.Length > 1 && bytes[0] == 0x1f && bytes[1] == 0x8b;
        if (!gzip) input.Position = 2;
        using Stream decompressor = gzip
            ? new GZipStream(input, CompressionMode.Decompress)
            : new DeflateStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(decompressor, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static string PlainLevelString(GDLevel level)
    {
        string ls = level.levelString.Trim();
        return ls.StartsWith("H4sI") || ls.StartsWith("eJ") ? Decompress(ls) : ls;
    }

    static List<GDLevel> ReadPlist(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var levels = new List<GDLevel>();
        foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            if (IsDictionary(node)) Walk(node, levels);
        return levels;
    }

    static bool IsDictionary(XmlNode n) => n.Name == "d" || n.Name == "dict";

    static void Walk(XmlNode dict, List<GDLevel> levels)
    {
        var values = new Dictionary<string, string>();
        var children = dict.ChildNodes;
        for (int i = 0; i + 1 < children.Count; i += 2)
        {
            XmlNode value = children[i + 1];
            if (IsDictionary(value)) Walk(value, levels);
            else values[children[i].InnerText] = value.Name == "t" ? "1" : value.InnerText;
        }
        if (!values.TryGetValue("k4", out string levelString) || string.IsNullOrEmpty(levelString)) return;

        int.TryParse(values.GetValueOrDefault("k45", "0"), out int custom);
        int.TryParse(values.GetValueOrDefault("k8", "0"), out int official);
        levels.Add(new GDLevel
        {
            name = values.GetValueOrDefault("k2", "Untitled"),
            levelString = levelString,
            customSong = custom,
            officialTrack = custom > 0 ? -1 : official,
        });
    }

    [Serializable]
    class GDBrowserJson
    {
        public string name;
        public string data;
        public int officialSong;
        public int customSong;
    }

    static GDLevel ReadGDBrowserJson(string json)
    {
        var j = JsonUtility.FromJson<GDBrowserJson>(json);
        if (string.IsNullOrEmpty(j.data))
            throw new InvalidDataException("The JSON has no level string: download it from GDBrowser with ?download.");
        return new GDLevel
        {
            name = j.name,
            levelString = j.data,
            customSong = j.customSong,
            officialTrack = j.customSong > 0 ? -1 : Mathf.Max(0, j.officialSong - 1),
        };
    }

    static GDLevel ReadTxt(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        bool official = int.TryParse(name, out int id) && id >= 1 && id <= OfficialLevelNames.Length;
        return new GDLevel
        {
            name = official ? OfficialLevelNames[id - 1] : name,
            levelString = File.ReadAllText(path),
            officialTrack = official ? id - 1 : -1,
        };
    }

    public static List<GDObject> ParseObjects(string levelString, out Dictionary<string, string> header)
    {
        string[] blocks = levelString.Split(';');
        header = Pairs(blocks[0]);
        var objects = new List<GDObject>(blocks.Length);
        for (int i = 1; i < blocks.Length; i++)
        {
            if (blocks[i].Length == 0) continue;
            var p = Pairs(blocks[i]);
            if (!p.TryGetValue("1", out string id)) continue;
            objects.Add(new GDObject
            {
                id = int.Parse(id, CultureInfo.InvariantCulture),
                x = Number(p, "2", 0f),
                y = Number(p, "3", 0f),
                flipX = p.GetValueOrDefault("4") == "1",
                flipY = p.GetValueOrDefault("5") == "1",
                rotation = Number(p, "6", 0f),
                scale = Number(p, "32", 1f),
                fields = p,
            });
        }
        return objects;
    }

    static Dictionary<string, string> Pairs(string block)
    {
        var d = new Dictionary<string, string>();
        string[] parts = block.Split(',');
        for (int i = 0; i + 1 < parts.Length; i += 2) d[parts[i]] = parts[i + 1];
        return d;
    }

    public static float Number(Dictionary<string, string> d, string key, float fallback) =>
        d.TryGetValue(key, out string v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;
}
