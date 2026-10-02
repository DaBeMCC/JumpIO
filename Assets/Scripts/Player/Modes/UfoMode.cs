using UnityEngine;

[System.Serializable]
public class UfoMode : PlayerMode
{
    public float hopVelocity = 13.15f;
    public float gravityFalling = 0.4f, gravityRising = 0.6f;
    public float fallingThreshold = 3.6f;
    public float maxUp = 15.03f, maxDown = 12.02f;

    public override float GravityMultiplier => 0f;
    public override float Bounds => 10f;

    public override float Enter(PlayerModeType previous, float relativeVy) =>
        relativeVy * (previous == PlayerModeType.Ship ? 0.25f : 0.5f);

    public override float Step(float relativeVy, float dt)
    {
        if (player.ConsumePress())
        {
            player.IsGrounded = false;
            relativeVy = Mathf.Max(relativeVy, hopVelocity);
            GameEvents.RaiseJumped(player.transform.position);
        }
        else if (player.IsGrounded) relativeVy = 0f;

        bool falling = relativeVy < fallingThreshold * player.GravityDirection;
        relativeVy -= (falling ? gravityFalling : gravityRising) * player.BaseGravity * dt;
        return FlightClamp(relativeVy, maxDown, maxUp);
    }
}
