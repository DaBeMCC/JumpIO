using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Tilemaps;

public static class AnimationVerification
{
    static IEnumerator tests;
    static double wakeAt;
    static double deadline;
    static bool failed;
    static bool finished;
    static int acted;
    static PlayerController player;
    static Rigidbody2D body;
    static Animator animator;
    static Keyboard keyboard;
    static Gamepad gamepad;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Level_01.unity");
        failed = finished = false;
        acted = 0;
        deadline = EditorApplication.timeSinceStartup + 100;
        wakeAt = 0;
        tests = Check();
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }
    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
    }
    static void OnActed(Vector3 position) => acted++;
    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Animation verification timed out.");
            if (finished)
            {
                if (!EditorApplication.isPlaying) Exit();
                return;
            }
            if (EditorApplication.timeSinceStartup < wakeAt) return;
            if (!tests.MoveNext())
            {
                finished = true;
                EditorApplication.isPlaying = false;
                return;
            }
            if (tests.Current is float delay) wakeAt = EditorApplication.timeSinceStartup + delay;
        }
        catch (Exception exception)
        {
            failed = true;
            Debug.LogException(exception);
            finished = true;
            EditorApplication.isPlaying = false;
        }
    }
    static void Exit()
    {
        GameEvents.PlayerActed -= OnActed;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorApplication.Exit(failed ? 1 : 0);
    }
    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Debug.Log("JUMPIO_ANIMATION_PASS " + message);
    }
    static void ChangeInput<T>(InputControl device, T state) where T : struct
    {
        typeof(InputSystem).Assembly.GetType("UnityEngine.InputSystem.LowLevel.InputUpdate")
            .GetField("s_LatestUpdateType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(null, InputUpdateType.Dynamic);
        var manager = typeof(InputSystem).GetField("s_Manager", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var buffers = manager.GetType().GetField("m_StateBuffers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        buffers.GetType().GetMethod("SwitchTo", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new object[] { buffers, InputUpdateType.Dynamic });
        InputState.Change(device, state, InputUpdateType.Dynamic);
    }
    static bool State(string name) => animator.GetCurrentAnimatorStateInfo(0).IsName(name);
    static void Grounded(bool value) => typeof(PlayerController).GetProperty("IsGrounded").SetValue(player, value);
    static void ResetAttempt()
    {
        body.simulated = false;
        GameEvents.RaiseAttemptStarted(900);
        player.SetGravity(false);
        typeof(PlayerController).GetField("dashing", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, false);
        body.position = new Vector2(-5f, 4f);
        player.transform.position = body.position;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        Grounded(true);
    }
    static void TouchEnemy(EnemyDamage enemy, bool stomp)
    {
        var target = enemy.GetComponent<Collider2D>().bounds.center;
        if (stomp) target.y += player.GetComponent<Collider2D>().bounds.extents.y + 0.05f;
        player.transform.position = target;
        body.position = target;
        body.linearVelocity = stomp ? Vector2.down : Vector2.zero;
        body.gravityScale = 0f;
        body.simulated = true;
        Physics2D.SyncTransforms();
    }
    static IEnumerator Check()
    {
        while (!EditorApplication.isPlaying || PlayerController.Instance == null || !PlayerController.Instance.IsActive) yield return null;
        Application.runInBackground = true;
        player = PlayerController.Instance;
        body = player.GetComponent<Rigidbody2D>();
        animator = player.GetComponentInChildren<Animator>();
        player.GetComponentInChildren<PlayerVisual>().ApplySkin(AssetDatabase.LoadAssetAtPath<CharacterSkin>("Assets/Art/Skins/Skin_Knight.asset"));
        player.enabled = false;
        var conductor = UnityEngine.Object.FindAnyObjectByType<MusicConductor>();
        conductor.enabled = false;
        body.simulated = false;
        keyboard = InputSystem.AddDevice<Keyboard>();
        gamepad = InputSystem.AddDevice<Gamepad>();
        GameEvents.PlayerActed += OnActed;
        var action = player.GetComponent<PlayerAction>();
        var health = player.GetComponent<PlayerHealth>();
        player.SetMode(PlayerModeType.Cube, 5f);
        Grounded(true);
        yield return 0.1f;
        Assert(State("Run"), "knight reaches Run on ground");
        Grounded(false);
        body.linearVelocity = new Vector2(10f, 10f);
        yield return 0.1f;
        Assert(State("Jump"), "knight reaches Jump while rising");
        Assert(Array.Exists(animator.GetCurrentAnimatorClipInfo(0), info => info.clip.name == "Knight_Jump" && info.weight > 0.5f), "air Blend Tree uses the knight Jump override");
        body.linearVelocity = new Vector2(10f, -10f);
        yield return 0.1f;
        Assert(State("Fall"), "knight reaches Fall while descending");
        Assert(Array.Exists(animator.GetCurrentAnimatorClipInfo(0), info => info.clip.name == "Knight_Fall" && info.weight > 0.5f), "air Blend Tree uses the knight Fall override");
        foreach (var mode in new[] { PlayerModeType.Ship, PlayerModeType.Ball, PlayerModeType.Ufo })
        {
            player.SetMode(mode, 5f);
            yield return 0.1f;
            Assert(State(mode.ToString()), "knight reaches " + mode);
        }
        player.SetMode(PlayerModeType.Cube, 5f);
        Grounded(true);
        yield return 0.1f;
        Time.timeScale = 0f;
        GameEvents.RaisePauseChanged(true);
        yield return 0.15f;
        Assert(State("Idle") && animator.updateMode == AnimatorUpdateMode.UnscaledTime, "pause shows Idle in unscaled time");
        Time.timeScale = 1f;
        GameEvents.RaisePauseChanged(false);
        ResetAttempt();
        yield return 0.1f;
        var velocity = body.linearVelocity;
        var gravity = body.gravityScale;
        var speed = player.Speed;
        InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var inputAction = (InputAction)typeof(PlayerInput).GetField("action", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player.GetComponent<PlayerInput>());
        Debug.Log($"JUMPIO_INPUT_BEFORE enabled={inputAction.enabled} keyboard={keyboard.enabled} active={player.IsActive} duration={action.Duration} controls={inputAction.controls.Count} bindings={string.Join(",", inputAction.bindings)}");
        float activatedAt = Time.time;
        ChangeInput(keyboard, new KeyboardState(Key.E));
        Debug.Log($"JUMPIO_INPUT_IMMEDIATE acted={acted} key={keyboard.eKey.isPressed} controls={inputAction.controls.Count} phase={inputAction.phase}");

        yield return 0.08f;
        ChangeInput(keyboard, new KeyboardState());

        Debug.Log($"JUMPIO_INPUT_AFTER acted={acted} acting={action.IsActing} pressed={inputAction.IsPressed()} state={animator.GetCurrentAnimatorStateInfo(0).shortNameHash}");
        Assert(acted == 1 && action.IsActing && State("Action"), "E publishes PlayerActed and starts ROLL");
        var feedback = UnityEngine.Object.FindAnyObjectByType<ActionFeedback>();
        Assert(feedback != null && feedback.BurstsPlayed == acted, "Action emits pooled particles through PlayerActed");
        Assert(body.linearVelocity == velocity && body.gravityScale == gravity && player.Speed == speed, "Action preserves velocity, gravity and speed");
        Assert(Mathf.Abs(action.Duration - AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Knight/Knight_Action.anim").length) < 0.0001f, "attack duration equals ROLL clip length");
        ChangeInput(keyboard, new KeyboardState(Key.X));

        yield return 0.08f;
        ChangeInput(keyboard, new KeyboardState());

        Assert(acted == 1, "X is rejected during cooldown");
        yield return 0.65f;
        Assert(!action.IsActing && !State("Action"), "ROLL and enemy attack window end with the clip");
        float readyAt = (float)typeof(PlayerAction).GetField("readyAt", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(action);
        Assert(Mathf.Abs(readyAt - activatedAt - 1f) < 0.001f, "cooldown is exactly one second from activation");
        if (Time.time < readyAt) Assert(!action.TryAct(), "cooldown rejects input after ROLL ends");
        while (Time.time <= readyAt) yield return null;
        ChangeInput(keyboard, new KeyboardState(Key.X));

        yield return 0.08f;
        ChangeInput(keyboard, new KeyboardState());

        Assert(acted == 2 && action.IsActing, "X starts Action after cooldown");
        yield return 0.15f;
        Capture("_local/animation_action.png");
        yield return 1.05f;
        ChangeInput(gamepad, new GamepadState().WithButton(GamepadButton.West));

        yield return 0.08f;
        ChangeInput(gamepad, new GamepadState());

        Assert(acted == 3 && action.IsActing, "gamepad west starts Action");
        var enemies = UnityEngine.Object.FindObjectsByType<EnemyDamage>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            ResetAttempt();
            yield return 0.08f;
            Assert(action.TryAct(), "Action can start on a fresh attempt");
            int before = acted;
            TouchEnemy(enemy, false);
            yield return 0.12f;
            Assert(enemy.IsDefeated && health.Health == health.MaxHealth && acted == before + 1, "Action contact defeats " + enemy.name + " without damage and publishes PlayerActed");
            var frame = enemy.GetComponent<SpriteRenderer>().sprite.name;
            Assert(frame.StartsWith("Dead-Sheet") || frame.StartsWith("Hit-Sheet"), "enemy shows its defeat sprite sequence");
            body.simulated = false;
        }
        ResetAttempt();
        yield return 0.08f;
        TouchEnemy(enemies[0], false);
        yield return 0.12f;
        Assert(!enemies[0].IsDefeated && health.Health == health.MaxHealth - 1, "ordinary enemy contact still deals damage");
        Assert(State("Hurt"), "surviving damage plays Hurt");
        ResetAttempt();
        yield return 0.08f;
        int stompBefore = acted;
        TouchEnemy(enemies[0], true);
        yield return 0.12f;
        Assert(enemies[0].IsDefeated && health.Health == health.MaxHealth && acted == stompBefore + 1, "stomp defeats enemy and publishes PlayerActed without changing trajectory");
        ResetAttempt();
        yield return 0.08f;
        var orb = UnityEngine.Object.FindAnyObjectByType<JumpOrb>();
        int orbBefore = acted;
        orb.Activate(player);
        yield return 0.08f;
        Assert(acted == orbBefore + 1 && State("Action"), "orb activation publishes PlayerActed and plays Action");
        Assert(feedback.BurstsPlayed == acted, "input, enemy defeat, stomp and orb share the same particle subscriber");
        ResetAttempt();
        yield return 0.08f;
        Assert(action.TryAct(), "Action starts before hazard contact");
        var hazards = GameObject.Find("Hazards_Tilemap").GetComponent<Tilemap>();
        foreach (var cell in hazards.cellBounds.allPositionsWithin)
        {
            if (!hazards.HasTile(cell)) continue;
            var position = hazards.GetCellCenterWorld(cell);
            player.transform.position = position;
            body.position = position;
            break;
        }
        body.linearVelocity = Vector2.zero;
        body.simulated = true;
        Physics2D.SyncTransforms();
        yield return 0.12f;
        Assert(health.Health == 0 && State("Death") && !action.IsActing, "spikes remain lethal during ROLL and play Death");
        Assert(player.GetComponentInChildren<SpriteRenderer>().enabled, "knight DEATH remains visible before automatic retry");
        yield return 0.2f;
        Capture("_local/animation_death.png");
        ResetAttempt();
        player.GetComponentInChildren<PlayerVisual>().ApplySkin(AssetDatabase.LoadAssetAtPath<CharacterSkin>("Assets/Art/Skins/Skin_Square.asset"));
        yield return 0.1f;
        Assert(State("Run") && Array.Exists(animator.GetCurrentAnimatorClipInfo(0), info => info.clip.name == "Run"), "cube skin retains its original Run clip");
        Assert(action.TryAct(), "cube skin can use the same Action window");
        yield return 0.1f;
        Assert(State("Action"), "cube skin reaches Action without replacing its locomotion clips");
        Debug.Log("JUMPIO_ANIMATION_VERIFICATION_OK");
    }
    static void Capture(string path)
    {
        var camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
        var originalPosition = camera.transform.position;
        var originalSize = camera.orthographicSize;
        var originalTarget = camera.targetTexture;
        var originalActive = RenderTexture.active;
        var texture = new RenderTexture(640, 360, 24);
        var image = new Texture2D(640, 360, TextureFormat.RGB24, false);
        try
        {
            camera.transform.position = player.transform.position + Vector3.back * 10f;
            camera.orthographicSize = 2f;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.transform.position = originalPosition;
            camera.orthographicSize = originalSize;
            camera.targetTexture = originalTarget;
            RenderTexture.active = originalActive;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
