using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.InputSystem;

public static class AnimationSetup
{
    const string KnightSheet = "Assets/Art/Sprites/Knight/Knight.png";
    const string Animations = "Assets/Animations/";

    public static void Configure()
    {
        var actionReference = ConfigureInput();
        SliceKnight();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(KnightSheet).OfType<Sprite>().ToDictionary(s => s.name);
        var run = Enumerable.Range(0, 16).Select(i => sprites["Run_" + i]).ToArray();
        var roll = Enumerable.Range(0, 8).Select(i => sprites["Roll_" + i]).ToArray();
        var death = Enumerable.Range(0, 4).Select(i => sprites["Death_" + i]).ToArray();
        SpriteClip(Animations + "Knight/Knight_Run.anim", run, 16f, true);
        var knightAction = SpriteClip(Animations + "Knight/Knight_Action.anim", roll, 12f, false);
        var knightDeath = SpriteClip(Animations + "Knight/Knight_Death.anim", death, 8f, false);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + "Player.controller");
        var cubeAction = CubeClip(Animations + "Action.anim", knightAction.length, true);
        var cubeDeath = CubeClip(Animations + "Death.anim", knightDeath.length, false);
        ConfigureController(controller, cubeAction, cubeDeath);
        var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Animations + "Knight/Knight.overrideController");
        overrides["Action"] = knightAction;
        overrides["Death"] = knightDeath;
        EditorUtility.SetDirty(overrides);
        ConfigureEnemy("Snail", "Assets/Art/Sprites/Source/Mob/Snail/Dead-Sheet.png", 8);
        ConfigureEnemy("Bee", "Assets/Art/Sprites/Source/Mob/Small Bee/Hit/Hit-Sheet.png", 4);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level_01.unity");
        foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            var input = new SerializedObject(player.GetComponent<PlayerInput>());
            input.FindProperty("actionReference").objectReferenceValue = actionReference;
            input.ApplyModifiedPropertiesWithoutUndo();
            var action = player.GetComponent<PlayerAction>();
            if (action == null) action = player.gameObject.AddComponent<PlayerAction>();
            var serialized = new SerializedObject(action);
            serialized.FindProperty("rollClip").objectReferenceValue = knightAction;
            serialized.FindProperty("cooldown").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        ConfigureCoins();
        ConfigureFeedback();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"JUMPIO_BLOCK_4_OK runFrames={run.Length} rollFrames={roll.Length} rollDuration={knightAction.length:F6} deathFrames={death.Length} states={controller.layers[0].stateMachine.states.Length}");
    }

    static void SliceKnight()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(KnightSheet);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("Knight sprite data provider is unavailable.");
        provider.InitSpriteEditorDataProvider();
        var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
            throw new InvalidOperationException("Knight importer does not permit adding sprites.");
        var rects = provider.GetSpriteRects().ToList();
        AddRow(rects, "Run_", 8, 8, 128);
        AddRow(rects, "Roll_", 0, 8, 64);
        AddRow(rects, "Death_", 0, 4, 0);
        provider.SetSpriteRects(rects.ToArray());
        var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (ids == null) throw new InvalidOperationException("Sprite name/file ID provider is unavailable.");
        var pairs = ids.GetNameFileIdPairs().ToList();
        foreach (var rect in rects)
            if (!pairs.Any(p => p.name == rect.name)) pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
        ids.SetNameFileIdPairs(pairs);
        provider.Apply();
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    static void AddRow(List<SpriteRect> rects, string prefix, int first, int count, int y)
    {
        for (int i = 0; i < count; i++)
        {
            string name = prefix + (first + i);
            if (rects.Any(r => r.name == name)) continue;
            rects.Add(new SpriteRect { name = name, rect = new Rect(i * 32, y, 32, 32),
                alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f), spriteID = GUID.Generate() });
        }
    }

    static AnimationClip SpriteClip(string path, Sprite[] frames, float fps, bool loop)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.frameRate = fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = frames.Select((frame, i) => new ObjectReferenceKeyframe { time = i / fps, value = frame }).ToList();
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.stopTime = frames.Length / fps;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static InputActionReference ConfigureInput()
    {
        const string path = "Assets/Settings/InputSystem_Actions.inputactions";
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        asset.Disable();
        var map = asset.FindActionMap("Player", true);
        var action = map.FindAction("Action");
        if (action == null) action = map.AddAction("Action", InputActionType.Button, expectedControlLayout: "Button");
        foreach (var binding in new[] { "<Keyboard>/e", "<Keyboard>/x", "<Gamepad>/buttonWest" })
            if (!action.bindings.Any(b => b.path == binding)) action.AddBinding(binding, groups: binding.Contains("Keyboard") ? "Keyboard&Mouse" : "Gamepad");
        System.IO.File.WriteAllText(path, asset.ToJson());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        action = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path).FindAction("Player/Action", true);
        const string referencePath = "Assets/Settings/ActionInput.asset";
        var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(referencePath);
        if (reference == null)
        {
            reference = InputActionReference.Create(action);
            AssetDatabase.CreateAsset(reference, referencePath);
        }
        else reference.Set(action);
        EditorUtility.SetDirty(reference);
        return reference;
    }

    static AnimationClip CubeClip(string path, float duration, bool action)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(duration / 2f, action ? 0.82f : 0.1f), new Keyframe(duration, action ? 1f : 0.1f));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.x"), curve);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.y"), curve);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        settings.stopTime = duration;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void ConfigureController(AnimatorController controller, AnimationClip action, AnimationClip death)
    {
        if (controller == null) throw new InvalidOperationException("Player controller is missing.");
        foreach (var parameter in new[] { ("Action", AnimatorControllerParameterType.Trigger), ("Dead", AnimatorControllerParameterType.Bool), ("Paused", AnimatorControllerParameterType.Bool) })
            if (!controller.parameters.Any(p => p.name == parameter.Item1)) controller.AddParameter(parameter.Item1, parameter.Item2);
        var machine = controller.layers[0].stateMachine;
        var states = machine.states.ToDictionary(s => s.state.name, s => s.state);
        foreach (var name in new[] { "Action", "Death" })
            if (!states.ContainsKey(name)) states[name] = machine.AddState(name);
        states["Action"].motion = action;
        states["Death"].motion = death;
        var tree = AssetDatabase.LoadAllAssetsAtPath(Animations + "Player.controller").OfType<BlendTree>().FirstOrDefault(t => t.name == "VerticalVelocity");
        if (tree == null)
        {
            tree = new BlendTree { name = "VerticalVelocity", blendType = BlendTreeType.Simple1D, blendParameter = "yVelocity", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
        }
        tree.children = new[] {
            new ChildMotion { motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + "Fall.anim"), threshold = -0.01f, timeScale = 1f },
            new ChildMotion { motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + "Jump.anim"), threshold = 0.01f, timeScale = 1f } };
        states["Jump"].motion = states["Fall"].motion = tree;
        foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
        foreach (var state in states.Values)
            foreach (var transition in state.transitions) state.RemoveTransition(transition);
        machine.defaultState = states["Idle"];
        var died = machine.AddAnyStateTransition(states["Death"]);
        ConfigureTransition(died);
        died.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        var pause = machine.AddAnyStateTransition(states["Idle"]);
        ConfigureTransition(pause);
        pause.AddCondition(AnimatorConditionMode.If, 0f, "Paused");
        pause.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        foreach (var name in new[] { "Hurt", "Action" })
        {
            var transition = machine.AddAnyStateTransition(states[name]);
            ConfigureTransition(transition);
            transition.canTransitionToSelf = name == "Action";
            transition.AddCondition(AnimatorConditionMode.If, 0f, name);
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Paused");
        }
        foreach (var source in states.Values.Where(s => s.name != "Death"))
        {
            foreach (var target in new[] { "Idle", "Run", "Jump", "Fall", "Ship", "Ball", "Ufo" })
            {
                if (target == source.name) continue;
                var transition = source.AddTransition(states[target]);
                ConfigureTransition(transition);
                transition.hasExitTime = source.name == "Action" || source.name == "Hurt";
                transition.exitTime = 1f;
                transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Paused");
                int mode = target == "Ship" ? 1 : target == "Ball" ? 2 : target == "Ufo" ? 3 : 0;
                transition.AddCondition(AnimatorConditionMode.Equals, mode, "Mode");
                if (mode > 0) continue;
                bool grounded = target == "Idle" || target == "Run";
                transition.AddCondition(grounded ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "IsGrounded");
                if (grounded) transition.AddCondition(target == "Idle" ? AnimatorConditionMode.Less : AnimatorConditionMode.Greater, 0.01f, "Speed");
                else transition.AddCondition(target == "Jump" ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, 0f, "yVelocity");
            }
        }
        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(controller);
    }

    static void ConfigureTransition(AnimatorStateTransition transition)
    {
        transition.duration = 0f;
        transition.hasExitTime = false;
        transition.canTransitionToSelf = false;
    }

    static void ConfigureEnemy(string name, string sheet, int expected)
    {
        string path = "Assets/Prefabs/Enemies/" + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(sheet);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = root.GetComponent<SpriteRenderer>().sprite.pixelsPerUnit;
            importer.SaveAndReimport();
            var frames = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
            if (frames.Length != expected) throw new InvalidOperationException($"{name}: expected {expected} defeat frames, found {frames.Length}.");
            var serialized = new SerializedObject(root.GetComponent<EnemyDamage>());
            var property = serialized.FindProperty("defeatedFrames");
            property.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ConfigureCoins()
    {
        const string sheet = "Assets/Art/Sprites/Source/coin.png";
        var frames = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
        if (frames.Length != 12) throw new InvalidOperationException($"Coin sheet must contain 12 frames; found {frames.Length}.");
        var coins = UnityEngine.Object.FindObjectsByType<Coin>(FindObjectsSortMode.None);
        var importer = (TextureImporter)AssetImporter.GetAtPath(sheet);
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        if (coins.Length > 0) importer.spritePixelsPerUnit = frames[0].rect.width / coins[0].GetComponent<SpriteRenderer>().sprite.bounds.size.x;
        importer.SaveAndReimport();
        frames = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
        foreach (var coin in coins)
        {
            var loop = coin.GetComponent<SpriteLoop>();
            if (loop == null) loop = coin.gameObject.AddComponent<SpriteLoop>();
            var serialized = new SerializedObject(loop);
            var property = serialized.FindProperty("frames");
            property.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            serialized.FindProperty("framesPerSecond").floatValue = 12f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void ConfigureFeedback()
    {
        const string path = "Assets/Prefabs/ActionParticles.prefab";
        const string materialPath = "Assets/Art/ActionParticles.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("URP 2D unlit shader is unavailable.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root = prefab != null ? PrefabUtility.LoadPrefabContents(path) : new GameObject("ActionParticles");
        try
        {
            var system = root.GetComponent<ParticleSystem>();
            if (system == null) system = root.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.08f);
            main.startColor = new Color(0.85f, 0.88f, 0.78f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.22f;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 20;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (prefab != null) PrefabUtility.UnloadPrefabContents(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
        var feedback = UnityEngine.Object.FindAnyObjectByType<ActionFeedback>();
        if (feedback == null)
        {
            var host = new GameObject("ActionFeedback");
            feedback = host.AddComponent<ActionFeedback>();
        }
        var source = feedback.GetComponent<AudioSource>();
        if (source == null) source = feedback.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0.55f;
        var serialized = new SerializedObject(feedback);
        serialized.FindProperty("particles").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ParticleSystem>();
        serialized.FindProperty("source").objectReferenceValue = source;
        serialized.FindProperty("actionSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/action.wav");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("JUMPIO_ACTION_FEEDBACK_OK pooled particles; optional action.wav hook configured");
    }
}
