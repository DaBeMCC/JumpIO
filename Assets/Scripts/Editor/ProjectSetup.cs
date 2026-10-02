using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class ProjectSetup
{
    public static void WireArchitecture()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level_01.unity");
        var camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
        foreach (var view in UnityEngine.Object.FindObjectsByType<CorridorView>(FindObjectsSortMode.None))
        {
            var serialized = new SerializedObject(view);
            serialized.FindProperty("cam").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (player.GetComponent<PlayerInput>() == null) player.gameObject.AddComponent<PlayerInput>();
        foreach (var bar in UnityEngine.Object.FindObjectsByType<HealthBar>(FindObjectsSortMode.None))
            foreach (var graphic in bar.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.enabled = true;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("JUMPIO_BLOCK_2_3_OK references wired; health graphics enabled");
    }
    public static void ConfigureHazards()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level_01.unity");
        var hazards = GameObject.Find("Hazards_Tilemap");
        if (hazards == null) throw new InvalidOperationException("Hazards_Tilemap is missing.");
        var tileCollider = hazards.GetComponent<TilemapCollider2D>();
        if (tileCollider == null) throw new InvalidOperationException("Hazard tile collider is missing.");
        var body = hazards.GetComponent<Rigidbody2D>();
        if (body == null) body = hazards.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        var composite = hazards.GetComponent<CompositeCollider2D>();
        if (composite == null) composite = hazards.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.isTrigger = true;
        tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
        var writer = LevelSceneWriter.FromActiveScene();
        if (writer == null) throw new InvalidOperationException("Level scene structure is incomplete.");
        var log = new StringBuilder();
        writer.RegenerateColliders(log);
        if (composite.shapeCount == 0 || !composite.isTrigger || composite.gameObject.layer != hazards.layer)
            throw new InvalidOperationException("Hazard composite validation failed.");
        EditorUtility.SetDirty(hazards);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"JUMPIO_BLOCK_1_OK shapes={composite.shapeCount} trigger={composite.isTrigger} layer={hazards.layer}\n{log}");
    }
}
