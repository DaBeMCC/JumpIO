using UnityEngine;

public static class PlaytestStart
{
    public const string Key = "JumpIO.PlaytestStart";

    public static bool Active { get; private set; }
    public static float Time { get; private set; }
    public static PlayerModeType Mode { get; private set; }
    public static float CorridorY { get; private set; }
    public static float Y { get; private set; }
    public static bool GravityInverted { get; private set; }

    [System.Serializable]
    public struct Data
    {
        public float time;
        public PlayerModeType mode;
        public float corridorY;
        public float y;
        public bool gravityInverted;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Load()
    {
        Active = false;
#if UNITY_EDITOR
        string json = UnityEditor.SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<Data>(json);
        Time = Mathf.Max(0f, data.time);
        Mode = data.mode;
        CorridorY = data.corridorY;
        Y = data.y;
        GravityInverted = data.gravityInverted;
        Active = true;
#endif
    }
}
