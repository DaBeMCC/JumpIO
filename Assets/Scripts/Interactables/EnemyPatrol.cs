using UnityEngine;

[RequireComponent(typeof(EnemyDamage), typeof(SpriteRenderer))]
// Movimiento de los enemigos. Depende solo del tiempo desde que empezó el intento: cada intento se repite igual y el jugador
// puede aprenderse el patrón como el resto del nivel, que también va sincronizado con la música.
public class EnemyPatrol : MonoBehaviour
{
    enum Pattern { Walk, Hover }

    [SerializeField] Pattern pattern = Pattern.Walk;
    // Walk: ancho total del recorrido. Hover: altura del vaivén hacia arriba y hacia abajo.
    [SerializeField] float distance = 3f;
    // Walk: unidades por segundo. Hover: ciclos por segundo.
    [SerializeField] float speed = 1.2f;
    // Desfase entre 0 y 1 para que dos enemigos iguales no se muevan sincronizados.
    [SerializeField, Range(0f, 1f)] float phase;
    [SerializeField] bool spriteFacesLeft = true;

    EnemyDamage damage;
    SpriteRenderer sprite;
    Vector3 origin;
    float startTime;

    void Awake()
    {
        damage = GetComponent<EnemyDamage>();
        sprite = GetComponent<SpriteRenderer>();
        origin = transform.position;
    }

    void OnEnable() => GameEvents.AttemptStarted += Restart;
    void OnDisable() => GameEvents.AttemptStarted -= Restart;

    void Restart(int attempt)
    {
        startTime = Time.time;
        transform.position = Place(0f);
    }

    void Update()
    {
        if (damage.IsDefeated) return;
        Vector3 next = Place(Time.time - startTime);
        float dx = next.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.0001f) sprite.flipX = (dx > 0f) == spriteFacesLeft;
        transform.position = next;
    }

    Vector3 Place(float t)
    {
        if (pattern == Pattern.Walk)
        {
            // PingPong va y vuelve entre los dos extremos; el desfase de 0,5 hace que empiece en el centro y no salte al reiniciar.
            float offset = Mathf.PingPong(t * speed + distance * (0.5f + phase), distance) - distance * 0.5f;
            return origin + Vector3.right * offset;
        }
        // Vaivén senoidal vertical con un balanceo horizontal más lento, para que la abeja no parezca un ascensor.
        float angle = (t * speed + phase) * 2f * Mathf.PI;
        return origin + new Vector3(Mathf.Sin(angle * 0.5f) * distance * 0.5f, Mathf.Sin(angle) * distance, 0f);
    }
}
