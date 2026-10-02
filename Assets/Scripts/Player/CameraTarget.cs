using Unity.Cinemachine;
using UnityEngine;

public class CameraTarget : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] CinemachineCamera vcam;

    bool snap;

    void OnEnable() => GameEvents.AttemptStarted += OnAttemptStarted;
    void OnDisable() => GameEvents.AttemptStarted -= OnAttemptStarted;

    void OnAttemptStarted(int attempt) => snap = true;

    void Update()
    {
        Follow();
        if (!snap) return;
        snap = false;
        vcam.PreviousStateIsValid = false;
    }

    void Follow()
    {
        Vector3 p = player.transform.position;
        if (!float.IsInfinity(player.CeilingY))
        {
            float center = (player.FloorY + player.CeilingY) / 2f;
            p.y = Mathf.Lerp(center, p.y, 0.25f);
        }
        transform.position = p;
    }
}
