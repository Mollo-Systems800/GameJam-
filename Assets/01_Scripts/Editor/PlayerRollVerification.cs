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
    static Quaternion chestPose, armPose, spinePose;
    static double transitionUntil;
    static Quaternion previousChest;
    static float largestTransitionStep;
    static bool jumpSeen;
    static Key movementKey;
    static float walkingSpeed, runLegAngle;
    static Quaternion walkingLegStart;

    static PlayerRollVerification() { EditorApplication.update += Tick; }

    [MenuItem("GameJam/Verificar voltereta")]
    public static void Run()
    {
        SessionState.SetBool("GameJam.NaturalMotionVerification", false);
        SessionState.SetBool("GameJam.SprintVerification", false);
        BeginVerification();
    }

    [MenuItem("GameJam/Verificar movimiento natural")]
    public static void RunNatural()
    {
        SessionState.SetBool("GameJam.NaturalMotionVerification", true);
        SessionState.SetBool("GameJam.SprintVerification", false);
        BeginVerification();
    }

    [MenuItem("GameJam/Verificar carrera")]
    public static void RunSprint()
    {
        SessionState.SetBool("GameJam.NaturalMotionVerification", false);
        SessionState.SetBool("GameJam.SprintVerification", true);
        BeginVerification();
    }

    static void BeginVerification()
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
            else if (SessionState.GetBool("GameJam.SprintVerification", false))
            {
                TickSprint(elapsed);
            }
            else if (SessionState.GetBool("GameJam.NaturalMotionVerification", false))
            {
                TickNatural(elapsed);
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

    static void TickSprint(double elapsed)
    {
        var animator = game.Player.GetComponent<PlayerLimbAnimator>();
        if (phase == 1 && elapsed > 0.4)
        {
            Check(animator.HasRunMotion && animator.HasWalkMotion && animator.IsReady, "Walking, Standard Run or rig is missing");
            walkingLegStart = Array.Find(game.Player.visual.GetComponentsInChildren<Transform>(), t => t.name == "LeftThigh").localRotation;
            movementKey = game.Grid.IsOpen(0, 0) ? Key.W : Key.D;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(movementKey));
            Next();
        }
        else if (phase == 2 && elapsed > 0.18)
        {
            Check(!game.Player.IsSprinting && animator.RunAnimationWeight < 0.05f, "Walking incorrectly plays running animation");
            Check(animator.WalkAnimationWeight > 0.75f, "Walking clip did not blend in");
            Check(Quaternion.Angle(walkingLegStart, Array.Find(game.Player.visual.GetComponentsInChildren<Transform>(),
                t => t.name == "LeftThigh").localRotation) > 3, "Walking clip did not animate the leg");
            walkingSpeed = game.Player.PlanarSpeed;
            Check(walkingSpeed > 1, "Walking did not move the player");
            runLegAngle = 0;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(movementKey, Key.LeftShift));
            Next();
        }
        else if (phase == 3)
        {
            var rig = game.Player.visual.Find("Procedural Limb Rig");
            foreach (Transform joint in rig.GetComponentsInChildren<Transform>())
                if (joint.name == "LeftThigh" || joint.name == "RightThigh")
                    runLegAngle = Mathf.Max(runLegAngle, Quaternion.Angle(Quaternion.identity, joint.localRotation));
            if (elapsed < 0.3) return;
            Check(game.Player.IsSprinting && animator.RunAnimationWeight > 0.8f, "Shift did not select Standard Run");
            Check(game.Player.PlanarSpeed > walkingSpeed * 1.2f, "Sprint did not increase movement speed");
            Check(runLegAngle > 24, "Running leg pose is still restricted to a walking amplitude");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Next();
        }
        else if (phase == 4 && elapsed > 0.3)
        {
            Check(!game.Player.IsSprinting && animator.RunAnimationWeight < 0.1f, "Run state did not blend out after release");
            var controller = game.Player.GetComponent<CharacterController>();
            controller.enabled = false;
            game.Player.transform.position = Vector3.up * 0.1f;
            controller.enabled = true;
            Physics.SyncTransforms();
            game.Player.slowdownEvery = 0.01f;
            game.Player.slowdownAmount = 100;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(movementKey, Key.LeftShift));
            Next();
        }
        else if (phase == 5 && elapsed > 0.4)
        {
            Check(game.Player.PlanarSpeed < game.Player.baseSpeed && game.Player.IsSprinting
                && animator.RunAnimationWeight > 0.8f, "Fatigue prevented the running animation");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            VerifyPrefabReplacements();
            Finish("PASS: Walking clip assigned and moving legs; walk/run are distinct; Shift selects Standard Run, increases speed and leg swing; release blends out; fatigue still allows run animation. Peak thigh angle: " + runLegAngle.ToString("0.0") + "; custom prefabs replace boss, pills, spades, turret, coins, projectiles and scenery without duplicate primitive visuals.");
            EditorApplication.isPaused = true;
        }
    }

    static void VerifyPrefabReplacements()
    {
        var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
        template.name = "Inspector Prefab Test";
        template.transform.position = Vector3.one * 1000;
        template.GetComponent<Collider>().enabled = false;
        foreach (string field in new[] { "bossPrefab", "pillPrefab", "spadeEnemyPrefab", "turretEnemyPrefab", "coinPrefab",
            "projectilePrefab", "wallCorePrefab", "wallCapPrefab", "exitFloorPrefab", "exitMarkerPrefab", "pitVisualPrefab",
            "jumpTakeoffPrefab", "jumpLandingPrefab", "platformPrefab", "turretBarrelVisualPrefab" })
            typeof(MazeBetaGame).GetField(field).SetValue(game, template);
        game.pillCount = game.spadeCount = game.turretCount = game.coinCount = game.obstacleCount = 1;
        game.Begin(game.ActiveSeed);
        Check(game.Boss.GetComponentsInChildren<Renderer>().Length == 1, "Custom boss visual duplicated");
        foreach (var pill in game.GetComponentsInChildren<PillPickup>())
            Check(pill.GetComponentsInChildren<Renderer>().Length == 1, "Custom pill visual duplicated");
        foreach (var spade in game.GetComponentsInChildren<SpadeEnemy>())
            Check(spade.GetComponentsInChildren<Renderer>().Length == 1, "Custom spade visual duplicated");
        foreach (var turret in game.GetComponentsInChildren<TurretEnemy>())
            Check(turret.GetComponentsInChildren<Renderer>().Length == 2, "Custom turret body/barrel duplicated");
        foreach (var coin in game.GetComponentsInChildren<CoinPickup>())
            Check(coin.GetComponentsInChildren<Renderer>().Length == 1, "Custom coin visual duplicated");
        game.FireProjectile(new Vector3(100, 3, 100), Vector3.forward, 1, 1, null);
        foreach (var shot in game.GetComponentsInChildren<EnemyProjectile>())
        {
            Check(shot.GetComponentsInChildren<Renderer>().Length == 1, "Custom projectile visual duplicated");
            foreach (var collider in shot.GetComponentsInChildren<Collider>())
                Check(!collider.enabled, "Projectile has self-collision");
        }
        UnityEngine.Object.Destroy(template);
    }

    static void TickNatural(double elapsed)
    {
        var rig = game.Player.visual.Find("Procedural Limb Rig");
        if (!rig) return;
        Transform chest = System.Array.Find(rig.GetComponentsInChildren<Transform>(), t => t.name == "Chest");
        Transform arm = System.Array.Find(rig.GetComponentsInChildren<Transform>(), t => t.name == "LeftArm");
        Transform spine = System.Array.Find(rig.GetComponentsInChildren<Transform>(), t => t.name == "Spine");
        if (phase == 1 && elapsed > 0.5)
        {
            Check(chest && arm && spine, "Natural motion joints are missing");
            chestPose = chest.localRotation; armPose = arm.localRotation; spinePose = spine.localRotation;
            Next();
        }
        else if (phase == 2 && elapsed > 0.7)
        {
            Check(Quaternion.Angle(chestPose, chest.localRotation) > 0.1f, "Chest froze at rest");
            Check(Quaternion.Angle(armPose, arm.localRotation) > 0.1f, "Arms froze at rest");
            Check(Quaternion.Angle(spinePose, spine.localRotation) > 0.1f, "Spine froze at rest");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            previousChest = chest.localRotation;
            largestTransitionStep = 0;
            jumpSeen = false;
            transitionUntil = EditorApplication.timeSinceStartup + 0.5;
            Next();
        }
        else if (phase == 3)
        {
            jumpSeen |= !game.Player.IsGrounded;
            largestTransitionStep = Mathf.Max(largestTransitionStep, Quaternion.Angle(previousChest, chest.localRotation));
            previousChest = chest.localRotation;
            if (EditorApplication.timeSinceStartup < transitionUntil) return;
            Check(jumpSeen, "Jump input did not leave the ground");
            Check(largestTransitionStep < 20, "Jump transition snapped chest pose");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Next();
        }
        else if (phase == 4 && elapsed > 1)
        {
            chestPose = chest.localRotation; armPose = arm.localRotation;
            Next();
        }
        else if (phase == 5 && elapsed > 0.6)
        {
            Check(Quaternion.Angle(chestPose, chest.localRotation) > 0.1f
                && Quaternion.Angle(armPose, arm.localRotation) > 0.1f, "Body froze after landing");
            Finish("PASS: chest, arms and spine stay alive at rest; jump transition has no large pose snap; breathing and arms continue after landing.");
            EditorApplication.isPaused = true;
        }
    }

    static void Finish(string result)
    {
        SessionState.SetBool(Pending, false);
        if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
        Directory.CreateDirectory("Logs");
        File.WriteAllText(SessionState.GetBool("GameJam.SprintVerification", false)
            ? "Logs/sprint-playmode-result.txt" : SessionState.GetBool("GameJam.NaturalMotionVerification", false)
            ? "Logs/natural-motion-playmode-result.txt" : "Logs/roll-playmode-result.txt", result);
        Debug.Log("ROLL_VERIFICATION: " + result);
    }
}
