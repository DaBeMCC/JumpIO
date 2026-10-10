using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

// Crédito del autor en la tarjeta de nivel completado; se genera desde el Editor para no tocar el prefab a mano.
public static class CreditSetup
{
    const string UIManagerPrefab = "Assets/Prefabs/UIManager.prefab";
    const string HudMaterialPath = "Assets/Art/HUDText.mat";

    [MenuItem("JumpIO/Setup/Completion Credit")]
    public static void Configure()
    {
        var root = PrefabUtility.LoadPrefabContents(UIManagerPrefab);
        try
        {
            var card = (RectTransform)root.GetComponentsInChildren<Transform>(true).First(t => t.name == "CompletionCard");
            var title = card.Find("Title").GetComponent<TMP_Text>();
            float titleSize = title.fontSize;
            title.text = "CONGRATULATIONS!";
            // El título es más largo que antes: se reduce solo si no cabe, nunca crece.
            title.enableAutoSizing = true;
            title.fontSizeMax = titleSize;
            title.fontSizeMin = titleSize * 0.5f;

            var old = card.Find("CreditText");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // El crédito va debajo del elemento más bajo de la tarjeta para no tapar los botones.
            float lowest = card.Cast<RectTransform>().Where(c => c.gameObject.activeSelf)
                .Min(c => c.anchoredPosition.y - c.sizeDelta.y * c.pivot.y);
            var go = new GameObject("CreditText", typeof(RectTransform));
            go.transform.SetParent(card, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(card.sizeDelta.x - 40f, 46f);
            rect.anchoredPosition = new Vector2(0f, lowest - 18f - rect.sizeDelta.y / 2f);
            var credit = go.AddComponent<TextMeshProUGUI>();
            credit.text = "by David Machaca";
            credit.fontSize = 32f;
            credit.alignment = TextAlignmentOptions.Center;
            credit.color = new Color(1f, 0.86f, 0.5f);
            credit.raycastTarget = false;
            var material = AssetDatabase.LoadAssetAtPath<Material>(HudMaterialPath);
            if (material != null) credit.fontSharedMaterial = material;

            // Si el crédito queda fuera de la tarjeta, se alarga hacia abajo lo justo.
            float bottom = rect.anchoredPosition.y - rect.sizeDelta.y / 2f;
            float cardBottom = -card.sizeDelta.y * card.pivot.y;
            if (bottom < cardBottom + 16f)
            {
                // Con el pivote en el centro la tarjeta crece por arriba y por abajo, así que se suma el doble.
                float grow = cardBottom + 16f - bottom;
                card.sizeDelta += new Vector2(0f, grow * 2f);
            }
            PrefabUtility.SaveAsPrefabAsset(root, UIManagerPrefab);
            Debug.Log($"JUMPIO_CREDIT_OK lowest={lowest:0} credit={rect.anchoredPosition.y:0} card={card.sizeDelta}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
