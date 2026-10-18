using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class StartupMenuSetup
{
    public const string ScenePath = "Assets/00_Scenes/Inicio.unity";
    static readonly Color Ink = new Color(.045f, .065f, .08f, .94f);
    static readonly Color Gold = new Color(.85f, .72f, .35f);
    static readonly Color Slate = new Color(.17f, .24f, .27f);
    static readonly Color Cream = new Color(.96f, .94f, .87f);
    static readonly Color Muted = new Color(.66f, .74f, .75f);
    static StartupMenuSetup() { EditorApplication.update += RequestedSetup; }
    static void RequestedSetup()
    {
        const string request = "Logs/configure-startup.request";
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (SceneManager.GetActiveScene().isDirty) return;
        File.Delete(request);
        try { Configure(); File.WriteAllText("Logs/startup-setup-result.txt", "PASS: The Maze of work dossier scene configured; native paper artwork and real player portrait; Inicio is first build scene."); }
        catch (Exception error) { File.WriteAllText("Logs/startup-setup-result.txt", error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("GameJam/Configurar menu en Inicio")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Sal de Play para configurar las escenas.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Guarda primero los cambios de la escena abierta.");
        LinkGameMenu();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var menu = UnityEngine.Object.FindAnyObjectByType<StartupMenu>();
        var canvas = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).First(x => x.name == "UI");
        if (!menu) throw new InvalidOperationException("Falta Start Menu en la escena del paquete.");
        menu.menuCanvas = canvas; menu.gameScene = "BetaMaze";
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.matchWidthOrHeight = .5f;
        canvas.pixelPerfect = false;
        StartupDossierSetup.Apply(menu, canvas);
        if (!UnityEngine.Object.FindAnyObjectByType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        menu.FitPanel();
        EditorUtility.SetDirty(menu); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        var scenes = EditorBuildSettings.scenes.Where(x => x.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = menu.gameObject;
        Debug.Log("STARTUP_MENU: configured The Maze of work dossier.");
    }

    static void LinkGameMenu()
    {
        var scene = EditorSceneManager.OpenScene("Assets/00_Scenes/BetaMaze.unity");
        var hud = UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();
        if (!hud) throw new InvalidOperationException("Falta el Canvas del laberinto.");
        var result = (RectTransform)hud.resultPanel.transform;
        result.sizeDelta = new Vector2(result.sizeDelta.x, 380);
        if (!hud.returnToMenuButton)
        {
            hud.returnToMenuButton = Button("Return to start", result, "Volver al inicio", new Vector2(65, -316), new Vector2(380, 40));
            hud.returnToMenuButton.GetComponentInChildren<Text>().fontSize = 20;
        }
        EditorUtility.SetDirty(hud); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    static void Clear(Transform root)
    { for (int i = root.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject); }
    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.gameObject.layer = 5;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }
    static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    static RectTransform Box(string name, Transform parent, Color color, Vector2 position, Vector2 size)
    { var r = Rect(name, parent, position, size); var image = r.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return r; }
    static RectTransform Dialog(string name, Transform parent, Vector2 size)
    {
        var r = Box(name, parent, Ink, Vector2.zero, size);
        r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f;
        return r;
    }
    static Text Label(string name, Transform parent, string value, int size, Color color, Vector2 position, Vector2 dimensions, bool bold = false)
    {
        var r = Rect(name, parent, position, dimensions); var t = r.gameObject.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.text = value; t.fontSize = size;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; t.color = color; t.alignment = TextAnchor.MiddleLeft;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; return t;
    }
    static Button Button(string name, Transform parent, string text, Vector2 position, Vector2 dimensions, bool primary = false)
    {
        var r = Box(name, parent, Color.white, position, dimensions); var image = r.GetComponent<Image>(); image.raycastTarget = true;
        var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image;
        Color normal = primary ? Gold : Slate;
        var colors = b.colors; colors.normalColor = normal; colors.highlightedColor = Color.Lerp(normal, Color.white, .14f);
        colors.selectedColor = colors.highlightedColor; colors.pressedColor = Color.Lerp(normal, Color.black, .22f);
        colors.disabledColor = new Color(.11f, .15f, .17f, .65f); colors.fadeDuration = .12f; b.colors = colors;
        var label = Label("Label", r, text, 27, primary ? Ink : Cream, Vector2.zero, Vector2.zero, primary);
        Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(16, 4); label.rectTransform.offsetMax = new Vector2(-16, -4);
        label.alignment = TextAnchor.MiddleCenter;
        return b;
    }
}
