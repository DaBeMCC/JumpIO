using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BeatPulse : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float strength = 0.55f;
    [SerializeField] float duration = 0.18f;
    [SerializeField, Min(1)] int every = 1;

    SpriteRenderer sprite;
    Color baseColor;
    float flash;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        baseColor = sprite.color;
    }

    void OnEnable() => GameEvents.Beat += OnBeat;

    void OnDisable()
    {
        GameEvents.Beat -= OnBeat;
        flash = 0f;
        sprite.color = baseColor;
    }

    void OnBeat(int beat)
    {
        if (beat % every == 0) flash = 1f;
    }

    void Update()
    {
        if (flash <= 0f) return;
        flash = Mathf.Max(0f, flash - Time.deltaTime / duration);
        sprite.color = Color.Lerp(baseColor, Color.white, flash * flash * strength);
    }
}
