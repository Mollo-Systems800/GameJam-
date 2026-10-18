using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One persistent mix across Inicio/BetaMaze. Audio never drives gameplay or its clock.
[DefaultExecutionOrder(-100)]
public sealed partial class MazeAudioDirector : MonoBehaviour
{
    public MazeAudioLibrary library;
    [Header("Mezcla")]
    [Range(0,1)] public float masterVolume=.8f;
    [Range(0,1)] public float musicVolume=.48f;
    [Range(0,1)] public float effectsVolume=.7f;
    [Range(.8f,1.4f)] public float effectsPlaybackSpeed=1.12f;
    [Header("Solicitud cercana (metros)")]
    [Min(1)] public float threatEnterDistance=8;
    [Min(2)] public float threatExitDistance=12;
    [Min(.1f)] public float musicFadeSeconds=1.4f;
    public static MazeAudioDirector Instance { get; private set; }
    public float ThreatBlend { get; private set; }
    public bool ThreatActive { get; private set; }
    public MazeStoryBeat? ActiveStory { get; private set; }
    public int PlayedCueCount { get; private set; }
    public MazeSound LastCue { get; private set; }
    public AudioSource MenuSource => menu;
    public AudioSource NormalSource => normal;
    public AudioSource ThreatSource => threat;
    public AudioSource StorySource => story;
    readonly Dictionary<MazeSound,MazeAudioCue> cues=new Dictionary<MazeSound,MazeAudioCue>();
    readonly Dictionary<MazeSound,float> lastPlayed=new Dictionary<MazeSound,float>();
    readonly Dictionary<MazeSound,int> previousVariant=new Dictionary<MazeSound,int>();
    readonly List<AudioSource> voices=new List<AudioSource>();
    readonly Dictionary<AudioSource,float> voiceGains=new Dictionary<AudioSource,float>();
    AudioSource menu,normal,threat,story;
    MazeBetaGame game;
    bool inMenu;
    float menuGain,gameGain,storyGain,focusGain=1;
    int retrySecond=-1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Instance=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() => Ensure();
    public static MazeAudioDirector Ensure()
    {
        if(Instance)return Instance;
        var prefab=Resources.Load<GameObject>("MazeAudio");
        var root=prefab?Instantiate(prefab):new GameObject("Audio - The Maze of work");
        var director=root.GetComponent<MazeAudioDirector>();
        if(!director)director=root.AddComponent<MazeAudioDirector>();
        return director;
    }
    void Awake()
    {
        if(Instance&&Instance!=this){Destroy(gameObject);return;}
        Instance=this;DontDestroyOnLoad(gameObject);
        defaultMusic=musicVolume;defaultEffects=effectsVolume;LoadPreferences();
        if(!library)library=Resources.Load<MazeAudioLibrary>("MazeAudioLibrary");
        if(library&&library.cues!=null)foreach(var cue in library.cues)if(cue!=null)cues[cue.sound]=cue;
        menu=Source("Musica - menu",true);normal=Source("Ambiente - oficina",true);
        threat=Source("Ambiente - solicitud cercana",true);story=Source("Musica - cinematica",true);
        if(library){menu.clip=library.menuMusic;normal.clip=library.normalMusic;threat.clip=library.threatMusic;}
        for(int i=0;i<16;i++)voices.Add(Source("Efecto "+i,false));
        SceneManager.sceneLoaded+=SceneLoaded;
    }
    AudioSource Source(string name,bool loop)
    {
        var child=new GameObject(name);child.transform.SetParent(transform,false);
        var source=child.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=loop;source.volume=0;
        source.priority=loop?32:128;source.dopplerLevel=0;source.rolloffMode=AudioRolloffMode.Linear;
        source.minDistance=1.6f;source.maxDistance=14;return source;
    }
    void SceneLoaded(Scene scene,LoadSceneMode mode)
    {
        SavePreferences();
        if(mode==LoadSceneMode.Additive)return;
        game=FindAnyObjectByType<MazeBetaGame>();inMenu=FindAnyObjectByType<StartupMenu>()!=null;
        ActiveStory=null;ThreatActive=false;ThreatBlend=0;retrySecond=-1;
        foreach(var voice in voices)voice.Stop();lastPlayed.Clear();
        foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))RegisterButtons(canvas.transform);
    }
    public static void RegisterButtons(Transform root)
    {
        foreach(var button in root.GetComponentsInChildren<Button>(true))
            if(!button.GetComponent<MazeButtonAudio>())button.gameObject.AddComponent<MazeButtonAudio>();
    }
    void Update()
    {
        if(!library)return;
        if(preferencesDirty&&Time.unscaledTime>=savePreferencesAt)SavePreferences();
        float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
        focusGain=Mathf.MoveTowards(focusGain,Application.isFocused?1:0,dt*4);
        bool playing=game&&game.IsPlaying;
        if(playing&&game.Player&&game.Boss)
        {
            float distance=Vector3.Distance(game.Player.transform.position,game.Boss.transform.position);
            bool previous=ThreatActive;
            if(distance<=threatEnterDistance)ThreatActive=true;
            else if(distance>=Mathf.Max(threatEnterDistance+.5f,threatExitDistance))ThreatActive=false;
            if(ThreatActive&&!previous)Emit(MazeSound.BossAlert,null,.6f);
        }
        else if(!game||game.HasWon||game.WaitingForRetry)ThreatActive=false;
        float fade=dt/Mathf.Max(.1f,musicFadeSeconds);
        ThreatBlend=Mathf.MoveTowards(ThreatBlend,ThreatActive?1:0,fade);
        menuGain=Mathf.MoveTowards(menuGain,inMenu?1:0,fade);
        float desiredGame=game&&!ActiveStory.HasValue?(playing?1:game.IsPaused||game.ShopOpen?.25f:game.HasWon?.32f:0):0;
        gameGain=Mathf.MoveTowards(gameGain,desiredGame,fade*1.5f);
        storyGain=Mathf.MoveTowards(storyGain,ActiveStory.HasValue?1:0,dt*3);
        float gain=MusicGain;
        Mix(menu,menuGain*gain,inMenu);Mix(normal,Mathf.Sqrt(1-ThreatBlend)*gameGain*gain,game&&!inMenu);
        Mix(threat,Mathf.Sqrt(ThreatBlend)*gameGain*gain,game&&!inMenu);Mix(story,storyGain*gain,ActiveStory.HasValue);
        if(game&&game.WaitingForRetry&&!game.IsCinematicPlaying)
        {
            int second=Mathf.CeilToInt(game.RetrySecondsRemaining);
            if(second!=retrySecond){retrySecond=second;Emit(MazeSound.RetryTick);}
        }
        else retrySecond=-1;
        foreach(var voice in voices)
            if(voice.isPlaying)voice.volume=voiceGains[voice]*EffectsGain;
    }
    static void Mix(AudioSource source,float volume,bool active)
    {
        source.volume=Mathf.Clamp01(volume);
        if(active&&source.clip&&!source.isPlaying)source.Play();
        if(!active&&volume<.0001f&&source.isPlaying)source.Stop();
        // Keep both gameplay loops running through proximity changes; no repeated restarts.
    }
    public static void Play(MazeSound sound,Vector3? position=null,float gain=1) { if(Instance)Instance.Emit(sound,position,gain); }
    public void Emit(MazeSound sound,Vector3? position=null,float gain=1)
    {
        if(!cues.TryGetValue(sound,out var cue)||cue.variations==null||cue.variations.Length==0)return;
        float now=Time.unscaledTime;
        if(lastPlayed.TryGetValue(sound,out float last)&&now-last<cue.minimumInterval)return;
        if(position.HasValue&&Camera.main&&(Camera.main.transform.position-position.Value).sqrMagnitude>14*14)return;
        AudioSource voice=null;
        foreach(var candidate in voices)if(!candidate.isPlaying){voice=candidate;break;}
        if(!voice)
        {
            // World chatter cannot steal a pickup, hit or interface confirmation.
            if(sound==MazeSound.BossRustle||sound==MazeSound.ShovelMove)return;
            voice=voices[0];foreach(var candidate in voices)if(candidate.volume<voice.volume)voice=candidate;
        }
        int pick=Random.Range(0,cue.variations.Length);
        if(cue.variations.Length>1&&previousVariant.TryGetValue(sound,out int old)&&pick==old)pick=(pick+1)%cue.variations.Length;
        if(!cue.variations[pick])return;
        previousVariant[sound]=pick;lastPlayed[sound]=now;
        voice.Stop();voice.transform.position=position??Vector3.zero;voice.spatialBlend=position.HasValue?1:0;
        voice.clip=cue.variations[pick];
        // Keep warnings low and breathing natural; action Foley gets a brisker response.
        float speed=sound==MazeSound.BossAlert||sound==MazeSound.BossAccelerate||sound==MazeSound.BossRustle
            ||sound==MazeSound.Breath||sound==MazeSound.SprintBreath?1:effectsPlaybackSpeed;
        voice.pitch=speed+Random.Range(-cue.pitchVariation,cue.pitchVariation);
        voiceGains[voice]=Mathf.Clamp01(cue.volume*gain);voice.volume=voiceGains[voice]*EffectsGain;
        voice.Play();LastCue=sound;PlayedCueCount++;
    }
    public static void BeginStory(MazeStoryBeat kind)
    {
        if(!Instance||!Instance.library)return;
        Instance.ActiveStory=kind;Instance.story.Stop();Instance.story.clip=Instance.library.StoryMusic(kind);
        Instance.storyGain=0;
        Play(kind==MazeStoryBeat.CampaignComplete?MazeSound.Victory:kind==MazeStoryBeat.AreaComplete?MazeSound.AreaWin:
            kind==MazeStoryBeat.BossDefeat?MazeSound.DefeatBoss:kind==MazeStoryBeat.HealthDefeat?MazeSound.DefeatHealth:MazeSound.PageTurn);
    }
    public static void EndStory(){if(Instance)Instance.ActiveStory=null;}
    public static void StoryShot(MazeStoryBeat kind,int shot)
    {
        if(shot==0)return;
        Play(kind==MazeStoryBeat.BossDefeat&&shot==2?MazeSound.Stamp:MazeSound.PageTurn,null,.65f);
        if(shot==1)Play(kind==MazeStoryBeat.HealthDefeat?MazeSound.SprintBreath:kind==MazeStoryBeat.AreaComplete||kind==MazeStoryBeat.CampaignComplete?MazeSound.Breath:MazeSound.BossRustle,null,.5f);
    }
    public static void LevelStarted(MazeBetaGame owner)
    {
        if(!Instance)return;
        Instance.game=owner;Instance.inMenu=false;Instance.ActiveStory=null;Instance.ThreatActive=false;
        foreach(var voice in Instance.voices)if(voice.spatialBlend>0)voice.Stop();
        if(owner.Player&&!owner.Player.GetComponent<PlayerAudio>())owner.Player.gameObject.AddComponent<PlayerAudio>();
        Play(MazeSound.LevelStart);
    }
    public static void Item(ItemType type)
    {
        Play(type==ItemType.Curacion?MazeSound.Heal:type==ItemType.Escudo?MazeSound.Shield:type==ItemType.Velocidad?MazeSound.Speed:MazeSound.DoubleJumpPower);
    }
    void OnDestroy(){if(Instance==this){SavePreferences();Instance=null;}SceneManager.sceneLoaded-=SceneLoaded;}
}

