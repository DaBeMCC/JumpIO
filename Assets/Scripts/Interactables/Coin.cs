using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public class Coin : MonoBehaviour
{
    [FormerlySerializedAs("puntos")]
    [SerializeField] int points = 100;

    Collider2D area;
    SpriteRenderer sr;

    void Awake()
    {
        area = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    void OnEnable() => GameEvents.AttemptStarted += Reappear;
    void OnDisable() => GameEvents.AttemptStarted -= Reappear;

    void Reappear(int attempt) => area.enabled = sr.enabled = true;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out PlayerController _)) return;
        area.enabled = sr.enabled = false;
        GameEvents.RaisePointsEarned(points, transform.position);
        GameEvents.RaiseCoinCollected(transform.position);
    }
}
