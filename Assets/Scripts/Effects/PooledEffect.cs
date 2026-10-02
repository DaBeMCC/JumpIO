using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(ParticleSystem))]
public class PooledEffect : MonoBehaviour
{
    ParticleSystem system;

    public IObjectPool<PooledEffect> Pool { get; set; }

    void Awake()
    {
        system = GetComponent<ParticleSystem>();
        var main = system.main;
        main.stopAction = ParticleSystemStopAction.Callback;
    }

    public void Play(Vector3 position)
    {
        transform.position = position;
        system.Play(true);
    }

    void OnParticleSystemStopped() => Pool?.Release(this);
}
