using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CinemachineImpulseSource))]
public class DamageShake : MonoBehaviour
{
    [FormerlySerializedAs("fuerzaDanio")]
    [SerializeField] float damageForce = 1f;
    [FormerlySerializedAs("fuerzaMuerte")]
    [SerializeField] float deathForce = 2f;

    CinemachineImpulseSource source;

    void Awake() => source = GetComponent<CinemachineImpulseSource>();

    void OnEnable()
    {
        GameEvents.PlayerDamaged += OnPlayerDamaged;
        GameEvents.PlayerDied += OnPlayerDied;
    }

    void OnDisable()
    {
        GameEvents.PlayerDamaged -= OnPlayerDamaged;
        GameEvents.PlayerDied -= OnPlayerDied;
    }

    void OnPlayerDamaged(int health, int maxHealth) => source.GenerateImpulse(damageForce);
    void OnPlayerDied() => source.GenerateImpulse(deathForce);
}
