using UnityEngine;

[System.Serializable]
public abstract class PlayerMode
{
    protected PlayerController player;

    public void Bind(PlayerController owner) => player = owner;

    public virtual float GravityMultiplier => 1f;
    public virtual bool CeilingKills => false;
    public virtual float Bounds => 0f;
    public virtual float PadMultiplier => 1f;
    public virtual float OrbMultiplier => 1f;

    public virtual float Enter(PlayerModeType previous, float relativeVy) => relativeVy * 0.5f;

    public abstract float Step(float relativeVy, float dt);

    public virtual float Rotate(float angle, float dt) => 0f;

    public virtual float Land(float angle) => Mathf.Round(angle / 90f) * 90f;

    protected float FlightClamp(float relativeVy, float maxDown, float maxUp)
    {
        if (relativeVy > -maxDown && relativeVy < maxUp) player.Boosted = false;
        return player.Boosted ? relativeVy : Mathf.Clamp(relativeVy, -maxDown, maxUp);
    }
    protected float TurnTowardsVelocity(float angle, float dt, float rate)
    {
        Vector2 v = player.Velocity;
        float target = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        return Mathf.LerpAngle(angle, target, 1f - Mathf.Pow(1f - rate, dt * 60f));
    }
}
