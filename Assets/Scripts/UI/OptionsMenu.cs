using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class OptionsMenu : MonoBehaviour
{
    [SerializeField] Slider master;
    [SerializeField] Slider music;
    [SerializeField] Slider sfx;
    [SerializeField] TMP_Text masterValue;
    [SerializeField] TMP_Text musicValue;
    [SerializeField] TMP_Text sfxValue;
    [SerializeField] Toggle jumpSound;

    UnityAction<float> onMaster, onMusic, onSfx;
    float nextPreview;

    void Awake()
    {
        onMaster = v => Change(AudioChannel.Master, v, masterValue);
        onMusic = v => Change(AudioChannel.Music, v, musicValue);
        onSfx = v => Change(AudioChannel.Sfx, v, sfxValue);
    }

    void OnEnable()
    {
        Bind(master, masterValue, AudioChannel.Master, onMaster);
        Bind(music, musicValue, AudioChannel.Music, onMusic);
        Bind(sfx, sfxValue, AudioChannel.Sfx, onSfx);
        jumpSound.SetIsOnWithoutNotify(AudioPreferences.JumpSound);
        jumpSound.onValueChanged.AddListener(OnJumpSound);
    }

    void OnDisable()
    {
        master.onValueChanged.RemoveListener(onMaster);
        music.onValueChanged.RemoveListener(onMusic);
        sfx.onValueChanged.RemoveListener(onSfx);
        jumpSound.onValueChanged.RemoveListener(OnJumpSound);
    }

    static void Bind(Slider slider, TMP_Text label, AudioChannel channel, UnityAction<float> listener)
    {
        float value = AudioManager.Instance != null ? AudioManager.Instance.GetVolume(channel) : AudioPreferences.GetVolume(channel);
        slider.SetValueWithoutNotify(value);
        Show(label, value);
        slider.onValueChanged.AddListener(listener);
    }

    void Change(AudioChannel channel, float value, TMP_Text label)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetVolume(channel, value);
        else AudioPreferences.SetVolume(channel, value);
        Show(label, value);
        if (channel == AudioChannel.Sfx && AudioManager.Instance != null && Time.unscaledTime >= nextPreview)
        {
            nextPreview = Time.unscaledTime + 0.12f;
            AudioManager.Instance.PlayUiClick();
        }
    }

    static void Show(TMP_Text label, float value) => label.text = Mathf.RoundToInt(value * 100f) + "%";

    void OnJumpSound(bool on) => AudioPreferences.JumpSound = on;
}
