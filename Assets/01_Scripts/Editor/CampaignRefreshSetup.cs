using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CampaignRefreshSetup
{
    static CampaignRefreshSetup() { EditorApplication.update += Tick; }
    static void Tick()
    {
        const string request="Logs/campaign-refresh.request";
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(active.isDirty && active.path!="Assets/00_Scenes/BetaMaze.unity")return;
        File.Delete(request);
        try { Apply(); } catch(Exception error) { File.WriteAllText("Logs/campaign-refresh-result.txt",error.ToString()); Debug.LogException(error); }
    }
    const string Park = "Assets/Palmov Island/Low Poly Environment Park/Prefabs/";
    const string RPG = "Assets/RPGPP_LT/Prefabs/";
    public static void Apply()
    {
        ConfigureAudio();
        var office = Prop("Office furnishings", new[] {
            Model(Park+"Environment/wooden table.prefab"), Model(RPG+"Props/Containers/rpgpp_lt_vase_01.prefab") });
        var archive = Prop("Archive shelving", new[] {
            Model(RPG+"Props/Containers/rpgpp_lt_box_wood_01.prefab"), Model(RPG+"Props/Containers/rpgpp_lt_package_01.prefab") }, true);
        var plant = Model(RPG+"Nature/Vegetation/Plants/rpgpp_lt_plant_01.prefab");
        var officeFloor = Floor("Office tiled floor", new Color(.32f,.36f,.35f), new Color(.43f,.46f,.43f));
        var archiveFloor = Model(RPG+"Exterior/Wood_path/rpgpp_lt_wood_path_01a.prefab");
        var patioFloor = Model(RPG+"Nature/Terrain/rpgpp_lt_terrain_path_01a.prefab");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/00_Scenes/BetaMaze.unity")scene=EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
        var game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
        game.areas[0].floorPrefab=officeFloor;
        game.areas[0].decorationPrefabs=new[]{office, plant};
        game.areas[1].floorPrefab=archiveFloor;
        game.areas[1].decorationPrefabs=new[]{archive, Model(RPG+"Props/Containers/rpgpp_lt_crate_02.prefab"), Model(RPG+"Props/Containers/rpgpp_lt_package_01.prefab")};
        game.areas[2].floorPrefab=patioFloor;
        game.areas[2].decorationPrefabs=new[]{plant, Model(Park+"Stones/stone 2.prefab"), Model(Park+"Environment/bench 1.prefab"), Model(RPG+"Nature/Vegetation/Flowers/rpgpp_lt_flower_01.prefab")};
        game.areas[2].landscapePrefab=Model(Park+"Trees/tree oak.prefab");
        game.playOutcomeCinematics=true;
        EditorUtility.SetDirty(game); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Verify();
    }
    static GameObject Model(string path)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(!prefab)throw new Exception("Missing supplied asset: "+path);
        // Imported packs use Built-in Standard materials; make their copies URP-compatible.
        foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
        foreach(var material in renderer.sharedMaterials)
        {
            if(!material || material.shader.name.StartsWith("Universal Render Pipeline/"))continue;
            var texture=material.mainTexture; var color=material.HasProperty("_Color")?material.color:Color.white;
            material.shader=Shader.Find("Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap",texture); material.SetColor("_BaseColor",color); EditorUtility.SetDirty(material);
        }
        return prefab;
    }
    static Material Mat(string name, Color color)
    {
        string path="Assets/04_Materials/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.color=color; EditorUtility.SetDirty(mat); return mat;
    }
    static void Box(Transform root,string name,Vector3 pos,Vector3 scale,Material mat)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name;
        obj.transform.SetParent(root,false);obj.transform.localPosition=pos;obj.transform.localScale=scale;
        obj.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
    }
    static GameObject Save(GameObject root)
    {
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/03_Prefabs/"+root.name+".prefab");
        UnityEngine.Object.DestroyImmediate(root);return prefab;
    }
    static GameObject Floor(string name,Color a,Color b)
    {
        var root=new GameObject(name);var mats=new[]{Mat(name+" A",a),Mat(name+" B",b)};
        for(int x=0;x<4;x++)for(int z=0;z<4;z++)
            Box(root.transform,"Baldosa",new Vector3((x-1.5f)*.25f,-.05f,(z-1.5f)*.25f),new Vector3(.245f,.1f,.245f),mats[(x+z)%2]);
        return Save(root);
    }
    static GameObject Prop(string name,GameObject[] models,bool shelf=false)
    {
        var root=new GameObject(name);
        var mat=Mat("Archive shelf wood",new Color(.28f,.17f,.095f));
        if(shelf)
        {
            Box(root.transform,"Lateral",new Vector3(-.45f,.75f,0),new Vector3(.08f,1.5f,.4f),mat);
            Box(root.transform,"Lateral",new Vector3(.45f,.75f,0),new Vector3(.08f,1.5f,.4f),mat);
            for(int i=0;i<3;i++)
            {
                Box(root.transform,"Estante",new Vector3(0,i*.5f+.08f,0),new Vector3(.9f,.06f,.4f),mat);
                var holder=new GameObject("Expedientes").transform;holder.SetParent(root.transform,false);
                MazeBetaGame.FitVisual(models[i%models.Length],holder,new Vector3(.7f,.36f,.35f),true);holder.localPosition=Vector3.up*(i*.5f+.11f);
            }
        }
        else
        {
            MazeBetaGame.FitVisual(models[0],root.transform,new Vector3(1,.7f,.55f),false);
            var holder=new GameObject("Jarron").transform;holder.SetParent(root.transform,false);
            MazeBetaGame.FitVisual(models[1],holder,new Vector3(.2f,.25f,.2f),true);holder.localPosition=new Vector3(.3f,.7f,0);
        }
        return Save(root);
    }
    static AudioClip Clip(string name)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/05_Sounds/phool/"+name+".mp3");
        if(!clip)throw new Exception("Missing audio "+name);return clip;
    }
    static void ConfigureAudio()
    {
        var lib=AssetDatabase.LoadAssetAtPath<MazeAudioLibrary>("Assets/Resources/MazeAudioLibrary.asset");
        lib.menuMusic=lib.introMusic=Clip("intro menu");lib.normalMusic=Clip("run work");lib.threatMusic=Clip("run 3 ");
        lib.areaMusic=lib.victoryMusic=Clip("victoria");lib.bossDefeatMusic=lib.healthDefeatMusic=Clip("lose");
        lib.areaTracks=new[]{Clip("run work"),Clip("run 1"),Clip("run 2")};
        foreach(var cue in lib.cues)
        {
            string name;
            switch(cue.sound)
            {
                case MazeSound.Jump: case MazeSound.DoubleJump:case MazeSound.DoubleJumpPower:name="salto";break;
                case MazeSound.Roll:case MazeSound.ShovelMove:name="voltereta";break;
                case MazeSound.Coin:case MazeSound.Pills:case MazeSound.Heal:case MazeSound.Purchase:case MazeSound.Shield:case MazeSound.Speed:name="moneda";break;
                case MazeSound.Hurt:case MazeSound.ShieldHit:case MazeSound.ShovelHit:name="grito";break;
                case MazeSound.BossAlert:name="grito1";break;
                case MazeSound.BossAccelerate:name="grito2";break;
                case MazeSound.LevelWin:case MazeSound.AreaWin:case MazeSound.Victory:name="victoria";break;
                case MazeSound.DefeatBoss:case MazeSound.DefeatHealth:name="lose";break;
                case MazeSound.BossRustle:case MazeSound.Breath:case MazeSound.SprintBreath:name="ecos";break;
                default:name="click";break;
            }
            cue.variations=cue.sound==MazeSound.Hurt?new[]{Clip("grito"),Clip("grito1"),Clip("grito2")}:new[]{Clip(name)};
            cue.pitchVariation=.025f;
            bool ambience=name=="ecos";
            cue.minimumInterval=ambience?8:cue.sound==MazeSound.BossAlert?6:.15f;
            cue.volume=ambience?.12f:name.StartsWith("grito")?.35f:cue.sound==MazeSound.Footstep?.1f:.5f;
        }
        EditorUtility.SetDirty(lib);
    }
    public static void Verify()
    {
        var lib=AssetDatabase.LoadAssetAtPath<MazeAudioLibrary>("Assets/Resources/MazeAudioLibrary.asset");
        var used=lib.cues.SelectMany(c=>c.variations).Concat(lib.areaTracks).Concat(new[]{lib.menuMusic,lib.threatMusic,lib.victoryMusic,lib.healthDefeatMusic}).Select(AssetDatabase.GetAssetPath).Distinct().ToArray();
        foreach(string path in Directory.GetFiles("Assets/05_Sounds/phool","*.mp3"))
            if(!used.Contains(path.Replace('\\','/')))throw new Exception("Unused supplied audio: "+path);
        foreach(var name in new[]{"VictoryMotion","WritingMotion"})
            if(!Resources.Load<TextAsset>(name))throw new Exception("Missing motion "+name);
        var progress=new RunProgress();progress.TakeDamage(1);
        if(new RunProgress(progress.Health).Health!=2)throw new Exception("Lives lost on retry");
        progress.LoseLife();progress.LoseLife();if(progress.Health!=0)throw new Exception("Final defeat unreachable");
        File.WriteAllText("Logs/campaign-refresh-result.txt","PASS: all 15 phool clips referenced; Victory/Writing baked; persistent lives; three themed areas configured.");
    }
}


