using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ModePortal : MonoBehaviour
{
    [SerializeField] PlayerModeType mode;

    public PlayerModeType Mode => mode;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out PlayerController player))
        {
            player.SetMode(mode, transform.position.y);
            GameEvents.RaisePortalUsed(player.transform.position);
        }
    }
}
