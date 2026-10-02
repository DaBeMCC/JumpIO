using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider2D))]
public class SpeedPortal : MonoBehaviour
{
    [FormerlySerializedAs("velocidad")]
    [SerializeField] float speed = 10.386f;

    public float Speed => speed;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController player))
        {
            player.Speed = speed;
            GameEvents.RaisePortalUsed(player.transform.position);
        }
    }
}
