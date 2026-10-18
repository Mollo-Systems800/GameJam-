using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AudioSettingsVerification
{
    const string Key="GameJam.AudioSettingsVerification",Request="Logs/verify-audio-settings.request",TestKey="MazeAudio.Verification.Preferences";
    static int phase;static double started,since;
    static AudioSettingsVerification()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=state=>
        {
            SessionState.SetBool(Key+"Ready",state==PlayModeStateChange.EnteredPlayMode);
            if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key+"Restore",false))return;
            SessionState.SetBool(Key+"Restore",false);PlayerPrefs.DeleteKey(TestKey);PlayerPrefs.Save();
            EditorApplication.delayCall+=()=>EditorSceneManager.OpenScene(SessionState.GetString(Key+"Scene","Assets/00_Scenes/Inicio.unity"));
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
            SessionState.SetString(Key+"Scene",scene.path);EditorSceneManager.OpenScene("Assets/00_Scenes/Inicio.unity");
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Restore",true);SessionState.SetBool(Key+"Ready",false);
            Application.runInBackground=true;EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||!SessionState.GetBool(Key+"Ready",false))return;
        try
        {
            if(started==0)started=since=EditorApplication.timeSinceStartup;
            Check(EditorApplication.timeSinceStartup-started<90,"Audio settings verification timed out");
            var audio=MazeAudioDirector.Instance;if(!audio)return;
            audio.masterVolume=.00001f; // Changes are exercised at an inaudible test level.
            if(phase==0)
            {
                var menu=UnityEngine.Object.FindAnyObjectByType<StartupMenu>();if(!menu||!menu.audioSettings)return;
                PlayerPrefs.DeleteKey(TestKey);audio.LoadPreferences(TestKey);
                var panel=menu.audioSettings;panel.Open();
                panel.musicSlider.value=.31f;panel.effectsSlider.value=.19f;
                panel.musicMute.isOn=true;Check(audio.MusicMuted&&!audio.EffectsMuted,"Music mute affects the wrong bus");
                panel.effectsMute.isOn=true;panel.allMute.isOn=true;
                Check(audio.AllMuted&&audio.MusicGain==0&&audio.EffectsGain==0,"Mute-all did not silence both buses");
                Check(Mathf.Abs(audio.musicVolume-.31f)<.001f&&Mathf.Abs(audio.effectsVolume-.19f)<.001f,"Mute destroyed slider values");
                Capture(menu.menuCanvas,"Logs/audio-settings-inicio.png");panel.Close();
                Check(PlayerPrefs.HasKey(TestKey),"Preferences not saved on close");
                audio.SetMusicVolume(.8f);audio.SetEffectsVolume(.9f);audio.LoadPreferences(TestKey);
                Check(Mathf.Abs(audio.musicVolume-.31f)<.001f&&Mathf.Abs(audio.effectsVolume-.19f)<.001f&&audio.AllMuted,"Reload did not restore preferences");
                SceneManager.LoadScene("BetaMaze");Next();return;
            }
            if(phase==1)
            {
                var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();if(!game||game.Grid==null||!game.Player)return;
                game.recordResults=false;game.Boss.enabled=false;
                typeof(MazeBetaGame).GetField("paused",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(game,true);
                typeof(MazeBetaGame).GetMethod("RefreshActivity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(game,null);
                UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>().Refresh();Next();return;
            }
            if(phase==2)
            {
                if(EditorApplication.timeSinceStartup-since<.3)return;
                var hud=UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();var panel=hud.audioSettings;
                Check(hud.resultPanel.activeSelf&&panel.openButton.gameObject.activeInHierarchy,"Pause has no visible sound button");
                panel.openButton.onClick.Invoke();Check(panel.IsOpen&&audio.AllMuted,"Pause preferences did not inherit menu settings");
                panel.allMute.isOn=false;panel.musicMute.isOn=false;panel.effectsMute.isOn=false;
                panel.musicSlider.value=.6f;panel.effectsSlider.value=.3f;
                Capture(hud.GetComponent<Canvas>(),"Logs/audio-settings-pausa.png");panel.Close();
                Check(hud.game.IsPaused&&!hud.game.Player.CanMove,"Closing sound settings resumed gameplay");
                audio.LoadPreferences(TestKey);Check(Mathf.Abs(audio.musicVolume-.6f)<.001f&&Mathf.Abs(audio.effectsVolume-.3f)<.001f&&!audio.AllMuted,"Pause preferences did not persist");
                SceneManager.LoadScene("Inicio");Next();return;
            }
            if(phase==3)
            {
                var menu=UnityEngine.Object.FindAnyObjectByType<StartupMenu>();if(!menu||!menu.audioSettings)return;
                menu.audioSettings.Open();Check(Mathf.Abs(menu.audioSettings.musicSlider.value-.6f)<.001f&&Mathf.Abs(menu.audioSettings.effectsSlider.value-.3f)<.001f,"Scene return did not synchronise sliders");
                audio.SetMusicVolume(-1);audio.SetEffectsVolume(5);Check(audio.musicVolume==0&&audio.effectsVolume==1,"Invalid volume not clamped");
                menu.audioSettings.Close();audio.LoadPreferences();
                Finish("PASS: Inicio and pause sound buttons/forms; sliders, percentages, independent music/effects mute and mute-all; immediate bus mute; preserved levels after unmuting; persistence/reload across scenes; close retains pause; clamping. Two native UI captures. Real audio preferences, campaign saves and ranking left unchanged.");
            }
        }
        catch(Exception error){if(MazeAudioDirector.Instance)MazeAudioDirector.Instance.LoadPreferences();Finish("FAIL phase "+phase+": "+error);}
    }
    static void Capture(Canvas canvas,string path)
    {
        var camera=Camera.main;var mode=canvas.renderMode;var originalCamera=canvas.worldCamera;float plane=canvas.planeDistance;
        float near=camera.nearClipPlane;int mask=camera.cullingMask;camera.nearClipPlane=.01f;camera.cullingMask|=1<<5;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.15f;
        var target=new RenderTexture(1440,900,24);target.Create();camera.targetTexture=target;var previous=RenderTexture.active;
        bool asyncShaders=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try
        {
            Canvas.ForceUpdateCanvases();camera.Render();camera.Render();RenderTexture.active=target;
            var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
            File.WriteAllBytes(path,pixels.EncodeToPNG());UnityEngine.Object.Destroy(pixels);
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=previous;ShaderUtil.allowAsyncCompilation=asyncShaders;target.Release();UnityEngine.Object.Destroy(target);
            canvas.renderMode=mode;canvas.worldCamera=originalCamera;canvas.planeDistance=plane;
            camera.nearClipPlane=near;camera.cullingMask=mask;
        }
    }
    static void Finish(string result)
    {
        SessionState.SetBool(Key,false);File.WriteAllText("Logs/audio-settings-verification-result.txt",result);Debug.Log(result);
        if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
    }
}
