using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// Singleton persistente que controla los intentos. Reiniciar reutiliza la escena cargada en vez de volver a cargarla.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [FormerlySerializedAs("esperaReinicio")]
    [SerializeField] float restartDelay = 3f;

    public int Attempt { get; private set; }
    public float RestartDelay => restartDelay;

    bool restartPending;

    void Awake()
    {
        // Al volver a cargar una escena con este prefab aparece una segunda copia; se conserva la original.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        GameEvents.PlayerDied += OnPlayerDied;
        GameEvents.LevelCompleted += OnLevelCompleted;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        GameEvents.PlayerDied -= OnPlayerDied;
        GameEvents.LevelCompleted -= OnLevelCompleted;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void LoadScene(string sceneName)
    {
        StopAllCoroutines();
        restartPending = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
        Attempt = 0;
        if (PlayerController.Instance != null && PlayerController.Instance.gameObject.scene == scene) StartAttempt();
    }

    void OnPlayerDied()
    {
        restartPending = true;
        StartCoroutine(RestartAfterDelay());
    }

    public void RestartNow()
    {
        if (!restartPending) return;
        StopAllCoroutines();
        StartAttempt();
    }

    void OnLevelCompleted()
    {
        restartPending = false;
        StopAllCoroutines();
    }

    IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(restartDelay);
        StartAttempt();
    }

    void StartAttempt()
    {
        restartPending = false;
        Attempt++;
        GameEvents.RaiseAttemptStarted(Attempt);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    // Reinicia los estáticos al dar Play para que el Editor funcione aunque esté desactivado el domain reload.
    static void ClearInstance() => Instance = null;
}
