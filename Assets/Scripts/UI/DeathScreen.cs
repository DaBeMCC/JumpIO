using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
// Muestra el número de intento y la cuenta atrás del reinicio automático, con accesos a Retry y Menu.
public class DeathScreen : MonoBehaviour
{
    [SerializeField] TMP_Text attemptText;
    [SerializeField] TMP_Text countdownText;
    [SerializeField] Selectable firstSelected;
    [SerializeField] string menuScene = "MainMenu";
    [SerializeField] float delay = 0.2f;
    [SerializeField] float fadeDuration = 0.15f;

    CanvasGroup group;
    Coroutine showing;
    float restartAt;
    int shownSeconds = -1;
    bool dead;
    bool paused;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        SetVisible(0f);
    }

    void OnEnable()
    {
        GameEvents.PlayerDied += Show;
        GameEvents.AttemptStarted += OnAttemptStarted;
        GameEvents.LevelCompleted += Hide;
        GameEvents.PauseChanged += OnPauseChanged;
    }

    void OnDisable()
    {
        GameEvents.PlayerDied -= Show;
        GameEvents.AttemptStarted -= OnAttemptStarted;
        GameEvents.LevelCompleted -= Hide;
        GameEvents.PauseChanged -= OnPauseChanged;
    }

    void Show()
    {
        int attempt = GameManager.Instance != null ? GameManager.Instance.Attempt : 0;
        attemptText.text = "ATTEMPT " + attempt;
        restartAt = Time.time + (GameManager.Instance != null ? GameManager.Instance.RestartDelay : 0f);
        shownSeconds = -1;
        dead = true;
        UpdateCountdown();
        if (showing != null) StopCoroutine(showing);
        showing = StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        yield return new WaitForSecondsRealtime(delay);
        while (paused) yield return null;
        if (EventSystem.current != null && firstSelected != null)
            EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            SetVisible(t / fadeDuration);
            yield return null;
        }
        SetVisible(1f);
        showing = null;
    }

    void Update()
    {
        if (group.alpha > 0f) UpdateCountdown();
    }

    void UpdateCountdown()
    {
        if (countdownText == null) return;
        int seconds = Mathf.Max(1, Mathf.CeilToInt(restartAt - Time.time));
        if (seconds == shownSeconds) return;
        shownSeconds = seconds;
        countdownText.text = "RESTARTING IN " + seconds;
    }

    void OnPauseChanged(bool isPaused)
    {
        paused = isPaused;
        if (!dead) return;
        if (showing != null) StopCoroutine(showing);
        showing = null;
        if (isPaused) SetVisible(0f);
        else showing = StartCoroutine(FadeIn());
    }

    void OnAttemptStarted(int attempt) => Hide();

    void Hide()
    {
        dead = false;
        if (showing != null) StopCoroutine(showing);
        showing = null;
        SetVisible(0f);
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && selected.transform.IsChildOf(transform)) EventSystem.current.SetSelectedGameObject(null);
    }

    void SetVisible(float alpha)
    {
        group.alpha = alpha;
        group.interactable = group.blocksRaycasts = alpha > 0.99f;
    }

    public void Retry()
    {
        if (GameManager.Instance != null) GameManager.Instance.RestartNow();
    }

    public void Menu() => SceneNavigation.Load(menuScene);
}
