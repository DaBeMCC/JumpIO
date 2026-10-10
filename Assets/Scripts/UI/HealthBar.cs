using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Vida en corazones. Se actualiza solo por eventos, nunca consultando en Update.
public class HealthBar : MonoBehaviour
{
    [SerializeField] Image heartTemplate;
    [SerializeField] Sprite fullHeart;
    [SerializeField] Sprite brokenHeart;
    [SerializeField] int hearts = 3;
    [SerializeField] float breakDuration = 0.45f;
    [SerializeField, Range(0f, 1f)] float emptyAlpha = 0.3f;

    readonly List<Image> slots = new List<Image>();
    int shown;

    void Awake() => Build(hearts);

    void OnEnable()
    {
        GameEvents.AttemptStarted += OnAttemptStarted;
        GameEvents.PlayerDamaged += OnPlayerDamaged;
    }

    void OnDisable()
    {
        GameEvents.AttemptStarted -= OnAttemptStarted;
        GameEvents.PlayerDamaged -= OnPlayerDamaged;
    }

    // Los corazones se duplican desde una plantilla: si cambia la vida máxima del jugador no hay que rehacer el HUD a mano.
    void Build(int count)
    {
        while (slots.Count < count)
            slots.Add(slots.Count == 0 ? heartTemplate : Instantiate(heartTemplate, heartTemplate.transform.parent));
        for (int i = 0; i < slots.Count; i++) slots[i].gameObject.SetActive(i < count);
        hearts = count;
        shown = Mathf.Min(shown, count);
    }

    void OnAttemptStarted(int attempt)
    {
        StopAllCoroutines();
        for (int i = 0; i < hearts; i++) SetHeart(slots[i], fullHeart, 1f);
        shown = hearts;
    }

    void OnPlayerDamaged(int health, int maxHealth)
    {
        if (maxHealth > 0 && maxHealth != hearts) Build(maxHealth);
        health = Mathf.Clamp(health, 0, hearts);
        for (int i = health; i < shown; i++) StartCoroutine(Break(slots[i]));
        for (int i = 0; i < health; i++) StartCoroutine(Pulse(slots[i]));
        shown = health;
    }

    void SetHeart(Image heart, Sprite sprite, float alpha)
    {
        heart.sprite = sprite;
        heart.color = new Color(1f, 1f, 1f, alpha);
        heart.rectTransform.localScale = Vector3.one;
        heart.rectTransform.localRotation = Quaternion.identity;
    }

    // El corazón perdido salta, tiembla y se apaga, para que el golpe se lea aunque la vista esté en los obstáculos.
    IEnumerator Break(Image heart)
    {
        SetHeart(heart, brokenHeart, 1f);
        for (float t = 0f; t < breakDuration; t += Time.unscaledDeltaTime)
        {
            float k = t / breakDuration;
            heart.rectTransform.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(k * Mathf.PI));
            heart.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f * Mathf.Sin(k * Mathf.PI * 6f) * (1f - k));
            heart.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, emptyAlpha, Mathf.SmoothStep(0f, 1f, k)));
            yield return null;
        }
        SetHeart(heart, brokenHeart, emptyAlpha);
    }

    IEnumerator Pulse(Image heart)
    {
        const float duration = 0.2f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            heart.rectTransform.localScale = Vector3.one * (1f - 0.15f * Mathf.Sin(t / duration * Mathf.PI));
            yield return null;
        }
        heart.rectTransform.localScale = Vector3.one;
    }
}
