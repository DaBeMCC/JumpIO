using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(PlayerController))]
// Vida e invulnerabilidad. Los pinchos matan al cubo al instante, pero en los modos de vuelo solo quitan un punto; los enemigos quitan un punto.
public class PlayerHealth : MonoBehaviour, IDamageable
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
    [SerializeField] float knockbackVelocity = 7f;

    PlayerController controller;
    int health;
    float invulnerableUntil;
    bool dead = true;

    public int Health => health;
    public int MaxHealth => maxHealth;
    public float InvulnerabilityDuration => invulnerabilityDuration;
    public bool IsAlive => !dead;

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
        // Como en Geometry Dash, el cubo muere con cualquier pincho; la vida solo cuenta en los modos de vuelo.
        if (controller.ModeType == PlayerModeType.Cube)
        {
            health = 0;
            GameEvents.RaisePlayerDamaged(health, maxHealth);
            GameEvents.RaiseDamaged(transform.position);
            Die();
            return;
        }
        TakeDamage(1, other.bounds.center);
    }

    public bool TakeDamage(int amount, Vector2 source)
    {
        if (dead || !controller.IsActive || Time.time < invulnerableUntil) return false;
        health = Mathf.Max(0, health - amount);
        // Invulnerabilidad breve para que un enemigo o una fila de pinchos no quite toda la vida en un solo contacto.
        invulnerableUntil = Time.time + invulnerabilityDuration;
        GameEvents.RaisePlayerDamaged(health, maxHealth);
        GameEvents.RaiseDamaged(transform.position);
        if (health <= 0) Die();
        else Knockback(source);
        return true;
    }

    // El empujón es solo vertical: el avance horizontal lo marca la música y moverlo desfasaría al jugador de la canción.
    void Knockback(Vector2 source)
    {
        float above = (transform.position.y - source.y) * controller.GravityDirection;
        controller.Launch(above >= 0f ? knockbackVelocity : -knockbackVelocity);
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
