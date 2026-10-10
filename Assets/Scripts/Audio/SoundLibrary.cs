using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SoundLibrary", menuName = "JumpIO/Sound Library")]
public class SoundLibrary : ScriptableObject
{
    [Serializable]
    public struct Sound
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
        [Range(0f, 0.3f)] public float pitchVariation;
    }

    [SerializeField] Sound jump;
    [SerializeField] Sound coin;
    [SerializeField] Sound hurt;
    [SerializeField] Sound death;
    [SerializeField] Sound powerUp;
    [SerializeField] Sound uiClick;
    [SerializeField] Sound victory;

    public Sound Jump => jump;
    public Sound Coin => coin;
    public Sound Hurt => hurt;
    public Sound Death => death;
    public Sound PowerUp => powerUp;
    public Sound UiClick => uiClick;
    public Sound Victory => victory;
}
