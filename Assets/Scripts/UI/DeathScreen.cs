using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
// Muestra el número de intento, el progreso y el puntaje del intento, con la cuenta atrás del reinicio automático y accesos a Retry y Menu.
public class DeathScreen : MonoBehaviour
{
    [SerializeField] TMP_Text attemptText;
    [SerializeField] TMP_Text countdownText;
    [SerializeField] TMP_Text progressText;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] Image restartFill;
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
    int runScore;
    float runProgress;
    RectTransform card;
    Vector2 restingPosition;
    float restartDuration;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        card = GetComponent<RectTransform>();
        restingPosition = card.anchoredPosition;
        SetVisible(0f);
    }

    void OnEnable()
    {
        GameEvents.PlayerDied += Show;
        GameEvents.AttemptStarted += OnAttemptStarted;
        GameEvents.LevelCompleted += Hide;
        GameEvents.PauseChanged += OnPauseChanged;
        GameEvents.ProgressChanged += OnProgressChanged;
        GameEvents.ScoreChanged += OnScoreChanged;
    }

    void OnDisable()
    {
        GameEvents.PlayerDied -= Show;
        GameEvents.AttemptStarted -= OnAttemptStarted;
        GameEvents.LevelCompleted -= Hide;
        GameEvents.PauseChanged -= OnPauseChanged;
        GameEvents.ProgressChanged -= OnProgressChanged;
        GameEvents.ScoreChanged -= OnScoreChanged;
    }

    void Show()
    {
        int attempt = GameManager.Instance != null ? GameManager.Instance.Attempt : 0;
        attemptText.text = "ATTEMPT " + attempt;
        if (progressText != null) progressText.text = Mathf.FloorToInt(runProgress * 100f) + "%";
        if (scoreText != null) scoreText.text = runScore.ToString("N0");
        restartDuration = GameManager.Instance != null ? GameManager.Instance.RestartDelay : 1f;
        restartAt = Time.time + restartDuration;
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
            card.anchoredPosition = restingPosition + Vector2.down * (18f * (1f - t / fadeDuration));
            yield return null;
        }
        SetVisible(1f);
        card.anchoredPosition = restingPosition;
        showing = null;
    }

    // La barra de reinicio se vacía cada frame porque es una cuenta atrás visual; el resto de la pantalla se actualiza por eventos.
    void Update()
    {
        if (group.alpha > 0f) UpdateCountdown();
        if (dead && restartFill != null) restartFill.fillAmount = Mathf.Clamp01((restartAt - Time.time) / Mathf.Max(0.01f, restartDuration));
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

    void OnProgressChanged(float progress) => runProgress = Mathf.Clamp01(progress);

    void OnScoreChanged(int score) => runScore = score;

    void OnAttemptStarted(int attempt)
    {
        runProgress = 0f;
        runScore = 0;
        Hide();
    }

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
