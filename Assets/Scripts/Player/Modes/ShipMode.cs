using UnityEngine;

[System.Serializable]
public class ShipMode : PlayerMode
{
    public float holdFalling = 0.5f, holdRising = 0.4f;
    public float releaseFalling = 0.32f, releaseRising = 0.48f;
    public float fallingThreshold = 3.6f;
    public float maxUp = 15.03f, maxDown = 12.02f;
    public float turnRate = 0.15f;

    public override float GravityMultiplier => 0f;
    public override float Bounds => 10f;

    public override float Enter(PlayerModeType previous, float relativeVy) =>
        relativeVy * (previous == PlayerModeType.Ufo ? 0.25f : 0.5f);

    public override float Step(float relativeVy, float dt)
    {
        if (player.IsGrounded && !player.IsHeld) relativeVy = 0f;
        bool falling = relativeVy < fallingThreshold * player.GravityDirection;
        float acceleration = player.IsHeld ? (falling ? holdFalling : holdRising)
                                           : -(falling ? releaseFalling : releaseRising);
        float v = relativeVy + acceleration * player.BaseGravity * dt;
        return FlightClamp(v, maxDown, maxUp);
    }

    public override float Rotate(float angle, float dt) => TurnTowardsVelocity(angle, dt, turnRate);

    public override float Land(float angle) => angle;
}
