using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class JumpOrb : MonoBehaviour
{
    public enum OrbType { Yellow, Pink, Red, Blue, Green, Black, Dash, GravityDash }

    [SerializeField] OrbType type;
    [SerializeField] float baseVelocity = 21f;
    [SerializeField] float blackVelocity = 28.17f, blackVelocityShip = 26.30f, blackVelocityUfo = 21.04f;
    [SerializeField] float maxDashAngle = 70f;

    bool used;

    void OnEnable() => GameEvents.AttemptStarted += Rearm;
    void OnDisable() => GameEvents.AttemptStarted -= Rearm;

    void Rearm(int attempt) => used = false;

    public float LaunchVelocity(PlayerModeType mode) => baseVelocity * Multiplier(mode);

    float Multiplier(PlayerModeType mode) => type switch
    {
        OrbType.Pink => mode switch { PlayerModeType.Ship => 0.37f, PlayerModeType.Ufo => 0.42f, PlayerModeType.Ball => 0.77f, _ => 0.72f },
        OrbType.Red => mode switch { PlayerModeType.Ship => 1f, PlayerModeType.Ufo => 1.02f, PlayerModeType.Ball => 1.34f, _ => 1.38f },
        OrbType.Blue => 0.8f,
        OrbType.Green => mode == PlayerModeType.Ship ? 0.7f : 1f,
        _ => 1f,
    };

    void OnTriggerStay2D(Collider2D other)
    {
        if (!used && other.TryGetComponent(out PlayerController player)) player.TouchOrb(this);
    }

    public void Activate(PlayerController player)
    {
        used = true;
        GameEvents.RaiseOrbUsed(player.transform.position);
        GameEvents.RaisePlayerActed(player.transform.position);
        PlayerModeType mode = player.ModeType;
        switch (type)
        {
            case OrbType.Black:
                float down = mode == PlayerModeType.Ship ? blackVelocityShip : mode == PlayerModeType.Ufo ? blackVelocityUfo : blackVelocity;
                player.Launch(-down);
                break;
            case OrbType.Dash:
            case OrbType.GravityDash:
                if (type == OrbType.GravityDash) player.ReverseGravity();
                float angle = Mathf.DeltaAngle(0f, transform.eulerAngles.z);
                player.StartDash(Mathf.Clamp(angle, -maxDashAngle, maxDashAngle));
                break;
            case OrbType.Green:
                player.FlipGravity();
                player.Launch(LaunchVelocity(mode) * player.Mode.OrbMultiplier);
                break;
            default:
                player.Launch(LaunchVelocity(mode) * player.Mode.OrbMultiplier, type == OrbType.Red);
                if (type == OrbType.Blue) player.ReverseGravity();
                break;
        }
    }
}
