using UnityEditor;

[InitializeOnLoad]
public static class LevelEditorPlaytest
{
    static LevelEditorPlaytest()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) SessionState.EraseString(PlaytestStart.Key);
        };
    }

    public static void Start(PlaytestStart.Data data)
    {
        SessionState.SetString(PlaytestStart.Key, UnityEngine.JsonUtility.ToJson(data));
        EditorApplication.isPlaying = true;
    }

    public static void StartFromBeginning()
    {
        SessionState.EraseString(PlaytestStart.Key);
        EditorApplication.isPlaying = true;
    }
}
