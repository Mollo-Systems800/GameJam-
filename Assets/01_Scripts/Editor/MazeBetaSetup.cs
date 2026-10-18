using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

public static class MazeBetaSetup
{
    public const string ScenePath = "Assets/00_Scenes/BetaMaze.unity";

    [InitializeOnLoadMethod]
    static void ScheduleSuppliedProps() => EditorApplication.delayCall += PrepareSuppliedProps;

    static void PrepareSuppliedProps()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isUpdating || EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += PrepareSuppliedProps;
            return;
        }
        PrepareProp("AndersonShopkeeper", "Assets/ImportedProps/Anderson/Anderson.obj", new Vector3(1.6f, 1.65f, 0.6f));
        PrepareProp("SeatedWoman", "Assets/ImportedProps/Anderson/AndersonSeated.obj", new Vector3(0.78f, 1.34f, 0.9f));
        PrepareProp("InterChair", "Assets/ImportedProps/Chair/InterChair.fbx", new Vector3(0.8f, 1, 0.8f));
    }

    static void PrepareProp(string name, string modelPath, Vector3 size)
    {
        string path = "Assets/03_Prefabs/" + name + ".prefab";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!model || !prefab) return;
        // Resolve the imported model's actual local IDs on the first editor import.
        // Afterwards leave the prefab editable without rebuilding it on every reload.
        if (prefab.transform.Find("Imported Visual")) return;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                Object.DestroyImmediate(child.gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Imported Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new System.InvalidOperationException("Modelo sin mallas: " + modelPath);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = Mathf.Min(size.x / Mathf.Max(0.001f, bounds.size.x),
                size.y / Mathf.Max(0.001f, bounds.size.y), size.z / Mathf.Max(0.001f, bounds.size.z));
            Vector3 offset = bounds.center - root.transform.position;
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = new Vector3(-offset.x, -(bounds.min.y - root.transform.position.y), -offset.z) * scale;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("GameJam/Crear o abrir beta jugable")]
    public static void Configure()
    {
        PrepareSuppliedProps();
        Scene scene = System.IO.File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath)
            : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var game = Object.FindAnyObjectByType<MazeBetaGame>();
        if (!game) game = new GameObject("Beta Game - Configuracion").AddComponent<MazeBetaGame>();
        if (!game.playerPrefab) game.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Player.prefab");
        if (!game.wallPrefab) game.wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Maze/Prefabs/Wall_3M.prefab");
        if (!game.floorPrefab) game.floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/OfficeSmoothFloor.prefab");
        if (!game.bossVisualPrefab || game.bossVisualPrefab.name == "SolicitudTrabajo") game.bossVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/EmploymentApplicationBoss.prefab")
            ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/SolicitudTrabajo.fbx");
        if (!game.pillVisualPrefab) game.pillVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/MedicineBox.prefab");
        if (!game.platformPrefab) game.platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/JumpPlatform.prefab");
        if (!game.spadeEnemyPrefab) game.spadeEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/SpadeEnemy.prefab");
        if (!game.turretEnemyPrefab) game.turretEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/TurretEnemy.prefab");
        if (!game.projectilePrefab) game.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/EnemyProjectile.prefab");
        if (!game.coinPrefab) game.coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/CoinPickup.prefab");
        if (!game.seatedWomanPrefab) game.seatedWomanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/SeatedWoman.prefab");
        if (!game.chairPrefab) game.chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/InterChair.prefab");
        game.exitCount = 1;
        if (game.areas == null || game.areas.Length != 3) System.Array.Resize(ref game.areas, 3);
        for (int i = 0; i < 3; i++)
            if (game.areas[i] == null) game.areas[i] = new MazeAreaTheme { areaName = "Area " + (i + 1) };
        // Area 1 uses the global prefab choices. Later areas have distinct kits,
        // and all assigned overrides remain editable in the scene inspector.
        if (!game.areas[1].wallPrefab) game.areas[1].wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Maze/Prefabs/Wall_2M.prefab");
        if (!game.areas[1].floorPrefab) game.areas[1].floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/ArchiveSmoothFloor.prefab");
        if (!game.areas[2].wallPrefab || game.areas[2].wallPrefab.name == "Wall_Lamp")
            game.areas[2].wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/CourtyardWall.prefab")
                ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Maze/Prefabs/Wall_3M.prefab");
        if (!game.areas[2].floorPrefab) game.areas[2].floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/CourtyardSmoothFloor.prefab");
        if (!game.playerPrefab || !game.wallPrefab || !game.floorPrefab)
            throw new System.InvalidOperationException("Faltan los recursos del personaje o del laberinto.");
        Camera.main.transform.rotation = Quaternion.Euler(18, 0, 0);
        Camera.main.transform.position = Vector3.up * game.cameraTargetHeight - Camera.main.transform.forward * game.cameraDistance;
        Camera.main.fieldOfView = game.cameraFieldOfView;
        var cameraData = Camera.main.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cameraData.antialiasingQuality = AntialiasingQuality.High;
        RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.65f);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("BETA_SETUP_OK: " + ScenePath);
    }
}
