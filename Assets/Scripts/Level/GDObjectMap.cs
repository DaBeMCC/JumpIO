using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "GDObjectMap", menuName = "JumpIO/GD Object Map")]
public class GDObjectMap : ScriptableObject
{
    public enum Category { Solid, Hazard, Object, Ignored }

    [Serializable]
    public class Entry
    {
        [FormerlySerializedAs("nombre")]
        public string name;
        public int[] ids;
        [FormerlySerializedAs("categoria")]
        public Category category;
        public TileBase tile;
        public GameObject prefab;
        [FormerlySerializedAs("escala")]
        public Vector2 scale = Vector2.one;
    }

    [FormerlySerializedAs("entradas")]
    public List<Entry> entries = new List<Entry>();

    [FormerlySerializedAs("idsConColisionReferencia")]
    public int[] referenceCollisionIds;

    Dictionary<int, Entry> lookup;

    public Entry Find(int id)
    {
        if (lookup == null)
        {
            lookup = new Dictionary<int, Entry>();
            foreach (var e in entries)
                foreach (int i in e.ids) lookup[i] = e;
        }
        return lookup.TryGetValue(id, out var entry) ? entry : null;
    }

    public bool HasCollisionPerReference(int id) => Array.IndexOf(referenceCollisionIds, id) >= 0;

    void OnValidate() => lookup = null;
}
