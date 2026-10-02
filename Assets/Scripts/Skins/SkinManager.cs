using System;
using UnityEngine;
using UnityEngine.Serialization;

public class SkinManager : MonoBehaviour
{
    const string PrefsKey = "JumpIO.SelectedSkin";

    [SerializeField] CharacterSkin[] skins;
    [FormerlySerializedAs("visualJugador")]
    [SerializeField] PlayerVisual playerVisual;
    [FormerlySerializedAs("efectoMuerte")]
    [SerializeField] DeathEffect deathEffect;

    int index;

    public CharacterSkin Current => skins[index];
    public event Action<CharacterSkin> SkinChanged;

    void Awake() => index = Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, 0), 0, skins.Length - 1);

    void Start() => Apply();

    public void Next() => Change(1);
    public void Previous() => Change(-1);

    void Change(int step)
    {
        index = (index + step + skins.Length) % skins.Length;
        PlayerPrefs.SetInt(PrefsKey, index);
        PlayerPrefs.Save();
        Apply();
    }

    void Apply()
    {
        if (playerVisual != null) playerVisual.ApplySkin(Current);
        if (deathEffect != null) deathEffect.FragmentColor = Current.particleColor;
        SkinChanged?.Invoke(Current);
    }
}
