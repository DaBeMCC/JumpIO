using UnityEngine;

[RequireComponent(typeof(PlayerController), typeof(PlayerInput))]
public sealed class PlayerAction : MonoBehaviour
{
    [SerializeField] private AnimationClip rollClip;
    [SerializeField] private float cooldown = 1f;
    PlayerController player;
    PlayerInput input;
    float activeUntil;
    float readyAt;
    public float Duration => rollClip != null ? rollClip.length : 0f;
    public bool IsActing => player.IsActive && Time.time < activeUntil;
    void Awake()
    {
        player = GetComponent<PlayerController>();
        input = GetComponent<PlayerInput>();
    }
    void OnEnable()
    {
        input.ActionRequested += OnRequested;
        GameEvents.PlayerActed += OnActed;
        GameEvents.AttemptStarted += Reset;
        GameEvents.PlayerDied += Stop;
        GameEvents.LevelCompleted += Stop;
    }
    void OnDisable()
    {
        input.ActionRequested -= OnRequested;
        GameEvents.PlayerActed -= OnActed;
        GameEvents.AttemptStarted -= Reset;
        GameEvents.PlayerDied -= Stop;
        GameEvents.LevelCompleted -= Stop;
    }
    void OnRequested() => TryAct();
    public bool TryAct()
    {
        if (!player.IsActive || Time.timeScale <= 0f || IsActing || Time.time < readyAt || Duration <= 0f) return false;
        readyAt = Time.time + cooldown;
        GameEvents.RaisePlayerActed(transform.position);
        return true;
    }
    void OnActed(Vector3 position)
    {
        if (player.IsActive) activeUntil = Time.time + Duration;
    }
    void Reset(int attempt)
    {
        activeUntil = readyAt = 0f;
    }
    void Stop() => activeUntil = 0f;
}
