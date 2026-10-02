using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelLandscape : MonoBehaviour
{
    [SerializeField] Tilemap ground;
    [SerializeField] TileBase grass;
    [SerializeField] TileBase earth;
    [SerializeField] Sprite forest;
    [SerializeField] TileBase slab;
    [SerializeField] TileBase slope;
    [SerializeField] Tilemap hazards;
    [SerializeField] TileBase spike;
    [SerializeField] Sprite backdropFill;

    public Tilemap Ground => ground;
    public TileBase Grass => grass;
    public TileBase Earth => earth;
    public Sprite Forest => forest;
    public TileBase Slab => slab;
    public TileBase Slope => slope;
    public Tilemap Hazards => hazards;
    public TileBase Spike => spike;
    public Sprite BackdropFill => backdropFill;
}
