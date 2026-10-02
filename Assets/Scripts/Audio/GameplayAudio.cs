using UnityEngine;

[RequireComponent(typeof(AudioManager))]
public class GameplayAudio : MonoBehaviour
{
    AudioManager manager;

    void Awake() => manager = GetComponent<AudioManager>();

    void OnEnable()
    {
        GameEvents.Jumped += OnJumped;
        GameEvents.PlayerDamaged += OnPlayerDamaged;
        GameEvents.Died += OnDied;
        GameEvents.CoinCollected += OnCoinCollected;
        GameEvents.OrbUsed += OnPowerUp;
        GameEvents.PadUsed += OnPowerUp;
        GameEvents.PortalUsed += OnPowerUp;
    }

    void OnDisable()
    {
        GameEvents.Jumped -= OnJumped;
        GameEvents.PlayerDamaged -= OnPlayerDamaged;
        GameEvents.Died -= OnDied;
        GameEvents.CoinCollected -= OnCoinCollected;
        GameEvents.OrbUsed -= OnPowerUp;
        GameEvents.PadUsed -= OnPowerUp;
        GameEvents.PortalUsed -= OnPowerUp;
    }

    void OnJumped(Vector3 position)
    {
        if (AudioPreferences.JumpSound) manager.Play(manager.Sounds.Jump);
    }

    void OnPlayerDamaged(int health, int maxHealth)
    {
        if (health > 0) manager.Play(manager.Sounds.Hurt);
    }

    void OnDied(Vector3 position) => manager.Play(manager.Sounds.Death);
    void OnCoinCollected(Vector3 position) => manager.Play(manager.Sounds.Coin);
    void OnPowerUp(Vector3 position) => manager.Play(manager.Sounds.PowerUp);
}
