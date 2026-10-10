using UnityEngine;
using UnityEngine.UI;

// Se actualiza solo por eventos, nunca consultando en Update.
public class HealthBar : MonoBehaviour
{
    [SerializeField] Image fill;

    Color normalColor;
    Coroutine blinking;
    void Awake() => normalColor = fill.color;

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

    void OnAttemptStarted(int attempt)
    {
        fill.fillAmount = 1f;
        if (blinking != null) StopCoroutine(blinking);
        fill.color = normalColor;
    }
    void OnPlayerDamaged(int health, int maxHealth)
    {
        fill.fillAmount = maxHealth > 0 ? health / (float)maxHealth : 0f;
        if (blinking != null) StopCoroutine(blinking);
        blinking = StartCoroutine(Flash());
    }
    System.Collections.IEnumerator Flash()
    {
        for (int i = 0; i < 6; i++)
        {
            fill.color = i % 2 == 0 ? Color.white : normalColor;
            yield return new WaitForSecondsRealtime(0.1f);
        }
        fill.color = normalColor;
        blinking = null;
    }
}
