using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelLandscape : MonoBehaviour
{
    [System.Serializable]
    public struct TileSwap
    {
        public TileBase from;
        public TileBase to;
        public bool ignoreScale;
    }

    [SerializeField] Tilemap ground;
    [SerializeField] TileBase grass;
    [SerializeField] TileBase earth;
    [SerializeField] Sprite forest;
    [SerializeField] TileBase slab;
    [SerializeField] TileBase slope;
    [SerializeField] Tilemap hazards;
    [SerializeField] TileBase spike;
    [SerializeField] Sprite backdropFill;
    [SerializeField] TileSwap[] swaps;

    public Tilemap Ground => ground;
    public TileBase Grass => grass;
    public TileBase Earth => earth;
    public Sprite Forest => forest;
    public TileBase Slab => slab;
    public TileBase Slope => slope;
    public Tilemap Hazards => hazards;
    public TileBase Spike => spike;
    public Sprite BackdropFill => backdropFill;
    public TileSwap[] Swaps => swaps;
}
