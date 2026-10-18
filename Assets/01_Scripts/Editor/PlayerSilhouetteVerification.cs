using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class PlayerSilhouetteVerification
{
    const string Pending = "GameJam.SilhouetteVerification";
    static int phase;
    static double since;
    static MazeBetaGame game;
    static Player player;
    static Keyboard keyboard;
    static PlayerSilhouetteVerification() { EditorApplication.update += Tick; }
    [MenuItem("GameJam/Verificar silueta del player")]
    public static void Run()
    {
        SessionState.SetInt("GameJam.ActionPreview", 0);
        phase = 0; since = 0;
        SessionState.SetBool(Pending, true);
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }
    [MenuItem("GameJam/Ver carrera adaptada")]
    public static void PreviewRun() { Run(); SessionState.SetInt("GameJam.ActionPreview", 2); }
    [MenuItem("GameJam/Ver salto adaptado")]
    public static void PreviewJump() { Run(); SessionState.SetInt("GameJam.ActionPreview", 1); }
    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (phase == 0)
            {
                game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
                if (!game || !game.Player || !game.Player.GetComponent<PlayerLimbAnimator>().IsReady) return;
                game.spadeCount = game.turretCount = game.chairCount = game.coinCount = 0;
                game.recordResults = false;
                game.bossGracePeriod = 10000;
                game.Begin(game.ActiveSeed);
                phase = 1; return;
            }
            player = game.Player;
            if (!player.GetComponent<PlayerLimbAnimator>().IsReady) return;
            if (phase == 1)
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = new Vector3(100, -0.1f, 100);
                floor.transform.localScale = new Vector3(20, 0.2f, 20);
                var controller = player.GetComponent<CharacterController>();
                controller.enabled = false;
                player.transform.position = new Vector3(100, 0.05f, 100);
                player.visual.rotation = Quaternion.identity;
                player.view = null;
                controller.enabled = true;
                game.Boss.IsActive = false;
                game.enabled = false;
                var camera = Camera.main;
                camera.GetComponent<PlayerFollowCamera>().enabled = false;
                camera.transform.position = new Vector3(103, 1, 100);
                camera.transform.LookAt(new Vector3(100, 1, 100));
                camera.orthographic = true;
                camera.orthographicSize = 1.15f;
                if (SessionState.GetInt("GameJam.ActionPreview", 0) == 1)
                {
                    camera.orthographicSize = 1.8f;
                    camera.transform.position = new Vector3(103, 1.6f, 100);
                    camera.transform.LookAt(new Vector3(100, 1.6f, 100));
                }
                Physics.SyncTransforms();
                since = EditorApplication.timeSinceStartup;
                phase = 2; return;
            }
            if (phase == 2 && EditorApplication.timeSinceStartup - since < 0.7) return;
            int preview = SessionState.GetInt("GameJam.ActionPreview", 0);
            if (preview != 0)
            {
                if (phase == 2)
                {
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    InputSystem.QueueStateEvent(keyboard, preview == 1 ? new KeyboardState(Key.Space)
                        : new KeyboardState(Key.W, Key.LeftShift));
                    phase = 3; since = EditorApplication.timeSinceStartup; return;
                }
                if (preview == 2)
                {
                    Camera.main.transform.position = player.transform.position + new Vector3(3, 1, 0);
                    Camera.main.transform.LookAt(player.transform.position + Vector3.up);
                    if (EditorApplication.timeSinceStartup - since < 0.4) return;
                }
                if (preview == 1 && (player.IsGrounded || player.VerticalVelocity > 0))
                {
                    if (EditorApplication.timeSinceStartup - since > 3) throw new TimeoutException("Jump preview did not reach apex");
                    return;
                }
                InputSystem.RemoveDevice(keyboard); keyboard = null;
                SessionState.SetBool(Pending, false);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                EditorApplication.isPaused = true;
                Debug.Log(preview == 1 ? "PREVIEW: adapted Jump at apex" : "PREVIEW: adapted Running in profile");
                return;
            }
            var log = new StringBuilder();
            var original = new List<Vector3>();
            foreach (var filter in player.visual.GetComponentsInChildren<MeshFilter>())
            {
                if (PlayerLimbAnimator.IsControl(filter.name)) continue;
                foreach (var vertex in filter.sharedMesh.vertices)
                    original.Add(player.visual.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
            }
            var actual = VisiblePoints(player);
            Dump(log, "ORIGINAL", original);
            Dump(log, "VISIBLE", actual);
            float lean = CheckVisiblePosture(player);
            log.AppendLine("PASS: visible mesh head/waist forward offset = " + lean.ToString("F4") + " of body height (maximum 0.08).");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/player-silhouette-result.txt", log.ToString());
            Debug.Log(log.ToString());
            SessionState.SetBool(Pending, false);
            EditorApplication.isPaused = true;
        }
        catch (Exception e)
        {
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            SessionState.SetBool(Pending, false);
            Debug.LogException(e);
            EditorApplication.isPaused = true;
        }
    }
    public static float CheckVisiblePosture(Player target)
    {
        var points = VisiblePoints(target);
        var bounds = new Bounds(points[0], Vector3.zero);
        foreach (var point in points) bounds.Encapsulate(point);
        float waist = SectionZ(points, bounds, 0.55f), head = SectionZ(points, bounds, 0.85f);
        float lean = Mathf.Abs(head - waist) / bounds.size.y;
        if (lean > 0.08f) throw new Exception("Visible mesh is still hunched: head/waist offset " + lean.ToString("F4"));
        return lean;
    }
    static List<Vector3> VisiblePoints(Player target)
    {
        var points = new List<Vector3>();
        foreach (var skin in target.visual.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = new Mesh(); skin.BakeMesh(mesh);
            foreach (var vertex in mesh.vertices)
                points.Add(target.visual.InverseTransformPoint(skin.transform.TransformPoint(vertex)));
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        return points;
    }
    static float SectionZ(List<Vector3> points, Bounds bounds, float height)
    {
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        foreach (var p in points)
            if (Mathf.Abs((p.y - bounds.min.y) / bounds.size.y - height) < 0.05f
                && Mathf.Abs(p.x - bounds.center.x) < bounds.size.y * 0.12f)
            { min = Mathf.Min(min, p.z); max = Mathf.Max(max, p.z); }
        if (float.IsInfinity(min)) throw new Exception("No visible mesh vertices in posture section");
        return (min + max) * 0.5f;
    }
    static void Dump(StringBuilder log, string title, List<Vector3> points)
    {
        var bounds = new Bounds(points[0], Vector3.zero);
        foreach (var p in points) bounds.Encapsulate(p);
        log.AppendLine(title + " bounds " + bounds);
        foreach (float height in new[] { 0.45f, 0.55f, 0.65f, 0.75f, 0.85f, 0.95f })
        {
            Bounds section = default; int count = 0;
            foreach (var p in points)
                if (Mathf.Abs((p.y - bounds.min.y) / bounds.size.y - height) < 0.05f
                    && Mathf.Abs(p.x - bounds.center.x) < bounds.size.y * 0.12f)
                {
                    if (count++ == 0) section = new Bounds(p, Vector3.zero);
                    else section.Encapsulate(p);
                }
            log.AppendLine(height + " centre " + section.center.ToString("F4") + " count " + count);
        }
    }
}
