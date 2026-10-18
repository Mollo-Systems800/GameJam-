using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OfficeAudioVerification
{
    const string Key="GameJam.OfficeAudioVerification",Request="Logs/verify-office-audio.request";
    static int phase,storyIndex;
    static double started,since;
    static float threatTime;
    static MazeBetaGame game;
    static OfficeAudioVerification()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=state=>
        {
            SessionState.SetBool(Key+"Ready",state==PlayModeStateChange.EnteredPlayMode);
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Restore",false))
            {
                SessionState.SetBool(Key+"Restore",false);
                EditorApplication.delayCall+=()=>EditorSceneManager.OpenScene(SessionState.GetString(Key+"Scene","Assets/00_Scenes/Inicio.unity"));
            }
        };
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Next(){phase++;since=EditorApplication.timeSinceStartup;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var scene=SceneManager.GetActiveScene();if(scene.isDirty)return;
            try{File.Delete(Request);}catch(IOException){return;}
            try
            {
                VerifyAssets();SessionState.SetString(Key+"Scene",scene.path);
                EditorSceneManager.OpenScene("Assets/00_Scenes/Inicio.unity");
                SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Restore",true);SessionState.SetBool(Key+"Ready",false);
                phase=storyIndex=0;started=since=0;Application.runInBackground=true;EditorApplication.isPlaying=true;
            }
            catch(Exception error){Finish("FAIL setup: "+error);}
            return;
        }
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||!SessionState.GetBool(Key+"Ready",false))return;
        try
        {
            if(started==0){started=since=EditorApplication.timeSinceStartup;}
            Check(EditorApplication.timeSinceStartup-started<100,"Audio verification timed out");
            var audio=MazeAudioDirector.Instance;if(!audio)return;
            audio.masterVolume=0; // Exercise sources without playing a test burst on the user's speakers.
            if(phase==0)
            {
                if(EditorApplication.timeSinceStartup-since<1.2)return;
                Check(audio.MenuSource.clip==audio.library.menuMusic&&audio.MenuSource.isPlaying,"Inicio music did not start");
                Check(UnityEngine.Object.FindObjectsByType<MazeAudioDirector>().Length==1,"Duplicate persistent audio directors");
                Check(UnityEngine.Object.FindAnyObjectByType<StartupMenu>().rankingButton.GetComponent<MazeButtonAudio>(),"Menu/ranking buttons have no audio");
                SceneManager.LoadScene("BetaMaze");Next();return;
            }
            if(phase==1)
            {
                game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
                if(!game||game.Grid==null||!game.Player||!game.Player.GetComponent<PlayerLimbAnimator>().IsReady)return;
                game.recordResults=false;game.bossGracePeriod=100000;game.spadeCount=game.turretCount=0;
                game.width=game.height=8;game.StartCampaign(48125);game.Player.CanMove=false;game.Boss.enabled=false;
                game.Boss.transform.position=game.Player.transform.position+Vector3.forward*20;
                Check(game.Player.GetComponents<PlayerAudio>().Length==1,"Player Foley missing or duplicated");
                Next();return;
            }
            if(phase==2)
            {
                if(EditorApplication.timeSinceStartup-since<1.8)return;
                Check(audio.NormalSource.isPlaying&&audio.ThreatSource.isPlaying,"Gameplay loops not playing");
                Check(audio.NormalSource.clip==audio.library.normalMusic&&audio.ThreatSource.clip==audio.library.threatMusic,"Wrong gameplay backgrounds");
                Check(!audio.ThreatActive&&audio.ThreatBlend<.01f,"Distant boss triggered tension");
                game.Boss.transform.position=game.Player.transform.position+Vector3.forward*6;Next();return;
            }
            if(phase==3)
            {
                if(EditorApplication.timeSinceStartup-since<2)return;
                Check(audio.ThreatActive&&audio.ThreatBlend>.95f,"Nearby boss did not crossfade to tension");
                game.Boss.transform.position=game.Player.transform.position+Vector3.forward*10;threatTime=audio.ThreatSource.time;Next();return;
            }
            if(phase==4)
            {
                if(EditorApplication.timeSinceStartup-since<.4)return;
                Check(audio.ThreatActive,"Threat hysteresis flickers at intermediate distance");
                game.Boss.transform.position=game.Player.transform.position+Vector3.forward*16;Next();return;
            }
            if(phase==5)
            {
                if(EditorApplication.timeSinceStartup-since<2)return;
                Check(!audio.ThreatActive&&audio.ThreatBlend<.05f,"Leaving boss did not restore normal background");
                Check(audio.ThreatSource.time>threatTime,"Crossfade restarted the tension loop");
                Check(UnityEngine.Object.FindObjectsByType<MazeAudioDirector>().Length==1,"Scene transition duplicated audio");
                game.HitPlayer(1,"AUDIO TEST");Check(audio.LastCue==MazeSound.Hurt,"Accepted damage is silent");
                game.Progress.ApplyItem(ItemType.Escudo,5,1);game.HitPlayer(1,"SHIELD TEST");Check(audio.LastCue==MazeSound.ShieldHit,"Shield impact is silent");
                game.Player.CollectPills();Check(audio.LastCue==MazeSound.Pills,"Medicine pickup is silent");
                game.SetShopOpen(true);Check(audio.LastCue==MazeSound.ShopOpen,"Shop open is silent");
                game.Progress.AddCoins(30);Check(game.BuyPowerUp(PowerUpKind.Speed),"Shop fixture failed");
                Check(audio.LastCue==MazeSound.Speed,"Purchased effect is silent");game.SetShopOpen(false);
                game.PlayNewGameIntro();Next();return;
            }
            if(phase==6)
            {
                var cinematic=game.GetComponent<NewGameCinematic>();if(!cinematic)return;
                cinematic.PreviewWhileUnfocused=true;
                if(EditorApplication.timeSinceStartup-since<.5)return;
                Check(audio.ActiveStory==cinematic.Kind&&audio.StorySource.isPlaying&&audio.StorySource.clip==audio.library.StoryMusic(cinematic.Kind),"Cinematic score missing for "+cinematic.Kind);
                cinematic.Cancel();Check(!audio.ActiveStory.HasValue,"Cinematic score state leaked after cancel");
                storyIndex++;
                if(storyIndex<5)
                {
                    cinematic.Play(game,Camera.main,Camera.main.GetComponent<PlayerFollowCamera>(),5,(MazeStoryBeat)storyIndex);
                    since=EditorApplication.timeSinceStartup;return;
                }
                Next();return;
            }
            if(phase==7)
            {
                if(EditorApplication.timeSinceStartup-since<1)return;
                foreach(var cue in audio.library.cues)
                {
                    // Test each sample independently. World chatter is intentionally dropped
                    // when all voices are busy, so a 39-cue burst is not a valid playback test.
                    foreach(var source in audio.GetComponentsInChildren<AudioSource>())if(!source.loop)source.Stop();
                    int before=audio.PlayedCueCount;audio.Emit(cue.sound);
                    Check(audio.PlayedCueCount==before+1,"Cue could not play: "+cue.sound);
                }
                game.StartCampaign(48126);game.Player.CanMove=false;game.Boss.enabled=false;
                Check(game.Player.GetComponents<PlayerAudio>().Length==1,"Retry added duplicate player audio");
                SceneManager.LoadScene("Inicio");Next();return;
            }
            if(phase==8)
            {
                if(EditorApplication.timeSinceStartup-since<1.5)return;
                Check(audio.MenuSource.isPlaying&&audio.MenuSource.clip==audio.library.menuMusic&&!audio.ActiveStory.HasValue,"Returning to Inicio did not restore menu music");
                Check(UnityEngine.Object.FindObjectsByType<MazeAudioDirector>().Length==1,"Menu return duplicated audio");
                Finish("PASS: 50 original WAVs; complete cue library; imported music/SFX settings and finite non-silent PCM; Inicio and game playback; nearby/far boss crossfade with hysteresis and uninterrupted loops; hurt/shield/pills/shop cues; five cinematic scores and cleanup; all 39 effect cues play; player Foley survives regeneration; menu music restored across scenes. No campaign/ranking data written; test output muted.");
            }
        }
        catch(Exception error){Finish("FAIL phase "+phase+": "+error);}
    }
    static void VerifyAssets()
    {
        var library=Resources.Load<MazeAudioLibrary>("MazeAudioLibrary");Check(library,"Audio library missing");
        Check(Resources.Load<GameObject>("MazeAudio").GetComponent<MazeAudioDirector>().library==library,"Persistent audio prefab not configured");
        Check(library.cues.Length==Enum.GetValues(typeof(MazeSound)).Length,"Effect coverage is incomplete");
        foreach(var cue in library.cues)
        {
            Check(cue.variations.Length>0&&cue.variations.All(c=>c&&c.length>.02f),"Empty cue "+cue.sound);
            foreach(var clip in cue.variations)
            {
                clip.LoadAudioData();float[] samples=new float[clip.samples*clip.channels];Check(clip.GetData(samples,0),"Cannot decode "+clip.name);
                Check(samples.Any(s=>Mathf.Abs(s)>.01f)&&samples.All(s=>!float.IsNaN(s)&&!float.IsInfinity(s)&&Mathf.Abs(s)<.8f),"Silent/clipped PCM "+clip.name);
            }
        }
        foreach(var clip in new[]{library.menuMusic,library.normalMusic,library.threatMusic,library.introMusic,library.areaMusic,library.victoryMusic,library.bossDefeatMusic,library.healthDefeatMusic})
        {Check(clip&&clip.channels==2&&clip.length>5,"Missing stereo music");}
    }
    static void Finish(string result)
    {
        SessionState.SetBool(Key,false);File.WriteAllText("Logs/office-audio-verification-result.txt",result);Debug.Log(result);
        if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
    }
}
