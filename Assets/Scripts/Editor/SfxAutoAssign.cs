using System.IO;
using UnityEditor;
using UnityEngine;

// Conecta los sonidos nuevos a la SoundLibrary en cuanto se copian a su carpeta,
// así añadir un sonido no requiere tocar el Inspector.
public class SfxAutoAssign : AssetPostprocessor
{
    const string SfxFolder = "Assets/Audio/SFX/";
    // El tema de victoria es largo y vive con la música, pero se reproduce como un sonido más.
    const string BgmFolder = "Assets/Audio/BGM/";
    const string LibraryPath = "Assets/Audio/SoundLibrary.asset";

    static readonly (string file, string field, float volume)[] Slots =
    {
        ("victory", "victory", 0.9f),
    };

    // Los clips cortos se descomprimen al cargar para que suenen sin retardo.
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(SfxFolder)) return;
        var importer = (AudioImporter)assetImporter;
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = settings;
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (!path.StartsWith(SfxFolder) && !path.StartsWith(BgmFolder)) continue;
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            foreach (var slot in Slots)
            {
                if (name != slot.file) continue;
                var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (library == null || clip == null) continue;
                var serialized = new SerializedObject(library);
                var sound = serialized.FindProperty(slot.field);
                if (sound.FindPropertyRelative("clip").objectReferenceValue == clip) continue;
                sound.FindPropertyRelative("clip").objectReferenceValue = clip;
                sound.FindPropertyRelative("volume").floatValue = slot.volume;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log($"SoundLibrary: {slot.field} = {path}");
            }
        }
    }
}
