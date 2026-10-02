using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

public class FloatingTextSpawner : MonoBehaviour
{
    [FormerlySerializedAs("prefabTexto")]
    [SerializeField] FloatingText textPrefab;
    [FormerlySerializedAs("capacidad")]
    [SerializeField] int capacity = 8;

    ObjectPool<FloatingText> pool;

    void Awake()
    {
        pool = new ObjectPool<FloatingText>(
            Create,
            t => t.gameObject.SetActive(true),
            t => t.gameObject.SetActive(false),
            t => { if (t != null) Destroy(t.gameObject); },
            collectionCheck: true,
            defaultCapacity: capacity,
            maxSize: capacity * 4);
    }

    void OnEnable()
    {
        GameEvents.PointsEarned += Show;
        GameEvents.PlayerDamaged += ShowHealth;
    }
    void OnDisable()
    {
        GameEvents.PointsEarned -= Show;
        GameEvents.PlayerDamaged -= ShowHealth;
    }

    FloatingText Create()
    {
        var text = Instantiate(textPrefab, transform);
        text.Pool = pool;
        return text;
    }

    void Show(int amount, Vector3 position) => pool.Get().Show("+" + amount, position);
    void ShowHealth(int health, int maximum)
    {
        var player = PlayerController.Instance;
        if (player != null && health > 0)
            pool.Get().Show("HP " + health + "/" + maximum, player.transform.position + Vector3.up);
    }
}
