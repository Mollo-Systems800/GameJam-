using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MazeVisualRepairSetup
{
    static MazeVisualRepairSetup() { EditorApplication.update += Requested; }
    static void Requested()
    {
        const string request = "Logs/repair-maze-visuals.request";
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) return;
        File.Delete(request);
        try { Apply(); File.WriteAllText("Logs/visual-repair-setup-result.txt", "PASS: courtyard wall prefab/material assigned, HUD transparent, source mesh audit exported."); }
        catch (Exception error) { File.WriteAllText("Logs/visual-repair-setup-result.txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("GameJam/Reparar paredes Patio y fondo HUD")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) return;
        var scene = EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
        var game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
        const string materialPath = "Assets/04_Materials/CourtyardWall.mat";
        const string prefabPath = "Assets/03_Prefabs/CourtyardWall.prefab";
        var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Maze/Prefabs/Wall_3M.prefab");
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            material = new Material(original.GetComponentInChildren<Renderer>().sharedMaterial);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.shader = Shader.Find("Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor", new Color(.50f, .56f, .48f));
        material.SetTexture("_EmissionMap", null);
        material.SetColor("_EmissionColor", Color.black); material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .18f);
        EditorUtility.SetDirty(material);
        var wall = (GameObject)PrefabUtility.InstantiatePrefab(original);
        try
        {
            wall.name = "CourtyardWall";
            foreach (var renderer in wall.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            PrefabUtility.SaveAsPrefabAsset(wall, prefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(wall); }
        game.areas[2].wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var hud = UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();
        var bar = hud.transform.Find("Stats/Maze HUD/Vitals panel").GetComponent<Image>();
        var color = bar.color; color.a = 0; bar.color = color; bar.raycastTarget = false;
        foreach (var label in bar.GetComponentsInChildren<Text>(true))
        {
            var shadow = label.GetComponent<Shadow>(); if (!shadow) shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .85f); shadow.effectDistance = new Vector2(1, -1);
        }
        DumpSource(game.playerPrefab);
        EditorUtility.SetDirty(game); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
    [Serializable] sealed class Source { public Bounds bounds; public Part[] parts; }
    [Serializable] sealed class Part { public string name; public Vector3[] points; public int[] triangles; }
    static void DumpSource(GameObject prefab)
    {
        Transform visual = prefab.GetComponent<Player>().visual;
        var parts = new List<Part>(); Bounds bounds = default; bool first = true;
        foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
        {
            if (!filter.sharedMesh || !filter.sharedMesh.isReadable || PlayerLimbAnimator.IsControl(filter.name)) continue;
            var points = filter.sharedMesh.vertices.Select(v => visual.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
            foreach (var point in points) { if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point); }
            parts.Add(new Part { name = filter.name, points = points, triangles = filter.sharedMesh.triangles });
        }
        File.WriteAllText("Logs/player-source-mesh-audit.json", JsonUtility.ToJson(new Source { bounds = bounds, parts = parts.ToArray() }));
    }
}
