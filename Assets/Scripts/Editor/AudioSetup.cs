using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

public static class AudioSetup
{
    const string MixerPath = "Assets/Audio/JumpIO.mixer";
    const string LibraryPath = "Assets/Audio/SoundLibrary.asset";
    const string ManagerPrefab = "Assets/Prefabs/AudioManager.prefab";
    const string Sfx = "Assets/Audio/SFX/";
    const string Bgm = "Assets/Audio/BGM/";

    [MenuItem("JumpIO/Setup/Audio")]
    public static void Configure()
    {
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) ?? CreateMixer();
        var music = mixer.FindMatchingGroups("Music").First();
        var sfx = mixer.FindMatchingGroups("SFX").First();
        ConfigureImports();
        var library = CreateLibrary();
        var managerPrefab = CreateManagerPrefab(mixer, music, sfx, library);
        ConfigurePrefabs(sfx);
        ConfigureLevelData();

        var menuMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(Bgm + "bgm_menu.mp3");
        var level = EditorSceneManager.OpenScene("Assets/Scenes/Level_01.unity");
        ConfigureScene(managerPrefab, null, music, sfx);
        EditorSceneManager.MarkSceneDirty(level);
        EditorSceneManager.SaveScene(level);
        var menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        ConfigureScene(managerPrefab, menuMusic, music, sfx);
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        AssetDatabase.SaveAssets();
        string exposed = string.Join(",", ExposedNames(mixer));
        Debug.Log($"JUMPIO_BLOCK_6_OK groups={string.Join(",", mixer.FindMatchingGroups("Master").Select(g => g.name))} exposed={exposed}");
    }

    static AudioMixer CreateMixer()
    {
        var editor = typeof(Editor).Assembly;
        var controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController", true);
        var groupType = editor.GetType("UnityEditor.Audio.AudioMixerGroupController", true);
        var pathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        var controller = controllerType.GetMethod("CreateMixerControllerAtPath", all).Invoke(null, new object[] { MixerPath });
        var master = controllerType.GetProperty("masterGroup", all).GetValue(controller);
        var createGroup = controllerType.GetMethod("CreateNewGroup", all);
        var addChild = controllerType.GetMethod("AddChildToParent", all);
        var addExposed = controllerType.GetMethod("AddExposedParameter", all);
        var volumeGuid = groupType.GetMethod("GetGUIDForVolume", all);

        object Group(string name)
        {
            var group = createGroup.Invoke(controller, new object[] { name, false });
            addChild.Invoke(controller, new[] { group, master });
            return group;
        }
        var groups = new[] { ("Master", master), ("Music", Group("Music")), ("Sfx", Group("SFX")) };
        foreach (var (_, group) in groups)
        {
            var path = Activator.CreateInstance(pathType, all, null, new[] { group, volumeGuid.Invoke(group, null) }, null);
            addExposed.Invoke(controller, new[] { path });
        }

        var exposedProperty = controllerType.GetProperty("exposedParameters", all);
        var exposed = (Array)exposedProperty.GetValue(controller);
        var elementType = exposed.GetType().GetElementType();
        var guidField = elementType.GetField("guid", all);
        var nameField = elementType.GetField("name", all);
        foreach (var (channel, group) in groups)
        {
            var guid = volumeGuid.Invoke(group, null);
            for (int i = 0; i < exposed.Length; i++)
            {
                var entry = exposed.GetValue(i);
                if (!guidField.GetValue(entry).Equals(guid)) continue;
                nameField.SetValue(entry, channel + "Volume");
                exposed.SetValue(entry, i);
            }
        }
        exposedProperty.SetValue(controller, exposed);
        EditorUtility.SetDirty((UnityEngine.Object)controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MixerPath);
        return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
    }

    static string[] ExposedNames(AudioMixer mixer)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var exposed = (Array)mixer.GetType().GetProperty("exposedParameters", all).GetValue(mixer);
        var nameField = exposed.GetType().GetElementType().GetField("name", all);
        return exposed.Cast<object>().Select(e => (string)nameField.GetValue(e)).ToArray();
    }

    static void ConfigureImports()
    {
        Import(Bgm + "bgm_menu.mp3", AudioClipLoadType.Streaming, AudioCompressionFormat.Vorbis, 0.7f);
        Import(Bgm + "bgm_tutorial.mp3", AudioClipLoadType.CompressedInMemory, AudioCompressionFormat.Vorbis, 0.7f);
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Sfx.TrimEnd('/') }))
            Import(AssetDatabase.GUIDToAssetPath(guid), AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.PCM, 1f);
    }

    static void Import(string path, AudioClipLoadType loadType, AudioCompressionFormat format, float quality)
    {
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var settings = importer.defaultSampleSettings;
        settings.loadType = loadType;
        settings.compressionFormat = format;
        settings.quality = quality;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = path.StartsWith(Sfx);
        importer.SaveAndReimport();
    }

    static SoundLibrary CreateLibrary()
    {
        var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        var serialized = new SerializedObject(library);
        void Set(string field, string clip, float volume, float pitch)
        {
            var sound = serialized.FindProperty(field);
            sound.FindPropertyRelative("clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + clip);
            sound.FindPropertyRelative("volume").floatValue = volume;
            sound.FindPropertyRelative("pitchVariation").floatValue = pitch;
        }
        Set("jump", "jump.wav", 0.45f, 0.06f);
        Set("coin", "coin.wav", 0.6f, 0.04f);
        Set("hurt", "hurt.wav", 0.75f, 0.03f);
        Set("death", "explosion.wav", 0.8f, 0f);
        Set("powerUp", "power_up.wav", 0.5f, 0.05f);
        Set("uiClick", "tap.wav", 0.6f, 0.03f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);
        return library;
    }

    static GameObject CreateManagerPrefab(AudioMixer mixer, AudioMixerGroup music, AudioMixerGroup sfx, SoundLibrary library)
    {
        var root = new GameObject("AudioManager", typeof(AudioManager), typeof(GameplayAudio));
        AudioSource Source(string name, AudioMixerGroup group, bool loop)
        {
            var child = new GameObject(name, typeof(AudioSource));
            child.transform.SetParent(root.transform, false);
            var source = child.GetComponent<AudioSource>();
            source.outputAudioMixerGroup = group;
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }
        var musicSources = new[] { Source("MusicA", music, true), Source("MusicB", music, true) };
        var voices = Enumerable.Range(0, 6).Select(i => Source("Voice" + i, sfx, false)).ToArray();
        var serialized = new SerializedObject(root.GetComponent<AudioManager>());
        serialized.FindProperty("mixer").objectReferenceValue = mixer;
        serialized.FindProperty("sounds").objectReferenceValue = library;
        var musicArray = serialized.FindProperty("musicSources");
        musicArray.arraySize = musicSources.Length;
        for (int i = 0; i < musicSources.Length; i++) musicArray.GetArrayElementAtIndex(i).objectReferenceValue = musicSources[i];
        var voiceArray = serialized.FindProperty("sfxVoices");
        voiceArray.arraySize = voices.Length;
        for (int i = 0; i < voices.Length; i++) voiceArray.GetArrayElementAtIndex(i).objectReferenceValue = voices[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, ManagerPrefab);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static void ConfigurePrefabs(AudioMixerGroup sfx)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == ManagerPrefab) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = Route(root, null, sfx) | AddClickSounds(root);
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ConfigureLevelData()
    {
        var song = AssetDatabase.LoadAssetAtPath<AudioClip>(Bgm + "bgm_tutorial.mp3");
        foreach (string guid in AssetDatabase.FindAssets("t:LevelData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
            data.song = song;
            data.songOffset = 0f;
            EditorUtility.SetDirty(data);
        }
    }

    static void ConfigureScene(GameObject managerPrefab, AudioClip sceneMusic, AudioMixerGroup music, AudioMixerGroup sfx)
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        if (!roots.Any(r => r.GetComponent<AudioManager>() != null))
            PrefabUtility.InstantiatePrefab(managerPrefab);
        var holder = roots.FirstOrDefault(r => r.name == "SceneMusic") ?? new GameObject("SceneMusic", typeof(SceneMusic));
        var serialized = new SerializedObject(holder.GetComponent<SceneMusic>());
        serialized.FindProperty("music").objectReferenceValue = sceneMusic;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        foreach (var root in roots.Where(r => r.GetComponent<AudioManager>() == null))
        {
            Route(root, music, sfx);
            AddClickSounds(root);
            foreach (var conductor in root.GetComponentsInChildren<MusicConductor>(true))
            {
                var source = conductor.GetComponent<AudioSource>();
                source.clip = conductor.Level != null ? conductor.Level.song : null;
                source.playOnAwake = false;
                source.loop = false;
                source.volume = 0.55f;
                source.outputAudioMixerGroup = music;
            }
        }
    }

    static bool Route(GameObject root, AudioMixerGroup music, AudioMixerGroup sfx)
    {
        bool changed = false;
        foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
        {
            var group = source.GetComponent<MusicConductor>() != null ? music : sfx;
            if (group == null || source.outputAudioMixerGroup == group) continue;
            source.outputAudioMixerGroup = group;
            changed = true;
        }
        return changed;
    }

    static bool AddClickSounds(GameObject root)
    {
        bool changed = false;
        foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
        {
            if (button.GetComponent<ButtonClickSound>() != null) continue;
            button.gameObject.AddComponent<ButtonClickSound>();
            changed = true;
        }
        return changed;
    }
}
