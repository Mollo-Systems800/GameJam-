using UnityEngine;
using UnityEngine.UI;

// Shared paper form on the existing scene Canvas; no additional Canvas or EventSystem.
public static class MazeAudioSettingsView
{
    static readonly Color Ink=new Color(.12f,.17f,.16f),Paper=new Color(.91f,.87f,.76f),Red=new Color(.63f,.20f,.14f);
    public static MazeAudioSettingsPanel Ensure(StartupMenu menu,MazeCanvasHUD hud)
    {
        var canvas=menu?menu.menuCanvas:hud.GetComponent<Canvas>();
        var panel=canvas.GetComponent<MazeAudioSettingsPanel>();if(panel&&panel.overlay)return panel;
        if(!panel)panel=canvas.gameObject.AddComponent<MazeAudioSettingsPanel>();panel.menu=menu;panel.hud=hud;
        if(menu)
        {
            menu.recordStatus.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,420);
            panel.openButton=Button("Audio settings",menu.mainPanel,"SONIDO",new Vector2(1410,-865),new Vector2(275,54),Ink);
        }
        else
        {
            panel.openButton=Button("Pause audio settings",hud.resultPanel.transform,"Sonido",new Vector2(65,-170),new Vector2(380,42),Ink);
            panel.openButton.gameObject.SetActive(false);
        }
        var overlay=Rect("Audio preferences overlay",canvas.transform,Vector2.zero,Vector2.zero);
        overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;
        var dim=overlay.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,.78f);dim.raycastTarget=true;
        panel.overlay=overlay.gameObject;
        var sheet=Rect("Audio preferences form",overlay,Vector2.zero,new Vector2(640,550));
        sheet.anchorMin=sheet.anchorMax=sheet.pivot=new Vector2(.5f,.5f);sheet.anchoredPosition=Vector2.zero;
        var background=sheet.gameObject.AddComponent<Image>();background.color=Paper;panel.sheet=sheet;
        // Content coordinates use a top-left pivot independent of the centred sheet.
        var content=Rect("Contenido",sheet,Vector2.zero,new Vector2(640,550));
        Label("Office",content,"OFICINA DE EMPLEO  /  PREFERENCIAS DE SONIDO",15,Ink,new Vector2(40,-20),new Vector2(560,25));
        Label("Title",content,"CONTROL DE RUIDO",32,Red,new Vector2(40,-51),new Vector2(560,50)).fontStyle=FontStyle.Bold;
        Label("Music label",content,"MÚSICA DE FONDO",21,Ink,new Vector2(40,-111),new Vector2(550,30));
        panel.musicSlider=Slider("Volumen musica",content,new Vector2(40,-154));
        panel.musicPercent=Label("Porcentaje musica",content,"",21,Ink,new Vector2(508,-154),new Vector2(95,36));
        panel.musicMute=Toggle("Silenciar musica",content,"Silenciar música",new Vector2(40,-206));
        Label("Effects label",content,"EFECTOS",21,Ink,new Vector2(40,-258),new Vector2(550,30));
        panel.effectsSlider=Slider("Volumen efectos",content,new Vector2(40,-301));
        panel.effectsPercent=Label("Porcentaje efectos",content,"",21,Ink,new Vector2(508,-301),new Vector2(95,36));
        panel.effectsMute=Toggle("Silenciar efectos",content,"Silenciar efectos",new Vector2(40,-352));
        panel.allMute=Toggle("Silenciar todo",content,"Silenciar TODO",new Vector2(40,-400));
        panel.closeButton=Button("Close audio settings",content,"LISTO · VOLVER",new Vector2(40,-455),new Vector2(560,44),Red);
        Label("Saved automatically",content,"Los cambios se guardan automáticamente.",16,Ink,new Vector2(40,-510),new Vector2(560,26));
        overlay.gameObject.SetActive(false);
        if(Application.isPlaying)MazeAudioDirector.RegisterButtons(canvas.transform);
        return panel;
    }
    static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.gameObject.layer=5;
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    static Text Label(string name,Transform parent,string text,int size,Color color,Vector2 position,Vector2 dimensions)
    {
        var rect=Rect(name,parent,position,dimensions);var label=rect.gameObject.AddComponent<Text>();
        label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=text;label.fontSize=size;label.color=color;
        label.alignment=TextAnchor.MiddleLeft;label.raycastTarget=false;return label;
    }
    static Button Button(string name,Transform parent,string text,Vector2 position,Vector2 size,Color color)
    {
        var rect=Rect(name,parent,position,size);var image=rect.gameObject.AddComponent<Image>();image.color=color;
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var label=Label("Texto",rect,text,21,Paper,Vector2.zero,size);label.alignment=TextAnchor.MiddleCenter;return button;
    }
    static Slider Slider(string name,Transform parent,Vector2 position)
    {
        var rect=Rect(name,parent,position,new Vector2(440,36));var hit=rect.gameObject.AddComponent<Image>();hit.color=Color.clear;
        var rail=Rect("Rail",rect,new Vector2(0,-13),new Vector2(440,10));rail.gameObject.AddComponent<Image>().color=new Color(.64f,.63f,.54f);
        var fillArea=Rect("Fill area",rect,new Vector2(12,-13),new Vector2(416,10));
        var fill=Rect("Fill",fillArea,Vector2.zero,Vector2.zero);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
        fill.gameObject.AddComponent<Image>().color=Red;
        var handles=Rect("Handle area",rect,new Vector2(12,0),new Vector2(416,36));
        var handle=Rect("Handle",handles,Vector2.zero,new Vector2(24,-4));handle.pivot=new Vector2(.5f,.5f);
        var grip=handle.gameObject.AddComponent<Image>();grip.color=Ink;
        var slider=rect.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=grip;return slider;
    }
    static Toggle Toggle(string name,Transform parent,string text,Vector2 position)
    {
        var rect=Rect(name,parent,position,new Vector2(560,34));var hit=rect.gameObject.AddComponent<Image>();hit.color=Color.clear;
        var box=Rect("Casilla",rect,new Vector2(0,-3),new Vector2(28,28));var image=box.gameObject.AddComponent<Image>();image.color=new Color(.68f,.66f,.56f);
        var check=Rect("Marca",box,new Vector2(6,-6),new Vector2(16,16));var mark=check.gameObject.AddComponent<Image>();mark.color=Red;
        Label("Texto",rect,text,21,Ink,new Vector2(42,0),new Vector2(510,34));
        var toggle=rect.gameObject.AddComponent<Toggle>();toggle.targetGraphic=image;toggle.graphic=mark;return toggle;
    }
}
