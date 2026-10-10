using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "NewSkin", menuName = "JumpIO/Character Skin")]
// Skin solo de datos (ScriptableObject): sprites por modo, colores y override de animaciones, así un personaje nuevo no necesita código.
public class CharacterSkin : ScriptableObject
{
    [FormerlySerializedAs("nombre")]
    public string displayName = "Skin";
    public Sprite sprite;
    [FormerlySerializedAs("tinte")]
    public Color tint = Color.white;
    [FormerlySerializedAs("colorEstela")]
    public Color trailColor = Color.white;
    [FormerlySerializedAs("colorParticulas")]
    public Color particleColor = Color.white;
    [FormerlySerializedAs("escala")]
    [Min(0.1f)] public float scale = 1f;

    [Header("Sprite per game mode")]
    public Sprite shipSprite;
    public Sprite ballSprite;
    public Sprite ufoSprite;

    public AnimatorOverrideController animationOverride;
    public bool uprightRunner;
    public bool showTrail = true;

    public Sprite SpriteFor(PlayerModeType mode)
    {
        Sprite s = mode switch
        {
            PlayerModeType.Ship => shipSprite,
            PlayerModeType.Ball => ballSprite,
            PlayerModeType.Ufo => ufoSprite,
            _ => sprite,
        };
        return s != null ? s : sprite;
    }
}
