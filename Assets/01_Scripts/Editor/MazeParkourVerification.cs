using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class MazeParkourVerification
{
    const string Pending = "GameJam.ParkourVerification";
    static MazeBetaGame game;
    static Keyboard keyboard;
    static int phase, target, jumps;
    static double since, started, jumpPressedAt;
    static bool jumpHeld, seenAir;
    static Vector3 axis;
    static Key move;
    static MazeParkourVerification() { EditorApplication.update += Tick; }

    [MenuItem("GameJam/Verificar parkour con puas")]
    public static void Run()
    {
        phase = 0; started = since = 0;
        SessionState.SetBool(Pending, true);
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }

    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    static float Along(Vector3 point) => Vector3.Dot(point, axis);
    static float Front(BoxCollider platform) => Along(platform.bounds.center) + Vector3.Dot(platform.bounds.extents, axis);
    static float Back(BoxCollider platform) => Along(platform.bounds.center) - Vector3.Dot(platform.bounds.extents, axis);
    static void Queue(bool jump = false, bool sprint = false) => InputSystem.QueueStateEvent(keyboard,
        jump ? new KeyboardState(move, Key.Space) : sprint ? new KeyboardState(move, Key.LeftShift) : new KeyboardState(move));
    static void Next() { phase++; since = EditorApplication.timeSinceStartup; }
    static void PlaceAtEntry()
    {
        axis = game.Grid.ParkourDirection == 0 ? Vector3.forward : Vector3.right;
        move = game.Grid.ParkourDirection == 0 ? Key.W : Key.D;
        var first = game.ParkourPlatforms[0];
        var controller = game.Player.GetComponent<CharacterController>();
        controller.enabled = false;
        game.Player.transform.position = first.bounds.center + axis * (Front(first) - Along(first.bounds.center) - 0.12f)
            + Vector3.up * (first.bounds.extents.y + 0.05f);
        controller.enabled = true;
        game.Player.view = null;
        game.Player.InitializeSpeed();
        Physics.SyncTransforms();
    }
    static void InspectGeometry()
    {
        Check(game.Grid.ParkourCells.Count == 5 && game.ParkourPlatforms.Count >= 3, "Missing continuous parkour sequence");
        Check(game.Grid.Exits.Count == 1, "Parkour changed the single exit");
        axis = game.Grid.ParkourDirection == 0 ? Vector3.forward : Vector3.right;
        for (int i = 1; i < game.ParkourPlatforms.Count; i++)
        {
            var a = game.ParkourPlatforms[i - 1]; var b = game.ParkourPlatforms[i];
            float gap = Back(b) - Front(a);
            Check(gap >= 1.7f && Mathf.Abs(gap - game.ActualParkourGap) < 0.01f, "Platform gap is too small or mismatched");
            Vector3 empty = (a.bounds.center + b.bounds.center) * 0.5f;
            Check(!Physics.Raycast(empty + Vector3.up, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore), "Invisible floor bridges the gap");
            Check(Physics.Raycast(empty + Vector3.up, Vector3.down, out var hit, 4, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.name.Contains("Parkour spike bed"), "No solid floor below the spikes");
        }
        foreach (var hazard in game.SpikeHazards) Check(hazard.bounds.max.y < -0.6f, "Spikes touch safe platforms");
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (started == 0) started = since = now;
            Check(now - started < 45, "Parkour verification timed out");
            if (phase == 0)
            {
                game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
                if (!game || !game.Player) return;
                game.recordResults = false;
                game.width = game.height = 12;
                game.parkourEnabled = true; game.parkourCellLength = 5; game.parkourGap = 1.8f;
                game.spadeCount = game.turretCount = game.chairCount = game.pillCount = game.coinCount = 0;
                game.bossGracePeriod = 10000;
                game.Begin(5329);
                keyboard = InputSystem.AddDevice<Keyboard>();
                Next();
            }
            else if (phase == 1 && now - since > 0.6)
            {
                InspectGeometry(); PlaceAtEntry(); Queue(sprint: true); Next();
            }
            else if (phase == 2 && now - since > 1.1)
            {
                Check(!game.IsPlaying && game.DefeatReason.Contains("PUAS"), "Sprinting without jumping crossed the gap or missed the spike bed");
                Check(game.Player.transform.position.y > game.fallDeathHeight, "Player fell into void before contacting spikes");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                game.Begin(5329); Next();
            }
            else if (phase == 3 && now - since > 0.6)
            {
                game.Player.baseSpeed = game.Player.minimumSpeed;
                PlaceAtEntry();
                target = 1; jumps = 0; seenAir = jumpHeld = false; jumpPressedAt = -1;
                Queue(); Next();
            }
            else if (phase == 4)
            {
                Check(game.IsPlaying, "Jump sequence hit spikes at platform " + target);
                var player = game.Player;
                if (!player.IsGrounded) seenAir = true;
                if (player.IsGrounded && Along(player.transform.position) > Back(game.ParkourPlatforms[target]) + 0.03f)
                {
                    Check(seenAir, "Platform reached without jumping");
                    target++; seenAir = false;
                    if (target == game.ParkourPlatforms.Count)
                    {
                        Check(jumps >= game.ParkourPlatforms.Count - 1, "Not every platform was jumped");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        Finish("PASS: continuous parkour; " + game.ActualParkourGap.ToString("0.00") +
                            "m clear gaps; sprinting without a jump hits spikes; all " + jumps +
                            " jumps are reachable at fatigue speed; solid spike bed prevents void falls; one exit preserved.");
                        return;
                    }
                }
                if (jumpHeld && now - jumpPressedAt > 0.12) { Queue(); jumpHeld = false; }
                if (!jumpHeld && player.IsGrounded && now - jumpPressedAt > 0.3 &&
                    Front(game.ParkourPlatforms[target - 1]) - Along(player.transform.position) < 0.13f)
                {
                    Queue(jump: true); jumpHeld = true; jumpPressedAt = now; jumps++;
                }
            }
        }
        catch (Exception error) { Finish("FAIL phase " + phase + ": " + error); }
    }
    static void Finish(string result)
    {
        if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
        SessionState.SetBool(Pending, false);
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/parkour-playmode-result.txt", result);
        if (game && game.ParkourPlatforms.Count > 0)
        {
            var camera = Camera.main;
            camera.GetComponent<PlayerFollowCamera>().enabled = false;
            Vector3 centre = (game.ParkourPlatforms[0].bounds.center + game.ParkourPlatforms[game.ParkourPlatforms.Count - 1].bounds.center) * 0.5f;
            game.enabled = false;
            camera.transform.position = centre - axis * 2 + Vector3.Cross(Vector3.up, axis) * 0.5f + Vector3.up * 11;
            camera.transform.LookAt(centre + Vector3.down * 0.7f);
        }
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Debug.Log("PARKOUR_VERIFICATION: " + result);
        EditorApplication.isPaused = true;
    }
}
