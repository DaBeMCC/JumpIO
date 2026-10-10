using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// Singleton de audio persistente: dos fuentes de música permiten fundidos entre escenas y un grupo de voces evita que los SFX se corten entre sí.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] AudioMixer mixer;
    [SerializeField] SoundLibrary sounds;
    [SerializeField] AudioSource[] musicSources = new AudioSource[2];
    [SerializeField] AudioSource[] sfxVoices;
    [SerializeField, Range(0f, 1f)] float musicVolume = 0.55f;
    [SerializeField] float crossfadeDuration = 0.6f;
    [SerializeField] float stopDuration = 0.1f;

    int activeMusic;
    int nextVoice;
    Coroutine fade;
    AudioSource jingle;

    public SoundLibrary Sounds => sounds;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // Fuente propia para la música de victoria: como un one-shot no se puede detener, seguía sonando al reiniciar o volver al menú.
        jingle = gameObject.AddComponent<AudioSource>();
        jingle.playOnAwake = false;
        if (sfxVoices.Length > 0) jingle.outputAudioMixerGroup = sfxVoices[0].outputAudioMixerGroup;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameEvents.AttemptStarted += OnAttemptStarted;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameEvents.AttemptStarted -= OnAttemptStarted;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StopJingle();
    void OnAttemptStarted(int attempt) => StopJingle();

    void Start()
    {
        foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
            ApplyVolume(channel, AudioPreferences.GetVolume(channel));
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public float GetVolume(AudioChannel channel) => AudioPreferences.GetVolume(channel);

    public void SetVolume(AudioChannel channel, float linear)
    {
        AudioPreferences.SetVolume(channel, linear);
        ApplyVolume(channel, linear);
    }

    void ApplyVolume(AudioChannel channel, float linear) =>
        mixer.SetFloat(channel + "Volume", AudioPreferences.ToDecibels(linear));

    public void PlayMusic(AudioClip clip)
    {
        var current = musicSources[activeMusic];
        if (clip != null && current.clip == clip && current.isPlaying) return;
        if (fade != null) StopCoroutine(fade);
        var next = musicSources[1 - activeMusic];
        if (clip != null)
        {
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            activeMusic = 1 - activeMusic;
            fade = StartCoroutine(Crossfade(current, next, crossfadeDuration));
        }
        else fade = StartCoroutine(Crossfade(current, null, stopDuration));
    }

    IEnumerator Crossfade(AudioSource from, AudioSource to, float duration)
    {
        float fromStart = from.volume;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            from.volume = Mathf.Lerp(fromStart, 0f, k);
            if (to != null) to.volume = Mathf.Lerp(0f, musicVolume, k);
            yield return null;
        }
        from.volume = 0f;
        from.Stop();
        if (to != null) to.volume = musicVolume;
        fade = null;
    }

    public void Play(SoundLibrary.Sound sound)
    {
        if (sound.clip == null) return;
        var voice = sfxVoices[nextVoice];
        nextVoice = (nextVoice + 1) % sfxVoices.Length;
        // Pequeñas variaciones de tono evitan que los sonidos repetidos suenen mecánicos.
        voice.pitch = 1f + Random.Range(-sound.pitchVariation, sound.pitchVariation);
        voice.PlayOneShot(sound.clip, sound.volume);
    }

    public void PlayJingle(SoundLibrary.Sound sound)
    {
        if (sound.clip == null || jingle == null) return;
        jingle.clip = sound.clip;
        jingle.volume = sound.volume;
        jingle.pitch = 1f;
        jingle.Play();
    }

    public void StopJingle()
    {
        if (jingle != null) jingle.Stop();
    }

    public void PlayUiClick() => Play(sounds.UiClick);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearInstance() => Instance = null;
}
