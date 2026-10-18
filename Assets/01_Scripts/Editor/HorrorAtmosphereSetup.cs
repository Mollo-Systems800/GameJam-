using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HorrorAtmosphereSetup
{
    const string Request="Logs/setup-horror-atmosphere.request";
    static HorrorAtmosphereSetup(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)return;
        try{File.Delete(Request);}catch(IOException){return;}
        try{Apply();File.WriteAllText("Logs/horror-atmosphere-setup-result.txt","PASS: nocturnal BetaMaze saved; lamp, exit and guide-light prefabs assigned; brisk Foley and recomposed calm/industrial boss audio imported.");}
        catch(Exception error){File.WriteAllText("Logs/horror-atmosphere-setup-result.txt",error.ToString());Debug.LogException(error);}
    }
    [MenuItem("Tools/Maze Beta/Configure horror atmosphere")]
    public static void Apply()
    {
        string original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        try
        {
            OfficeAudioSetup.Apply();
            var root=PrefabUtility.LoadPrefabContents("Assets/Resources/MazeAudio.prefab");
            try{var audio=root.GetComponent<MazeAudioDirector>();audio.effectsPlaybackSpeed=1.12f;audio.musicFadeSeconds=1.05f;
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/MazeAudio.prefab");}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var housing=Mat("NightLampHousing",new Color(.055f,.09f,.09f));
            var metal=Mat("NightLampSteel",new Color(.20f,.23f,.23f));
            var warm=Mat("NightLampFluorescent",new Color(.95f,.75f,.40f),new Color(1,.62f,.22f)*3);
            var green=Mat("NightExitGreen",new Color(.24f,.72f,.48f),new Color(.08f,.8f,.3f)*2);
            var office=LampPrefab("OfficeNightLamp",housing,metal,warm,false);
            var beacon=LampPrefab("OfficeExitBeacon",housing,metal,green,true);
            var guide=new GameObject("PlayerGuideLight");var light=guide.AddComponent<Light>();
            light.type=LightType.Spot;light.color=new Color(.75f,.83f,1);light.intensity=2.2f;light.range=8;
            light.spotAngle=68;light.innerSpotAngle=28;light.shadows=LightShadows.Soft;light.shadowBias=.04f;light.shadowNormalBias=.18f;
            var guidePrefab=PrefabUtility.SaveAsPrefabAsset(guide,"Assets/03_Prefabs/PlayerGuideLight.prefab");UnityEngine.Object.DestroyImmediate(guide);
            var scene=EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
            var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
            game.horrorLighting=true;game.officeLampPrefab=office;game.exitBeaconPrefab=beacon;game.playerGuideLightPrefab=guidePrefab;
            game.ApplyNightEnvironment();EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally{if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);}
    }
    static GameObject LampPrefab(string name,Material housing,Material steel,Material face,bool emergency)
    {
        string path="Assets/03_Prefabs/"+name+".prefab";
        var root=new GameObject(name);
        try
        {
            Box(root.transform,"Base",new Vector3(0,.025f,0),new Vector3(.18f,.05f,.18f),housing);
            Box(root.transform,"Poste",new Vector3(0,1.29f,0),new Vector3(.045f,2.56f,.045f),steel);
            Box(root.transform,"Soporte",new Vector3(.13f,2.58f,0),new Vector3(.30f,.04f,.045f),steel);
            Box(root.transform,"Carcasa",new Vector3(.25f,2.56f,0),new Vector3(.58f,.09f,.22f),housing);
            Box(root.transform,"Difusor",new Vector3(.25f,2.507f,0),new Vector3(.50f,.015f,.16f),face);
            for(int i=0;i<5;i++)Box(root.transform,"Rejilla",new Vector3(.05f+i*.1f,2.492f,0),new Vector3(.012f,.012f,.19f),housing);
            var source=new GameObject("Haz de luz").AddComponent<Light>();source.transform.SetParent(root.transform,false);
            source.transform.localPosition=new Vector3(.25f,2.47f,0);source.transform.localRotation=Quaternion.Euler(90,0,0);
            source.type=LightType.Spot;source.range=6.3f;source.spotAngle=112;source.innerSpotAngle=65;source.intensity=9;
            source.color=emergency?new Color(.32f,.95f,.62f):new Color(1,.78f,.48f);
            source.shadows=LightShadows.Soft;source.shadowStrength=.9f;source.shadowBias=.04f;source.shadowNormalBias=.18f;
            root.AddComponent<HorrorLamp>().intensityVariation=emergency?0:.09f;
            return PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static Material Mat(string name,Color color,Color emission=default)
    {
        string path="Assets/04_Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.16f);
        if(emission.maxColorComponent>0){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",emission);}
        EditorUtility.SetDirty(material);return material;
    }
    static void Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(parent,false);
        part.transform.localPosition=position;part.transform.localScale=size;part.GetComponent<Renderer>().sharedMaterial=material;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
    }
}
