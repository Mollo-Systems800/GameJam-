using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MazePresentationVerification
{
    const string Pending = "GameJam.PresentationVerification";
    static MazePresentationVerification() { EditorApplication.update += Tick; }
    public static void SetupAndVerify()
    {
        MazePresentationSetup.Apply(); VerifyData(); VerifyAssets();
        if (Application.isBatchMode) Debug.Log("PRESENTATION_ASSETS: PASS");
    }
    public static void RunPlay()
    {
        VerifyData(); VerifyAssets();
        EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
        var game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
        game.width = game.height = 7; game.coinCount = 2; game.pillCount = 2; game.spadeCount = 2; game.turretCount = 1;
        game.chairCount = 0; game.startLevel = 1; game.randomSeed = false; game.seed = 123; game.bossGracePeriod = 10000;
        SessionState.SetBool(Pending, true); SessionState.SetInt(Pending + "Level", 0);
        SessionState.SetFloat(Pending + "Started", (float)EditorApplication.timeSinceStartup);
        EditorApplication.isPlaying = true;
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void VerifyData()
    {
        string key = "MazeBeta.Verification.Ranking." + Guid.NewGuid();
        try
        {
            Require(CampaignRanking.Read(key).Count == 0, "New ranking is not empty");
            Require(!CampaignRanking.TrySave("   ", 50, key), "Blank name accepted");
            Require(!CampaignRanking.TrySave("Ana", float.NaN, key), "NaN time accepted");
            Require(CampaignRanking.TrySave("José", 300.25f, key), "First completed-campaign record rejected");
            Require(CampaignRanking.TrySave("Slower", 301, key) && CampaignRanking.TrySave("Tie", 300.25f, key), "Slower/tied finisher rejected");
            Require(CampaignRanking.TrySave("Lucía", 280.5f, key), "Faster record rejected");
            Require(CampaignRanking.Read(key)[0].name == "Lucía" && CampaignRanking.Read(key)[1].name == "José", "Ranking not persisted/sorted");
            for (int i = 0; i < 15; i++) Require(CampaignRanking.TrySave("Player " + i, 250 - i, key), "Improving record rejected");
            Require(CampaignRanking.Read(key).Count == 19, "Finishers were lost after the first page");
            Require(CampaignRanking.CleanName(" <Ana>\n ") == "Ana", "Unsafe/invalid name formatting");
            Require(CampaignRanking.Format(125.25f) == "02:05.25", "Time format incorrect");
            PlayerPrefs.SetString(key, "broken json"); Require(CampaignRanking.Read(key).Count == 0, "Corrupt save breaks ranking");
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
    }
    static Bounds VisualBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(); Require(renderers.Length > 0, "Missing visual");
        Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds); return bounds;
    }
    static void VerifyAssets()
    {
        EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
        var game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
        Require(game.bossVisualPrefab && game.pillVisualPrefab, "Printed prefabs not assigned");
        var root = new GameObject("Visual measurement");
        var boss = MazeBetaGame.FitVisual(game.bossVisualPrefab, root.transform, new Vector3(1.44f,1.8f,.18f), true);
        Require(Mathf.Abs(VisualBounds(boss).size.y - 1.8f) < .001f, "Boss does not match player height");
        Require(boss.GetComponent<PaperBossFacing>() && boss.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterial.mainTexture), "Printed boss is missing image/facing");
        UnityEngine.Object.DestroyImmediate(root);
        var pill = game.pillVisualPrefab.GetComponentInChildren<MeshFilter>().sharedMesh;
        Require(pill.bounds.size.z > .1f && pill.uv.All(p=>p.x>=0 && p.x<=1 && p.y>=0 && p.y<=1), "Pill box has no depth or invalid projective UVs");
        var shovel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Pala.prefab");
        var colors = shovel.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Select(m=>m.GetColor("_BaseColor")).Distinct().ToArray();
        Require(colors.Length >= 2 && colors.All(c=>c != Color.white), "Shovel wood/steel colors missing");
        var hud = UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();
        var form = hud.GetComponent<CampaignRecordForm>();
        Require(form && form.hud == hud && form.playerName.characterLimit == 20 && form.saveButton && form.form.transform.IsChildOf(hud.resultPanel.transform), "Record form is not inside the original Canvas");
        EditorSceneManager.OpenScene("Assets/00_Scenes/Inicio.unity");
        var menu = UnityEngine.Object.FindAnyObjectByType<StartupMenu>();
        Require(menu.rankingButton && menu.rankingPanel && menu.rankingNames && menu.rankingTimes && menu.closeRankingButton && menu.finalistsButton && menu.finalistsSummary && menu.previousRankingPage && menu.nextRankingPage, "Start-menu finalists section or pagination missing");
        menu.ShowRanking(); Require(menu.rankingPanel.activeSelf && !menu.panelGroup.interactable, "Ranking modal does not open");
        menu.CloseRanking(); Require(!menu.rankingPanel.activeSelf && menu.panelGroup.interactable, "Ranking modal does not close");
        var rows = new[] {menu.newGameButton,menu.loadGameButton,menu.controlsButton,menu.rankingButton}.Select(b=>b.GetComponent<RectTransform>()).ToArray();
        for (int i=1;i<rows.Length;i++) Require(-rows[i].anchoredPosition.y >= -rows[i-1].anchoredPosition.y+rows[i-1].sizeDelta.y, "Menu action rows overlap");
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(Pending+"Started",0)<110, "Play verification timeout");
            var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
            if (!game || !game.Player || !game.Player.GetComponent<PlayerLimbAnimator>().IsReady) return;
            game.recordResults=false; game.Player.CanMove=false; game.Boss.enabled=false;
            game.playOutcomeCinematics=false;
            foreach(var enemy in UnityEngine.Object.FindObjectsByType<SpadeEnemy>()) enemy.enabled=false;
            foreach(var turret in UnityEngine.Object.FindObjectsByType<TurretEnemy>()) turret.enabled=false;
            int level=SessionState.GetInt(Pending+"Level",0);
            Require(game.ResidentCells.Count >= 3 && (game.ActiveArea.seatedResidentPrefab || game.seatedResidentPrefab) &&
                UnityEngine.Object.FindObjectsByType<Transform>().Count(t => t.name == "Abuela con silla") >= 3,
                "Supplied seated resident/chair assembly missing at level " + (level + 1));
            if (level==0)
            {
                Require(Mathf.Abs(VisualBounds(game.Boss.gameObject).size.y-game.Player.GetComponent<CharacterController>().height)<.01f,"Runtime boss height mismatch");
                var labels=game.GetComponentsInChildren<TextMesh>().Select(t=>t.text.ToUpperInvariant()).ToArray();
                Require(!labels.Any(t=>t.Contains("SALIDA") || t.Contains("SALTA") || t.Contains("PASTILLAS") || t.Contains("SE BUSCA")),"Removed world labels still visible");
                var pills=UnityEngine.Object.FindObjectsByType<PillPickup>(); Require(pills.Length==2 && pills.All(p=>p.GetComponentInChildren<Renderer>().sharedMaterial.mainTexture),"Runtime pill images missing");
            }
            game.CampaignClock.Advance(10); game.Finish(true);
            Require(game.CampaignClock.CompletedLevels==level+1,"Campaign victory was not counted exactly once");
            if(level<8)
            {
                Require(!game.CampaignComplete && !game.HasPendingCampaignRecord,"Ranking unlocked early");
                float before=game.CampaignSeconds; Require(game.AdvanceLevel(),"Next campaign level failed");
                Require(game.CampaignSeconds==before,"Level change resets campaign clock");
                SessionState.SetInt(Pending+"Level",level+1);
            }
            else
            {
                Require(game.CampaignComplete && game.CampaignClock.Finished && game.CampaignSeconds>=90,"Nine-level campaign did not complete with total time");
                Require(!game.HasPendingCampaignRecord,"Non-recording verification saved a real player record");
                string key="MazeBeta.Verification.FinishedCampaign."+Guid.NewGuid();
                try
                {
                    Require(CampaignRanking.TrySave("Earlier winner", 1, key), "Ranking fixture failed");
                    game.recordResults=true;
                    typeof(MazeBetaGame).GetMethod("ConsiderCampaignRecord",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(game,new object[]{key});
                    Require(game.HasPendingCampaignRecord && !game.IsCampaignBest,"Slower completed campaign was rejected or announced as a record");
                    var hud=UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();hud.Refresh();
                    var entry=hud.GetComponent<CampaignRecordForm>();entry.playerName.text="";entry.saveButton.onClick.Invoke();
                    Require(game.HasPendingCampaignRecord && CampaignRanking.Read(key).Count==1,"Blank name saved through record form");
                    entry.playerName.text="Prueba Ñ";entry.saveButton.onClick.Invoke();
                    Require(!game.HasPendingCampaignRecord && CampaignRanking.Read(key).Count==2 && CampaignRanking.Read(key)[1].name=="Prueba Ñ","Final campaign name/record form failed");
                    entry.saveButton.onClick.Invoke();Require(CampaignRanking.Read(key).Count==2,"Record form saved duplicate entry");
                }
                finally{game.recordResults=false;PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
                Finish("PASS: source images, boss height, 3D pill UVs, shovel colors, removed labels, original Canvas record form, start-menu ranking, persistent sorted records, nine sequential levels and cumulative campaign time.");
            }
        }
        catch(Exception error){Finish("FAIL: "+error);}
    }
    static void Finish(string report)
    {
        SessionState.SetBool(Pending,false);File.WriteAllText("Logs/presentation-play-result.txt",report);Debug.Log(report);
        EditorApplication.isPlaying=false;
        if(Application.isBatchMode) EditorApplication.delayCall+=()=>EditorApplication.Exit(report.StartsWith("PASS")?0:1);
    }
}
