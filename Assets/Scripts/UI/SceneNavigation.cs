using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneNavigation
{
    public static void Load(string sceneName)
    {
        // Se restaura el tiempo antes de cargar para que una partida en pausa no abra la siguiente escena congelada.
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
