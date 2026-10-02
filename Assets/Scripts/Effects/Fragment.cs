using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public class Fragment : MonoBehaviour
{
    [FormerlySerializedAs("duracion")]
    [SerializeField] float lifetime = 0.8f;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Color color;
    float time;

    public IObjectPool<Fragment> Pool { get; set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    public void Launch(Vector3 position, Vector2 velocity, Color startColor)
    {
        transform.SetPositionAndRotation(position, Quaternion.identity);
        rb.position = position;
        rb.linearVelocity = velocity;
        rb.angularVelocity = Random.Range(-720f, 720f);
        color = startColor;
        sr.color = color;
        time = 0f;
    }

    void Update()
    {
        time += Time.deltaTime;
        sr.color = new Color(color.r, color.g, color.b, color.a * (1f - time / lifetime));
        if (time >= lifetime) Pool.Release(this);
    }
}
