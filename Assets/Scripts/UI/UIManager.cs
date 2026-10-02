using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] GameObject pausePanel;
    [SerializeField] GameObject completedPanel;
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

    void OnPausePressed(InputAction.CallbackContext context)
    {
        if (!IsCompleted) SetPaused(!IsPaused);
    }

    void ShowCompletion()
    {
        IsCompleted = true;
        pausePanel.SetActive(false);
        if (completedPanel != null) completedPanel.SetActive(true);
        Time.timeScale = 0f;
        GameEvents.RaisePauseChanged(true);
        if (completedPanel != null)
        {
            var button = completedPanel.GetComponentInChildren<UnityEngine.UI.Button>();
            if (button != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }

    public void SetPaused(bool paused)
    {
        if (IsCompleted) return;
        IsPaused = paused;
        pausePanel.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
        GameEvents.RaisePauseChanged(paused);
    }

    public void Resume() => SetPaused(false);

    public void Retry()
    {
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
