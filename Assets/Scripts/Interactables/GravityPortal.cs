using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider2D))]
public class GravityPortal : MonoBehaviour
{
    [FormerlySerializedAs("invertir")]
    [SerializeField] bool invert;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController player) && player.GravityDirection < 0f != invert)
        {
            player.ReverseGravity();
            GameEvents.RaisePortalUsed(player.transform.position);
        }
    }
}
