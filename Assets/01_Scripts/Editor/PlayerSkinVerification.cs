using System;
using System.IO;
using System.Text;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Samples the real generated skin and the same animation path used in gameplay.
public static class PlayerSkinVerification
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
    static object Get(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);

    public static void RepairAndRun() { MazeVisualRepairSetup.Apply(); Run(); }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this verification in an isolated Unity batch project.");
        var report = new StringBuilder();
        try
        {
            VerifyScene(report);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Player.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab);
            var player = instance.GetComponent<Player>();
            var animator = instance.GetComponent<PlayerLimbAnimator>();
            Call(player, "Awake"); Call(animator, "Start");
            if (!animator.IsReady || !animator.HasRunMotion || !animator.HasJumpMotion || !animator.HasWalkMotion || !animator.HasRollMotion)
                throw new Exception("The player rig or imported animation tracks are missing.");
            var bones = (Transform[])Get(animator, "bones");
            var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (Array.Exists(skins, skin => skin.name == "Animated Cube.008") ||
                Array.Exists(instance.GetComponentsInChildren<MeshRenderer>(), r => r.name == "Cube.008" && r.enabled))
                throw new Exception("Displaced duplicate eyebrows are still visible.");
            var roll = (PlayerRollState)Get(player, "roll");
            var dump = new SkinDump { parts = new Part[skins.Length] };
            var neutral = new Vector3[skins.Length][];
            for (int k = 0; k < skins.Length; k++)
            {
                var mesh = new Mesh(); skins[k].BakeMesh(mesh);
                neutral[k] = mesh.vertices;
                dump.parts[k] = new Part { name = skins[k].name, points = neutral[k], triangles = skins[k].sharedMesh.triangles };
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            File.WriteAllText("Logs/player-neutral-skin.json", JsonUtility.ToJson(dump));
            foreach (string action in new[] { "idle", "walk", "run", "jump", "roll" })
            {
                float maximum = 1, handMaximum = 1;
                string worst = "";
                for (int frame = 0; frame <= 60; frame++)
                {
                    float t = frame / 60f;
                    foreach (var bone in bones) bone.localRotation = Quaternion.identity;
                    Set(animator, "stride", action == "walk" || action == "run" ? 1f : 0f);
                    Set(animator, "sprintWeight", action == "run" ? 1f : 0f);
                    Set(animator, "airWeight", action == "jump" ? 1f : 0f);
                    Set(animator, "rollWeight", action == "roll" ? 1f : 0f);
                    Set(animator, "airborne", action == "jump");
                    Set(animator, "lifeTime", t * 4); Set(animator, "idleTime", t * 4);
                    Set(animator, "breath", Mathf.Sin(t * Mathf.PI * 2));
                    Set(animator, "locomotionPhase", t);
                    foreach (var pair in new[] { ("walkData", "walkTime"), ("runData", "runTime"), ("jumpData", "jumpSampleTime") })
                    {
                        object data = Get(animator, pair.Item1);
                        float duration = (float)data.GetType().GetField("duration").GetValue(data);
                        Set(animator, pair.Item2, t * duration);
                    }
                    roll.Reset();
                    if (action == "roll") { roll.Request(); roll.Step(0, true, 1, 0); roll.Step(t * .999f, true, 1, 0); }
                    foreach (PlayerRigBone bone in Enum.GetValues(typeof(PlayerRigBone))) Call(animator, "Pose", bone, 0f, 0f, 1f);
                    CheckEyebrows(skins);
                    if (frame % 15 == 0 && (action == "idle" || action == "walk" || action == "run"))
                        PlayerSilhouetteVerification.CheckVisiblePosture(player);
                    for (int k = 0; k < skins.Length; k++)
                    {
                        var skin = skins[k]; var mesh = new Mesh(); skin.BakeMesh(mesh);
                        Vector3[] actual = mesh.vertices, rest = skin.sharedMesh.vertices;
                        int[] triangles = skin.sharedMesh.triangles;
                        Bounds bounds = (Bounds)Get(animator, "restBounds");
                        for (int i = 0; i < triangles.Length; i++)
                        {
                            int a = triangles[i], b = triangles[i - i % 3 + (i + 1) % 3];
                            float length = (neutral[k][a] - neutral[k][b]).magnitude;
                            if (length < .00001f) continue;
                            float ratio = (actual[a] - actual[b]).magnitude / length;
                            if (float.IsNaN(ratio) || float.IsInfinity(ratio)) throw new Exception("Invalid animated vertex");
                            if (ratio > maximum) { maximum = ratio; worst = skin.name + " edge " + a + "/" + b; }
                            Vector3 mid = (rest[a] + rest[b]) * .5f;
                            float y = (mid.y - bounds.min.y) / bounds.size.y;
                            if (skin.name == "Animated Cube.001" && y < .52f && y > .28f && Mathf.Abs(mid.x - bounds.center.x) / bounds.size.y > .15f)
                                handMaximum = Mathf.Max(handMaximum, ratio);
                        }
                        if (frame == 30) dump.parts[k] = new Part { name = skin.name, points = actual, triangles = triangles };
                        UnityEngine.Object.DestroyImmediate(mesh);
                    }
                }
                File.WriteAllText("Logs/player-" + action + "-skin.json", JsonUtility.ToJson(dump));
                report.AppendLine(action + ": maximum edge stretch " + maximum.ToString("F3") + ", hand " + handMaximum.ToString("F3") + "; " + worst);
                if (handMaximum > 1.02f) throw new Exception(action + " deforms the hand/fingers.");
                if (maximum > 2.4f) throw new Exception(action + " excessively stretches the body/clothing.");
            }
            File.WriteAllText("Logs/player-skin-result.txt", report.ToString());
            Debug.Log(report.ToString());
        }
        catch (Exception error)
        {
            File.WriteAllText("Logs/player-skin-result.txt", report + error.ToString());
            throw;
        }
    }
    static void CheckEyebrows(SkinnedMeshRenderer[] skins)
    {
        var head = Array.Find(skins, skin => skin.name == "Animated Cube");
        var brows = Array.Find(skins, skin => skin.name == "Animated Cube.004");
        if (!head || !brows) throw new Exception("Head or original facial eyebrows missing.");
        var headMesh = new Mesh(); var browMesh = new Mesh();
        try
        {
            head.BakeMesh(headMesh); brows.BakeMesh(browMesh);
            float tolerance = headMesh.bounds.size.y * .12f;
            foreach (var point in browMesh.vertices)
                if (headMesh.bounds.SqrDistance(point) > tolerance * tolerance)
                    throw new Exception("An eyebrow detached from the head during animation.");
        }
        finally { UnityEngine.Object.DestroyImmediate(headMesh); UnityEngine.Object.DestroyImmediate(browMesh); }
    }
    static void VerifyScene(StringBuilder report)
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
        var game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
        var hud = UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();
        var bar = hud.transform.Find("Stats/Maze HUD/Vitals panel").GetComponent<Image>();
        if (bar.color.a != 0 || bar.raycastTarget || hud.transform.Find("Stats").GetComponent<Image>().color.a != 0)
            throw new Exception("HUD background is not transparent.");
        if (hud.hearts.Length != 3 || !hud.coinIcon.sprite) throw new Exception("HUD icons are missing.");
        var wall = game.areas[2].wallPrefab;
        if (!wall || wall.name != "CourtyardWall" || wall.GetComponentsInChildren<Light>(true).Length != 0)
            throw new Exception("Patio is not using the repaired wall prefab.");
        var root = new GameObject("Wall verification");
        var target = new Vector3(3.7f, 3, .58f);
        var instance = MazeBetaGame.FitVisual(wall, root.transform, target, false);
        Bounds actual = default; bool first = true;
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
        {
            if (first) { actual = renderer.bounds; first = false; } else actual.Encapsulate(renderer.bounds);
            foreach (var material in renderer.sharedMaterials)
            {
                if (!material) throw new Exception("Missing Patio wall material.");
                report.AppendLine("Wall material: " + material.name + "; shader " + material.shader.name + "; texture " + material.mainTexture
                    + "; emission " + material.GetColor("_EmissionColor") + "; keyword " + material.IsKeywordEnabled("_EMISSION"));
                if (material.shader.name != "Universal Render Pipeline/Lit" || !material.mainTexture
                    || material.GetTexture("_EmissionMap") || material.IsKeywordEnabled("_EMISSION") || material.GetColor("_EmissionColor").maxColorComponent > .001f)
                    throw new Exception("Patio wall material is missing or emissive.");
            }
        }
        if ((actual.size - target).magnitude > .001f) throw new Exception("Wall bounds are distorted.");
        UnityEngine.Object.DestroyImmediate(root);
        report.AppendLine("PASS: Patio solid wall dimensions/material and transparent HUD with original heart/coin images.");
    }
    [Serializable] sealed class SkinDump { public Part[] parts; }
    [Serializable] sealed class Part { public string name; public Vector3[] points; public int[] triangles; }
}
