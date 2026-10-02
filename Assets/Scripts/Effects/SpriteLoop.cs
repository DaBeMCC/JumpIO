using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteLoop : MonoBehaviour
{
    [SerializeField] Sprite[] frames;
    [SerializeField] float framesPerSecond = 8f;
    SpriteRenderer sprite;
    float clock;
    void Awake() => sprite = GetComponent<SpriteRenderer>();
    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        clock += Time.deltaTime;
        sprite.sprite = frames[(int)(clock * framesPerSecond) % frames.Length];
    }
}
