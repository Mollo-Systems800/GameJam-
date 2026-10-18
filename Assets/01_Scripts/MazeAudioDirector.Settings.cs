using System;
using UnityEngine;

public sealed partial class MazeAudioDirector
{
    public const string PreferencesKey="MazeAudio.Preferences.v1";
    [Serializable] sealed class Preferences
    { public float music,effects;public bool musicMuted,effectsMuted,allMuted; }
    public bool MusicMuted { get; private set; }
    public bool EffectsMuted { get; private set; }
    public bool AllMuted { get; private set; }
    public event Action PreferencesChanged;
    public float MusicGain=>AllMuted||MusicMuted?0:masterVolume*musicVolume*focusGain;
    public float EffectsGain=>AllMuted||EffectsMuted?0:masterVolume*effectsVolume*focusGain;
    float defaultMusic=.48f,defaultEffects=.7f,savePreferencesAt;
    bool preferencesDirty;
    string preferencesKey=PreferencesKey;
    public void LoadPreferences(string key=PreferencesKey)
    {
        preferencesKey=key;preferencesDirty=false;
        musicVolume=defaultMusic;effectsVolume=defaultEffects;
        MusicMuted=EffectsMuted=AllMuted=false;
        if(PlayerPrefs.HasKey(key))
        {
            try
            {
                var value=JsonUtility.FromJson<Preferences>(PlayerPrefs.GetString(key));
                if(value!=null)
                {
                    musicVolume=ValidVolume(value.music,defaultMusic);effectsVolume=ValidVolume(value.effects,defaultEffects);
                    MusicMuted=value.musicMuted;EffectsMuted=value.effectsMuted;AllMuted=value.allMuted;
                }
            }
            catch(ArgumentException){ /* Invalid local preferences fall back to the configured mix. */ }
        }
        ApplyPreferenceGains();PreferencesChanged?.Invoke();
    }
    static float ValidVolume(float value,float fallback)=>float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp01(value);
    public void SetMusicVolume(float value){musicVolume=ValidVolume(value,musicVolume);Changed();}
    public void SetEffectsVolume(float value){effectsVolume=ValidVolume(value,effectsVolume);Changed();}
    public void SetMusicMuted(bool value){MusicMuted=value;Changed();}
    public void SetEffectsMuted(bool value){EffectsMuted=value;Changed();}
    public void SetAllMuted(bool value){AllMuted=value;Changed();}
    void Changed()
    {
        preferencesDirty=true;savePreferencesAt=Time.unscaledTime+.35f;
        ApplyPreferenceGains();PreferencesChanged?.Invoke();
    }
    void ApplyPreferenceGains()
    {
        if(menu)menu.volume=menuGain*MusicGain;
        if(normal)normal.volume=Mathf.Sqrt(1-ThreatBlend)*gameGain*MusicGain;
        if(threat)threat.volume=Mathf.Sqrt(ThreatBlend)*gameGain*MusicGain;
        if(story)story.volume=storyGain*MusicGain;
        foreach(var voice in voices)if(voice&&voiceGains.TryGetValue(voice,out float gain))voice.volume=gain*EffectsGain;
    }
    public void SavePreferences()
    {
        if(!preferencesDirty)return;
        var value=new Preferences{music=musicVolume,effects=effectsVolume,musicMuted=MusicMuted,effectsMuted=EffectsMuted,allMuted=AllMuted};
        PlayerPrefs.SetString(preferencesKey,JsonUtility.ToJson(value));PlayerPrefs.Save();preferencesDirty=false;
    }
    void OnApplicationPause(bool paused){if(paused)SavePreferences();}
    void OnApplicationQuit()=>SavePreferences();
}
