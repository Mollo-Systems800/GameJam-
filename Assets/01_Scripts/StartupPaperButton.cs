using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class StartupPaperButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    RectTransform rect;
    Button button;
    Vector2 rest;
    bool hovered, selected;
    void Awake() { rect = (RectTransform)transform; rest = rect.anchoredPosition; button = GetComponent<Button>(); }
    void Update()
    {
        bool active = button.IsInteractable() && (hovered || selected);
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, rest + (active ? Vector2.right * 7 : Vector2.zero), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 16));
    }
    public void OnPointerEnter(PointerEventData data) => hovered = true;
    public void OnPointerExit(PointerEventData data) => hovered = false;
    public void OnSelect(BaseEventData data) => selected = true;
    public void OnDeselect(BaseEventData data) => selected = false;
}
