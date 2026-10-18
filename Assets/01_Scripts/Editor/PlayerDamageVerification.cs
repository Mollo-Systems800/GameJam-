using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// An isolated arena above the maze exercises real CharacterController collisions
// and the runtime skinned renderers. No campaign, ranking or audio settings saved.
[InitializeOnLoad]
public static class PlayerDamageVerification
{
    const string Key = "GameJam.PlayerDamageVerification";
    const string Request = "Logs/verify-player-damage.request";
    static MazeBetaGame game;
    static Keyboard keyboard;
    static int phase;
    static double started, since;
    static Vector3 origin, pausedPosition;
    static float pausedRecoil;
    static GameObject wall;
    static Renderer[] surfaces;
    static Material[] materials;
    static Color[] colors;
    static float outputVolume;
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    static PlayerDamageVerification()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => {
            SessionState.SetBool(Key + "Ready", state == PlayModeStateChange.EnteredPlayMode);
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + "Restore", false))
            {
                SessionState.SetBool(Key + "Restore", false);
                EditorApplication.delayCall += () => EditorSceneManager.OpenScene(SessionState.GetString(Key + "Scene", "Assets/00_Scenes/Inicio.unity"));
            }
        };
    }

    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Next() { phase++; since = EditorApplication.timeSinceStartup; }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.isDirty) return;
            try { File.Delete(Request); } catch (IOException) { return; }
            SessionState.SetString(Key + "Scene", scene.path);
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + "Restore", true);
            SessionState.SetBool(Key + "Ready", false);
            phase = 0; started = since = 0;
            Application.runInBackground = true;
            EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
            EditorApplication.isPlaying = true;
            return;
        }
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || !SessionState.GetBool(Key + "Ready", false)) return;
        try
        {
            if (started == 0) started = since = EditorApplication.timeSinceStartup;
            Check(EditorApplication.timeSinceStartup - started < 60, "Damage verification timed out");
            double elapsed = EditorApplication.timeSinceStartup - since;
            if (phase == 0)
            {
                game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
                if (!game || game.Grid == null || !game.Player || !game.Player.GetComponent<PlayerLimbAnimator>().IsReady) return;
                game.recordResults = false;
                game.playOutcomeCinematics = false;
                game.width = game.height = 8;
                game.spadeCount = game.turretCount = 0;
                game.bossGracePeriod = 100000;
                game.StartCampaign(71952);
                game.Boss.enabled = false;
                outputVolume = AudioListener.volume; AudioListener.volume = 0;
                var arena = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arena.name = "Temporary damage test arena";
                arena.transform.position = new Vector3(0, 99.8f, 0);
                arena.transform.localScale = new Vector3(20, .4f, 20);
                var light = new GameObject("Temporary damage test light").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1;
                light.transform.rotation = Quaternion.Euler(40, -25, 0);
                var controller = game.Player.GetComponent<CharacterController>();
                controller.enabled = false; game.Player.transform.position = new Vector3(0, 100.1f, 0); controller.enabled = true;
                var camera = Camera.main;
                camera.GetComponent<PlayerFollowCamera>().enabled = false;
                camera.transform.position = new Vector3(0, 102.6f, -4);
                camera.transform.LookAt(new Vector3(0, 101, 0));
                keyboard = InputSystem.AddDevice<Keyboard>();
                Next(); return;
            }
            if (phase == 1)
            {
                if (elapsed < .7 || !game.Player.GetComponent<PlayerLimbAnimator>().IsReady) return;
                game.Player.visual.rotation = Quaternion.identity;
                surfaces = game.Player.visual.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.sharedMaterial && r.sharedMaterial.HasProperty(ColorId)).ToArray();
                Check(surfaces.Length > 0 && surfaces.Any(r => r is SkinnedMeshRenderer), "No visible runtime skin to tint");
                materials = surfaces.Select(r => r.sharedMaterial).ToArray();
                colors = materials.Select(m => m.GetColor(ColorId)).ToArray();
                origin = game.Player.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.W));
                Check(game.HitPlayer(1, "DAMAGE TEST", hitSource: origin + Vector3.forward), "Hit rejected");
                Check(game.Progress.Health == 2 && game.Player.DamageFlashActive && game.Player.IsRecoiling, "Hit feedback not activated");
                Check(MazeAudioDirector.Instance.LastCue == MazeSound.Hurt, "Pain cue missing");
                var clip = MazeAudioDirector.Instance.library.cues.First(c => c.sound == MazeSound.Hurt).variations[0];
                Check(clip.length > .55f && clip.length < .7f, "New pain vocal not imported");
                Check(surfaces.All(IsRed), "Red flash missing on visible skin");
                var position = game.Player.transform.position;
                Check(!game.HitPlayer(1, "INVULNERABLE TEST"), "Repeated contact bypassed invulnerability");
                Check(game.Player.transform.position == position && game.Progress.Health == 2, "Ignored hit applied another impulse");
                Next(); return;
            }
            if (phase == 2)
            {
                if (elapsed < .09) return;
                Check(game.Player.transform.position.z < origin.z - .22f, "Recoil failed against held forward input");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Capture(); Next(); return;
            }
            if (phase == 3)
            {
                if (elapsed < .75) return;
                Check(!game.Player.IsRecoiling && !game.Player.DamageFlashActive, "Feedback did not finish");
                CheckRestored();
                game.Progress.ApplyItem(ItemType.Escudo, 2, 1);
                origin = game.Player.transform.position;
                Check(!game.HitPlayer(1, "SHIELD TEST"), "Shield did not block hit");
                Check(game.Player.transform.position == origin && !game.Player.DamageFlashActive, "Shielded hit triggered injury");
                game.Progress.Advance(2.1f);
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Temporary recoil collision wall";
                wall.transform.position = origin + new Vector3(0, 1.5f, -.85f);
                wall.transform.localScale = new Vector3(4, 3, .2f);
                Physics.SyncTransforms();
                Check(game.HitPlayer(1, "WALL TEST", hitSource: origin + Vector3.forward), "Wall fixture hit rejected");
                Next(); return;
            }
            if (phase == 4)
            {
                if (elapsed < .06) return;
                game.SetShopOpen(true);
                pausedPosition = game.Player.transform.position;
                pausedRecoil = RecoilRemaining();
                Next(); return;
            }
            if (phase == 5)
            {
                if (elapsed < .35) return;
                Check((game.Player.transform.position - pausedPosition).sqrMagnitude < .00001f, "Paused recoil moved player");
                Check(Mathf.Abs(RecoilRemaining() - pausedRecoil) < .0001f && game.Player.DamageFlashActive, "Pause consumed damage reaction");
                game.SetShopOpen(false); Next(); return;
            }
            if (phase == 6)
            {
                if (elapsed < .75) return;
                var body = game.Player.GetComponent<CharacterController>().bounds;
                Check(body.min.z >= wall.GetComponent<Collider>().bounds.max.z - .06f, "Recoil passed through wall");
                Check(body.min.y > 99.9f, "Recoil moved below floor");
                CheckRestored();
                game.Progress.Advance(2);
                Check(game.HitPlayer(3, "LETHAL TEST", true, game.Player.transform.position + Vector3.forward), "Lethal hit rejected");
                Check(!game.IsPlaying && game.DefeatedByBoss && game.Player.DamageFlashActive, "Lethal feedback or defeat flow missing");
                Next(); return;
            }
            if (phase == 7)
            {
                if (elapsed < .75) return;
                CheckRestored();
                Finish("PASS: native skinned-mesh red flash and material restoration; recoil against held W; invulnerability; shield; pause/resume; wall/floor collisions; voiced pain import/cue; lethal boss defeat. No saves changed.");
            }
        }
        catch (Exception error) { Finish("FAIL: " + error); }
    }

    static float RecoilRemaining() => (float)typeof(Player).GetField("recoilRemaining", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game.Player);
    static bool IsRed(Renderer renderer)
    {
        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, 0);
        Color color = block.GetColor(ColorId);
        return color.r > .9f && color.g < .08f && color.b < .08f;
    }
    static void CheckRestored()
    {
        for (int i = 0; i < surfaces.Length; i++)
        {
            var block = new MaterialPropertyBlock(); surfaces[i].GetPropertyBlock(block, 0);
            Check(block.isEmpty, "Tint property block not removed");
            Check(surfaces[i].sharedMaterial == materials[i] && materials[i].GetColor(ColorId) == colors[i], "Shared material changed");
        }
    }
    static void Capture()
    {
        var camera = Camera.main; var previous = camera.targetTexture; var active = RenderTexture.active;
        var target = new RenderTexture(960, 720, 24); var picture = new Texture2D(960, 720, TextureFormat.RGB24, false);
        try {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            picture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); picture.Apply();
            File.WriteAllBytes("Logs/player-damage-flash.png", picture.EncodeToPNG());
        }
        finally {
            camera.targetTexture = previous; RenderTexture.active = active;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(picture);
        }
    }
    static void Finish(string result)
    {
        SessionState.SetBool(Key, false);
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        keyboard = null;
        AudioListener.volume = outputVolume;
        File.WriteAllText("Logs/player-damage-verification-result.txt", result);
        EditorApplication.isPlaying = false;
    }
}
