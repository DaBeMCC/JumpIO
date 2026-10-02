using UnityEngine;

[System.Serializable]
public class CubeMode : PlayerMode
{
    public float jumpVelocity = 21f;

    public override bool CeilingKills => true;

    public override float Enter(PlayerModeType previous, float relativeVy) =>
        previous == PlayerModeType.Ball ? relativeVy : relativeVy * 0.5f;

    public float JumpDuration => 2f * jumpVelocity / player.BaseGravity;

    public override float Step(float relativeVy, float dt)
    {
        if (!player.IsGrounded || !player.IsHeld) return relativeVy;
        player.IsGrounded = false;
        player.ClearPress();
        GameEvents.RaiseJumped(player.transform.position);
        return jumpVelocity;
    }

    public override float Rotate(float angle, float dt)
    {
        if (player.IsGrounded) return angle;
        return angle - player.GravityDirection * 180f / JumpDuration * dt;
    }
}
