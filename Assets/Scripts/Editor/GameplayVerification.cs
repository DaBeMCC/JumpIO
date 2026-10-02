using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class GameplayVerification
{
    static double deadline;
    static int stage;
    static bool died;
    static bool failed;

    public static void VerifyHazardContact()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Level_01.unity");
        deadline = EditorApplication.timeSinceStartup + 60;
        stage = 0;
        died = failed = false;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
    }

    static void OnDied() => died = true;

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Hazard Play Mode verification timed out.");
            if (stage == 0)
            {
                if (!EditorApplication.isPlaying || PlayerController.Instance == null || !PlayerController.Instance.IsActive) return;
                Application.runInBackground = true;
                var player = PlayerController.Instance;
                var hazards = GameObject.Find("Hazards_Tilemap").GetComponent<Tilemap>();
                var composite = hazards.GetComponent<CompositeCollider2D>();
                if (composite == null || !composite.isTrigger || composite.shapeCount != 130)
                    throw new InvalidOperationException("Hazard composite changed unexpectedly.");
                Vector3 position = Vector3.zero;
                bool found = false;
                foreach (var cell in hazards.cellBounds.allPositionsWithin)
                {
                    if (!hazards.HasTile(cell)) continue;
                    position = hazards.GetCellCenterWorld(cell);
                    found = true;
                    break;
                }
                if (!found) throw new InvalidOperationException("No hazard tile found.");
                typeof(PlayerController).GetField("conductor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, null);
                GameEvents.PlayerDied += OnDied;
                player.transform.position = position;
                var body = player.GetComponent<Rigidbody2D>();
                body.position = position;
                body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
                Debug.Log($"JUMPIO_PLAY_TEST_CONTACT position={position}");
                stage = 1;
                deadline = EditorApplication.timeSinceStartup + 10;
            }
            else if (stage == 1 && died)
            {
                Debug.Log("JUMPIO_PLAY_TEST_OK composite spike contact raised PlayerDied");
                GameEvents.PlayerDied -= OnDied;
                stage = 2;
                EditorApplication.isPlaying = false;
            }
            else if (stage == 2 && !EditorApplication.isPlaying)
                Finish(failed ? 1 : 0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        GameEvents.PlayerDied -= OnDied;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorApplication.Exit(code);
    }
}
