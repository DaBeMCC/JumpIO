using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(PlayerController))]
public class PlayerHealth : MonoBehaviour
{
    [FormerlySerializedAs("vidaMaxima")]
    [SerializeField] int maxHealth = 3;
    [FormerlySerializedAs("duracionInvulnerabilidad")]
    [SerializeField] float invulnerabilityDuration = 1f;
    [FormerlySerializedAs("capaPeligro")]
    [SerializeField] LayerMask hazardLayer;
    [FormerlySerializedAs("capaSuelo")]
    [SerializeField] LayerMask groundLayer;
    [FormerlySerializedAs("umbralChoque")]
    [SerializeField, Range(0f, 1f)] float crashThreshold = 0.7f;

    PlayerController controller;
    int health;
    float invulnerableUntil;
    bool dead = true;

    public int Health => health;
    public int MaxHealth => maxHealth;
    public float InvulnerabilityDuration => invulnerabilityDuration;

    void Awake() => controller = GetComponent<PlayerController>();

    void OnEnable() => GameEvents.AttemptStarted += Restart;
    void OnDisable() => GameEvents.AttemptStarted -= Restart;

    void Restart(int attempt)
    {
        health = maxHealth;
        invulnerableUntil = 0f;
        dead = false;
    }

    void OnTriggerEnter2D(Collider2D other) => CheckHazard(other);
    void OnTriggerStay2D(Collider2D other) => CheckHazard(other);

    void CheckHazard(Collider2D other)
    {
        if (dead || !InLayer(other.gameObject, hazardLayer)) return;
        if (controller.ModeType == PlayerModeType.Cube)
        {
            health = 0;
            GameEvents.RaisePlayerDamaged(health, maxHealth);
            GameEvents.RaiseDamaged(transform.position);
            Die();
            return;
        }
        TakeDamage();
    }

    public bool TakeDamage()
    {
        if (dead || !controller.IsActive || Time.time < invulnerableUntil) return false;
        health--;
        invulnerableUntil = Time.time + invulnerabilityDuration;
        GameEvents.RaisePlayerDamaged(health, maxHealth);
        GameEvents.RaiseDamaged(transform.position);
        if (health <= 0) Die();
        return true;
    }

    void OnCollisionEnter2D(Collision2D collision) => CheckCrash(collision);
    void OnCollisionStay2D(Collision2D collision) => CheckCrash(collision);

    void CheckCrash(Collision2D collision)
    {
        if (dead || !InLayer(collision.gameObject, groundLayer)) return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            bool leavingSurface = Vector2.Dot(controller.Velocity, normal) > 0.01f;
            bool ceiling = controller.Mode.CeilingKills && !leavingSurface
                           && normal.y * controller.GravityDirection < -crashThreshold;
            bool wall = normal.x < -crashThreshold && controller.WallAhead();
            if (wall || ceiling)
            {
                Die();
                return;
            }
        }
    }

    public void Die()
    {
        if (dead) return;
        dead = true;
        GameEvents.RaiseDied(transform.position);
        GameEvents.RaisePlayerDied();
    }

    static bool InLayer(GameObject obj, LayerMask mask) => (mask.value & (1 << obj.layer)) != 0;
}
