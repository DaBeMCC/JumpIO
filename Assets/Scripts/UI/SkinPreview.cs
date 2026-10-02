using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkinPreview : MonoBehaviour
{
    [SerializeField] SkinManager skins;
    [SerializeField] Image preview;
    [SerializeField] TMP_Text nameText;

    void OnEnable() => skins.SkinChanged += Show;
    void OnDisable() => skins.SkinChanged -= Show;

    void Show(CharacterSkin skin)
    {
        preview.sprite = skin.sprite;
        preview.color = skin.tint;
        preview.rectTransform.localScale = Vector3.one * skin.scale;
        nameText.text = skin.displayName;
    }
}
