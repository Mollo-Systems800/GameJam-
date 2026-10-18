using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class OfficeAudioSetup
{
    const string Request="Logs/setup-office-audio.request";
    static OfficeAudioSetup(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)return;
        try{File.Delete(Request);}catch(IOException){return;}
        try{Apply();File.WriteAllText("Logs/office-audio-setup-result.txt","PASS: 50 original clips imported; audio library, persistent audio prefab, player Foley and scene listeners configured.");}
        catch(Exception error){File.WriteAllText("Logs/office-audio-setup-result.txt",error.ToString());Debug.LogException(error);}
    }
    [MenuItem("Tools/Maze Beta/Configure office audio")]
    public static void Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string path in Directory.GetFiles("Assets/05_Sounds","*.wav",SearchOption.AllDirectories))
        {
            var importer=AssetImporter.GetAtPath(path.Replace('\\','/')) as AudioImporter;
            if(!importer)throw new Exception("Missing audio importer: "+path);
            bool music=path.Replace('\\','/').Contains("/Music/");
            var settings=importer.defaultSampleSettings;
            settings.loadType=music?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=music?AudioCompressionFormat.Vorbis:AudioCompressionFormat.PCM;
            settings.quality=.8f;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData=!music;
            importer.defaultSampleSettings=settings;importer.forceToMono=!music;importer.ambisonic=false;
            importer.loadInBackground=music;importer.SaveAndReimport();
        }
        if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
        const string libraryPath="Assets/Resources/MazeAudioLibrary.asset";
        var library=AssetDatabase.LoadAssetAtPath<MazeAudioLibrary>(libraryPath);
        bool fresh=!library;if(fresh){library=ScriptableObject.CreateInstance<MazeAudioLibrary>();AssetDatabase.CreateAsset(library,libraryPath);}
        if(!library.menuMusic)library.menuMusic=Music("Menu");
        if(!library.normalMusic)library.normalMusic=Music("OfficeNormal");
        if(!library.threatMusic)library.threatMusic=Music("OfficeThreat");
        if(!library.introMusic)library.introMusic=Music("StoryIntro");
        if(!library.areaMusic)library.areaMusic=Music("StoryArea");
        if(!library.victoryMusic)library.victoryMusic=Music("StoryVictory");
        if(!library.bossDefeatMusic)library.bossDefeatMusic=Music("StoryBossLoss");
        if(!library.healthDefeatMusic)library.healthDefeatMusic=Music("StoryHealthLoss");
        var cues=library.cues==null?new System.Collections.Generic.List<MazeAudioCue>():library.cues.ToList();
        foreach(MazeSound sound in Enum.GetValues(typeof(MazeSound)))
        {
            if(cues.Any(c=>c.sound==sound))continue;
            var clip=sound==MazeSound.Footstep?Enumerable.Range(1,4).Select(i=>Foley("Footstep"+i)).ToArray():new[]{Foley(sound.ToString())};
            if(clip.Any(c=>!c))throw new Exception("Missing clip "+sound);
            float volume=sound==MazeSound.Footstep?.4f:sound==MazeSound.Breath||sound==MazeSound.SprintBreath?.22f:
                sound==MazeSound.UIHover?.18f:sound==MazeSound.BossRustle||sound==MazeSound.ShovelMove?.3f:.6f;
            cues.Add(new MazeAudioCue{sound=sound,variations=clip,volume=volume,
                pitchVariation=sound==MazeSound.Footstep?.09f:sound==MazeSound.LevelWin||sound==MazeSound.Victory?0:.035f,
                minimumInterval=sound==MazeSound.ShieldHit?.35f:sound==MazeSound.ShovelMove?.35f:.07f});
        }
        library.cues=cues.ToArray();EditorUtility.SetDirty(library);
        const string prefabPath="Assets/Resources/MazeAudio.prefab";
        if(!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
        {
            var audio=new GameObject("Audio - The Maze of work");audio.AddComponent<MazeAudioDirector>().library=library;
            try{PrefabUtility.SaveAsPrefabAsset(audio,prefabPath);}finally{UnityEngine.Object.DestroyImmediate(audio);}
        }
        string playerPath="Assets/03_Prefabs/Player.prefab";var player=PrefabUtility.LoadPrefabContents(playerPath);
        try{if(!player.GetComponent<PlayerAudio>())player.AddComponent<PlayerAudio>();PrefabUtility.SaveAsPrefabAsset(player,playerPath);}
        finally{PrefabUtility.UnloadPrefabContents(player);}
        string original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        try
        {
            foreach(string scene in new[]{"Assets/00_Scenes/Inicio.unity","Assets/00_Scenes/BetaMaze.unity"})
            {
                var opened=EditorSceneManager.OpenScene(scene);var cameras=UnityEngine.Object.FindObjectsByType<Camera>();
                if(!cameras.Any(c=>c.GetComponent<AudioListener>()))
                {
                    var camera=Camera.main?Camera.main:cameras.FirstOrDefault();
                    if(!camera)camera=new GameObject("Main Camera",typeof(Camera)).GetComponent<Camera>();
                    camera.gameObject.AddComponent<AudioListener>();EditorSceneManager.MarkSceneDirty(opened);EditorSceneManager.SaveScene(opened);
                }
            }
        }
        finally{if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);}
        AssetDatabase.SaveAssets();
    }
    static AudioClip Music(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/05_Sounds/Music/"+name+".wav");
    static AudioClip Foley(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/05_Sounds/Foley/"+name+".wav");
}
