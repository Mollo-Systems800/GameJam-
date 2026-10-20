using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MazeInterfaceButton : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
{
    bool hover,selected;
    Button button;
    void Awake(){button=GetComponent<Button>();}
    void Update()
    {
        float target=button&&button.IsInteractable()&&(hover||selected)?1.015f:1;
        transform.localScale=Vector3.Lerp(transform.localScale,Vector3.one*target,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
    }
    void OnDisable(){hover=selected=false;transform.localScale=Vector3.one;}
    public void OnPointerEnter(PointerEventData data){hover=true;}
    public void OnPointerExit(PointerEventData data){hover=false;}
    public void OnSelect(BaseEventData data){selected=true;}
    public void OnDeselect(BaseEventData data){selected=false;}
}