[InitializeOnLoad]
public static class CampaignRefreshVerification
{
    const string Key="CampaignRefreshVerification.Active";
    static int stage;
    static double next;
    static CampaignRefreshVerification(){EditorApplication.update+=Tick;}
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(!EditorApplication.isPlaying && File.Exists("Logs/campaign-refresh-play.request"))
        {
            File.Delete("Logs/campaign-refresh-play.request");
            SessionState.SetBool(Key,true);stage=0;next=EditorApplication.timeSinceStartup+3;
            EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        try
        {
            var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
            if(!game||!game.Player)return;
            game.recordResults=false;game.playNewGameIntro=false;game.bossGracePeriod=10000;
            if(stage==0)
            {
                game.SkipCinematic();game.playOutcomeCinematics=false;game.StartCampaign(9842,4);
                Check(game.Grid.Width==game.width+4 && game.Grid.Height==game.height+2,"Level size did not grow");
                Check(game.Boss.startingSpeed>game.bossSpeed && game.Difficulty.Spades>game.spadeCount,"Difficulty did not grow");
                for(int i=2;i>=0;i--)
                {
                    Check(game.HitPlayer(3,"CAPTURE TEST",true),"Capture rejected");
                    Check(game.Progress.Health==i,"Capture must cost exactly one life");
                    Check(game.WaitingForRetry,"Capture must end attempt");
                    if(i>0){int seed=game.ActiveSeed;game.RetryWithNewMaze();Check(game.ActiveSeed!=seed && game.Progress.Health==i&&game.CurrentLevelIndex==3,"Retry must change maze and preserve lives/level");}
                }
                Check(game.IsFinalDefeat,"Zero lives not final defeat");
                game.defeatRestartDelay=1;
                typeof(MazeBetaGame).GetProperty("RetrySecondsRemaining").SetValue(game,0f);
                stage++;next=EditorApplication.timeSinceStartup+1;return;
            }
            if(stage==1)
            {
                Check(game.CurrentLevelIndex==0&&game.Progress.Health==3,"Final defeat did not return to level 1 with three lives");
                game.playOutcomeCinematics=true;game.Finish(true);
                Check(game.IsCinematicPlaying,"Level 1 victory missing cinematic");
                Check(game.Player.GetComponent<PlayerLimbAnimator>().HasOutcomeMotion,"Victory motion missing");
                var cinematic=game.GetComponent<NewGameCinematic>();cinematic.PreviewWhileUnfocused=true;
                stage++;next=EditorApplication.timeSinceStartup+1;return;
            }
            if(stage==2)
            {
                Capture("victory");game.GetComponent<NewGameCinematic>().Cancel();
                game.StartCampaign(9842,1,lives:1);game.HitPlayer(1,"FINAL TEST",true);
                Check(game.IsFinalDefeat&&game.Player.GetComponent<PlayerLimbAnimator>().HasOutcomeMotion,"Writing motion missing");
                game.GetComponent<NewGameCinematic>().PreviewWhileUnfocused=true;
                stage++;next=EditorApplication.timeSinceStartup+1.5;return;
            }
            if(stage==3)
            {
                Capture("writing");game.GetComponent<NewGameCinematic>().Cancel();game.playOutcomeCinematics=false;
                stage++;next=0;return;
            }
            if(stage>=4&&stage<=6)
            {
                int area=stage-4;game.StartCampaign(9842,area*3+1);
                Check(game.GetComponentsInChildren<TextMesh>().Length>0,"Exit sign missing");
                Check(game.areas[area].decorationPrefabs.Length>0,"Theme props missing");
                var camera=Camera.main;camera.GetComponent<PlayerFollowCamera>().enabled=false;
                Vector3 target=game.CellPosition(game.Grid.ExitCell)+Vector3.up;
                camera.transform.position=target+new Vector3(-3,5,-4);camera.transform.LookAt(target);
                game.SendMessage("UpdateExitSigns");
                Capture("area-"+(area+1));stage++;next=EditorApplication.timeSinceStartup+.5;return;
            }
            File.WriteAllText("Logs/campaign-refresh-play-result.txt","PASS: capture 3→2→1→0; each retry changes seed and preserves level/lives; size and difficulty grow at level 4; final defeat returns to level 1 with 3 lives; Victory on level 1; Writing on final loss; all three area scenes rendered.");
            SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;
        }
        catch(Exception error)
        {
            File.WriteAllText("Logs/campaign-refresh-play-result.txt",error.ToString());SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;Debug.LogException(error);
        }
    }
    static void Capture(string name)
    {
        var camera=Camera.main;var previous=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(960,600,24);var image=new Texture2D(960,600,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,600),0,0);image.Apply();File.WriteAllBytes("Logs/refresh-"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
}
