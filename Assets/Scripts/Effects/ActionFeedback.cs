using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public sealed class ActionFeedback : MonoBehaviour
{
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip actionSound;
    readonly HashSet<ParticleSystem> active = new HashSet<ParticleSystem>();
    ObjectPool<ParticleSystem> pool;
    public int BurstsPlayed { get; private set; }
    void Awake()
    {
        pool = new ObjectPool<ParticleSystem>(
            () => Instantiate(particles, transform),
            effect => effect.gameObject.SetActive(true),
            effect => { effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); effect.gameObject.SetActive(false); },
            effect => { if (effect != null) Destroy(effect.gameObject); },
            defaultCapacity: 4, maxSize: 16);
    }
    void OnEnable() => GameEvents.PlayerActed += Play;
    void OnDisable()
    {
        GameEvents.PlayerActed -= Play;
        StopAllCoroutines();
        foreach (var effect in active) if (effect != null) Destroy(effect.gameObject);
        active.Clear();
        pool?.Clear();
    }
    void Play(Vector3 position)
    {
        if (actionSound != null && source != null) source.PlayOneShot(actionSound);
        if (particles == null) return;
        var effect = pool.Get();
        effect.transform.position = position + Vector3.down * 0.3f;
        active.Add(effect);
        effect.Play();
        effect.Emit(12);
        BurstsPlayed++;
        StartCoroutine(Recycle(effect));
    }
    IEnumerator Recycle(ParticleSystem effect)
    {
        while (effect.IsAlive(true)) yield return null;
        active.Remove(effect);
        pool.Release(effect);
    }
}
