using UnityEngine;
using UnityEngine.UI;

public sealed class MazeInterfaceMotion : MonoBehaviour
{
    public RectTransform panel;
    public Image dim;
    CanvasGroup group;
    float opened;
    bool wasOpen;
    void LateUpdate()
    {
        if(!panel)return;
        bool open=panel.gameObject.activeSelf;dim.enabled=open;
        if(!open){wasOpen=false;return;}
        if(!group){group=panel.GetComponent<CanvasGroup>();if(!group)group=panel.gameObject.AddComponent<CanvasGroup>();}
        if(!wasOpen){opened=Time.unscaledTime;wasOpen=true;}
        float t=Mathf.SmoothStep(0,1,(Time.unscaledTime-opened)/.18f);
        var available=((RectTransform)transform).rect.size;
        float fit=Mathf.Min(1,(available.x-32)/panel.sizeDelta.x,(available.y-32)/panel.sizeDelta.y);
        panel.localScale=Vector3.one*Mathf.Max(.1f,fit)*Mathf.Lerp(.97f,1,t);
        panel.anchoredPosition=new Vector2(0,Mathf.Lerp(-12,0,t));group.alpha=t;
    }
}
