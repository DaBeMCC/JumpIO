using UnityEngine;

// Reproduce los sonidos escuchando GameEvents, así los scripts de juego nunca llaman directamente al sistema de audio.
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
        GameEvents.LevelCompleted += OnLevelCompleted;
    }

    // Se desuscribe para evitar llamadas a un objeto destruido tras cambiar de escena.
    void OnDisable()
    {
        GameEvents.Jumped -= OnJumped;
        GameEvents.PlayerDamaged -= OnPlayerDamaged;
        GameEvents.Died -= OnDied;
        GameEvents.CoinCollected -= OnCoinCollected;
        GameEvents.OrbUsed -= OnPowerUp;
        GameEvents.PadUsed -= OnPowerUp;
        GameEvents.PortalUsed -= OnPowerUp;
        GameEvents.LevelCompleted -= OnLevelCompleted;
    }

    // El sonido de salto se puede silenciar desde Options sin tocar el volumen de SFX.
    void OnJumped(Vector3 position)
    {
        if (AudioPreferences.JumpSound) manager.Play(manager.Sounds.Jump);
    }

    // El último golpe reproduce el sonido de muerte en su lugar, así nunca se solapan.
    void OnPlayerDamaged(int health, int maxHealth)
    {
        if (health > 0) manager.Play(manager.Sounds.Hurt);
    }

    void OnDied(Vector3 position) => manager.Play(manager.Sounds.Death);
    void OnCoinCollected(Vector3 position) => manager.Play(manager.Sounds.Coin);
    void OnPowerUp(Vector3 position) => manager.Play(manager.Sounds.PowerUp);
    void OnLevelCompleted() => manager.Play(manager.Sounds.Victory);
}
