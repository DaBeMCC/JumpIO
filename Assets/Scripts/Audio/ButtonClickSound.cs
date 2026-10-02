using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonClickSound : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    Selectable selectable;

    void Awake() => selectable = GetComponent<Selectable>();

    public void OnPointerClick(PointerEventData eventData) => Play();
    public void OnSubmit(BaseEventData eventData) => Play();

    void Play()
    {
        if (selectable != null && selectable.IsInteractable() && AudioManager.Instance != null)
            AudioManager.Instance.PlayUiClick();
    }
}
