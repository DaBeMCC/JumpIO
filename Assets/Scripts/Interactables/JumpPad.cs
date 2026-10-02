using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class JumpPad : MonoBehaviour
{
    public enum PadType { Yellow, Pink, Red, Blue }

    [SerializeField] PadType type;
    [SerializeField] float baseVelocity = 30.05f;

    bool used;

    void OnEnable() => GameEvents.AttemptStarted += ResetActivation;
    void OnDisable() => GameEvents.AttemptStarted -= ResetActivation;
    void ResetActivation(int attempt) => used = false;

    public float LaunchVelocity(PlayerModeType mode) => baseVelocity * Multiplier(mode);

    float Multiplier(PlayerModeType mode) => type switch
    {
        PadType.Pink => mode switch { PlayerModeType.Ship => 0.35f, PlayerModeType.Ufo => 0.4f, PlayerModeType.Ball => 0.7f, _ => 0.65f },
        PadType.Red => mode switch { PlayerModeType.Ship => 0.63f, PlayerModeType.Ufo => 0.6f, _ => 1.25f },
        PadType.Blue => 0.8f,
        _ => 1f,
    };

    void OnTriggerEnter2D(Collider2D other) => Activate(other);
    void OnTriggerStay2D(Collider2D other) => Activate(other);

    void Activate(Collider2D other)
    {
        if (used || !other.TryGetComponent(out PlayerController player) || !player.IsActive) return;
        float facing = transform.up.y * Mathf.Sign(transform.lossyScale.y);
        if (type == PadType.Blue && facing * player.GravityDirection < 0f) return;
        used = true;
        GameEvents.RaisePadUsed(player.transform.position);
        player.Launch(LaunchVelocity(player.ModeType) * player.Mode.PadMultiplier);
        if (type != PadType.Blue) player.Boosted = type == PadType.Red;
        if (type == PadType.Blue) player.ReverseGravity();
    }
}
