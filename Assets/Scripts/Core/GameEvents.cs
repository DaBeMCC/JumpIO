using System;
using UnityEngine;

public static class GameEvents
{
    public static event Action<int> AttemptStarted;
    public static event Action<int, int> PlayerDamaged;
    public static event Action PlayerDied;
    public static event Action<int, Vector3> PointsEarned;
    public static event Action LevelCompleted;
    public static event Action<int> ScoreChanged;
    public static event Action<bool> PauseChanged;
    public static event Action<Vector3> Jumped;
    public static event Action<Vector3> Landed;
    public static event Action<Vector3> PlayerActed;
    public static event Action<Vector3> Damaged;
    public static event Action<Vector3> Died;
    public static event Action<Vector3> CoinCollected;
    public static event Action<Vector3> OrbUsed;
    public static event Action<Vector3> PadUsed;
    public static event Action<Vector3> PortalUsed;
    public static event Action<float> ProgressChanged;

    public static void RaiseAttemptStarted(int attempt) => AttemptStarted?.Invoke(attempt);
    public static void RaisePlayerDamaged(int health, int maxHealth) => PlayerDamaged?.Invoke(health, maxHealth);
    public static void RaisePlayerDied() => PlayerDied?.Invoke();
    public static void RaisePointsEarned(int amount, Vector3 position) => PointsEarned?.Invoke(amount, position);
    public static void RaiseLevelCompleted() => LevelCompleted?.Invoke();
    public static void RaiseScoreChanged(int score) => ScoreChanged?.Invoke(score);
    public static void RaisePauseChanged(bool paused) => PauseChanged?.Invoke(paused);
    public static void RaiseJumped(Vector3 position) => Jumped?.Invoke(position);
    public static void RaiseLanded(Vector3 position) => Landed?.Invoke(position);
    public static void RaisePlayerActed(Vector3 position) => PlayerActed?.Invoke(position);
    public static void RaiseDamaged(Vector3 position) => Damaged?.Invoke(position);
    public static void RaiseDied(Vector3 position) => Died?.Invoke(position);
    public static void RaiseCoinCollected(Vector3 position) => CoinCollected?.Invoke(position);
    public static void RaiseOrbUsed(Vector3 position) => OrbUsed?.Invoke(position);
    public static void RaisePadUsed(Vector3 position) => PadUsed?.Invoke(position);
    public static void RaisePortalUsed(Vector3 position) => PortalUsed?.Invoke(position);
    public static void RaiseProgressChanged(float progress) => ProgressChanged?.Invoke(progress);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Clear()
    {
        AttemptStarted = null;
        PlayerDamaged = null;
        PlayerDied = null;
        PointsEarned = null;
        LevelCompleted = null;
        ScoreChanged = null;
        PauseChanged = null;
        Jumped = null;
        Landed = null;
        PlayerActed = null;
        Damaged = null;
        Died = null;
        CoinCollected = null;
        OrbUsed = null;
        PadUsed = null;
        PortalUsed = null;
        ProgressChanged = null;
    }
}
