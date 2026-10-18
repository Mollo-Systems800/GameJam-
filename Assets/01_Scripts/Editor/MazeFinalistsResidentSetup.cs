using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MazeFinalistsResidentSetup
{
    const string ResidentPath = "Assets/03_Prefabs/AndersonSeatedWithChair.prefab";
    [MenuItem("GameJam/Actualizar abuela con silla y ranking")]
    public static void Apply()
    {
        var root = new GameObject("Anderson sentada con silla");
        try
        {
            var chair = new GameObject("Silla Inter").transform; chair.SetParent(root.transform, false);
            MazeBetaGame.FitVisual(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/InterChair.prefab"), chair, new Vector3(.72f,.82f,.72f), true);
            chair.localRotation = Quaternion.Euler(0,180,0);
            var woman = new GameObject("Abuela Anderson sentada").transform; woman.SetParent(root.transform, false);
            MazeBetaGame.FitVisual(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ImportedProps/Anderson/AndersonSeated.obj"), woman, new Vector3(.78f,1.34f,.9f), true);
            PrefabUtility.SaveAsPrefabAsset(woman.gameObject, "Assets/03_Prefabs/SeatedWoman.prefab");
            woman.localPosition = new Vector3(0,0,.25f);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                if (renderer.sharedMaterials.Any(m => !m || !m.mainTexture && m.name.StartsWith("Anderson")))
                    throw new Exception("Missing supplied resident texture: " + renderer.name);
            // The supplied GLB declares double-sided surfaces (including glasses).
            foreach (var material in root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct())
                if (material && material.name.StartsWith("Anderson"))
                { material.SetFloat("_Cull",0); material.doubleSidedGI=true; EditorUtility.SetDirty(material); }
            PrefabUtility.SaveAsPrefabAsset(root, ResidentPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
        var game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
        game.seatedResidentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResidentPath);
        foreach (var area in game.areas) area.seatedResidentPrefab = null;
        EditorUtility.SetDirty(game); EditorSceneManager.SaveOpenScenes();
        EditorSceneManager.OpenScene("Assets/00_Scenes/Inicio.unity");
        var menu = UnityEngine.Object.FindAnyObjectByType<StartupMenu>();
        StartupDossierSetup.AddRanking(menu);
        menu.RefreshMenuState();
        EditorUtility.SetDirty(menu); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
    }
    public static void VerifyAndRun()
    {
        Apply();
        PlayerSkinVerification.Run();
        RenderResidentsAndPlayer();
        MazePresentationPreview.Render();
        MazePresentationVerification.RunPlay();
    }
    static void RenderResidentsAndPlayer()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.65f,.65f,.65f);
        var camera = new GameObject("Resident and player review",typeof(Camera)).GetComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.23f,.29f,.30f);
        camera.orthographic=true;camera.orthographicSize=1.02f;camera.nearClipPlane=.03f;camera.farClipPlane=30;
        var light=new GameObject("Softbox",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;
        light.intensity=2;light.transform.rotation=Quaternion.Euler(35,-25,0);
        var resident=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ResidentPath));
        camera.transform.position=new Vector3(2,1.3f,3);camera.transform.LookAt(new Vector3(0,.66f,.15f));
        Capture(camera,"Logs/resident-chair-front.png");
        camera.transform.position=new Vector3(3,1,0);camera.transform.LookAt(new Vector3(0,.65f,.15f));
        Capture(camera,"Logs/resident-chair-profile.png");
        UnityEngine.Object.DestroyImmediate(resident);
        var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Player.prefab"));
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(Player).GetMethod("Awake",flags).Invoke(instance.GetComponent<Player>(),null);
        typeof(PlayerLimbAnimator).GetMethod("Start",flags).Invoke(instance.GetComponent<PlayerLimbAnimator>(),null);
        camera.orthographicSize=.55f;
        camera.transform.position=new Vector3(.1f,1.33f,3);camera.transform.LookAt(new Vector3(0,1.33f,0));
        Capture(camera,"Logs/player-eyebrows-front.png");
        camera.transform.position=new Vector3(.6f,1.6f,-3);camera.transform.LookAt(new Vector3(0,1.33f,0));
        Capture(camera,"Logs/player-eyebrows-back.png");
    }
    static void Capture(Camera camera,string path)
    {
        var target=new RenderTexture(1200,1000,24);target.Create();camera.targetTexture=target;
        var previous=RenderTexture.active;
        camera.Render();camera.Render();RenderTexture.active=target;
        var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
        File.WriteAllBytes(path,pixels.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;
        UnityEngine.Object.DestroyImmediate(pixels);target.Release();UnityEngine.Object.DestroyImmediate(target);
    }
}
