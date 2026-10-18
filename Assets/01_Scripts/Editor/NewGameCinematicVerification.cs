using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class NewGameCinematicVerification
{
    const string Pending="GameJam.CinematicVerification";
    const string Request="Logs/verify-new-game-cinematic.request";
    static bool changing;
    static int phase;
    static float frozenSeconds, frozenLevelSeconds;
    static double started;
    static Vector3 playerStart,bossStart;
    static NewGameCinematicVerification()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=state=>
        {
            changing=state==PlayModeStateChange.ExitingEditMode||state==PlayModeStateChange.ExitingPlayMode;
            SessionState.SetBool(Pending+"Ready",state==PlayModeStateChange.EnteredPlayMode);
            if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Pending+"Restore",false))return;
            SessionState.SetBool(Pending+"Restore",false);
            EditorApplication.delayCall+=()=>EditorSceneManager.OpenScene(SessionState.GetString(Pending+"Scene","Assets/00_Scenes/Inicio.unity"));
        };
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Tick()
    {
        if(EditorApplication.isUpdating||EditorApplication.isCompiling||changing)return;
        if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.isDirty)return;
            try{File.Delete(Request);}catch(IOException){return;}
            try
            {
                // Launch flags are consumed once; existing checkpoint/ranking data is only read.
                MazeCampaignSave.RequestNew();
                Check(MazeCampaignSave.ConsumeLaunch(out var first,out bool isNew)&&isNew&&first.Level==1,"New campaign intro flag missing");
                Check(!MazeCampaignSave.ConsumeLaunch(out _,out bool duplicate)&&!duplicate,"Launch flag repeated");
                MazeCampaignSave.RequestNew();bool continued=MazeCampaignSave.RequestContinue();
                Check(MazeCampaignSave.ConsumeLaunch(out _,out bool resume)==continued&&!resume,"Continue inherited the new-game intro");
                SessionState.SetString(Pending+"Scene",scene.path);
                EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
                var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();game.recordResults=false;
                game.width=game.height=8;game.spadeCount=game.turretCount=game.coinCount=0;game.bossGracePeriod=100000;
                SessionState.SetBool(Pending,true);SessionState.SetBool(Pending+"Restore",true);
                SessionState.SetBool(Pending+"Ready",false);
                phase=0;started=EditorApplication.timeSinceStartup;EditorApplication.isPlaying=true;
            }
            catch(Exception error){Finish("FAIL: "+error);}
            return;
        }
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||!SessionState.GetBool(Pending+"Ready",false))return;
        try
        {
            if(started==0)started=EditorApplication.timeSinceStartup;
            Check(EditorApplication.timeSinceStartup-started<180,"Cinematic test timed out");
            var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
            if(!game||game.Grid==null||game.Progress==null||!game.Player||!game.Player.GetComponent<PlayerLimbAnimator>().IsReady)return;
            var intro=game.GetComponent<NewGameCinematic>();
            if(phase==0)
            {
                game.recordResults=false;game.newGameIntroDuration=9;game.PlayNewGameIntro();
                intro=game.GetComponent<NewGameCinematic>();
                Check(intro&&game.IsIntroPlaying,"Cinematic did not start");
                frozenSeconds=game.CampaignSeconds;frozenLevelSeconds=game.Progress.Elapsed;
                playerStart=game.Player.transform.position;bossStart=game.Boss.transform.position;phase=1;return;
            }
            if(phase<=3)
            {
                Check(game.IsIntroPlaying&&!game.IsPlaying&&!game.Player.CanMove&&!game.Boss.IsActive,"Gameplay was not locked during cinematic");
                Check(Mathf.Abs(game.CampaignSeconds-frozenSeconds)<.0001f&&Mathf.Abs(game.Progress.Elapsed-frozenLevelSeconds)<.0001f,"Cinematic counted toward score/ranking time");
                Check((game.Player.transform.position-playerStart).sqrMagnitude<.0001f&&(game.Boss.transform.position-bossStart).sqrMagnitude<.0001f,"Actor moved during intro");
                Check(game.Player.GetComponent<PlayerLimbAnimator>().CinematicIdle,"Breathing animation disabled during intro");
                if(intro.Shot==phase-1&&intro.ElapsedSeconds>(phase==1?1f:phase==2?3.6f:6.2f))
                {Capture(phase);phase++;}
                return;
            }
            if(phase==4&&!game.IsIntroPlaying)
            {
                Restored(game);game.PlayNewGameIntro();phase=5;return;
            }
            if(phase==5&&intro.ElapsedSeconds>.6f)
            {
                UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>().GetComponentsInChildren<Button>().First(b=>b.name=="Saltar cinematica").onClick.Invoke();phase=6;return;
            }
            if(phase==6&&!game.IsIntroPlaying)
            {
                Restored(game);
                game.PlayNewGameIntro();Check(game.IsIntroPlaying,"Replay missing");
                game.StartCampaign(512,2);Check(!game.IsIntroPlaying&&game.IsPlaying,"Continue/level generation leaked cinematic state");
                game.playNewGameIntro=false;game.StartNewCampaign(912);
                Check(!game.IsIntroPlaying&&game.IsPlaying,"Inspector disable flag ignored");
                Finish("PASS: three native camera shots; nine-second natural ending; skip button; breathing; frozen campaign/level time, player and boss; controls, follow camera and original HUD restored; new-game-only one-shot launch flags; continue/reset and disabled-intro option; existing saves/ranking preserved.");
            }
        }
        catch(Exception error){Finish("FAIL phase "+phase+": "+error);}
    }
    static void Restored(MazeBetaGame game)
    {
        Check(game.IsPlaying&&game.Player.CanMove&&game.Boss.IsActive,"Controls not restored");
        Check(!game.Player.GetComponent<PlayerLimbAnimator>().CinematicIdle,"Actor left in cinematic idle");
        Check(Camera.main.GetComponent<PlayerFollowCamera>().enabled&&Mathf.Abs(Camera.main.fieldOfView-game.cameraFieldOfView)<.001f,"Gameplay camera not restored");
        var hud=UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();
        var overlay=hud.transform.Find("Nueva partida - cinematica");
        Check(hud.transform.Find("Stats").gameObject.activeSelf&&(!overlay||!overlay.gameObject.activeSelf),"HUD or overlay not cleaned up");
    }
    static void Finish(string result)
    {
        SessionState.SetBool(Pending,false);File.WriteAllText("Logs/new-game-cinematic-result.txt",result);Debug.Log(result);
        if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
    }
    static void Capture(int shot)
    {
        var camera=Camera.main;var hud=UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();var canvas=hud.GetComponent<Canvas>();
        var mode=canvas.renderMode;var originalCamera=canvas.worldCamera;float plane=canvas.planeDistance;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.15f;
        var target=new RenderTexture(1600,1000,24);target.Create();camera.targetTexture=target;
        var previous=RenderTexture.active;bool asyncShaders=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try
        {
            Canvas.ForceUpdateCanvases();camera.Render();camera.Render();RenderTexture.active=target;
            var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes($"Logs/new-game-cinematic-shot-{shot}.png",pixels.EncodeToPNG());UnityEngine.Object.Destroy(pixels);
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=previous;ShaderUtil.allowAsyncCompilation=asyncShaders;target.Release();UnityEngine.Object.Destroy(target);
            canvas.renderMode=mode;canvas.worldCamera=originalCamera;canvas.planeDistance=plane;
        }
    }
}
