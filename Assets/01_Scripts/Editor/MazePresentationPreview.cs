using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Native Unity renders for visual review, kept separate from the saved scenes.
public static class MazePresentationPreview
{
    public static void RenderAndRun()
    {
        Render(); MazePresentationVerification.RunPlay();
    }
    public static void Render()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        Directory.CreateDirectory("Logs");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.65f,.65f,.65f);
        var camera = new GameObject("Presentation review camera",typeof(Camera)).GetComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.16f,.20f,.20f);
        camera.transform.position=new Vector3(-3,2.2f,5.5f); camera.transform.LookAt(new Vector3(0,.95f,0));
        camera.orthographic=true;camera.orthographicSize=1.6f;camera.nearClipPlane=.03f;camera.farClipPlane=30;
        var light = new GameObject("Review key light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;
        light.intensity=2;light.transform.rotation=Quaternion.Euler(40,-40,0);light.shadows=LightShadows.Soft;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.position=new Vector3(0,-.06f,0);ground.transform.localScale=new Vector3(10,.1f,10);
        var groundMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));groundMaterial.SetColor("_BaseColor",new Color(.42f,.47f,.43f));ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
        var boss=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/EmploymentApplicationBoss.prefab"));
        boss.transform.position=new Vector3(-1.35f,0,0);Vector3 facing=camera.transform.position-boss.transform.position;facing.y=0;boss.transform.rotation=Quaternion.LookRotation(facing);
        var pills=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/MedicineBox.prefab"));pills.transform.position=new Vector3(.15f,.5f,0);
        var spade=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/SpadeEnemy.prefab"));spade.transform.position=new Vector3(1.35f,0,0);
        typeof(SpadeEnemy).GetMethod("NormalizeShovel",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(spade.GetComponent<SpadeEnemy>(),null);
        Capture(camera,"Logs/printed-enemies-preview.png",1600,1000);
        EditorSceneManager.OpenScene("Assets/00_Scenes/Inicio.unity");
        var menu=Object.FindAnyObjectByType<StartupMenu>();
        var uiCamera=new GameObject("UI review camera",typeof(Camera)).GetComponent<Camera>();
        uiCamera.clearFlags=CameraClearFlags.SolidColor;uiCamera.backgroundColor=Color.black;uiCamera.orthographic=true;
        uiCamera.transform.position=new Vector3(0,0,-10);uiCamera.nearClipPlane=.01f;uiCamera.farClipPlane=20;
        menu.menuCanvas.renderMode=RenderMode.ScreenSpaceCamera;menu.menuCanvas.worldCamera=uiCamera;menu.menuCanvas.planeDistance=5;
        menu.panelGroup.alpha=1;
        CaptureMenu(menu,uiCamera,"Logs/start-ranking-option-preview.png");
        menu.ShowRanking();
        menu.rankingNames.text="01   Lucía\n02   José\n03   Santi";menu.rankingTimes.text="07:15.32\n08:42.10\n09:03.58";
        menu.rankingStatus.text="Solo campañas completas. Menor tiempo, mejor posición.\nLos tiempos incluyen los intentos fallidos.";
        CaptureMenu(menu,uiCamera,"Logs/ranking-layout-preview.png");
        EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
        var game=Object.FindAnyObjectByType<MazeBetaGame>();game.CampaignClock.Advance(435.32f);
        for(int level=0;level<9;level++)game.CampaignClock.CompleteLevel(level);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(MazeBetaGame).GetField("campaign",flags).SetValue(game,new MazeCampaignProgress(8));
        typeof(MazeBetaGame).GetProperty("HasWon").SetValue(game,true);
        typeof(MazeBetaGame).GetField("pendingRecord",flags).SetValue(game,435.32f);
        var hud=Object.FindAnyObjectByType<MazeCanvasHUD>();hud.Refresh();hud.GetComponent<CampaignRecordForm>().form.SetActive(true);
        var resultCamera=new GameObject("Record form review camera",typeof(Camera)).GetComponent<Camera>();
        resultCamera.clearFlags=CameraClearFlags.SolidColor;resultCamera.backgroundColor=new Color(.30f,.38f,.35f);
        resultCamera.transform.position=new Vector3(0,0,-10);resultCamera.nearClipPlane=.01f;resultCamera.farClipPlane=20;resultCamera.orthographic=true;
        var canvas=hud.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=resultCamera;canvas.planeDistance=5;
        var resultTexture=new RenderTexture(1600,1000,24);resultTexture.Create();resultCamera.targetTexture=resultTexture;
        Canvas.ForceUpdateCanvases();Read(resultCamera,resultTexture,"Logs/campaign-record-form-preview.png");resultCamera.targetTexture=null;resultTexture.Release();Object.DestroyImmediate(resultTexture);
    }
    static void CaptureMenu(StartupMenu menu,Camera camera,string path)
    {
        var texture=new RenderTexture(1600,1000,24);texture.Create();camera.targetTexture=texture;
        Canvas.ForceUpdateCanvases();menu.FitPanel();Canvas.ForceUpdateCanvases();
        foreach(var portrait in Object.FindObjectsByType<Camera>()) if(portrait.name=="Dossier photo camera") portrait.Render();
        Read(camera,texture,path);camera.targetTexture=null;texture.Release();Object.DestroyImmediate(texture);
    }
    static void Capture(Camera camera,string path,int width,int height)
    {
        var texture=new RenderTexture(width,height,24);texture.Create();camera.targetTexture=texture;
        Read(camera,texture,path);camera.targetTexture=null;texture.Release();Object.DestroyImmediate(texture);
    }
    static void Read(Camera camera,RenderTexture texture,string path)
    {
        camera.Render();camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;
        var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
        RenderTexture.active=previous;Object.DestroyImmediate(pixels);
    }
}
