using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [FormerlySerializedAs("esperaReinicio")]
    [SerializeField] float restartDelay = 1f;

    public int Attempt { get; private set; }

    void Awake()
    {
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

    void OnPlayerDied() => StartCoroutine(RestartAfterDelay());

    void OnLevelCompleted() => StopAllCoroutines();

    IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(restartDelay);
        StartAttempt();
    }

    void StartAttempt()
    {
        Attempt++;
        GameEvents.RaiseAttemptStarted(Attempt);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearInstance() => Instance = null;
}
