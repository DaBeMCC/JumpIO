using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneNavigation
{
    public static void Load(string sceneName)
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.LoadScene(sceneName);
        else SceneManager.LoadScene(sceneName);
    }
    public static void Retry() => Load(SceneManager.GetActiveScene().name);
    public static void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
