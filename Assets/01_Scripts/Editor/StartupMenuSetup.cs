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
        try { Configure(); File.WriteAllText("Logs/startup-setup-result.txt", "PASS: supplied Inicio scene and illustration adapted; existing game Canvas linked; Inicio is first build scene."); }
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
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.matchWidthOrHeight = .5f;
        canvas.pixelPerfect = false;
        menu.backgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/04_Materials/Escape hacia un nuevo comienzo.png");
        var background = canvas.transform.Find("Background").GetComponent<Image>();
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(menu.backgroundTexture));
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(importer.assetPath);
        background.color = Color.white; background.raycastTarget = false;
        Stretch(background.rectTransform);
        var fitter = background.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = menu.backgroundTexture.width / (float)menu.backgroundTexture.height;
        menu.backgroundDisplay = background;
        canvas.transform.Find("Dark Overlay").GetComponent<Image>().color = new Color(.02f, .03f, .04f, .12f);
        var oldShade = canvas.transform.Find("Directional shade"); if (oldShade) UnityEngine.Object.DestroyImmediate(oldShade.gameObject);
        var shade = Rect("Directional shade", canvas.transform, Vector2.zero, Vector2.zero);
        Stretch(shade); shade.gameObject.AddComponent<StartupBackdropShade>().raycastTarget = false;
        shade.SetSiblingIndex(2);

        var panel = canvas.transform.Find("Main Panel").GetComponent<RectTransform>();
        Clear(panel);
        panel.GetComponent<Image>().color = Ink;
        panel.anchorMin = panel.anchorMax = new Vector2(0, .5f); panel.pivot = new Vector2(0, .5f);
        panel.sizeDelta = new Vector2(620, 860); panel.anchoredPosition = new Vector2(144, 0);
        menu.mainPanel = panel;
        menu.panelGroup = panel.GetComponent<CanvasGroup>();
        if (!menu.panelGroup) menu.panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        menu.panelGroup.alpha = 1; menu.panelGroup.interactable = true;
        var stripe = Box("Gold rule", panel, Gold, new Vector2(40, -36), new Vector2(540, 3));
        Label("Edition", panel, "GAME JAM   /   BETA", 16, Gold, new Vector2(40, -62), new Vector2(540, 26));
        Label("Title", panel, "THE MAZE", 68, Cream, new Vector2(40, -110), new Vector2(548, 92), true);
        Label("Subtitle", panel, "ESCAPA HACIA UN NUEVO COMIENZO", 20, Gold, new Vector2(42, -212), new Vector2(536, 32));
        Label("Premise", panel, "Un laberinto. Una salida.\nEl empleo viene por ti.", 24, Muted, new Vector2(42, -266), new Vector2(536, 64));
        Box("Divider", panel, new Color(.33f, .41f, .43f, .6f), new Vector2(42, -348), new Vector2(536, 1));
        menu.newGameButton = Button("New Game", panel, "NUEVA PARTIDA", new Vector2(42, -372), new Vector2(536, 74), true);
        menu.loadGameButton = Button("Load Game", panel, "Continuar partida", new Vector2(42, -462), new Vector2(536, 70));
        menu.controlsButton = Button("Controls", panel, "Cómo jugar", new Vector2(42, -548), new Vector2(536, 64));
        menu.checkpointStatus = Label("Checkpoint Status", panel, "Tu próximo comienzo está aquí.", 18, Muted, new Vector2(42, -636), new Vector2(536, 30));
        menu.recordStatus = Label("Records", panel, "3 áreas   /   9 niveles   /   una salida", 17, Gold, new Vector2(42, -672), new Vector2(536, 28));
        menu.feedback = Label("Feedback", panel, "El progreso se guarda al comenzar cada nivel.", 16, Muted, new Vector2(42, -714), new Vector2(536, 44));
        menu.resetButton = Button("Reset", panel, "Borrar progreso", new Vector2(42, -788), new Vector2(316, 40));
        menu.exitButton = Button("Exit", panel, "Salir", new Vector2(378, -788), new Vector2(200, 40));
        menu.resetButton.GetComponentInChildren<Text>().fontSize = menu.exitButton.GetComponentInChildren<Text>().fontSize = 18;

        var confirmation = canvas.transform.Find("Reset Confirmation").GetComponent<RectTransform>();
        Clear(confirmation); Stretch(confirmation);
        confirmation.GetComponent<Image>().color = new Color(.015f, .025f, .035f, .86f);
        confirmation.GetComponent<Image>().raycastTarget = true;
        var dialog = Dialog("Confirmation Panel", confirmation, new Vector2(660, 366));
        menu.confirmationTitle = Label("Title", dialog, "¿BORRAR EL PROGRESO?", 30, Cream, new Vector2(36, -32), new Vector2(588, 54), true);
        menu.confirmationMessage = Label("Message", dialog, "", 24, Muted, new Vector2(36, -112), new Vector2(588, 122));
        menu.confirmResetButton = Button("Confirm Reset", dialog, "Borrar progreso", new Vector2(36, -270), new Vector2(284, 58), true);
        menu.cancelResetButton = Button("Cancel Reset", dialog, "Cancelar", new Vector2(340, -270), new Vector2(284, 58));
        menu.confirmationAction = menu.confirmResetButton.GetComponentInChildren<Text>();
        menu.resetConfirmation = confirmation.gameObject;
        confirmation.SetAsLastSibling(); confirmation.gameObject.SetActive(false);

        var oldControls = canvas.transform.Find("Controls overlay"); if (oldControls) UnityEngine.Object.DestroyImmediate(oldControls.gameObject);
        var overlay = Box("Controls overlay", canvas.transform, new Color(.015f, .025f, .035f, .88f), Vector2.zero, Vector2.zero);
        Stretch(overlay); overlay.GetComponent<Image>().raycastTarget = true;
        var controls = Dialog("Controls card", overlay, new Vector2(790, 820));
        Label("Title", controls, "CÓMO JUGAR", 36, Cream, new Vector2(40, -34), new Vector2(710, 60), true);
        Label("Goal", controls, "Encuentra la única salida. Recoge monedas y compra mejoras para sobrevivir.", 23, Muted, new Vector2(40, -110), new Vector2(710, 70));
        string[] actions = { "Mover", "Mirar", "Correr", "Saltar", "Rodar", "Tienda", "Pausa" };
        string[] keys = { "WASD / Flechas / Stick izquierdo", "Ratón / Stick derecho", "Shift / L3", "Espacio / A", "C / Ctrl / B del mando", "B / Select", "Esc" };
        for (int i = 0; i < actions.Length; i++)
        {
            Label("Action " + i, controls, actions[i], 23, Gold, new Vector2(40, -204 - i * 48), new Vector2(164, 40));
            Label("Keys " + i, controls, keys[i], 23, Cream, new Vector2(210, -204 - i * 48), new Vector2(540, 40));
        }
        Label("Advice", controls, "El perseguidor acelera cada 30 s.\nCerca de las señoras, pasa rodando.\nDos derrotas generan otro laberinto.", 22, Muted, new Vector2(40, -572), new Vector2(710, 104));
        menu.closeControlsButton = Button("Close controls", controls, "Entendido", new Vector2(40, -714), new Vector2(710, 64), true);
        menu.controlsPanel = overlay.gameObject; overlay.gameObject.SetActive(false);
        menu.dialogs = new[] { dialog, controls };
        if (!UnityEngine.Object.FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        menu.FitPanel();
        EditorUtility.SetDirty(menu); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        var scenes = EditorBuildSettings.scenes.Where(x => x.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = menu.gameObject;
        Debug.Log("STARTUP_MENU: configured supplied scene and illustration.");
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
