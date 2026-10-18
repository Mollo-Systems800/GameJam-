using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MazeCanvasSetup
{
    static readonly Color Ink = new Color(.055f, .075f, .10f, .95f);
    static readonly Color Accent = new Color(.18f, .40f, .46f, 1);
    static MazeCanvasSetup() { EditorApplication.delayCall += ApplyRequested; }
    static void ApplyRequested()
    {
        const string request = "Logs/apply-canvas-setup.request";
        if (!File.Exists(request) || EditorApplication.isPlaying) return;
        File.Delete(request);
        try { Apply(); }
        catch (Exception error) { File.WriteAllText("Logs/canvas-setup-result.txt", error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("GameJam/Configurar Canvas e items")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Sal de Play antes de configurar el Canvas.");
        var game = UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
        if (!game) throw new InvalidOperationException("Abre la escena BetaMaze.");
        var canvas = game.gameObject.scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).FirstOrDefault(x => x.name == "UI");
        if (!canvas) throw new InvalidOperationException("No se encontró el Canvas UI del usuario; no se creará uno duplicado.");
        var stats = canvas.transform.Find("Stats") as RectTransform;
        if (!stats) throw new InvalidOperationException("Falta el panel Stats del Canvas.");
        RepairItems(game);
        var heart = ImportIcon("Assets/02_Sprites/heart_icon.png");
        var coin = ImportIcon("Assets/02_Sprites/icon_coins.png");
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1120, 700);
        scaler.matchWidthOrHeight = .5f;
        stats.anchorMin = Vector2.zero; stats.anchorMax = Vector2.one;
        stats.offsetMin = stats.offsetMax = Vector2.zero;
        var statsImage = stats.GetComponent<Image>();
        if (statsImage) statsImage.raycastTarget = false;
        // Re-running this tool only replaces our own layout, preserving the user's panel.
        var previous = stats.Find("Maze HUD");
        if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var root = Rect("Maze HUD", stats, Vector2.zero, Vector2.zero);
        Stretch(root);
        var hud = canvas.GetComponent<MazeCanvasHUD>();
        if (!hud) hud = canvas.gameObject.AddComponent<MazeCanvasHUD>();
        hud.game = game;
        var bar = Panel("Vitals panel", root, new Vector2(0, -18), new Vector2(-36, 138));
        var barColor = bar.GetComponent<Image>().color; barColor.a = 0; bar.GetComponent<Image>().color = barColor;
        bar.anchorMin = new Vector2(0, 1); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(.5f, 1);
        hud.hearts = new Image[RunProgress.MaxHealth];
        for (int i = 0; i < hud.hearts.Length; i++)
            hud.hearts[i] = Icon("Heart " + (i + 1), bar, heart, new Vector2(24 + i * 58, -15), new Vector2(46, 46));
        hud.coinIcon = Icon("Coins icon", bar, coin, new Vector2(-190, -15), new Vector2(48, 46));
        hud.coinIcon.rectTransform.anchorMin = hud.coinIcon.rectTransform.anchorMax = Vector2.one;
        hud.coinCount = Label("Coin count", bar, "0", new Vector2(-134, -15), new Vector2(115, 46), 30);
        hud.coinCount.rectTransform.anchorMin = hud.coinCount.rectTransform.anchorMax = Vector2.one;
        hud.levelText = Label("Level", bar, "Nivel", new Vector2(24, -70), new Vector2(450, 24), 20);
        hud.timeText = Label("Time and score", bar, "00:00", new Vector2(-306, -70), new Vector2(282, 24), 20);
        hud.timeText.rectTransform.anchorMin = hud.timeText.rectTransform.anchorMax = Vector2.one;
        hud.timeText.alignment = TextAnchor.MiddleRight;
        hud.attemptsText = Label("Defeat rule", bar, "Cada 2 derrotas se genera un nuevo laberinto.", new Vector2(24, -104), new Vector2(-48, 23), 18);
        hud.attemptsText.rectTransform.anchorMax = Vector2.one;
        foreach (var text in bar.GetComponentsInChildren<Text>())
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .85f); shadow.effectDistance = new Vector2(1, -1);
        }
        hud.hintText = Label("Hints and restart notice", root, "", new Vector2(0, 26), new Vector2(-40, 52), 20);
        hud.hintText.rectTransform.anchorMin = Vector2.zero; hud.hintText.rectTransform.anchorMax = Vector2.right;
        hud.hintText.rectTransform.pivot = new Vector2(.5f, 0);
        hud.hintText.alignment = TextAnchor.MiddleCenter;
        hud.hintText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
        hud.effectsText = Label("Active power ups", root, "", new Vector2(22, -169), new Vector2(650, 32), 19);
        hud.shopButton = Button("Open shop", root, "Tienda [B]", new Vector2(-186, -169), new Vector2(164, 38));
        hud.shopButton.GetComponent<RectTransform>().anchorMin = hud.shopButton.GetComponent<RectTransform>().anchorMax = Vector2.one;

        var result = Modal("Result panel", root, 510, 380);
        hud.resultPanel = result.gameObject;
        hud.resultTitle = Label("Title", result, "", new Vector2(24, -20), new Vector2(462, 44), 28);
        hud.resultTitle.alignment = TextAnchor.MiddleCenter;
        hud.resultDetails = Label("Details and countdown", result, "", new Vector2(26, -78), new Vector2(458, 158), 22);
        hud.resultDetails.alignment = TextAnchor.MiddleCenter;
        hud.primaryButton = Button("Continue", result, "Continuar", new Vector2(65, -260), new Vector2(380, 40));
        hud.primaryLabel = hud.primaryButton.GetComponentInChildren<Text>();
        hud.returnToMenuButton = Button("Return to start", result, "Volver al inicio", new Vector2(65, -316), new Vector2(380, 40));
        AddCampaignRecordForm(hud);
        result.gameObject.SetActive(false);

        var shop = Modal("Shop panel", root, 550, 378);
        hud.shopPanel = shop.gameObject;
        Label("Title", shop, "TIENDA", new Vector2(26, -18), new Vector2(498, 42), 28).alignment = TextAnchor.MiddleCenter;
        hud.shopBalance = Label("Balance", shop, "", new Vector2(26, -64), new Vector2(498, 48), 19);
        hud.purchaseButtons = new[] {
            Button("Buy speed", shop, "Velocidad x1.5 · 10 s — 6 monedas", new Vector2(26, -124), new Vector2(498, 44)),
            Button("Buy shield", shop, "Escudo · 8 s — 8 monedas", new Vector2(26, -180), new Vector2(498, 44)),
            Button("Buy health", shop, "Recuperar 1 corazón — 4 monedas", new Vector2(26, -236), new Vector2(498, 44)) };
        hud.closeShopButton = Button("Close shop", shop, "Volver al laberinto [B / Esc]", new Vector2(26, -310), new Vector2(498, 42));
        shop.gameObject.SetActive(false);
        if (!UnityEngine.Object.FindAnyObjectByType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        EditorUtility.SetDirty(hud); EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveScene(game.gameObject.scene);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/canvas-setup-result.txt", "PASS: existing UI/Stats configured, icons imported, 4 item prefabs/materials repaired and assigned; scene saved.");
        Debug.Log("CANVAS_SETUP: completed using existing UI/Stats.");
    }

    static void RepairItems(MazeBetaGame game)
    {
        string[] materials = { "Heart Purple", "Shield Metal", "Lightning Blue", "Arrows Green" };
        string[] prefabs = { "Item_Heart", "Item_Shield", "Item_Speed", "Item_Jump" };
        string[] data = { "ItemData_Healt", "ItemData_Shield", "ItemData_Speed", "ItemData_Jump" };
        var pool = new ItemData[4];
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        for (int i = 0; i < materials.Length; i++)
        {
            string name = materials[i];
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/PowerUps Vol 1 Free/Materials/" + name + ".mat");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PowerUps Vol 1 Free/Textures/" + name + ".tga");
            if (!material || !texture || !shader) throw new InvalidOperationException("Falta material/textura URP: " + name);
            material.shader = shader;
            material.SetTexture("_BaseMap", texture); material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", Color.white); material.SetColor("_Color", Color.white);
            material.SetFloat("_Metallic", i == 1 ? .35f : .05f);
            material.SetFloat("_Smoothness", .32f);
            EditorUtility.SetDirty(material);
            string path = "Assets/03_Prefabs/Item/" + prefabs[i] + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var assigned = renderer.sharedMaterials;
                    for (int slot = 0; slot < assigned.Length; slot++) assigned[slot] = material;
                    renderer.sharedMaterials = assigned;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            pool[i] = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_SO/" + data[i] + ".asset");
            if (!pool[i]) throw new InvalidOperationException("Falta ItemData: " + data[i]);
        }
        // Populate the empty generator; retain any additional ItemData added by the user.
        game.itemPool = (game.itemPool ?? Array.Empty<ItemData>()).Concat(pool).Where(x => x).Distinct().ToArray();
        if (!game.shieldEffectPrefab)
            game.shieldEffectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Item/ShieldEffect.prefab");
        RepairShieldEffect();
    }

    public static void AddCampaignRecordForm(MazeCanvasHUD hud)
    {
        var old = hud.GetComponent<CampaignRecordForm>();
        if (old) { if (old.form) UnityEngine.Object.DestroyImmediate(old.form); UnityEngine.Object.DestroyImmediate(old); }
        var controller = hud.gameObject.AddComponent<CampaignRecordForm>(); controller.hud = hud;
        var form = Rect("Campaign record entry", hud.resultPanel.transform, new Vector2(26, -178), new Vector2(458, 76));
        controller.form = form.gameObject;
        controller.message = Label("Record message", form, "Nuevo récord de campaña. Firma tu marca:", Vector2.zero, new Vector2(458, 27), 17);
        var inputRect = Panel("Player name", form, new Vector2(0, -32), new Vector2(290, 40));
        inputRect.GetComponent<Image>().color = new Color(.96f, .94f, .87f); inputRect.GetComponent<Image>().raycastTarget = true;
        var text = Label("Text", inputRect, "", new Vector2(10, 0), new Vector2(270, 40), 21);
        text.color = new Color(.12f, .16f, .15f); text.supportRichText = false;
        var placeholder = Label("Placeholder", inputRect, "Tu nombre", new Vector2(10, 0), new Vector2(270, 40), 21);
        placeholder.color = new Color(.4f, .43f, .40f); placeholder.fontStyle = FontStyle.Italic;
        var input = inputRect.gameObject.AddComponent<InputField>(); input.targetGraphic = inputRect.GetComponent<Image>();
        input.textComponent = text; input.placeholder = placeholder; input.characterLimit = 20;
        input.lineType = InputField.LineType.SingleLine; input.contentType = InputField.ContentType.Standard;
        controller.playerName = input;
        controller.saveButton = Button("Save campaign record", form, "Guardar", new Vector2(304, -32), new Vector2(154, 40));
        controller.form.SetActive(false); EditorUtility.SetDirty(controller);
    }

    static void RepairShieldEffect()
    {
        const string path = "Assets/03_Prefabs/Item/ShieldEffect.prefab";
        const string materialPath = "Assets/04_Materials/ItemShieldEffect.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            if (!AssetDatabase.IsValidFolder("Assets/04_Materials")) AssetDatabase.CreateFolder("Assets", "04_Materials");
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0); material.SetColor("_BaseColor", new Color(.2f, .75f, 1, .14f));
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent"); EditorUtility.SetDirty(material);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Sprite ImportIcon(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = Rect(name, parent, position, size);
        var image = rect.gameObject.AddComponent<Image>(); image.color = Ink; image.raycastTarget = false;
        return rect;
    }
    static RectTransform Modal(string name, Transform parent, float width, float height)
    {
        var rect = Panel(name, parent, Vector2.zero, new Vector2(width, height));
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.GetComponent<Image>().raycastTarget = true;
        return rect;
    }
    static Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var rect = Rect(name, parent, position, size);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = value; label.fontSize = fontSize; label.color = new Color(.97f, .96f, .92f);
        label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }
    static Image Icon(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var rect = Rect(name, parent, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }
    static Button Button(string name, Transform parent, string title, Vector2 position, Vector2 size)
    {
        var rect = Rect(name, parent, position, size);
        var image = rect.gameObject.AddComponent<Image>(); image.color = Accent;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Label("Label", rect, title, Vector2.zero, Vector2.zero, 20);
        Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(8, 2); label.rectTransform.offsetMax = new Vector2(-8, -2);
        label.alignment = TextAnchor.MiddleCenter;
        return button;
    }
}
