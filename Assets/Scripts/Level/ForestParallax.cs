using UnityEngine;

public class ForestParallax : MonoBehaviour
{
    [SerializeField] Camera target;
    [SerializeField, Range(0f, 1f)] float scrollFactor = 0.2f;
    [SerializeField] float offsetY;
    [SerializeField] SpriteRenderer[] panels;

    float width;
    float startX;

    public void Configure(Camera camera, float factor, float verticalOffset, SpriteRenderer[] layerPanels)
    {
        target = camera;
        scrollFactor = factor;
        offsetY = verticalOffset;
        panels = layerPanels;
    }

    void Start()
    {
        if (target == null || panels == null || panels.Length == 0) return;
        startX = target.transform.position.x;
        width = panels[0].bounds.size.x;
    }

    void LateUpdate()
    {
        if (width <= 0f) return;
        Vector3 cameraPosition = target.transform.position;
        float origin = cameraPosition.x - (cameraPosition.x - startX) * scrollFactor;
        int center = Mathf.FloorToInt((cameraPosition.x - origin) / width + 0.5f);
        for (int i = 0; i < panels.Length; i++)
        {
            int cell = center + i - panels.Length / 2;
            panels[i].transform.position = new Vector3(origin + cell * width, cameraPosition.y + offsetY, transform.position.z);
        }
    }
}
