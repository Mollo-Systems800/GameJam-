using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MazeCanvasVerification
{
    const string Pending = "GameJam.CanvasVerification";
    static MazeBetaGame game;
    static MazeCanvasHUD hud;
    static int phase, originalSeed;
    static string originalLayout;
    static double since, started;
    static MazeCanvasVerification() { EditorApplication.update += Tick; }

    [MenuItem("GameJam/Verificar Canvas items y derrotas")]
    public static void Run()
    {
        phase = 0; started = since = EditorApplication.timeSinceStartup;
        SessionState.SetBool(Pending, true);
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }

    [MenuItem("GameJam/Previsualizar derrota en Canvas")]
    public static void DefeatPreview()
    {
        var owner = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
        if (!EditorApplication.isPlaying || !owner || !owner.IsPlaying) return;
        owner.defeatRestartDelay = 60;
        owner.Finish(false);
        UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>().Refresh();
        EditorApplication.isPaused = true;
    }

    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Next() { phase++; since = EditorApplication.timeSinceStartup; }
    static int Hearts() => hud.hearts.Count(x => x.gameObject.activeSelf);
    static string Layout() => string.Join(",", Enumerable.Range(0, game.Grid.Count)
        .Select(c => string.Concat(Enumerable.Range(0, 4).Select(d => game.Grid.IsOpen(c, d) ? "1" : "0"))));

    static void Tick()
    {
        const string request = "Logs/run-canvas-verification.request";
        if (!EditorApplication.isPlaying && !EditorApplication.isCompiling && File.Exists(request))
        {
            File.Delete(request); Run(); return;
        }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (started == 0) started = since = now;
            Check(now - started < 85, "Timeout waiting for automatic retry.");
            if (phase == 0)
            {
                game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
                hud = UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();
                if (!game || !game.Player || !hud || game.Progress == null) return;
                game.recordResults = false; game.bossGracePeriod = 10000;
                game.spadeCount = game.turretCount = 0;
                game.defeatRestartDelay = 1.25f;
                game.StartCampaign(17831);
                Check(UnityEngine.Object.FindObjectsByType<Canvas>().Length == 1, "Duplicated Canvas.");
                Check(hud.transform.Find("Stats") != null, "User Stats panel was replaced.");
                Check(hud.hearts.Length == 3 && hud.hearts.All(x => x.sprite && x.sprite.name == "heart_icon"), "Wrong heart sprite.");
                Check(hud.coinIcon.sprite && hud.coinIcon.sprite.name == "icon_coins", "Wrong coin sprite.");
                foreach (var data in game.itemPool)
                {
                    Check(data && data.itemPrefab, "Missing item/prefab reference.");
                    foreach (var renderer in data.itemPrefab.GetComponentsInChildren<Renderer>())
                        foreach (var material in renderer.sharedMaterials)
                            Check(material && material.shader.name == "Universal Render Pipeline/Lit" && material.shader.isSupported
                                && material.GetTexture("_BaseMap"), "Missing/unsupported item texture: " + data.name);
                }
                Next();
            }
            else if (phase == 1 && now - since > .4)
            {
                var items = UnityEngine.Object.FindObjectsByType<ItemPickup>();
                Check(items.Length == game.itemCount, "Generator did not spawn configured items.");
                foreach (var item in items)
                {
                    Check(item.GetComponentInChildren<Renderer>() != null, "Generated item has no prefab renderer.");
                    int c = game.WorldCell(item.transform.position);
                    Check(!game.Grid.IsParkourCell(c) && !game.ObstacleCells.Contains(c), "Item spawned in a pit.");
                }
                hud.Refresh(); Check(Hearts() == 3, "Initial hearts.");
                game.HitPlayer(1, "TEST"); hud.Refresh(); Check(Hearts() == 2, "Damage did not hide a heart.");
                game.Progress.Advance(1.3f); game.HitPlayer(1, "TEST"); hud.Refresh(); Check(Hearts() == 1, "Second heart did not disappear.");
                game.CollectItem(game.itemPool.First(x => x.itemType == ItemType.Curacion));
                hud.Refresh(); Check(Hearts() == 2, "Healing did not restore a heart.");
                // Exercise a real collectible at the player's position on the next frame.
                var pickup = new GameObject("Canvas currency verification");
                pickup.transform.SetParent(game.Player.transform.parent);
                pickup.transform.position = game.Player.transform.position + Vector3.up * .8f;
                pickup.AddComponent<CoinPickup>().Initialize(game, 0);
                Next();
            }
            else if (phase == 2 && now - since > .25)
            {
                hud.Refresh(); Check(game.Progress.Coins == 1 && hud.coinCount.text == "1", "Collected coin not reflected in HUD.");
                game.Progress.AddCoins(19); hud.Refresh(); Check(hud.coinCount.text == "20", "Coin quantity mismatch.");
                hud.shopButton.onClick.Invoke(); hud.Refresh();
                Check(game.ShopOpen && hud.shopPanel.activeSelf && !game.Player.CanMove, "Canvas shop did not pause play.");
                hud.purchaseButtons[2].onClick.Invoke(); hud.Refresh();
                Check(game.Progress.Health == 3 && Hearts() == 3 && game.Progress.Coins == 16, "Shop heal/price/HUD mismatch.");
                hud.purchaseButtons[0].onClick.Invoke();
                Check(game.Progress.SpeedSeconds > 0 && game.Progress.Coins == 10, "Canvas speed purchase.");
                hud.closeShopButton.onClick.Invoke();
                foreach (var data in game.itemPool.Where(x => x.itemType != ItemType.Curacion)) game.CollectItem(data);
                Check(game.Progress.ShieldSeconds > 0 && game.Player.GetComponent<PlayerShield>().IsShieldActive
                    && game.Player.CanDoubleJump && game.Player.SpeedMultiplier > 1, "Imported power-up effects not connected.");
                originalSeed = game.ActiveSeed; originalLayout = Layout();
                game.Finish(false); game.Finish(false); hud.Refresh();
                Check(game.LossesOnCurrentMaze == 1 && !game.RegenerateOnRetry, "Duplicate defeat/first retry counter.");
                Check(game.WaitingForRetry && hud.resultPanel.activeSelf && !hud.primaryButton.gameObject.activeSelf,
                    "Defeat should have countdown without buttons.");
                Check(hud.GetComponentsInChildren<Button>().Length == 0, "Visible defeat buttons remain.");
                Next();
            }
            else if (phase == 3 && !game.WaitingForRetry && now - since > .2)
            {
                Check(game.IsPlaying && game.ActiveSeed == originalSeed && Layout() == originalLayout && game.LossesOnCurrentMaze == 1,
                    "First defeat changed the maze or failed to restart.");
                hud.Refresh(); Check(Hearts() == 3 && hud.coinCount.text == "0", "Retry did not reset HUD.");
                game.HitPlayer(3, "TEST SECOND DEFEAT"); hud.Refresh();
                Check(game.LossesOnCurrentMaze == 2 && game.RegenerateOnRetry
                    && hud.resultDetails.text.Contains("NUEVO laberinto"), "Second loss/new maze notice missing.");
                Next();
            }
            else if (phase == 4 && !game.WaitingForRetry && now - since > .2)
            {
                Check(game.IsPlaying && game.ActiveSeed != originalSeed && Layout() != originalLayout && game.LossesOnCurrentMaze == 0,
                    "Second defeat failed to generate a new maze/reset counter.");
                Check(game.CurrentLevelIndex == 0 && game.Grid.Exits.Count == 1, "Retry changed level or exits.");
                Check(game.RestartNotice != null && game.RestartNotice.Contains("2 derrotas"), "New maze confirmation missing.");
                // A new pair must start with a same-seed retry again.
                originalSeed = game.ActiveSeed;
                game.Finish(false); Check(game.LossesOnCurrentMaze == 1 && !game.RegenerateOnRetry, "Third defeat pair did not reset.");
                Next();
            }
            else if (phase == 5 && !game.WaitingForRetry && now - since > .2)
            {
                Check(game.ActiveSeed == originalSeed, "Third loss must keep the new maze.");
                game.Finish(true); hud.Refresh();
                Check(hud.primaryButton.gameObject.activeSelf, "Victory progression button missing.");
                hud.primaryButton.onClick.Invoke(); hud.Refresh();
                Check(game.CurrentLevelIndex == 1 && game.LossesOnCurrentMaze == 0 && game.IsPlaying, "Canvas level progression/reset failed.");
                game.HitPlayer(1, "TEST"); game.Progress.AddCoins(17); hud.Refresh();
                Finish("PASS: existing Canvas/icons; 3→2→1 hearts and healing; real coin pickup/count; shop purchases; 4 textured item prefabs and effects; first/third defeats retain maze, second generates a different maze; no defeat buttons; countdown and notices; next-level progression.");
            }
        }
        catch (Exception error) { Finish("FAIL phase " + phase + ": " + error); }
    }

    static void Finish(string result)
    {
        SessionState.SetBool(Pending, false);
        File.WriteAllText("Logs/canvas-playmode-result.txt", result);
        Debug.Log("CANVAS_VERIFICATION: " + result);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        EditorApplication.isPaused = true;
    }
}
