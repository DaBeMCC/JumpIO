using UnityEngine;

public class SceneMusic : MonoBehaviour
{
    [SerializeField] AudioClip music;

    void Start()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(music);
    }
}
