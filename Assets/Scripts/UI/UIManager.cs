using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Coordina la UI del nivel: pausa, opciones y panel de nivel completado.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] GameObject pausePanel;
    [SerializeField] GameObject completedPanel;
    [SerializeField] TMPro.TMP_Text completedAttemptText;
    [SerializeField] TMPro.TMP_Text completedScoreText;
    [SerializeField] CanvasGroup completedGroup;
    [SerializeField] RectTransform completedCard;
    [SerializeField] UnityEngine.UI.Selectable pauseFirstSelected;
    [SerializeField] UnityEngine.UI.Selectable completedFirstSelected;
    [SerializeField] GameObject optionsPanel;
    [SerializeField] UnityEngine.UI.Selectable optionsFirstSelected;
    [SerializeField] UnityEngine.UI.Selectable pauseOptionsButton;
    [SerializeField] string menuScene = "MainMenu";
    [SerializeField] InputAction pauseAction = CreatePauseAction();

    public bool IsPaused { get; private set; }
    public bool IsCompleted { get; private set; }
    int runScore;

    static InputAction CreatePauseAction()
    {
        var action = new InputAction("Pause", InputActionType.Button);
        action.AddBinding("<Keyboard>/escape");
        action.AddBinding("<Gamepad>/start");
        return action;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        pausePanel.SetActive(false);
        if (completedPanel != null) completedPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    void OnEnable()
    {
        pauseAction.Enable();
        pauseAction.performed += OnPausePressed;
        GameEvents.LevelCompleted += ShowCompletion;
        GameEvents.ScoreChanged += OnScoreChanged;
        GameEvents.AttemptStarted += OnAttemptStarted;
    }

    void OnDisable()
    {
        pauseAction.performed -= OnPausePressed;
        pauseAction.Disable();
        GameEvents.LevelCompleted -= ShowCompletion;
        GameEvents.ScoreChanged -= OnScoreChanged;
        GameEvents.AttemptStarted -= OnAttemptStarted;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    bool InOptions => optionsPanel != null && optionsPanel.activeSelf;

    void OnPausePressed(InputAction.CallbackContext context)
    {
        if (IsCompleted) return;
        if (InOptions) CloseOptions();
        else SetPaused(!IsPaused);
    }

    public void OpenOptions()
    {
        pausePanel.SetActive(false);
        optionsPanel.SetActive(true);
        Select(optionsFirstSelected);
    }

    public void CloseOptions()
    {
        optionsPanel.SetActive(false);
        pausePanel.SetActive(true);
        Select(pauseOptionsButton);
    }

    static void Select(UnityEngine.UI.Selectable selectable)
    {
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events != null) events.SetSelectedGameObject(selectable != null ? selectable.gameObject : null);
    }

    void ShowCompletion()
    {
        IsCompleted = true;
        pausePanel.SetActive(false);
        if (completedPanel != null) completedPanel.SetActive(true);
        if (completedAttemptText != null) completedAttemptText.text = (GameManager.Instance != null ? GameManager.Instance.Attempt : 1).ToString("00");
        if (completedScoreText != null) completedScoreText.text = runScore.ToString("N0");
        if (completedGroup != null && completedCard != null) StartCoroutine(RevealCompletion());
        Time.timeScale = 0f;
        GameEvents.RaisePauseChanged(true);
        if (completedFirstSelected != null && UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(completedFirstSelected.gameObject);
    }

    // El puntaje se guarda al escuchar el evento, así la tarjeta final no tiene que preguntarle a nadie.
    void OnScoreChanged(int score) => runScore = score;

    void OnAttemptStarted(int attempt) => runScore = 0;

    // La tarjeta aparece con un fundido y un leve zoom; los botones no responden hasta el final para evitar clics accidentales mientras se anima.
    System.Collections.IEnumerator RevealCompletion()
    {
        completedGroup.interactable = completedGroup.blocksRaycasts = false;
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / 0.25f);
            completedGroup.alpha = p;
            completedCard.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, 1f - Mathf.Pow(1f - p, 3f));
            yield return null;
        }
        completedCard.localScale = Vector3.one;
        completedGroup.alpha = 1f;
        completedGroup.interactable = completedGroup.blocksRaycasts = true;
    }

    public void SetPaused(bool paused)
    {
        if (IsCompleted) return;
        IsPaused = paused;
        pausePanel.SetActive(paused);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        // Congelar el tiempo también detiene la física y el reloj de la música, así el juego se reanuda sincronizado.
        Time.timeScale = paused ? 0f : 1f;
        Select(paused ? pauseFirstSelected : null);
        GameEvents.RaisePauseChanged(paused);
    }

    public void Resume() => SetPaused(false);

    public void Retry()
    {
        SceneNavigation.Retry();
    }

    public void Restart()
    {
        IsPaused = false;
        SceneNavigation.Retry();
    }

    public void GoToMainMenu()
    {
        IsPaused = false;
        SceneNavigation.Load(menuScene);
    }

    public void Quit()
    {
        SceneNavigation.Quit();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearInstance() => Instance = null;
}
