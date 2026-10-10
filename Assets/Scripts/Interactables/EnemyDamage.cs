using UnityEngine;

[RequireComponent(typeof(Collider2D))]
// Los enemigos dañan al contacto salvo que el jugador esté atacando (ROLL) o les caiga encima.
public class EnemyDamage : MonoBehaviour
{
    [SerializeField] private Sprite[] defeatedFrames;
    [SerializeField] private float framesPerSecond = 12f;
    bool used;
    bool defeated;
    Collider2D area;
    SpriteRenderer sprite;
    SpriteLoop loop;
    Sprite initialSprite;
    Coroutine defeatAnimation;
    public bool IsDefeated => defeated;
    void Awake()
    {
        area = GetComponent<Collider2D>();
        sprite = GetComponent<SpriteRenderer>();
        loop = GetComponent<SpriteLoop>();
        initialSprite = sprite.sprite;
    }
    void OnEnable() => GameEvents.AttemptStarted += Reset;
    void OnDisable() => GameEvents.AttemptStarted -= Reset;
    void Reset(int attempt)
    {
        used = defeated = false;
        if (defeatAnimation != null) StopCoroutine(defeatAnimation);
        area.enabled = sprite.enabled = true;
        sprite.sprite = initialSprite;
        if (loop != null) loop.enabled = true;
    }
    void OnTriggerEnter2D(Collider2D other) => Hit(other);
    void OnTriggerStay2D(Collider2D other) => Hit(other);
    void Hit(Collider2D other)
    {
        if (defeated || !other.TryGetComponent(out PlayerController player) || !player.IsActive) return;
        bool acting = other.TryGetComponent(out PlayerAction action) && action.IsActing;
        bool above = player.GravityDirection > 0f
            ? other.bounds.min.y >= area.bounds.center.y - 0.1f
            : other.bounds.max.y <= area.bounds.center.y + 0.1f;
        bool stomping = above && player.RelativeVerticalVelocity < -0.01f;
        if (acting || stomping)
        {
            defeated = true;
            area.enabled = false;
            if (loop != null) loop.enabled = false;
            GameEvents.RaisePlayerActed(player.transform.position);
            defeatAnimation = StartCoroutine(ShowDefeat());
        }
        else if (!used && other.TryGetComponent(out PlayerHealth health)) used = health.TakeDamage();
    }
    System.Collections.IEnumerator ShowDefeat()
    {
        foreach (var frame in defeatedFrames)
        {
            sprite.sprite = frame;
            yield return new WaitForSeconds(1f / framesPerSecond);
        }
        sprite.enabled = false;
        defeatAnimation = null;
    }
}
