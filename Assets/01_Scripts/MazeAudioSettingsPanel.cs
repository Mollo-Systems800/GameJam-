using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MazeAudioSettingsPanel : MonoBehaviour
{
    public GameObject overlay;
    public RectTransform sheet;
    public Slider musicSlider,effectsSlider;
    public Toggle musicMute,effectsMute,allMute;
    public Text musicPercent,effectsPercent;
    public Button openButton,closeButton;
    public StartupMenu menu;
    public MazeCanvasHUD hud;
    public bool IsOpen=>overlay&&overlay.activeSelf;
    MazeAudioDirector audio;
    bool wired;
    void Start()=>Wire();
    void Wire()
    {
        if(wired)return;wired=true;audio=MazeAudioDirector.Ensure();
        foreach(var toggle in new[]{musicMute,effectsMute,allMute})toggle.toggleTransition=Toggle.ToggleTransition.None;
        openButton.onClick.AddListener(Open);closeButton.onClick.AddListener(Close);
        musicSlider.onValueChanged.AddListener(audio.SetMusicVolume);effectsSlider.onValueChanged.AddListener(audio.SetEffectsVolume);
        musicMute.onValueChanged.AddListener(audio.SetMusicMuted);effectsMute.onValueChanged.AddListener(audio.SetEffectsMuted);allMute.onValueChanged.AddListener(audio.SetAllMuted);
        audio.PreferencesChanged+=Refresh;Refresh();
    }
    public void Open()
    {
        if(menu&&menu.IsLoading || hud&&(!hud.game||!hud.game.IsPaused||hud.game.IsCinematicPlaying))return;
        Wire();overlay.SetActive(true);overlay.transform.SetAsLastSibling();
        if(menu&&menu.panelGroup)menu.panelGroup.interactable=false;
        Refresh();Fit();EventSystem.current?.SetSelectedGameObject(musicSlider.gameObject);
    }
    public void Close()
    {
        if(!IsOpen)return;
        audio?.SavePreferences();overlay.SetActive(false);
        if(menu&&menu.panelGroup)menu.panelGroup.interactable=true;
        EventSystem.current?.SetSelectedGameObject(openButton.gameObject);
    }
    void LateUpdate()
    {
        if(hud)
        {
            bool pause=hud.game&&hud.game.IsPaused&&!hud.game.IsCinematicPlaying;
            openButton.gameObject.SetActive(pause);
            if(!pause&&IsOpen)Close();
        }
        if(IsOpen)Fit();
    }
    void Fit()
    {
        var canvas=GetComponent<Canvas>();if(!canvas)return;
        Vector2 size=((RectTransform)canvas.transform).rect.size;
        sheet.localScale=Vector3.one*Mathf.Clamp(Mathf.Min((size.x-32)/640,(size.y-32)/550),.1f,1);
    }
    public void Refresh()
    {
        if(!audio)return;
        musicSlider.SetValueWithoutNotify(audio.musicVolume);effectsSlider.SetValueWithoutNotify(audio.effectsVolume);
        musicMute.SetIsOnWithoutNotify(audio.MusicMuted);effectsMute.SetIsOnWithoutNotify(audio.EffectsMuted);allMute.SetIsOnWithoutNotify(audio.AllMuted);
        foreach(var toggle in new[]{musicMute,effectsMute,allMute})toggle.graphic.canvasRenderer.SetAlpha(toggle.isOn?1:0);
        musicPercent.text=Mathf.RoundToInt(audio.musicVolume*100)+" %";effectsPercent.text=Mathf.RoundToInt(audio.effectsVolume*100)+" %";
    }
    void OnDestroy(){if(audio)audio.PreferencesChanged-=Refresh;}
}
