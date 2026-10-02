using UnityEngine;

public class CorridorView : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] SpriteRenderer floorBar;
    [SerializeField] SpriteRenderer ceilingBar;
    [SerializeField] float thickness = 0.15f;

    [SerializeField] Camera cam;

    void LateUpdate()
    {
        bool show = cam != null && player.IsActive && !float.IsInfinity(player.CeilingY);
        floorBar.enabled = ceilingBar.enabled = show;
        if (!show) return;

        float width = cam.orthographicSize * cam.aspect * 2f + 2f;
        float x = cam.transform.position.x;
        Place(floorBar, x, player.FloorY - thickness / 2f, width);
        Place(ceilingBar, x, player.CeilingY + thickness / 2f, width);
        ceilingBar.enabled = cam.WorldToViewportPoint(ceilingBar.transform.position).y < 0.87f;
    }

    void Place(SpriteRenderer bar, float x, float y, float width)
    {
        bar.transform.position = new Vector3(x, y, 0f);
        bar.transform.localScale = new Vector3(width, thickness, 1f);
    }
}
