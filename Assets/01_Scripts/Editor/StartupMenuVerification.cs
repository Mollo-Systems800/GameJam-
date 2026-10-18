using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class StartupMenuVerification
{
    const string Pending = "GameJam.StartupVerification";
    static readonly string[] Keys = { "MazeBeta.CampaignSave.Exists", "MazeBeta.CampaignSave.Seed", "MazeBeta.CampaignSave.Level", "MazeBeta.CampaignSave.MazeSeed", "MazeBeta.BestScore", "MazeBeta.BestTime",
        "MazeBeta.CampaignSave.Seconds", "MazeBeta.CampaignSave.CompletedLevels", "MazeBeta.CampaignSave.RankingEligible", CampaignRanking.StorageKey };
    static bool FloatKey(string key) => key.EndsWith("BestTime") || key.EndsWith("Seconds");
    static StartupMenu menu;
    static MazeBetaGame game;
    static int phase, retrySeed, regeneratedSeed;
    static double started, since;
    static StartupMenuVerification() { EditorApplication.update += Tick; }

    [MenuItem("GameJam/Verificar menu de inicio")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) return;
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Guarda la escena antes de verificar.");
        foreach (string key in Keys)
        {
            SessionState.SetBool(Pending + key + "Exists", PlayerPrefs.HasKey(key));
            SessionState.SetString(Pending + key + "Value", key == CampaignRanking.StorageKey ? PlayerPrefs.GetString(key) : FloatKey(key) ? PlayerPrefs.GetFloat(key).ToString("R", System.Globalization.CultureInfo.InvariantCulture) : PlayerPrefs.GetInt(key).ToString());
        }
        MazeCampaignSave.Reset();
        EditorSceneManager.OpenScene(StartupMenuSetup.ScenePath);
        phase = 0; started = since = 0;
        SessionState.SetBool(Pending, true);
        EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Next() { phase++; since = EditorApplication.timeSinceStartup; }
    static bool MenuReady()
    { menu = UnityEngine.Object.FindAnyObjectByType<StartupMenu>(); return menu && SceneManager.GetActiveScene().name == "Inicio"; }
    static bool GameReady()
    {
        game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
        if (!game || !game.Player || game.Progress == null) return false;
        game.Boss.enabled = false;
        foreach (var spade in UnityEngine.Object.FindObjectsByType<SpadeEnemy>()) spade.enabled = false;
        foreach (var turret in UnityEngine.Object.FindObjectsByType<TurretEnemy>()) turret.enabled = false;
        return true;
    }
    static void Tick()
    {
        const string request = "Logs/verify-startup.request";
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(request))
        { File.Delete(request); Run(); return; }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (started == 0) started = since = now;
            Check(now - started < 110, "Startup verification timed out.");
            if (phase == 0 && MenuReady() && now - since > .5)
            {
                Check(EditorBuildSettings.scenes[0].path == StartupMenuSetup.ScenePath, "Inicio is not first build scene.");
                Check(menu.GetComponent<StartupCharacterPortrait>().playerPrefab && menu.GetComponent<StartupCharacterPortrait>().photo.texture, "Actual player portrait missing.");
                Check(menu.mainPanel.Find("Application form/Title").GetComponent<UnityEngine.UI.Text>().text == "THE MAZE\nOF WORK", "Official game title missing.");
                Check(menu.mainPanel.Find("Plan B/Maze pencil sketch").GetComponent<StartupPaperArt>().drawing == StartupPaperArt.Drawing.Maze,
                    "Native maze sketch missing.");
                Check(UnityEngine.Object.FindObjectsByType<StartupPaperArt>().All(x => x.GetComponent<CanvasRenderer>()), "Paper renderer missing.");
                Check(!menu.loadGameButton.interactable, "Continue enabled without checkpoint.");
                menu.controlsButton.onClick.Invoke();
                Check(menu.controlsPanel.activeSelf && !menu.panelGroup.interactable, "Controls modal not isolated.");
                menu.closeControlsButton.onClick.Invoke(); Check(!menu.controlsPanel.activeSelf, "Controls close failed.");
                MazeCampaignSave.Store(2468, 5, 99118); menu.RefreshMenuState();
                Check(menu.loadGameButton.interactable && menu.checkpointStatus.text.Contains("ÁREA 2/3"), "Checkpoint status mismatch.");
                menu.newGameButton.onClick.Invoke(); Check(menu.resetConfirmation.activeSelf, "New campaign overwrite confirmation missing.");
                menu.cancelResetButton.onClick.Invoke();
                Check(MazeCampaignSave.TryLoad(out var save) && save.MazeSeed == 99118, "Cancel overwrote checkpoint.");
                menu.resetButton.onClick.Invoke(); menu.cancelResetButton.onClick.Invoke();
                Check(MazeCampaignSave.HasSave, "Cancel reset deleted save.");
                menu.loadGameButton.onClick.Invoke(); Next();
            }
            else if (phase == 1 && GameReady())
            {
                Check(game.CurrentLevelIndex == 4 && game.ActiveSeed == 99118, "Continue did not restore level/maze seed.");
                game.Finish(true);
                Check(MazeCampaignSave.TryLoad(out var save) && save.Level == 6, "Victory did not checkpoint next level.");
                var hud = UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>(); hud.Refresh();
                Check(hud.returnToMenuButton && hud.returnToMenuButton.gameObject.activeInHierarchy, "Return button missing.");
                hud.returnToMenuButton.onClick.Invoke(); Next();
            }
            else if (phase == 2 && MenuReady())
            { Check(menu.loadGameButton.interactable && !menu.resetConfirmation.activeSelf, "Return to menu state."); menu.loadGameButton.onClick.Invoke(); Next(); }
            else if (phase == 3 && GameReady())
            {
                Check(game.CurrentLevelIndex == 5, "Continue after win did not advance level.");
                retrySeed = game.ActiveSeed; game.defeatRestartDelay = 1;
                game.Finish(false); Next();
            }
            else if (phase == 4 && GameReady() && !game.WaitingForRetry && now - since > .2)
            { Check(game.ActiveSeed == retrySeed, "First loss changed maze."); game.Finish(false); Next(); }
            else if (phase == 5 && GameReady() && !game.WaitingForRetry && now - since > .2)
            {
                regeneratedSeed = game.ActiveSeed;
                Check(regeneratedSeed != retrySeed && MazeCampaignSave.TryLoad(out var save) && save.MazeSeed == regeneratedSeed, "Regenerated maze checkpoint missing.");
                StartupMenu.ReturnToStart(); Next();
            }
            else if (phase == 6 && MenuReady()) { menu.loadGameButton.onClick.Invoke(); Next(); }
            else if (phase == 7 && GameReady())
            {
                Check(game.CurrentLevelIndex == 5 && game.ActiveSeed == regeneratedSeed, "Continue lost regenerated maze.");
                game.StartCampaign(7311, 9); game.Finish(true);
                Check(!MazeCampaignSave.HasSave, "Completed campaign still resumable.");
                StartupMenu.ReturnToStart(); Next();
            }
            else if (phase == 8 && MenuReady())
            {
                Check(!menu.loadGameButton.interactable, "Continue enabled on completed campaign.");
                menu.resetButton.onClick.Invoke(); menu.confirmResetButton.onClick.Invoke();
                Check(!MazeCampaignSave.HasSave && !MazeCampaignSave.HasRecords, "Confirmed reset failed.");
                menu.newGameButton.onClick.Invoke(); Next();
            }
            else if (phase == 9 && GameReady())
            {
                Check(game.CurrentLevelIndex == 0 && MazeCampaignSave.HasSave, "New game did not start/checkpoint level 1.");
                StartupMenu.ReturnToStart(); Next();
            }
            else if (phase == 10 && MenuReady())
            {
                RestorePrefs(); menu.RefreshMenuState();
                Finish("PASS: supplied artwork, controls modal, save/overwrite/reset confirmations, new game, level/seed continue, victory checkpoint, Canvas return, two-loss regeneration checkpoint/resume, completed campaign, reset; original preferences restored.");
            }
        }
        catch (Exception error) { RestorePrefs(); Finish("FAIL phase " + phase + ": " + error); }
    }
    static void RestorePrefs()
    {
        foreach (string key in Keys)
        {
            if (!SessionState.GetBool(Pending + key + "Exists", false)) PlayerPrefs.DeleteKey(key);
            else if (key == CampaignRanking.StorageKey) PlayerPrefs.SetString(key, SessionState.GetString(Pending + key + "Value", "{}"));
            else if (FloatKey(key)) PlayerPrefs.SetFloat(key, float.Parse(SessionState.GetString(Pending + key + "Value", "0"), System.Globalization.CultureInfo.InvariantCulture));
            else PlayerPrefs.SetInt(key, int.Parse(SessionState.GetString(Pending + key + "Value", "0")));
        }
        PlayerPrefs.Save();
    }
    static void Finish(string result)
    {
        SessionState.SetBool(Pending, false); File.WriteAllText("Logs/startup-playmode-result.txt", result);
        Debug.Log("STARTUP_VERIFICATION: " + result); EditorApplication.isPaused = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
}
