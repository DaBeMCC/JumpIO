using UnityEngine;

public sealed class PlayerBounds
{
    readonly PlayerController player;
    readonly Rigidbody2D rb;
    readonly Collider2D body;
    readonly LayerMask groundLayer;
    readonly float wallDeathBand;
    public PlayerBounds(PlayerController player, Rigidbody2D rb, Collider2D body, LayerMask groundLayer, float wallDeathBand)
    {
        this.player = player;
        this.rb = rb;
        this.body = body;
        this.groundLayer = groundLayer;
        this.wallDeathBand = wallDeathBand;
    }
    public void Clamp(float FloorY, float CeilingY, float GravityDirection, out bool onFloor, out bool onCeiling)
    {
        onFloor = onCeiling = false;
        if (float.IsInfinity(CeilingY)) return;
        float half = body.bounds.extents.y;
        Vector2 p = rb.position, v = rb.linearVelocity;
        bool atFloor = p.y - half <= FloorY + 0.001f, atCeiling = p.y + half >= CeilingY - 0.001f;
        if (atFloor) { p.y = FloorY + half; v.y = Mathf.Max(v.y, 0f); }
        if (atCeiling) { p.y = CeilingY - half; v.y = Mathf.Min(v.y, 0f); }
        if (atFloor || atCeiling) { rb.position = p; rb.linearVelocity = v; }
        onFloor = GravityDirection > 0f ? atFloor : atCeiling;
        onCeiling = GravityDirection > 0f ? atCeiling : atFloor;
    }

    public bool WallAhead()
    {
        Bounds b = body.bounds;
        var size = new Vector2(0.06f, b.size.y * wallDeathBand);
        return Physics2D.OverlapBox(new Vector2(b.max.x + size.x / 2f, b.center.y), size, 0f, groundLayer);
    }

    public void SlidePastCorner(Collision2D collision)
    {
        if (!player.IsActive || (groundLayer.value & (1 << collision.gameObject.layer)) == 0 || WallAhead()) return;
        float half = body.bounds.extents.y;
        for (int i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            if (contact.normal.x >= -0.7f) continue;
            float dy = contact.point.y - rb.position.y;
            float y = dy > 0f ? contact.point.y - half - 0.01f : contact.point.y + half + 0.01f;
            rb.position = new Vector2(rb.position.x, y);
            return;
        }
    }
}
