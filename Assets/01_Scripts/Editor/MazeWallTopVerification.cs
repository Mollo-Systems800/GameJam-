using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MazeWallTopVerification
{
    const string Pending="GameJam.WallTopVerification";
    const string Request="Logs/verify-wall-tops.request";
    static bool changingPlayMode;
    static MazeWallTopVerification()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=state=>
        {
            changingPlayMode=state==PlayModeStateChange.ExitingEditMode||state==PlayModeStateChange.ExitingPlayMode;
            if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Pending+"Restore",false)) return;
            SessionState.SetBool(Pending+"Restore",false);
            EditorApplication.delayCall+=()=>EditorSceneManager.OpenScene(SessionState.GetString(Pending+"Scene","Assets/00_Scenes/Inicio.unity"));
        };
    }
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||changingPlayMode)return;
        if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.isDirty)return;
            try{File.Delete(Request);}catch(IOException){return;}
            SessionState.SetString(Pending+"Scene",scene.path);
            EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
            var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
            game.recordResults=false;game.width=game.height=7;
            game.coinCount=game.pillCount=game.spadeCount=game.turretCount=game.chairCount=0;
            game.bossGracePeriod=100000;
            SessionState.SetBool(Pending,true);SessionState.SetBool(Pending+"Restore",true);
            EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying)return;
        var active=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
        if(!active||!active.Player)return;
        var report=new StringBuilder();
        try
        {
            for(int area=0;area<3;area++) for(int variant=0;variant<3;variant++)
            {
                active.cellSize=variant==1?3.7f:variant==2?2f:3f;
                active.wallThickness=variant==1?.35f:variant==2?1.1f:.75f;
                active.StartCampaign(431+variant*137,area*3+1);
                active.Player.CanMove=false;active.Boss.enabled=false;
                var nodes=active.GetComponentsInChildren<Transform>();
                var caps=nodes.Where(t=>t.name=="Wall top cap"||t.name.StartsWith("Wall top junction ")).ToArray();
                var bounds=caps.Select(t=>VisualBounds(t)).ToArray();
                Require(caps.Length>0,"Missing wall coping");
                float expectedTop=active.wallHeight+.035f;
                for(int i=0;i<bounds.Length;i++)
                {
                    Require(Mathf.Abs(bounds[i].max.y-expectedTop)<.0001f,"Coping is coplanar with wall body");
                    for(int j=0;j<i;j++)
                        Require(Mathf.Min(bounds[i].max.x,bounds[j].max.x)-Mathf.Max(bounds[i].min.x,bounds[j].min.x)<.0001f ||
                            Mathf.Min(bounds[i].max.z,bounds[j].max.z)-Mathf.Max(bounds[i].min.z,bounds[j].min.z)<.0001f,"Overlapping coping faces");
                }
                int walls=0;
                foreach(var wall in nodes.Where(t=>t.name.StartsWith("Wall ")&&t.GetComponent<BoxCollider>()&&int.TryParse(t.name.Split(' ')[1],out _)))
                {
                    walls++;
                    foreach(float x in new[]{-(active.cellSize+active.WallThickness)*.5f,-active.cellSize*.5f,0,active.cellSize*.5f,(active.cellSize+active.WallThickness)*.5f})
                    foreach(float z in new[]{-active.WallThickness*.5f,0,active.WallThickness*.5f})
                    {
                        Vector3 point=wall.TransformPoint(new Vector3(x,expectedTop,z));
                        Require(bounds.Any(b=>point.x>=b.min.x-.0001f&&point.x<=b.max.x+.0001f&&point.z>=b.min.z-.0001f&&point.z<=b.max.z+.0001f),"Exposed/coplanar wall top at a junction");
                    }
                }
                Require(walls>0,"No walls measured");
                report.AppendLine($"PASS: area {area+1}, seed {431+variant*137}, cell {active.cellSize}, thickness {active.WallThickness}: {walls} walls fully covered, {caps.Length} caps without overlap.");
                if(variant==0)Capture(active,area);
            }
            File.WriteAllText("Logs/wall-top-verification-result.txt",report.ToString());Debug.Log(report.ToString());
        }
        catch(Exception error){File.WriteAllText("Logs/wall-top-verification-result.txt",report+"FAIL: "+error);Debug.LogException(error);}
        finally{SessionState.SetBool(Pending,false);EditorApplication.isPlaying=false;}
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static Bounds VisualBounds(Transform root)
    {
        var renderers=root.GetComponentsInChildren<Renderer>();Require(renderers.Length>0,"Coping without geometry");
        Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;
    }
    static void Capture(MazeBetaGame game,int area)
    {
        var camera=Camera.main;var follow=camera.GetComponent<PlayerFollowCamera>();if(follow)follow.enabled=false;
        camera.transform.position=new Vector3(6,8,-4);camera.transform.LookAt(new Vector3(6,1.8f,6));
        var texture=new RenderTexture(1200,900,24);texture.Create();camera.targetTexture=texture;
        bool asyncShaders=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        var previous=RenderTexture.active;
        try
        {
            camera.Render();camera.Render();RenderTexture.active=texture;
            var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();
            File.WriteAllBytes($"Logs/wall-tops-area-{area+1}.png",pixels.EncodeToPNG());UnityEngine.Object.Destroy(pixels);
        }
        finally{camera.targetTexture=null;RenderTexture.active=previous;ShaderUtil.allowAsyncCompilation=asyncShaders;texture.Release();UnityEngine.Object.Destroy(texture);}
    }
}
