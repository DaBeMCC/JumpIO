using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FinishLine : MonoBehaviour
{
    bool completed;
    void OnEnable() => GameEvents.AttemptStarted += Reset;
    void OnDisable() => GameEvents.AttemptStarted -= Reset;
    void Reset(int attempt) => completed = false;
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!completed && other.TryGetComponent(out PlayerController player) && player.IsActive)
        {
            completed = true;
            GameEvents.RaiseLevelCompleted();
        }
    }
}
