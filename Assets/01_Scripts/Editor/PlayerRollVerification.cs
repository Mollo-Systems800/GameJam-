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
    static float walkingSpeed, runLegAngle, walkLegChange;
    static float runKneeAngle, jumpKneeAngle, jumpPeakWeight;
    static Quaternion walkingLegStart;
    static Quaternion previousRollRoot, frozenKnee;
    static Vector3 frozenRigPosition;
    static float rollTravel;

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
            bool rollTest = !SessionState.GetBool("GameJam.SprintVerification", false)
                && !SessionState.GetBool("GameJam.NaturalMotionVerification", false);
            if (rollTest && phase >= 2 && game && game.Player)
            {
                var currentRig = game.Player.visual.Find("Procedural Limb Rig");
                rollTravel += Quaternion.Angle(previousRollRoot, currentRig.localRotation);
                previousRollRoot = currentRig.localRotation;
                var camera = Camera.main;
                camera.transform.position = game.Player.transform.position + new Vector3(3, 0.9f, 0);
                camera.transform.LookAt(game.Player.transform.position + Vector3.up * 0.9f);
            }
            if (phase == 0)
            {
                game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
                if (!game || !game.Player) return;
                game.recordResults = false;
                game.spadeCount = game.turretCount = game.coinCount = game.chairCount = game.obstacleCount = 0;
                game.storePrefab = null;
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
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Roll verification floor";
                floor.transform.position = new Vector3(100, -0.1f, 100);
                floor.transform.localScale = new Vector3(30, 0.2f, 30);
                var controller = game.Player.GetComponent<CharacterController>();
                controller.enabled = false;
                game.Player.transform.position = new Vector3(100, 0.05f, 100);
                controller.enabled = true;
                game.Player.view = null;
                game.enabled = false;
                Camera.main.GetComponent<PlayerFollowCamera>().enabled = false;
                Camera.main.orthographic = true;
                Camera.main.orthographicSize = 1.1f;
                Physics.SyncTransforms();
                previousRollRoot = Quaternion.identity;
                rollTravel = 0;
                position = game.Player.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.C));
                Next();
            }
            else if (phase == 2 && elapsed > 0.32)
            {
                Check(game.Player.IsRolling, "C input did not activate roll");
                Check(Vector3.Distance(position, game.Player.transform.position) > 0.05f, "Roll activated but player did not move");
                var rig = game.Player.visual.Find("Procedural Limb Rig");
                Check(rig && Quaternion.Angle(Quaternion.identity, rig.localRotation) > 5, "Body did not rotate");
                var thigh = Array.Find(rig.GetComponentsInChildren<Transform>(), t => t.name == "LeftThigh");
                var knee = Array.Find(rig.GetComponentsInChildren<Transform>(), t => t.name == "LeftShin");
                Check(Quaternion.Angle(Quaternion.identity, thigh.localRotation) > 55, "Roll did not tuck the thighs");
                Check(Quaternion.Angle(Quaternion.identity, knee.localRotation) > 70, "Roll did not fold the knees");
                frozenKnee = knee.localRotation;
                frozenRigPosition = rig.localPosition;
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
                var rig = game.Player.visual.Find("Procedural Limb Rig");
                var knee = Array.Find(rig.GetComponentsInChildren<Transform>(), t => t.name == "LeftShin");
                Check(Quaternion.Angle(knee.localRotation, frozenKnee) < 0.01f
                    && Vector3.Distance(rig.localPosition, frozenRigPosition) < 0.001f, "Roll pose/ground support changed while paused");
                game.SetShopOpen(false);
                Next();
            }
            else if (phase == 4 && elapsed > 2)
            {
                Check(!game.Player.IsRolling, "Roll did not finish");
                Check(rollTravel > 330 && rollTravel < 400, "Roll did not perform one continuous revolution: " + rollTravel);
                Check(game.Player.GetComponent<PlayerLimbAnimator>().RollAnimationWeight < 0.02f, "Tucked pose did not blend out");
                CheckStandingPosture();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.D));
                Next();
            }
            else if (phase == 5 && elapsed > 0.32)
            {
                Check(game.Player.IsRolling, "Left Ctrl input did not activate roll");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Finish("PASS: C and Left Ctrl activate roll; thighs tuck and knees fold; one continuous revolution; shop freezes movement, pose and floor support; roll completes and upright visible posture returns.");
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
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
            Check(animator.HasRunMotion && animator.HasWalkMotion && animator.IsReady, "Walking, Running or rig is missing");
            Check(animator.runMotion && animator.runMotion.name == "Running", "New Running clip is not assigned to the player");
            Check(animator.runMotion.text.Contains("\"standingPose\":true"), "Running uses the old crouched reference");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Locomotion verification floor";
            floor.transform.position = new Vector3(100, -0.1f, 100);
            floor.transform.localScale = new Vector3(30, 0.2f, 30);
            var controller = game.Player.GetComponent<CharacterController>();
            controller.enabled = false;
            game.Player.transform.position = new Vector3(100, 0.05f, 100);
            game.Player.view = null;
            controller.enabled = true;
            Physics.SyncTransforms();
            walkingLegStart = Array.Find(game.Player.visual.GetComponentsInChildren<Transform>(), t => t.name == "LeftThigh").localRotation;
            walkLegChange = 0;
            movementKey = Key.W;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(movementKey));
            Next();
        }
        else if (phase == 2)
        {
            walkLegChange = Mathf.Max(walkLegChange, Quaternion.Angle(walkingLegStart,
                Array.Find(game.Player.visual.GetComponentsInChildren<Transform>(), t => t.name == "LeftThigh").localRotation));
            if (elapsed < 0.6) return;
            Check(!game.Player.IsSprinting && animator.RunAnimationWeight < 0.05f, "Walking incorrectly plays running animation");
            Check(animator.WalkAnimationWeight > 0.75f, "Walking clip did not blend in");
            Check(walkLegChange > 3, "Walking clip did not animate the leg");
            walkingSpeed = game.Player.PlanarSpeed;
            Check(walkingSpeed > 1, "Walking did not move the player");
            runLegAngle = runKneeAngle = 0;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(movementKey, Key.LeftShift));
            Next();
        }
        else if (phase == 3)
        {
            var rig = game.Player.visual.Find("Procedural Limb Rig");
            foreach (Transform joint in rig.GetComponentsInChildren<Transform>())
            {
                if (joint.name == "LeftThigh" || joint.name == "RightThigh")
                    runLegAngle = Mathf.Max(runLegAngle, Quaternion.Angle(Quaternion.identity, joint.localRotation));
                if (joint.name == "LeftShin" || joint.name == "RightShin")
                    runKneeAngle = Mathf.Max(runKneeAngle, Quaternion.Angle(Quaternion.identity, joint.localRotation));
            }
            if (elapsed < 0.65) return;
            Check(game.Player.IsSprinting && animator.RunAnimationWeight > 0.8f, "Shift did not select Running");
            CheckStandingPosture();
            Check(game.Player.PlanarSpeed > walkingSpeed * 1.2f, "Sprint did not increase movement speed");
            Check(runLegAngle > 24, "Running leg pose is still restricted to a walking amplitude");
            Check(runKneeAngle > 55, "Running does not flex the knees enough to recover the foot");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Next();
        }
        else if (phase == 4 && elapsed > 0.3)
        {
            Check(!game.Player.IsSprinting && animator.RunAnimationWeight < 0.1f, "Run state did not blend out after release");
            var controller = game.Player.GetComponent<CharacterController>();
            controller.enabled = false;
            game.Player.transform.position = new Vector3(100, 0.05f, 100);
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
            CheckStandingPosture();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Running jump verification floor";
            floor.transform.position = new Vector3(100, -0.1f, 100);
            floor.transform.localScale = new Vector3(30, 0.2f, 30);
            var controller = game.Player.GetComponent<CharacterController>();
            controller.enabled = false;
            game.Player.transform.position = new Vector3(100, 0.05f, 100);
            controller.enabled = true;
            game.Player.slowdownEvery = 60;
            game.Player.InitializeSpeed();
            Physics.SyncTransforms();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            Next();
        }
        else if (phase == 6 && elapsed > 0.25)
        {
            Check(game.Player.IsSprinting, "Running jump did not start from sprint");
            jumpKneeAngle = jumpPeakWeight = 0;
            jumpSeen = false;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift, Key.Space));
            Next();
        }
        else if (phase == 7)
        {
            ObserveJump();
            if (elapsed < 0.5) return;
            Check(jumpSeen && jumpPeakWeight > 0.85f && jumpKneeAngle > 50, "Running jump did not use the adapted Jump pose");
            Check(animator.RunAnimationWeight < 0.2f, "Running overrides the airborne Jump clip");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            Next();
        }
        else if (phase == 8 && elapsed > 1)
        {
            Check(game.Player.IsGrounded && game.Player.IsSprinting && animator.RunAnimationWeight > 0.8f
                && animator.JumpAnimationWeight < 0.1f, "Landing did not return smoothly to Running");
            CheckStandingPosture();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            VerifyPrefabReplacements();
            Finish("PASS: adapted Running uses standing pose, moves thighs/knees, keeps upright torso and works with fatigue; sprint-jump selects adapted Jump, bends knees in flight and returns to Running after landing. Peak run knee: " + runKneeAngle.ToString("0.0") + "; peak jump knee: " + jumpKneeAngle.ToString("0.0") + "; prefab replacements preserved.");
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
            var animator = game.Player.GetComponent<PlayerLimbAnimator>();
            Check(animator.jumpMotion && animator.jumpMotion.name == "Jump"
                && animator.jumpMotion.text.Contains("\"standingPose\":true"), "Adapted Jump clip is not assigned");
            CheckStandingPosture();
            Check(Quaternion.Angle(chestPose, chest.localRotation) > 0.1f, "Chest froze at rest");
            Check(Quaternion.Angle(armPose, arm.localRotation) > 0.1f, "Arms froze at rest");
            Check(Quaternion.Angle(spinePose, spine.localRotation) > 0.1f, "Spine froze at rest");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            previousChest = chest.localRotation;
            largestTransitionStep = 0;
            jumpSeen = false;
            jumpKneeAngle = jumpPeakWeight = 0;
            transitionUntil = EditorApplication.timeSinceStartup + 0.5;
            Next();
        }
        else if (phase == 3)
        {
            ObserveJump();
            largestTransitionStep = Mathf.Max(largestTransitionStep, Quaternion.Angle(previousChest, chest.localRotation));
            previousChest = chest.localRotation;
            if (EditorApplication.timeSinceStartup < transitionUntil) return;
            Check(jumpSeen, "Jump input did not leave the ground");
            Check(jumpPeakWeight > 0.85f && jumpKneeAngle > 50, "Jump clip was suppressed by walking limits");
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
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Posture verification floor";
            floor.transform.position = new Vector3(100, -0.1f, 100);
            floor.transform.localScale = new Vector3(20, 0.2f, 20);
            var controller = game.Player.GetComponent<CharacterController>();
            controller.enabled = false;
            game.Player.transform.position = new Vector3(100, 0.05f, 100);
            controller.enabled = true;
            Physics.SyncTransforms();
            walkingLegStart = Array.Find(rig.GetComponentsInChildren<Transform>(), t => t.name == "LeftThigh").localRotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            Next();
        }
        else if (phase == 6 && elapsed > 0.4)
        {
            var animator = game.Player.GetComponent<PlayerLimbAnimator>();
            Check(game.Player.PlanarSpeed > 1 && animator.WalkAnimationWeight > 0.75f, "Natural walking did not activate");
            Check(Quaternion.Angle(walkingLegStart, Array.Find(rig.GetComponentsInChildren<Transform>(),
                t => t.name == "LeftThigh").localRotation) > 3, "Walking leg froze");
            CheckStandingPosture();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Finish("PASS: upright idle and walking; walking moves legs; chest, arms and spine stay alive at rest; jump transition has no large pose snap; breathing and arms continue after landing.");
            EditorApplication.isPaused = true;
        }
    }

    static void ObserveJump()
    {
        jumpSeen |= !game.Player.IsGrounded;
        jumpPeakWeight = Mathf.Max(jumpPeakWeight, game.Player.GetComponent<PlayerLimbAnimator>().JumpAnimationWeight);
        foreach (Transform joint in game.Player.visual.GetComponentsInChildren<Transform>())
            if (joint.name == "LeftShin" || joint.name == "RightShin")
                jumpKneeAngle = Mathf.Max(jumpKneeAngle, Quaternion.Angle(Quaternion.identity, joint.localRotation));
    }

    static void CheckStandingPosture()
    {
        PlayerSilhouetteVerification.CheckVisiblePosture(game.Player);
        var animator = game.Player.GetComponent<PlayerLimbAnimator>();
        Transform chest = Array.Find(game.Player.visual.GetComponentsInChildren<Transform>(), t => t.name == "Chest");
        Vector3 up = Quaternion.Inverse(game.Player.visual.rotation) * chest.up;
        float tilt = Vector3.Angle(Vector3.up, up);
        Check(tilt <= animator.maxGroundedTorsoTilt + 0.5f, "Torso is hunched: " + tilt.ToString("0.00") + " degrees");
        for (int yaw = -180; yaw <= 180; yaw += 45)
            for (int pitch = -60; pitch <= 60; pitch += 20)
            {
                Quaternion result = PlayerLimbAnimator.LimitTilt(Quaternion.Euler(pitch, yaw, 15), 4);
                Check(Vector3.Angle(Vector3.up, result * Vector3.up) <= 4.01f, "Posture correction failed at rotated heading");
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
