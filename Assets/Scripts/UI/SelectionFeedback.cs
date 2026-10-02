using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SelectionFeedback : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [SerializeField] float selectedScale = 1.08f;

    Selectable selectable;
    Vector3 baseScale;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
        baseScale = transform.localScale;
    }

    void OnDisable() => transform.localScale = baseScale;

    public void OnSelect(BaseEventData eventData) => transform.localScale = baseScale * selectedScale;

    public void OnDeselect(BaseEventData eventData) => transform.localScale = baseScale;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable != null && selectable.IsInteractable() && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
