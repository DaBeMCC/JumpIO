using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class DamageOverlay : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float peakAlpha = 0.45f;
    [SerializeField] float fadeDuration = 0.4f;

    Image image;
    Coroutine fade;

    void Awake()
    {
        image = GetComponent<Image>();
        SetAlpha(0f);
    }

    void OnEnable() => GameEvents.PlayerDamaged += Flash;
    void OnDisable() => GameEvents.PlayerDamaged -= Flash;

    void Flash(int health, int maxHealth)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Fade());
    }

    IEnumerator Fade()
    {
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            SetAlpha(peakAlpha * (1f - t / fadeDuration));
            yield return null;
        }
        SetAlpha(0f);
        fade = null;
    }

    void SetAlpha(float alpha)
    {
        var c = image.color;
        image.color = new Color(c.r, c.g, c.b, alpha);
    }
}
