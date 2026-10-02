using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

public class DeathEffect : MonoBehaviour
{
    [FormerlySerializedAs("prefabFragmento")]
    [SerializeField] Fragment fragmentPrefab;
    [FormerlySerializedAs("cantidad")]
    [SerializeField] int count = 10;
    [FormerlySerializedAs("rangoFuerza")]
    [SerializeField] Vector2 forceRange = new Vector2(4f, 10f);

    ObjectPool<Fragment> pool;

    public Color FragmentColor { get; set; } = Color.white;

    void Awake()
    {
        pool = new ObjectPool<Fragment>(
            Create,
            f => f.gameObject.SetActive(true),
            f => f.gameObject.SetActive(false),
            f => { if (f != null) Destroy(f.gameObject); },
            collectionCheck: true,
            defaultCapacity: count,
            maxSize: count * 3);
    }

    void OnEnable() => GameEvents.PlayerDied += Explode;
    void OnDisable() => GameEvents.PlayerDied -= Explode;

    Fragment Create()
    {
        var fragment = Instantiate(fragmentPrefab);
        fragment.Pool = pool;
        return fragment;
    }

    void Explode()
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            pool.Get().Launch(transform.position, direction * Random.Range(forceRange.x, forceRange.y), FragmentColor);
        }
    }
}
