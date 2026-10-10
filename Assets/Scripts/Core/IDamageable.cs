using UnityEngine;

// Contrato común para todo lo que recibe daño (jugador y enemigos): quien golpea no necesita saber qué clase concreta hay del otro lado.
public interface IDamageable
{
    bool IsAlive { get; }

    // Devuelve true si el golpe se aplicó (false durante la invulnerabilidad o si ya estaba derrotado).
    bool TakeDamage(int amount, Vector2 source);
}
