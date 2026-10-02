using UnityEngine;

public class ScoreCounter : MonoBehaviour
{
    [SerializeField] Transform player;

    int blocks, bonus;

    void OnEnable()
    {
        GameEvents.AttemptStarted += ResetScore;
        GameEvents.PointsEarned += AddBonus;
    }

    void OnDisable()
    {
        GameEvents.AttemptStarted -= ResetScore;
        GameEvents.PointsEarned -= AddBonus;
    }

    void ResetScore(int attempt)
    {
        blocks = bonus = 0;
        Publish();
    }

    void AddBonus(int amount, Vector3 position)
    {
        bonus += amount;
        Publish();
    }

    void Update()
    {
        int travelled = Mathf.Max(0, Mathf.FloorToInt(player.position.x));
        if (travelled <= blocks) return;
        blocks = travelled;
        Publish();
    }

    void Publish() => GameEvents.RaiseScoreChanged(blocks + bonus);
}
