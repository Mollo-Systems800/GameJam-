using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class OfficeTurretSetup
{
    const string Request="Logs/setup-office-turrets.request";
    static OfficeTurretSetup(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.isDirty)return;
        try{File.Delete(Request);}catch(IOException){return;}
        string original=scene.path;
        try{AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);Apply();File.WriteAllText("Logs/office-turret-setup-result.txt","PASS: HR printer turret and paper projectile prefabs saved and assigned in BetaMaze.");}
        catch(Exception error){File.WriteAllText("Logs/office-turret-setup-result.txt",error.ToString());Debug.LogException(error);}
        finally{if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);}
    }
    [MenuItem("Tools/Maze Beta/Build office printer turrets")]
    public static void Apply()
    {
        var cream=Mat("OfficePrinterCream",new Color(.77f,.75f,.65f));
        var teal=Mat("OfficePrinterTeal",new Color(.075f,.23f,.22f));
        var black=Mat("OfficePrinterInk",new Color(.035f,.055f,.05f));
        var red=Mat("OfficeRejectedRed",new Color(.65f,.105f,.055f));
        var paper=Mat("OfficeFormPaper",new Color(.96f,.93f,.82f));
        var root=new GameObject("TurretEnemy - Impresora de RRHH");
        try
        {
            var turret=root.AddComponent<TurretEnemy>();
            var housing=new GameObject("Archivador y ruedas").transform;housing.SetParent(root.transform,false);turret.housingRoot=housing;
            Box(housing,"Cuerpo del archivador",new Vector3(0,.39f,0),new Vector3(.72f,.64f,.6f),cream);
            Box(housing,"Zocalo",new Vector3(0,.11f,0),new Vector3(.76f,.12f,.64f),teal);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Box(housing,"Rueda",new Vector3(x*.26f,.055f,z*.2f),new Vector3(.09f,.1f,.1f),black);
            for(int i=0;i<2;i++)
            {
                Box(housing,"Frente del cajon",new Vector3(0,.28f+i*.27f,.309f),new Vector3(.64f,.23f,.018f),teal);
                Box(housing,"Tirador",new Vector3(0,.32f+i*.27f,.34f),new Vector3(.22f,.035f,.04f),cream);
                Box(housing,"Etiqueta papel",new Vector3(0,.23f+i*.27f,.323f),new Vector3(.25f,.065f,.006f),paper);
            }
            var barrel=new GameObject("Cabezal giratorio").transform;barrel.SetParent(root.transform,false);barrel.localPosition=Vector3.up*.96f;turret.barrelRoot=barrel;
            Box(root.transform,"Soporte giratorio",new Vector3(0,.78f,0),new Vector3(.32f,.16f,.32f),black);
            var head=new GameObject("Impresora y bandeja").transform;head.SetParent(barrel,false);turret.headVisualRoot=head;
            Box(head,"Carcasa de impresora",new Vector3(0,0,.02f),new Vector3(.8f,.29f,.63f),cream);
            Box(head,"Tapa del escaner",new Vector3(0,.17f,-.02f),new Vector3(.82f,.065f,.63f),teal);
            Box(head,"Ranura de salida",new Vector3(0,-.035f,.34f),new Vector3(.61f,.095f,.025f),black);
            Box(head,"Bandeja de salida",new Vector3(0,-.11f,.47f),new Vector3(.63f,.025f,.35f),teal);
            Box(head,"Formulario saliendo",new Vector3(0,-.087f,.46f),new Vector3(.36f,.008f,.25f),paper);
            for(int i=0;i<4;i++)Box(head,"Lineas del formulario",new Vector3(0,-.08f,.39f+i*.043f),new Vector3(.26f,.003f,.007f),black);
            for(int i=0;i<4;i++)Box(head,"Pila de solicitudes",new Vector3(0,.22f+i*.014f,-.03f),new Vector3(.43f,.008f,.45f),paper);
            Box(head,"Indicador de captura",new Vector3(.31f,.045f,.346f),new Vector3(.065f,.045f,.014f),red);
            var sign=new GameObject("Placa RRHH");sign.transform.SetParent(head,false);sign.transform.localPosition=new Vector3(-.25f,.065f,.344f);sign.transform.localRotation=Quaternion.Euler(0,180,0);
            var text=sign.AddComponent<TextMesh>();text.text="RR. HH.";text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=48;text.characterSize=.035f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.10f,.23f,.21f);
            sign.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
            var muzzle=new GameObject("Salida de formularios").transform;muzzle.SetParent(barrel,false);muzzle.localPosition=new Vector3(0,-.035f,.72f);turret.muzzleRoot=muzzle;
            var solid=root.AddComponent<BoxCollider>();solid.center=Vector3.up*.42f;solid.size=new Vector3(.72f,.84f,.62f);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/03_Prefabs/TurretEnemy.prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        var projectile=new GameObject("EnemyProjectile - Formulario rechazado");
        try
        {
            projectile.AddComponent<EnemyProjectile>();
            Box(projectile.transform,"Hoja",Vector3.zero,new Vector3(.23f,.31f,.012f),paper);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<5;i++)Box(projectile.transform,"Casilla de solicitud",new Vector3(0,.09f-i*.035f,side*.007f),new Vector3(.16f,.008f,.003f),teal);
                var stamp=Box(projectile.transform,"Sello rechazado",new Vector3(.025f,-.075f,side*.01f),new Vector3(.14f,.028f,.004f),red);stamp.localRotation=Quaternion.Euler(0,0,-12);
            }
            PrefabUtility.SaveAsPrefabAsset(projectile,"Assets/03_Prefabs/EnemyProjectile.prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(projectile);}
        EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
        var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
        game.turretEnemyPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/TurretEnemy.prefab");
        game.projectilePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/EnemyProjectile.prefab");
        // Explicitly save the new inspector settings into the shipped scene.
        game.playOutcomeCinematics=true;game.areaCinematicDuration=7;game.victoryCinematicDuration=11;game.defeatCinematicDuration=6;
        EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);AssetDatabase.SaveAssets();
    }
    static Material Mat(string name,Color color)
    {
        string path="Assets/04_Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.18f);EditorUtility.SetDirty(material);return material;
    }
    static Transform Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
    {
        var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(parent,false);box.transform.localPosition=position;box.transform.localScale=scale;
        box.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());return box.transform;
    }
}
