using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Focused verification in the existing editor; never closes Unity.
[InitializeOnLoad]
public static class PlayerRollVerification
{
    const string Pending = "GameJam.RollVerification";
    static MazeBetaGame game;
    static Keyboard keyboard;
    static int phase;
    static double since, started;
    static Vector3 position;
    static float progress;

    static PlayerRollVerification() { EditorApplication.update += Tick; }

    [MenuItem("GameJam/Verificar voltereta")]
    public static void Run()
    {
        phase = 0;
        since = started = EditorApplication.timeSinceStartup;
        SessionState.SetBool(Pending, true);
        EditorApplication.isPaused = false;
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Next() { phase++; since = EditorApplication.timeSinceStartup; }

    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (started <= 0) since = started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started > 40) throw new TimeoutException("Roll verification timed out");
            double elapsed = EditorApplication.timeSinceStartup - since;
            if (phase == 0)
            {
                game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
                if (!game || !game.Player) return;
                game.recordResults = false;
                game.spadeCount = game.turretCount = game.coinCount = game.chairCount = game.obstacleCount = 0;
                game.shopkeeperPrefab = null;
                game.bossGracePeriod = 10000;
                game.Begin(game.ActiveSeed);
                keyboard = InputSystem.AddDevice<Keyboard>();
                Next();
            }
            else if (phase == 1 && elapsed > 0.4)
            {
                var animator = game.Player.GetComponent<PlayerLimbAnimator>();
                Check(animator.IsReady && animator.HasRollMotion, "Player rig or roll asset not ready");
                Check(game.Player.CanMove && game.Player.IsGrounded, "Player cannot move or is not grounded");
                position = game.Player.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.C));
                Next();
            }
            else if (phase == 2 && elapsed > 0.15)
            {
                Check(game.Player.IsRolling, "C input did not activate roll");
                Check(Vector3.Distance(position, game.Player.transform.position) > 0.05f, "Roll activated but player did not move");
                var rig = game.Player.visual.Find("Procedural Limb Rig");
                Check(rig && Quaternion.Angle(Quaternion.identity, rig.localRotation) > 5, "Body did not rotate");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                game.SetShopOpen(true);
                progress = game.Player.RollProgress;
                position = game.Player.transform.position;
                Next();
            }
            else if (phase == 3 && elapsed > 0.2)
            {
                Check(game.Player.RollProgress == progress && Vector3.Distance(position, game.Player.transform.position) < 0.001f,
                    "Roll did not pause with shop");
                game.SetShopOpen(false);
                Next();
            }
            else if (phase == 4 && elapsed > 2)
            {
                Check(!game.Player.IsRolling, "Roll did not finish");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.D));
                Next();
            }
            else if (phase == 5 && elapsed > 0.15)
            {
                Check(game.Player.IsRolling, "Left Ctrl input did not activate roll");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Finish("PASS: C and Left Ctrl activate roll; actual player movement and body rotation; shop freezes action; roll completes.");
                EditorApplication.isPaused = true;
            }
        }
        catch (Exception error) { Finish("FAIL phase " + phase + ": " + error); }
    }

    static void Finish(string result)
    {
        SessionState.SetBool(Pending, false);
        if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/roll-playmode-result.txt", result);
        Debug.Log("ROLL_VERIFICATION: " + result);
    }
}
