using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MazeBetaSetup
{
    public const string ScenePath = "Assets/00_Scenes/BetaMaze.unity";

    [MenuItem("GameJam/Crear o abrir beta jugable")]
    public static void Configure()
    {
        Scene scene = System.IO.File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath)
            : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var game = Object.FindAnyObjectByType<MazeBetaGame>();
        if (!game) game = new GameObject("Beta Game - Configuracion").AddComponent<MazeBetaGame>();
        game.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Player.prefab");
        game.wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Maze/Prefabs/Wall_3M.prefab");
        game.floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Maze/Prefabs/Floor_3M.prefab");
        game.bossVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/SolicitudTrabajo.fbx");
        game.platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/JumpPlatform.prefab");
        game.spadeEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/SpadeEnemy.prefab");
        game.turretEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/TurretEnemy.prefab");
        game.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/EnemyProjectile.prefab");
        game.coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/CoinPickup.prefab");
        if (!game.playerPrefab || !game.wallPrefab || !game.floorPrefab)
            throw new System.InvalidOperationException("Faltan los recursos del personaje o del laberinto.");
        Camera.main.transform.position = game.cameraOffset;
        Camera.main.transform.LookAt(Vector3.zero);
        RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.65f);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("BETA_SETUP_OK: " + ScenePath);
    }
}
