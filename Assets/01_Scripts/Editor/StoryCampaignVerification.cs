using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class StoryCampaignVerification
{
    const string Key="GameJam.StoryVerification",Request="Logs/verify-story-campaign.request";
    static int phase,index,originalSeed;
    static double started;
    static float clock,retry;
    static MazeStoryBeat expected;
    static readonly int[] Levels={1,3,6,9,1,4,7,3,6,9};
    static StoryCampaignVerification()
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
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.isDirty)return;
            try{File.Delete(Request);}catch(IOException){return;}
            try
            {
                SessionState.SetString(Key+"Scene",scene.path);EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
                var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();game.recordResults=false;
                game.width=game.height=8;game.spadeCount=game.turretCount=game.coinCount=0;game.bossGracePeriod=100000;
                SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Restore",true);SessionState.SetBool(Key+"Ready",false);
                EditorApplication.isPlaying=true;
            }
            catch(Exception e){Finish("FAIL setup: "+e);}
            return;
        }
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||!SessionState.GetBool(Key+"Ready",false))return;
        try
        {
            if(started==0)started=EditorApplication.timeSinceStartup;
            Check(EditorApplication.timeSinceStartup-started<240,"Test timeout");
            var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
            if(!game||game.Grid==null||!game.Player||!game.Player.GetComponent<PlayerLimbAnimator>().IsReady)return;
            var hud=UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();
            var cinematic=game.GetComponent<NewGameCinematic>();
            if(phase==0)
            {
                game.recordResults=false;game.playOutcomeCinematics=true;
                Application.runInBackground=true;
                game.newGameIntroDuration=game.areaCinematicDuration=game.victoryCinematicDuration=game.defeatCinematicDuration=5;
                game.defeatRestartDelay=1;game.StartCampaign(7100+index,Levels[index]);
                if(index==3)
                {
                    game.StartCampaign(7103);game.playOutcomeCinematics=false;
                    for(int level=0;level<8;level++){game.CampaignClock.Advance(1);game.Finish(true);Check(game.AdvanceLevel(),"Nine-level campaign sequence failed");}
                    game.playOutcomeCinematics=true;
                }
                phase=1;return;
            }
            if(phase==1)
            {
                if(index==0){game.PlayNewGameIntro();expected=MazeStoryBeat.NewGame;}
                else if(index<=3){game.Finish(true);expected=index==3?MazeStoryBeat.CampaignComplete:MazeStoryBeat.AreaComplete;}
                else
                {
                    bool boss=index>=7;
                    game.HitPlayer(3,boss?"EL EMPLEO TE ATRAPO":"TE ALCANZO UN FORMULARIO DE RR. HH.",boss);
                    expected=boss?MazeStoryBeat.BossDefeat:MazeStoryBeat.HealthDefeat;
                }
                cinematic=game.GetComponent<NewGameCinematic>();
                cinematic.PreviewWhileUnfocused=true;
                if(index==3)
                {
                    // Create the pending name form without writing any ranking/checkpoint data.
                    game.recordResults=true;
                    typeof(MazeBetaGame).GetMethod("ConsiderCampaignRecord",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                        .Invoke(game,new object[]{"MazeStory.Verification.ReadOnly"});
                    game.recordResults=false;Check(game.HasPendingCampaignRecord,"Finisher name registration was lost");
                }
                Check(cinematic&&cinematic.IsActive&&cinematic.Kind==expected,"Wrong story beat at level "+Levels[index]);
                Check(!game.AdvanceLevel(),"Level advanced while cinematic active");
                originalSeed=game.ActiveSeed;clock=game.CampaignSeconds;retry=game.RetrySecondsRemaining;phase=2;return;
            }
            if(phase==2)
            {
                Check(!game.IsPlaying&&!game.Player.CanMove&&!game.Boss.IsActive,"Actor AI/input not locked");
                Check(Mathf.Abs(game.CampaignSeconds-clock)<.0001f,"Story counted in ranking time");
                Check(Mathf.Abs(game.RetrySecondsRemaining-retry)<.0001f,"Retry elapsed during defeat cinematic");
                hud.Refresh();Check(!hud.resultPanel.activeSelf,"Result/name form visible over cinematic");
                if(cinematic.ElapsedSeconds<2.1f)return;
                Capture("Logs/story-"+index+"-"+expected+".png");
                // Also exercise the actual skip-button listener, with a natural ending for the first area.
                if(index!=1)hud.GetComponentsInChildren<Button>().First(b=>b.name=="Saltar cinematica").onClick.Invoke();
                phase=3;return;
            }
            if(phase==3)
            {
                if(game.IsCinematicPlaying)return;
                hud.Refresh();Check(Camera.main.GetComponent<PlayerFollowCamera>().enabled,"Follow camera not restored");
                Check(!game.Player.GetComponent<PlayerLimbAnimator>().CinematicIdle,"Cinematic actor flag leaked");
                Check(!game.GetComponentsInChildren<Transform>().Any(t=>t.name=="Solicitud - utileria cinematica"&&t.gameObject.activeInHierarchy),"Stage prop leaked");
                if(index==0)Check(game.IsPlaying,"Intro did not unlock controls");
                else Check(hud.resultPanel.activeSelf,"Result not restored after ending");
                if(index==3)Check(game.CampaignComplete&&game.CampaignClock.Finished,"Final did not complete nine sequential levels");
                if(index>=4){phase=4;return;}
                Next();return;
            }
            if(phase==4)
            {
                if(game.WaitingForRetry)return;
                Check(game.IsPlaying&&game.ActiveSeed==originalSeed&&game.LossesOnCurrentMaze==1,"First defeat did not retry same maze");
                game.HitPlayer(3,"SECOND HEALTH LOSS");Check(game.IsCinematicPlaying&&game.RegenerateOnRetry,"Second defeat lost regeneration flag");
                game.SkipCinematic();phase=5;return;
            }
            if(phase==5)
            {
                if(game.WaitingForRetry||game.IsCinematicPlaying)return;
                Check(game.IsPlaying&&game.ActiveSeed!=originalSeed,"Two defeats did not generate new maze after cinematic");Next();return;
            }
            if(phase==6)
            {
                VerifyTurret(game);
                Finish("PASS: revised intro; both area endings; nine-level campaign ending; health/boss defeats across all three areas; natural ending and skip; clocks/AI/input frozen; results deferred; automatic same-maze retry and regeneration after two losses; office turret/projectile assets. Existing checkpoint/ranking untouched.");
            }
        }
        catch(Exception e){Finish("FAIL case "+index+" phase "+phase+": "+e);}
    }
    static void Next(){index++;phase=index<Levels.Length?0:6;}
    static void VerifyTurret(MazeBetaGame game)
    {
        var turretPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/TurretEnemy.prefab");var turret=turretPrefab.GetComponent<TurretEnemy>();
        Check(turret.barrelRoot&&turret.muzzleRoot&&turret.housingRoot&&turret.headVisualRoot,"Printer prefab references missing");
        Check(turretPrefab.GetComponentsInChildren<Renderer>().Length>25,"Printer geometry missing");
        var projectile=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/EnemyProjectile.prefab");
        Check(projectile.GetComponentsInChildren<Renderer>().Length>=10,"Paper projectile is empty");
        var instance=UnityEngine.Object.Instantiate(turretPrefab,new Vector3(-20,0,-20),Quaternion.identity);instance.GetComponent<TurretEnemy>().Initialize(game);
        CaptureTurret(instance);
        VerifyPrinterFire(game,instance.GetComponent<TurretEnemy>());
        UnityEngine.Object.Destroy(instance);
    }
    static void VerifyPrinterFire(MazeBetaGame game,TurretEnemy turret)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var controller=game.Player.GetComponent<CharacterController>();controller.enabled=false;
        game.Player.transform.position=turret.transform.position+new Vector3(0,.1f,4);controller.enabled=true;game.Player.CanMove=false;
        Physics.SyncTransforms();
        var update=typeof(TurretEnemy).GetMethod("Update",flags);var cooldown=typeof(TurretEnemy).GetField("cooldown",flags);
        cooldown.SetValue(turret,0f);update.Invoke(turret,null);
        var shots=UnityEngine.Object.FindObjectsByType<EnemyProjectile>();Check(shots.Length==1,"Printer failed to launch a paper at the visible player");
        Check(Vector3.Distance(shots[0].transform.position,turret.muzzleRoot.position)<.001f,"Paper did not leave the tray muzzle");
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary LOS verification wall";
        wall.transform.position=turret.transform.position+new Vector3(0,1,2);wall.transform.localScale=new Vector3(3,2,.25f);Physics.SyncTransforms();
        cooldown.SetValue(turret,0f);update.Invoke(turret,null);
        Check(UnityEngine.Object.FindObjectsByType<EnemyProjectile>().Length==1,"Printer fired through a wall");
        int health=game.Progress.Health;
        typeof(EnemyProjectile).GetField("velocity",flags).SetValue(shots[0],turret.barrelRoot.forward*10000);
        typeof(EnemyProjectile).GetMethod("Update",flags).Invoke(shots[0],null);
        Check(game.Progress.Health==health,"Swept paper projectile passed through a wall");
        wall.SetActive(false);UnityEngine.Object.Destroy(wall);Physics.SyncTransforms();
        game.FireProjectile(turret.muzzleRoot.position,turret.barrelRoot.forward,10000,1,turret.transform);
        var direct=UnityEngine.Object.FindObjectsByType<EnemyProjectile>().First(p=>p!=shots[0]);
        typeof(EnemyProjectile).GetMethod("Update",flags).Invoke(direct,null);
        Check(game.Progress.Health==health-1,"Unobstructed paper projectile failed to damage player");
    }
    static void CaptureTurret(GameObject instance)
    {
        var camera=Camera.main;var savedPosition=camera.transform.position;var savedRotation=camera.transform.rotation;
        var target=instance.transform.position+Vector3.up*.62f;camera.transform.position=target+new Vector3(1.35f,.8f,2);
        camera.transform.LookAt(target);var hud=UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();hud.gameObject.SetActive(false);
        Capture("Logs/office-printer-turret.png");hud.gameObject.SetActive(true);camera.transform.SetPositionAndRotation(savedPosition,savedRotation);
    }
    static void Finish(string result)
    {
        SessionState.SetBool(Key,false);File.WriteAllText("Logs/story-campaign-verification-result.txt",result);Debug.Log(result);
        if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
    }
    static void Capture(string path)
    {
        var camera=Camera.main;var hud=UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>(FindObjectsInactive.Include);var canvas=hud.GetComponent<Canvas>();
        var mode=canvas.renderMode;var originalCamera=canvas.worldCamera;float plane=canvas.planeDistance;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.15f;
        var target=new RenderTexture(1600,1000,24);target.Create();camera.targetTexture=target;
        var previous=RenderTexture.active;bool asyncShaders=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
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
        }
    }
}
