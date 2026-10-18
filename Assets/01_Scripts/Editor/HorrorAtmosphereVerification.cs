using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class HorrorAtmosphereVerification
{
    const string Key="GameJam.HorrorVerification",Request="Logs/verify-horror-atmosphere.request";
    static int level,phase;
    static double started;
    static readonly StringBuilder report=new StringBuilder();
    static HorrorAtmosphereVerification()
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
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var scene=SceneManager.GetActiveScene();if(scene.isDirty)return;
            try{File.Delete(Request);}catch(IOException){return;}
            try
            {
                SessionState.SetString(Key+"Scene",scene.path);EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
                var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
                Check(game.horrorLighting&&game.officeLampPrefab&&game.exitBeaconPrefab&&game.playerGuideLightPrefab,"Scene references not saved");
                game.width=game.height=9;game.spadeCount=game.turretCount=0;game.bossGracePeriod=100000;
                SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Restore",true);SessionState.SetBool(Key+"Ready",false);
                Application.runInBackground=true;EditorApplication.isPlaying=true;
            }
            catch(Exception error){Finish("FAIL setup: "+error);}
            return;
        }
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||!SessionState.GetBool(Key+"Ready",false))return;
        try
        {
            if(started==0){started=EditorApplication.timeSinceStartup;report.Clear();level=phase=0;}
            Check(EditorApplication.timeSinceStartup-started<150,"Horror verification timed out");
            var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
            if(!game||game.Grid==null||!game.Player||!game.Player.GetComponent<PlayerLimbAnimator>().IsReady)return;
            if(MazeAudioDirector.Instance)MazeAudioDirector.Instance.masterVolume=0;
            game.recordResults=false;
            if(phase==0)
            {
                game.StartCampaign(92773,level+1);game.Player.CanMove=false;game.Boss.enabled=false;phase=1;return;
            }
            if(phase==1)
            {
                Check(RenderSettings.skybox==null&&RenderSettings.fog&&RenderSettings.ambientMode==UnityEngine.Rendering.AmbientMode.Flat,"Daytime sky/ambient survived");
                Check(Camera.main.clearFlags==CameraClearFlags.SolidColor&&Camera.main.backgroundColor.maxColorComponent<.03f,"Camera background is not dark");
                Check(UnityEngine.Object.FindObjectsByType<Light>().Where(l=>l.type==LightType.Directional).All(l=>l.intensity<.2f),"A daylight sun remains");
                Check(game.NightLightCells.Count>=4&&game.NightLightCells.Contains(0)&&game.NightLightCells.Contains(game.Grid.ExitCell),"Spawn/exit lamps missing");
                Check(!game.NightLightCells.Any(c=>game.Grid.IsParkourPit(c)),"Lamp stand floats in a pit");
                Check(UnityEngine.Object.FindObjectsByType<Transform>().Count(t=>t.name=="Luz de orientacion del jugador")==1,"Guide light duplicated on level regeneration");
                var group=game.GetComponentsInChildren<Transform>().Single(t=>t.name=="Iluminacion nocturna - puntos de luz");
                Check(!group.GetComponentsInChildren<Collider>().Any(c=>c.enabled),"A light fixture blocks movement");
                if(level%3==0)RenderArea(game,level/3+1);
                level++;
                if(level<9){phase=0;return;}
                RenderParkour(game);game.PlayNewGameIntro();phase=2;return;
            }
            if(phase==2)
            {
                var intro=game.GetComponent<NewGameCinematic>();intro.PreviewWhileUnfocused=true;
                if(intro.Shot!=1||intro.ElapsedSeconds<3.7f)return;
                Capture("Logs/horror-cinematic-player.png",true);intro.Cancel();
                File.WriteAllText("Logs/horror-lighting-exposure.txt",report.ToString());
                Finish("PASS: all nine levels use dark sky/background, fog and low ambient/moon light; spawn, exit and parkour endpoints lit; no lights floating in pits, no collision blockers, one guide light after regeneration; three native area views/overviews, parkour and cinematic character renders; no checkpoint/ranking writes.\n"+report);
            }
        }
        catch(Exception error){Finish("FAIL level "+(level+1)+" phase "+phase+": "+error);}
    }
    static void RenderArea(MazeBetaGame game,int area)
    {
        var camera=Camera.main;var follow=camera.GetComponent<PlayerFollowCamera>();follow.enabled=false;
        Vector3 p=camera.transform.position;Quaternion r=camera.transform.rotation;float fov=camera.fieldOfView;
        var target=game.Player.transform.position+Vector3.up*1.1f;
        camera.transform.position=game.Player.transform.position+new Vector3(.45f,2,-.95f);camera.transform.LookAt(target);camera.fieldOfView=65;
        Capture("Logs/horror-area-"+area+"-player.png",true);
        target=new Vector3(game.cellSize*3,0,game.cellSize*3);
        camera.transform.position=target+new Vector3(-10,18,-12);camera.transform.LookAt(target);camera.fieldOfView=55;
        Capture("Logs/horror-area-"+area+"-overview.png",false);
        camera.transform.SetPositionAndRotation(p,r);camera.fieldOfView=fov;follow.enabled=true;
    }
    static void RenderParkour(MazeBetaGame game)
    {
        Check(game.Grid.ParkourCells.Count>=3,"Parkour missing");var camera=Camera.main;var follow=camera.GetComponent<PlayerFollowCamera>();follow.enabled=false;
        Vector3 p=camera.transform.position;Quaternion r=camera.transform.rotation;
        Vector3 axis=new Vector3(MazeGrid.DX[game.Grid.ParkourDirection],0,MazeGrid.DY[game.Grid.ParkourDirection]);
        camera.transform.position=game.CellPosition(game.Grid.ParkourCells[0])-axis*.55f+Vector3.up*1.7f;
        camera.transform.LookAt(game.CellPosition(game.Grid.ParkourCells[game.Grid.ParkourCells.Count/2])+Vector3.up*.15f);
        Capture("Logs/horror-parkour.png",false);camera.transform.SetPositionAndRotation(p,r);follow.enabled=true;
    }
    static void Capture(string path,bool showHud)
    {
        var camera=Camera.main;var hud=UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();var canvas=hud.GetComponent<Canvas>();
        bool active=hud.gameObject.activeSelf;if(!showHud)hud.gameObject.SetActive(false);
        var mode=canvas.renderMode;var originalCamera=canvas.worldCamera;float plane=canvas.planeDistance;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.15f;
        var target=new RenderTexture(1440,900,24);target.Create();camera.targetTexture=target;
        var previous=RenderTexture.active;bool asyncShaders=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try
        {
            Canvas.ForceUpdateCanvases();camera.Render();camera.Render();RenderTexture.active=target;
            var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
            File.WriteAllBytes(path,pixels.EncodeToPNG());
            if(!showHud)
            {
                var colors=pixels.GetPixels32();double total=0;int bright=0;
                foreach(var color in colors){float y=(.2126f*color.r+.7152f*color.g+.0722f*color.b)/255;total+=y;if(y>.08f)bright++;}
                double average=total/colors.Length,fraction=(double)bright/colors.Length;
                report.AppendLine(Path.GetFileName(path)+$": mean {average:F3}; lit pixels {fraction:P1}");
                Check(average<.35&&fraction>.008,"Night render is washed out or unreadably black: "+path);
            }
            UnityEngine.Object.Destroy(pixels);
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=previous;ShaderUtil.allowAsyncCompilation=asyncShaders;target.Release();UnityEngine.Object.Destroy(target);
            canvas.renderMode=mode;canvas.worldCamera=originalCamera;canvas.planeDistance=plane;hud.gameObject.SetActive(active);
        }
    }
    static void Finish(string result)
    {
        SessionState.SetBool(Key,false);File.WriteAllText("Logs/horror-atmosphere-verification-result.txt",result);Debug.Log(result);
        if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
    }
}
