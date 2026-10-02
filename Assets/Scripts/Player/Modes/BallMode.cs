using UnityEngine;

[System.Serializable]
public class BallMode : PlayerMode
{
    public float flipVelocity = 6.3f;
    public float maxSpeed = 15.03f;
    public float rollSpeed = 720f;

    public override float GravityMultiplier => 0.6f;
    public override float Bounds => 8f;
    public override float PadMultiplier => 0.6f;
    public override float OrbMultiplier => 0.7f;

    public override float Enter(PlayerModeType previous, float relativeVy) =>
        previous == PlayerModeType.Ship || previous == PlayerModeType.Ufo ? relativeVy * 0.5f : relativeVy;

    public override float Step(float relativeVy, float dt)
    {
        if (player.IsGrounded && player.ConsumePress())
        {
            player.FlipGravity();
            return -flipVelocity;
        }
        if (player.IsOnCeiling && player.ConsumePress())
        {
            player.FlipGravity();
            return 0f;
        }
        return Mathf.Clamp(relativeVy, -maxSpeed, maxSpeed);
    }

    public override float Rotate(float angle, float dt) => angle - player.GravityDirection * rollSpeed * dt;

    public override float Land(float angle) => angle;
}
