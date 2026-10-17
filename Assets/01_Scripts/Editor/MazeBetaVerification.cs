using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Run with Unity -batchmode -executeMethod MazeBetaVerification.Run (without -quit).
[InitializeOnLoad]
public static class MazeBetaVerification
{
    const string Pending = "GameJam.BetaVerification";
    static int phase;
    static double phaseTime;
    static Vector3 start;
    static Vector3 direction;
    static Keyboard keyboard;
    static MazeBetaGame game;
    static int pickupCount;
    static int oldSeed;

    static MazeBetaVerification()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    static void OnLog(string message, string trace, LogType type)
    {
        if (SessionState.GetBool(Pending, false) && (type == LogType.Exception || type == LogType.Error))
        {
            SessionState.SetBool(Pending, false);
            Debug.LogWarning("BETA_VERIFICATION_FAILED: " + message);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("GameJam/Verificar topologia de laberintos")]
    public static void VerifyTopology()
    {
        for (int seed = -150; seed < 150; seed++)
        {
            int width = 3 + Math.Abs(seed % 14), height = 3 + Math.Abs(seed % 11);
            var grid = new MazeGrid(width, height, seed);
            var twin = new MazeGrid(width, height, seed);
            int edges = 0, exits = 0;
            Require(grid.DistancesFrom(0).All(d => d >= 0), "Disconnected cells");
            for (int c = 0; c < grid.Count; c++)
                for (int d = 0; d < 4; d++)
                {
                    Require(grid.IsOpen(c, d) == twin.IsOpen(c, d), "Seed is not deterministic");
                    if (!grid.IsOpen(c, d)) continue;
                    int next = grid.Neighbor(c, d);
                    if (next < 0) { exits++; continue; }
                    Require(grid.IsOpen(next, (d + 2) % 4), "Asymmetric passage");
                    edges++;
                }
            Require(exits == 1, "Expected exactly one outer exit");
            Require(edges / 2 == grid.Count - 1, "Maze must be a spanning tree");
            int current = 0;
            foreach (int next in grid.FindPath(0, grid.ExitCell))
            {
                Require(Enumerable.Range(0, 4).Any(d => grid.Neighbor(current, d) == next && grid.IsOpen(current, d)),
                    "Path crosses a wall");
                current = next;
            }
            Require(current == grid.ExitCell, "No valid exit path");
        }
        Debug.Log("BETA_TOPOLOGY_OK: 300 seeds; connectivity, one exit, tree, reproducibility, BFS paths.");
    }

    public static void Run()
    {
        try
        {
            VerifyTopology();
            MazeBetaSetup.Configure();
            SessionState.SetBool(Pending, true);
            SessionState.SetFloat("GameJam.VerificationStart", (float)EditorApplication.timeSinceStartup);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void Next()
    {
        phase++;
        phaseTime = EditorApplication.timeSinceStartup;
    }

    static void Teleport(Vector3 position)
    {
        var controller = game.Player.GetComponent<CharacterController>();
        controller.enabled = false;
        game.Player.transform.position = position;
        controller.enabled = true;
        Physics.SyncTransforms();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat("GameJam.VerificationStart", 0) > 120)
                throw new TimeoutException("Play mode verification timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (phase == 0)
            {
                game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
                if (!game || !game.Player || !game.Boss) return;
                Require(game.playerPrefab && game.wallPrefab && game.floorPrefab && game.platformPrefab && game.bossVisualPrefab,
                    "Missing scene references");
                Require(game.playerPrefab.GetComponent<Player>() && game.playerPrefab.GetComponent<CharacterController>(),
                    "Player Prefab must contain the player controller and CharacterController");
                Require(game.Grid.Exits.Count == game.exitCount, "Wrong configured exit count");
                Require(game.Player.visual.GetComponentsInChildren<Renderer>().Length > 0, "Missing character renderer");
                Require(Physics.Raycast(Vector3.up, Vector3.down, 2), "Start floor missing");
                game.Boss.gracePeriod = 1000;
                keyboard = InputSystem.AddDevice<Keyboard>();
                start = game.Player.transform.position;
                int d = Enumerable.Range(0, 4).First(i => game.Grid.IsOpen(0, i));
                direction = new Vector3(MazeGrid.DX[d], 0, MazeGrid.DY[d]);
                Key key = d == 0 ? Key.W : d == 1 ? Key.D : d == 2 ? Key.S : Key.A;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                Next();
            }
            else if (phase == 1 && EditorApplication.timeSinceStartup - phaseTime > 0.3)
            {
                Require(Vector3.Dot(game.Player.transform.position - start, direction) > 0.1f, "Player input did not move character");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Teleport(Vector3.up * 0.1f);
                game.Player.CanMove = false;
                game.Player.GetComponent<CharacterController>().Move(Vector3.back * game.cellSize);
                Require(game.Player.transform.position.z > -game.cellSize * 0.5f, "Player crossed a boundary wall");
                var pickup = UnityEngine.Object.FindAnyObjectByType<PillPickup>();
                Require(pickup, "No pills spawned");
                pickupCount = game.Player.Collected;
                Teleport(new Vector3(pickup.transform.position.x, 0.1f, pickup.transform.position.z));
                Next();
            }
            else if (phase == 2 && EditorApplication.timeSinceStartup - phaseTime > 0.1)
            {
                Require(game.Player.Collected == pickupCount + 1, "Pill pickup did not restore player");
                Require(Mathf.Approximately(game.Player.CurrentSpeed, game.playerSpeed), "Pills did not restore speed");
                game.Player.CanMove = true;
                game.Player.slowdownEvery = 0.1f;
                Next();
            }
            else if (phase == 3 && EditorApplication.timeSinceStartup - phaseTime > 0.3)
            {
                Require(game.Player.CurrentSpeed < game.playerSpeed, "Fatigue timer did not reduce speed");
                game.Player.CollectPills();
                Require(Mathf.Approximately(game.Player.CurrentSpeed, game.playerSpeed), "Pill restoration failed");
                game.Player.CanMove = false;
                game.Boss.increaseEvery = 0.1f;
                game.Boss.gracePeriod = 0;
                start = game.Boss.transform.position;
                Next();
            }
            else if (phase == 4 && EditorApplication.timeSinceStartup - phaseTime > 0.3)
            {
                Require(game.Boss.CurrentSpeed > game.Boss.startingSpeed, "Boss timer did not accelerate");
                Require(Vector3.Distance(start, game.Boss.transform.position) > 0.05f, "Boss did not pursue");
                game.Boss.IsActive = false;
                Vector3 exit = game.CellPosition(game.Grid.ExitCell);
                var exitDirection = new Vector3(MazeGrid.DX[game.Grid.ExitDirection], 0, MazeGrid.DY[game.Grid.ExitDirection]);
                Teleport(exit + exitDirection * game.cellSize * 0.8f + Vector3.up * 0.1f);
                Next();
            }
            else if (phase == 5 && EditorApplication.timeSinceStartup - phaseTime > 0.1)
            {
                Require(game.HasWon && !game.IsPlaying, "Exit did not finish game");
                oldSeed = game.ActiveSeed;
                game.Begin(oldSeed);
                Require(game.ActiveSeed == oldSeed && game.IsPlaying && game.Player.Collected == 0, "Same-seed restart failed");
                game.Begin(oldSeed ^ 9173);
                Require(game.ActiveSeed != oldSeed && game.IsPlaying, "New-seed restart failed");
                game.Boss.gracePeriod = 1000;
                Teleport(game.Boss.transform.position + Vector3.up * 0.1f);
                game.Boss.gracePeriod = 0;
                game.Player.CanMove = false;
                Next();
            }
            else if (phase == 6 && EditorApplication.timeSinceStartup - phaseTime > 0.1)
            {
                Require(!game.IsPlaying && !game.HasWon, "Boss contact did not cause defeat");
                game.Begin(oldSeed);
                game.Boss.gracePeriod = 1000;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Next();
            }
            else if (phase == 7 && EditorApplication.timeSinceStartup - phaseTime > 0.25)
            {
                Require(game.Player.IsGrounded, "Player did not land on spawn floor");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                Next();
            }
            else if (phase == 8 && EditorApplication.timeSinceStartup - phaseTime > 0.15)
            {
                Require(game.Player.transform.position.y > 0.3f, "Jump input did not lift player");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Next();
            }
            else if (phase == 9 && EditorApplication.timeSinceStartup - phaseTime > 1.2)
            {
                Require(game.Player.IsGrounded, "Player did not land after jump");
                Require(game.ObstacleCells.Count > 0, "No jumping sections generated");
                int cell = game.ObstacleCells[0];
                Vector3 centre = game.CellPosition(cell);
                Vector3 axis = game.Grid.IsOpen(cell, 0) ? Vector3.forward : Vector3.right;
                Require(Physics.Raycast(centre + Vector3.up, Vector3.down, 2), "Stepping platform missing");
                Require(!Physics.Raycast(centre + axis * game.cellSize * 0.22f + Vector3.up, Vector3.down, 2),
                    "Gap has an invisible full floor collider");
                Teleport(centre + axis * game.cellSize * 0.22f + Vector3.up * 0.1f);
                Next();
            }
            else if (phase == 10 && EditorApplication.timeSinceStartup - phaseTime > 1.2)
            {
                Require(!game.IsPlaying && !game.HasWon && game.DefeatReason.Contains("PRECIPICIO"), "Falling did not cause defeat");
                // Every exterior opening must independently end the game.
                for (int i = 0; i < game.Grid.Exits.Count; i++)
                {
                    game.Begin(oldSeed);
                    game.Boss.IsActive = false;
                    var exit = game.Grid.Exits[i];
                    var direction = new Vector3(MazeGrid.DX[exit.Direction], 0, MazeGrid.DY[exit.Direction]);
                    Teleport(game.CellPosition(exit.Cell) + direction * game.cellSize * 0.8f + Vector3.up * 0.1f);
                    game.SendMessage("Update");
                    Require(game.HasWon && !game.IsPlaying, "One of the exits did not win");
                }
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                SessionState.SetBool(Pending, false);
                Debug.Log("BETA_PLAYMODE_OK: input, collision, pickups, fatigue, boss, jump/landing, real floor gaps, falling, all exits, both restarts.");
                EditorApplication.Exit(0);
            }
        }
        catch (Exception error)
        {
            SessionState.SetBool(Pending, false);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
