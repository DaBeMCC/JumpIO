using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Coordina la UI del nivel: pausa, opciones y panel de nivel completado.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] GameObject pausePanel;
    [SerializeField] GameObject completedPanel;
    [SerializeField] UnityEngine.UI.Selectable pauseFirstSelected;
    [SerializeField] UnityEngine.UI.Selectable completedFirstSelected;
    [SerializeField] GameObject optionsPanel;
    [SerializeField] UnityEngine.UI.Selectable optionsFirstSelected;
    [SerializeField] UnityEngine.UI.Selectable pauseOptionsButton;
    [SerializeField] string menuScene = "MainMenu";
    [SerializeField] InputAction pauseAction = CreatePauseAction();

    public bool IsPaused { get; private set; }
    public bool IsCompleted { get; private set; }

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
    }

    void OnDisable()
    {
        pauseAction.performed -= OnPausePressed;
        pauseAction.Disable();
        GameEvents.LevelCompleted -= ShowCompletion;
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
        Time.timeScale = 0f;
        GameEvents.RaisePauseChanged(true);
        if (completedFirstSelected != null && UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(completedFirstSelected.gameObject);
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
