using UnityEngine;

[ExecuteAlways]
public class ForestParallax : MonoBehaviour
{
    [SerializeField] private Camera target;
    [SerializeField] private Sprite forest;
    [SerializeField] private SpriteRenderer scenePreview;
    public SpriteRenderer ScenePreview => scenePreview;
    public void Configure(Camera camera, Sprite sprite, SpriteRenderer preview)
    {
        target = camera;
        forest = sprite;
        scenePreview = preview;
    }
    const float ScrollRatio = 0.22f;
    Transform[] panels;
    SpriteRenderer[] renderers;
    float width;
    float startX;
    void OnEnable()
    {
        if (scenePreview != null) scenePreview.enabled = !Application.isPlaying;
    }
    void Start()
    {
        if (!Application.isPlaying || target == null || forest == null) return;
        startX = target.transform.position.x;
        float fit = Mathf.Max(target.orthographicSize * 2.2f / forest.bounds.size.y,
            target.orthographicSize * target.aspect * 2f / forest.bounds.size.x);
        width = forest.bounds.size.x * fit;
        panels = new Transform[3];
        renderers = new SpriteRenderer[3];
        for (int i = 0; i < panels.Length; i++)
        {
            var panel = new GameObject("ForestPanel", typeof(SpriteRenderer));
            panel.transform.SetParent(transform, false);
            panel.transform.localScale = Vector3.one * fit;
            var sprite = panel.GetComponent<SpriteRenderer>();
            sprite.sprite = forest;
            sprite.sortingOrder = -100;
            sprite.color = new Color(.7f,.85f,.8f,1f);
            panels[i] = panel.transform;
            renderers[i] = sprite;
        }
    }
    void LateUpdate()
    {
        if (!Application.isPlaying || panels == null) return;
        var cameraPosition = target.transform.position;
        float origin = cameraPosition.x - (cameraPosition.x - startX) * ScrollRatio;
        int center = Mathf.FloorToInt((cameraPosition.x - origin) / width);
        for (int i = 0; i < panels.Length; i++)
        {
            int cell = center + i - 1;
            panels[i].position = new Vector3(origin + cell * width, cameraPosition.y, 5f);
            renderers[i].flipX = (cell & 1) != 0;
        }
    }
}
