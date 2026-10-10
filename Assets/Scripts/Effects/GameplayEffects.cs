using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// Lanza partículas reutilizadas (pool) a partir de los eventos de juego; el pool evita crear objetos durante la partida.
public class GameplayEffects : MonoBehaviour
{
    [SerializeField] PooledEffect jumpDust;
    [SerializeField] PooledEffect landDust;
    [SerializeField] PooledEffect hitSparks;
    [SerializeField] PooledEffect deathBurst;
    [SerializeField] PooledEffect coinSparkle;
    [SerializeField] PooledEffect powerUpRing;
    [SerializeField] float feetOffset = 0.45f;
    [SerializeField] int poolSize = 6;

    readonly Dictionary<PooledEffect, ObjectPool<PooledEffect>> pools = new Dictionary<PooledEffect, ObjectPool<PooledEffect>>();

    void OnEnable()
    {
        GameEvents.Jumped += OnJumped;
        GameEvents.Landed += OnLanded;
        GameEvents.Damaged += OnDamaged;
        GameEvents.Died += OnDied;
        GameEvents.CoinCollected += OnCoinCollected;
        GameEvents.OrbUsed += OnPowerUp;
        GameEvents.PadUsed += OnPowerUp;
        GameEvents.PortalUsed += OnPowerUp;
    }

    void OnDisable()
    {
        GameEvents.Jumped -= OnJumped;
        GameEvents.Landed -= OnLanded;
        GameEvents.Damaged -= OnDamaged;
        GameEvents.Died -= OnDied;
        GameEvents.CoinCollected -= OnCoinCollected;
        GameEvents.OrbUsed -= OnPowerUp;
        GameEvents.PadUsed -= OnPowerUp;
        GameEvents.PortalUsed -= OnPowerUp;
    }

    void OnJumped(Vector3 position) => Spawn(jumpDust, Feet(position));
    void OnLanded(Vector3 position) => Spawn(landDust, Feet(position));
    void OnDamaged(Vector3 position) => Spawn(hitSparks, position);
    void OnDied(Vector3 position) => Spawn(deathBurst, position);
    void OnCoinCollected(Vector3 position) => Spawn(coinSparkle, position);
    void OnPowerUp(Vector3 position) => Spawn(powerUpRing, position);

    Vector3 Feet(Vector3 position)
    {
        float gravity = PlayerController.Instance != null ? PlayerController.Instance.GravityDirection : 1f;
        return position + Vector3.down * (feetOffset * gravity);
    }

    void Spawn(PooledEffect prefab, Vector3 position)
    {
        if (prefab == null) return;
        if (!pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<PooledEffect>(
                () =>
                {
                    var effect = Instantiate(prefab, transform);
                    effect.gameObject.SetActive(false);
                    return effect;
                },
                effect => effect.gameObject.SetActive(true),
                effect => effect.gameObject.SetActive(false),
                effect => { if (effect != null) Destroy(effect.gameObject); },
                collectionCheck: false, defaultCapacity: poolSize, maxSize: poolSize * 3);
            pools[prefab] = pool;
        }
        var instance = pool.Get();
        instance.Pool = pool;
        instance.Play(position);
    }

    void OnDestroy()
    {
        foreach (var pool in pools.Values) pool.Clear();
        pools.Clear();
    }
}
