using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

[RequireComponent(typeof(TextMeshPro))]
public class FloatingText : MonoBehaviour
{
    [FormerlySerializedAs("duracion")]
    [SerializeField] float duration = 0.7f;
    [FormerlySerializedAs("subida")]
    [SerializeField] float rise = 1.5f;
    [FormerlySerializedAs("escala")]
    [SerializeField] AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.2f);

    TextMeshPro text;
    Vector3 origin;
    float time;

    public IObjectPool<FloatingText> Pool { get; set; }

    void Awake() => text = GetComponent<TextMeshPro>();

    public void Show(string content, Vector3 position)
    {
        text.text = content;
        origin = position;
        time = 0f;
        Refresh(0f);
    }

    void Update()
    {
        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / duration);
        Refresh(t);
        if (t >= 1f) Pool.Release(this);
    }

    void Refresh(float t)
    {
        transform.position = origin + Vector3.up * (rise * t);
        transform.localScale = Vector3.one * scaleCurve.Evaluate(t);
        text.alpha = 1f - t * t;
    }
}
