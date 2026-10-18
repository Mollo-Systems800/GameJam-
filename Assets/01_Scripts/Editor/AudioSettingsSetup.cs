using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AudioSettingsSetup
{
    const string Request="Logs/setup-audio-settings.request";
    static AudioSettingsSetup(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)return;
        try{File.Delete(Request);}catch(IOException){return;}
        string original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/00_Scenes/Inicio.unity");var menu=UnityEngine.Object.FindAnyObjectByType<StartupMenu>();
            menu.audioSettings=MazeAudioSettingsView.Ensure(menu,null);FitHandles(menu.audioSettings);EditorUtility.SetDirty(menu);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            scene=EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");var hud=UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();
            hud.audioSettings=MazeAudioSettingsView.Ensure(null,hud);FitHandles(hud.audioSettings);EditorUtility.SetDirty(hud);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Logs/audio-settings-setup-result.txt","PASS: sound button and shared audio settings form saved in Inicio and pause Canvas.");
        }
        catch(Exception error){File.WriteAllText("Logs/audio-settings-setup-result.txt",error.ToString());Debug.LogException(error);}
        finally{if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);}
    }
    static void FitHandles(MazeAudioSettingsPanel panel)
    {panel.musicSlider.handleRect.sizeDelta=panel.effectsSlider.handleRect.sizeDelta=new Vector2(24,-4);}
}
