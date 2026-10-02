using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VfxSetup
{
    const string Folder = "Assets/Prefabs/Effects";
    const string Material = "Assets/Art/ActionParticles.mat";

    struct Burst
    {
        public string name;
        public int count;
        public Vector2 lifetime, speed, size;
        public Color colorA, colorB;
        public float gravity;
        public ParticleSystemShapeType shape;
        public float radius, angle;
        public Vector3 rotation;
    }

    [MenuItem("JumpIO/Setup/VFX")]
    public static void Configure()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Effects");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Material);
        var dust = new Color(0.86f, 0.8f, 0.62f, 1f);
        var jump = Create(material, new Burst
        {
            name = "JumpDust", count = 8, lifetime = new Vector2(0.25f, 0.4f), speed = new Vector2(1.5f, 3f),
            size = new Vector2(0.08f, 0.16f), colorA = dust, colorB = new Color(0.7f, 0.66f, 0.5f), gravity = 0.4f,
            shape = ParticleSystemShapeType.Cone, angle = 35f, radius = 0.2f, rotation = new Vector3(90f, 0f, 0f),
        });
        var land = Create(material, new Burst
        {
            name = "LandDust", count = 10, lifetime = new Vector2(0.3f, 0.45f), speed = new Vector2(1.5f, 3.5f),
            size = new Vector2(0.08f, 0.18f), colorA = dust, colorB = new Color(0.62f, 0.7f, 0.4f), gravity = 0.6f,
            shape = ParticleSystemShapeType.Cone, angle = 80f, radius = 0.3f, rotation = new Vector3(-90f, 0f, 0f),
        });
        var hit = Create(material, new Burst
        {
            name = "HitSparks", count = 14, lifetime = new Vector2(0.2f, 0.35f), speed = new Vector2(3f, 6f),
            size = new Vector2(0.06f, 0.14f), colorA = new Color(1f, 0.25f, 0.2f), colorB = Color.white, gravity = 0f,
            shape = ParticleSystemShapeType.Circle, radius = 0.3f,
        });
        var death = Create(material, new Burst
        {
            name = "DeathBurst", count = 30, lifetime = new Vector2(0.5f, 0.9f), speed = new Vector2(4f, 9f),
            size = new Vector2(0.12f, 0.26f), colorA = new Color(1f, 0.75f, 0.2f), colorB = new Color(0.95f, 0.25f, 0.15f),
            gravity = 1.2f, shape = ParticleSystemShapeType.Circle, radius = 0.3f,
        });
        var coin = Create(material, new Burst
        {
            name = "CoinSparkle", count = 12, lifetime = new Vector2(0.3f, 0.5f), speed = new Vector2(1f, 3f),
            size = new Vector2(0.06f, 0.14f), colorA = new Color(1f, 0.9f, 0.35f), colorB = Color.white, gravity = -0.3f,
            shape = ParticleSystemShapeType.Circle, radius = 0.25f,
        });
        var power = Create(material, new Burst
        {
            name = "PowerUpRing", count = 16, lifetime = new Vector2(0.25f, 0.4f), speed = new Vector2(2f, 2.5f),
            size = new Vector2(0.08f, 0.14f), colorA = new Color(0.45f, 0.95f, 1f), colorB = Color.white, gravity = 0f,
            shape = ParticleSystemShapeType.Circle, radius = 0.5f,
        });

        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level_01.unity");
        var effects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Effects");
        var player = effects.GetComponent<GameplayEffects>();
        if (player == null) player = effects.AddComponent<GameplayEffects>();
        var serialized = new SerializedObject(player);
        serialized.FindProperty("jumpDust").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PooledEffect>(jump);
        serialized.FindProperty("landDust").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PooledEffect>(land);
        serialized.FindProperty("hitSparks").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PooledEffect>(hit);
        serialized.FindProperty("deathBurst").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PooledEffect>(death);
        serialized.FindProperty("coinSparkle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PooledEffect>(coin);
        serialized.FindProperty("powerUpRing").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PooledEffect>(power);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("JUMPIO_BLOCK_7_OK effects=6 (+ActionParticles)");
    }

    static string Create(Material material, Burst burst)
    {
        var go = new GameObject(burst.name, typeof(ParticleSystem), typeof(PooledEffect));
        var system = go.GetComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = burst.lifetime.y;
        main.startLifetime = new ParticleSystem.MinMaxCurve(burst.lifetime.x, burst.lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(burst.speed.x, burst.speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(burst.size.x, burst.size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(burst.colorA, burst.colorB);
        main.gravityModifier = burst.gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Callback;
        main.maxParticles = burst.count * 2;

        var emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst.count) });

        var shape = system.shape;
        shape.shapeType = burst.shape;
        shape.radius = burst.radius;
        shape.angle = burst.angle;
        shape.rotation = burst.rotation;

        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        var color = system.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerName = LevelArtSetup.Foreground;
        renderer.sortingOrder = 5;

        string path = $"{Folder}/{burst.name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        return path;
    }
}
