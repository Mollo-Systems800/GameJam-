using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class MazeResidentsVerification
{
    const string Pending="GameJam.ResidentsVerification";
    static MazeBetaGame game;
    static Keyboard keyboard;
    static double since,started;
    static int phase,cell;
    static Vector3 axis,start;
    static Key move;
    [MenuItem("GameJam/Girar vista de senora sentada")]
    public static void ReversePreview()
    {
        var preview=GameObject.Find("Seated woman visual verification");
        if(!preview||!EditorApplication.isPlaying)return;
        var camera=Camera.main;
        Vector3 target=preview.transform.position+Vector3.up*.65f;
        Vector3 offset=camera.transform.position-target;
        camera.transform.position=target+new Vector3(-offset.x,offset.y,-offset.z);
        camera.transform.LookAt(target);
        EditorApplication.Step();
    }
    static MazeResidentsVerification(){EditorApplication.update+=Tick;}
    [MenuItem("GameJam/Verificar senoras y piso")]
    public static void Run()
    {
        phase=0;since=started=0;
        SessionState.SetBool(Pending,true);
        EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Next(){phase++;since=EditorApplication.timeSinceStartup;}
    static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            double now=EditorApplication.timeSinceStartup;
            if(started==0)started=since=now;
            Check(now-started<50,"Residents verification timed out");
            if(phase==0)
            {
                game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
                if(!game||!game.Player)return;
                game.recordResults=false;game.bossGracePeriod=10000;
                game.spadeCount=game.turretCount=0;
                game.StartCampaign(7921);
                keyboard=InputSystem.AddDevice<Keyboard>();Next();
            }
            else if(phase==1&&now-since>.6)
            {
                // Exercise every campaign level and theme, without serializing test changes.
                for(int level=1;level<=9;level++)
                {
                    game.StartCampaign(7921,level);
                    Check(game.ResidentCells.Count>=3&&game.ResidentZones.Count>=3,"Less than three women at level "+level);
                    Check(!game.transform.Find("Anderson - Tienda"),"Woman is still the shopkeeper");
                    var floor=game.ActiveArea.floorPrefab?game.ActiveArea.floorPrefab:game.floorPrefab;
                    Check(floor.name.Contains("SmoothFloor"),"Relief floor still assigned at level "+level);
                    foreach(var renderer in floor.GetComponentsInChildren<Renderer>())
                        foreach(var material in renderer.sharedMaterials)
                            Check(material&&material.shader.isSupported&&!ShaderUtil.ShaderHasError(material.shader),"Floor shader failed");
                }
                game.StartCampaign(7921);Next();
            }
            else if(phase==2&&now-since>.6)
            {
                cell=game.ResidentCells[0];
                foreach(int candidate in game.ResidentCells)
                    if((game.Grid.IsOpen(candidate,0)&&game.Grid.IsOpen(candidate,2))||
                       (game.Grid.IsOpen(candidate,1)&&game.Grid.IsOpen(candidate,3))){cell=candidate;break;}
                bool vertical=game.Grid.IsOpen(cell,0)&&game.Grid.IsOpen(cell,2);
                axis=vertical?Vector3.forward:Vector3.right;move=vertical?Key.W:Key.D;
                var controller=game.Player.GetComponent<CharacterController>();controller.enabled=false;
                start=game.CellPosition(cell)-axis*(game.cellSize*.5f+.7f)+Vector3.up*.05f;
                game.Player.transform.position=start;controller.enabled=true;game.Player.view=null;
                Physics.SyncTransforms();InputSystem.QueueStateEvent(keyboard,new KeyboardState(move));Next();
            }
            else if(phase==3&&now-since>.65)
            {
                Check(!game.RequiresRollNear(game.Player.transform.position,0),"Walking entered resident zone");
                Check(Vector3.Dot(game.Player.transform.position-start,axis)<1,"Walking crossed the gate");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(move,Key.LeftShift));Next();
            }
            else if(phase==4&&now-since>.4)
            {
                Check(!game.Player.IsSprinting&&game.Player.PlanarSpeed<.1f,"Sprint bypassed the woman");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(move,Key.Space));Next();
            }
            else if(phase==5&&now-since>.3)
            {
                Check(game.Player.IsGrounded&&!game.RequiresRollNear(game.Player.transform.position,0),"Jump bypassed the woman");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(move,Key.C));Next();
            }
            else if(phase==6&&now-since>.35)
            {
                Check(game.Player.IsRolling&&game.RequiresRollNear(game.Player.transform.position,0),"Roll did not enter the passage");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());Next();
            }
            else if(phase==7&&now-since>1.2)
            {
                Check(!game.Player.IsRolling&&Vector3.Dot(game.Player.transform.position-game.CellPosition(cell),axis)>game.cellSize*.5f,
                    "Roll did not exit the tested passage");
                game.SetShopOpen(true);Check(game.ShopOpen&&!game.Player.CanMove,"Terminal shop did not pause play");
                game.SetShopOpen(false);Check(game.Player.CanMove,"Shop did not release player");
                Finish("PASS: 9 levels each have >=3 seated women and flat floors; shader supported; walk, sprint and jump blocked; C roll crosses the passage; independent shop still pauses/resumes play.");
            }
        }
        catch(Exception error){Finish("FAIL phase "+phase+": "+error);}
    }
    static void Finish(string result)
    {
        if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}
        SessionState.SetBool(Pending,false);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/residents-playmode-result.txt",result);
        if(game&&game.ResidentZones.Count>0)
        {
            var source=game.ResidentZones[0].transform.parent.gameObject;
            var preview=UnityEngine.Object.Instantiate(source,new Vector3(100,0,100),Quaternion.identity);
            preview.name="Seated woman visual verification";
            foreach(var collider in preview.GetComponentsInChildren<Collider>())collider.enabled=false;
            foreach(var label in preview.GetComponentsInChildren<TextMesh>())label.gameObject.SetActive(false);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position=new Vector3(100,-.1f,100);floor.transform.localScale=new Vector3(8,.2f,8);
            floor.GetComponent<Renderer>().sharedMaterial=game.floorPrefab.GetComponentInChildren<Renderer>().sharedMaterial;
            game.enabled=false;
            var camera=Camera.main;camera.GetComponent<PlayerFollowCamera>().enabled=false;
            camera.transform.position=new Vector3(101.8f,1.2f,102.6f);camera.transform.LookAt(new Vector3(100,.65f,100));
            camera.fieldOfView=45;
            var diagnostic=new System.Text.StringBuilder();
            foreach(var filter in preview.GetComponentsInChildren<MeshFilter>())
                diagnostic.AppendLine(filter.name+" bounds="+filter.sharedMesh.bounds+" local="+filter.transform.localPosition+" world="+filter.transform.position);
            File.WriteAllText("Logs/seated-geometry-result.txt",diagnostic.ToString());
        }
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        Debug.Log("RESIDENTS_VERIFICATION: "+result);EditorApplication.isPaused=true;
    }
}
