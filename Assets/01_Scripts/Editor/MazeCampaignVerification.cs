using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MazeCampaignVerification
{
    const string Pending = "GameJam.CampaignVerification";
    static MazeBetaGame game;
    static int level = -1;
    static double started;
    static MazeCampaignVerification() { EditorApplication.update += Tick; }

    [MenuItem("GameJam/Verificar campana")]
    public static void Run()
    {
        level = -1;
        started = 0;
        SessionState.SetBool(Pending, true);
        EditorApplication.isPaused = false;
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) { started = EditorApplication.timeSinceStartup; level = -1; }
            if (EditorApplication.timeSinceStartup - started > 50) throw new TimeoutException("Campaign verification timed out");
            if (level < 0)
            {
                game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
                if (!game || !game.Player) return;
                game.recordResults = false;
                game.bossGracePeriod = 10000;
                game.spadeCount = game.turretCount = 0;
                game.StartCampaign(1729);
                level = 0;
                return;
            }
            Check(game.CurrentLevelIndex == level && game.CurrentAreaIndex == level / 3 && game.CurrentLevelInArea == level % 3, "Incorrect area/level");
            var grid = game.Grid;
            Check(grid.Exits.Count == 1 && grid.ExtraConnectionCount > 0, "Expected one exit and alternate paths");
            Check(grid.ExitCell % grid.Width + grid.ExitCell / grid.Width >= Mathf.CeilToInt((grid.Width + grid.Height - 2) * game.minimumExitDistanceRatio), "Exit near spawn");
            Check(!game.AdvanceLevel(), "Advanced without escaping");
            var wall = game.ActiveArea.wallPrefab ? game.ActiveArea.wallPrefab : game.wallPrefab;
            var wallMeshes = wall.GetComponentsInChildren<MeshFilter>().Select(m => m.sharedMesh).ToArray();
            Check(wallMeshes.Length > 0 && game.GetComponentsInChildren<MeshFilter>().Any(m => wallMeshes.Contains(m.sharedMesh)), "Area wall prefab missing");
            int oldSeed = game.ActiveSeed;
            game.Begin(oldSeed);
            Check(game.CurrentLevelIndex == level && game.ActiveSeed == oldSeed, "Retry lost campaign stage");
            var direction = new Vector3(MazeGrid.DX[grid.ExitDirection], 0, MazeGrid.DY[grid.ExitDirection]);
            var controller = game.Player.GetComponent<CharacterController>();
            controller.enabled = false;
            game.Player.transform.position = game.CellPosition(grid.ExitCell) + direction * (game.cellSize * 0.5f + 0.5f) + Vector3.up * 0.1f;
            game.SendMessage("Update");
            Check(game.HasWon, "Crossing sole exit failed");
            if (level == 8)
            {
                Check(game.CampaignComplete && !game.AdvanceLevel(), "Campaign did not finish at level 9");
                Finish("PASS: 9 playable levels, 3 area prefab kits, distant sole exit with loops, retry preserves stage, real exit crossing and final completion.");
            }
            else
            {
                Check(game.AdvanceLevel(), "Next level unavailable after win");
                level++;
            }
        }
        catch (Exception error) { Finish("FAIL: " + error); }
    }

    static void Finish(string result)
    {
        SessionState.SetBool(Pending, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/campaign-playmode-result.txt", result);
        Debug.Log(result);
        EditorApplication.isPaused = true;
    }
}
