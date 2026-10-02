using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "LevelData", menuName = "JumpIO/Level Data")]
public class LevelData : ScriptableObject
{
    [FormerlySerializedAs("nombre")]
    public string levelName;
    [FormerlySerializedAs("cancion")]
    public AudioClip song;
    [FormerlySerializedAs("offsetCancion")]
    public float songOffset;
    [FormerlySerializedAs("latenciaMs")]
    public float latencyMs;
    public PlayerModeType startMode;
    public bool startGravityInverted;
    public float startCorridorY = 0.5f;
    public float startY = 0.5f;
    [FormerlySerializedAs("largo")]
    public float length;

    [Header("Beat")]
    public float bpm;
    public float firstBeat;

    public int BeatAt(double levelTime) =>
        bpm > 0f ? (int)System.Math.Floor((songOffset + levelTime - firstBeat) * bpm / 60.0) : int.MinValue;

    [Header("x → time table")]
    [FormerlySerializedAs("xCambios")]
    public float[] changeX = { 0f };
    [FormerlySerializedAs("velocidades")]
    public float[] speeds = { 10.386f };
    [FormerlySerializedAs("tCambios")]
    public float[] changeTimes = { 0f };

    public void RecalculateTimes()
    {
        changeTimes = new float[changeX.Length];
        for (int i = 1; i < changeX.Length; i++)
            changeTimes[i] = changeTimes[i - 1] + (changeX[i] - changeX[i - 1]) / speeds[i - 1];
    }

    public float XAtTime(float t)
    {
        int i = Segment(changeTimes, t);
        return changeX[i] + (t - changeTimes[i]) * speeds[i];
    }

    public float TimeAtX(float x)
    {
        int i = Segment(changeX, x);
        return changeTimes[i] + (x - changeX[i]) / speeds[i];
    }

    public float SpeedAtTime(float t) => speeds[Segment(changeTimes, t)];

    static int Segment(float[] table, float v)
    {
        int lo = 0, hi = table.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (table[mid] <= v) lo = mid; else hi = mid - 1;
        }
        return lo;
    }
}
