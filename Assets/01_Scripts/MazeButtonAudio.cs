using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class MazeButtonAudio : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    Button button;
    void Awake(){button=GetComponent<Button>();button.onClick.AddListener(Click);}
    void Click(){if(button.interactable)MazeAudioDirector.Play(MazeSound.UIClick);}
    public void OnPointerEnter(PointerEventData eventData){Hover();}
    public void OnSelect(BaseEventData eventData){Hover();}
    void Hover(){if(button&&button.interactable)MazeAudioDirector.Play(MazeSound.UIHover,null,.5f);}
    void OnDestroy(){if(button)button.onClick.RemoveListener(Click);}
}
