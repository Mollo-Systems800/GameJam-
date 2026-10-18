using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class StartupDossierSetup
{
    static readonly Color Paper = new Color(.91f, .87f, .77f);
    static readonly Color Ink = new Color(.12f, .16f, .15f);
    static readonly Color Red = new Color(.63f, .20f, .14f);
    static readonly Color Pencil = new Color(.34f, .36f, .30f);
    public static void Apply(StartupMenu menu, Canvas canvas)
    {
        var oldPortrait = menu.GetComponent<StartupCharacterPortrait>();
        if (oldPortrait) Object.DestroyImmediate(oldPortrait);
        for (int i = canvas.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(canvas.transform.GetChild(i).gameObject);
        menu.backgroundTexture = null; menu.backgroundDisplay = null;
        var background = Rect("Desk", canvas.transform, Vector2.zero, Vector2.zero); Stretch(background);
        Art(background, StartupPaperArt.Drawing.Desk, new Color(.12f, .23f, .21f));

        var board = Rect("Desktop composition", canvas.transform, Vector2.zero, new Vector2(1730, 950));
        board.anchorMin = board.anchorMax = board.pivot = new Vector2(0, .5f);
        menu.mainPanel = board; menu.panelGroup = board.gameObject.AddComponent<CanvasGroup>();
        var shadow = Box("Document shadow", board, new Color(0, 0, 0, .22f), new Vector2(15, -12), new Vector2(864, 944));
        var underneath = Sheet("Carbon copy", board, new Vector2(16, -8), new Vector2(856, 936), new Color(.71f, .70f, .57f));
        underneath.localRotation = Quaternion.Euler(0, 0, -2.1f);
        var paper = Sheet("Application form", board, Vector2.zero, new Vector2(860, 946), Paper);
        Line(paper, "Header rule", new Vector2(52, -64), new Vector2(756, 2), Ink);
        Label("Office", paper, "OFICINA DE EMPLEO  /  SOLICITUD DE INGRESO", 18, Ink, new Vector2(52, -27), new Vector2(756, 28));
        Label("File", paper, "N.º 001", 18, Pencil, new Vector2(52, -88), new Vector2(260, 28));
        Stamp("Vacancies", paper, "SIN VACANTES", new Vector2(522, -92), new Vector2(282, 58), -6);
        Label("Title", paper, "THE MAZE\nOF WORK", 86, Ink, new Vector2(45, -164), new Vector2(560, 244), true);
        Label("Subject", paper, "ASUNTO:", 16, Red, new Vector2(606, -240), new Vector2(200, 26));
        Label("Purpose", paper, "Encontrar\nuna salida.", 25, Ink, new Vector2(606, -279), new Vector2(200, 88), true);
        Label("Note", paper, "Se requiere experiencia para conseguir experiencia.", 22, Pencil, new Vector2(54, -437), new Vector2(740, 38));
        Line(paper, "Options rule", new Vector2(52, -493), new Vector2(756, 2), Ink);
        menu.newGameButton = Row("New Game", paper, "01", "NUEVA PARTIDA", "Presentar otra solicitud", new Vector2(52, -520), new Vector2(756, 92), true);
        menu.loadGameButton = Row("Load Game", paper, "02", "CONTINUAR PARTIDA", "Retomar el trámite pendiente", new Vector2(52, -631), new Vector2(756, 82));
        menu.controlsButton = Row("Controls", paper, "03", "CÓMO JUGAR", "Leer la letra chica", new Vector2(52, -731), new Vector2(756, 82));
        menu.checkpointStatus = Label("Checkpoint Status", paper, "SIN EXPEDIENTE ABIERTO", 17, Red, new Vector2(54, -849), new Vector2(752, 28), true);
        menu.feedback = Label("Feedback", paper, "Tu avance se guarda al empezar cada nivel.", 16, Pencil, new Vector2(54, -892), new Vector2(752, 26));

        var photoCard = Sheet("Applicant photo", board, new Vector2(1110, -18), new Vector2(450, 486), new Color(.94f, .92f, .85f));
        photoCard.localRotation = Quaternion.Euler(0, 0, -5);
        var photoRect = Rect("Actual player portrait", photoCard, new Vector2(22, -22), new Vector2(406, 348));
        var photo = photoRect.gameObject.AddComponent<RawImage>(); photo.raycastTarget = false;
        Label("Photo caption", photoCard, "DISPONIBILIDAD\nINMEDIATA", 28, Ink, new Vector2(25, -392), new Vector2(398, 76), true);
        var tape = Rect("Photo tape", photoCard, new Vector2(134, 17), new Vector2(184, 46));
        tape.localRotation = Quaternion.Euler(0, 0, -4); Art(tape, StartupPaperArt.Drawing.Tape, new Color(.80f, .68f, .39f, .74f));
        var sticky = Sheet("Experience note", board, new Vector2(918, -195), new Vector2(234, 242), new Color(.84f, .69f, .32f));
        sticky.localRotation = Quaternion.Euler(0, 0, 5);
        Label("Experience joke", sticky, "Se busca:\nexperiencia.\n\nSe ofrece:\nexperiencia.", 24, Ink, new Vector2(24, -20), new Vector2(194, 204), true).fontStyle = FontStyle.Italic;

        var plan = Sheet("Plan B", board, new Vector2(955, -525), new Vector2(720, 334), new Color(.84f, .85f, .74f));
        plan.localRotation = Quaternion.Euler(0, 0, 2.3f);
        Label("Plan heading", plan, "PLAN B", 30, Ink, new Vector2(27, -16), new Vector2(300, 46), true);
        Label("Plan remark", plan, "si lo de arriba no funciona", 18, Pencil, new Vector2(250, -29), new Vector2(430, 30)).fontStyle = FontStyle.Italic;
        var maze = Rect("Maze pencil sketch", plan, new Vector2(25, -82), new Vector2(671, 223));
        var drawing = Art(maze, StartupPaperArt.Drawing.Maze, Ink); drawing.variation = 2106;
        Stamp("Maze warning", plan, "NO HAY ATAJOS", new Vector2(422, -257), new Vector2(254, 48), -4);
        menu.recordStatus = Label("Records", board, "3 ÁREAS  /  9 NIVELES  /  1 SALIDA", 18, new Color(.76f, .80f, .68f), new Vector2(975, -884), new Vector2(700, 30));
        menu.resetButton = Row("Reset", board, "", "BORRAR PROGRESO", "", new Vector2(965, -936), new Vector2(414, 62));
        menu.exitButton = Row("Exit", board, "", "SALIR", "Por hoy es suficiente", new Vector2(1410, -936), new Vector2(275, 62));
        // Utility slips sit on the desk; the small offset is included in the composition bounds.
        board.sizeDelta = new Vector2(1730, 1020);

        var confirmation = Overlay("Reset Confirmation", canvas.transform);
        var dialog = Sheet("Confirmation form", confirmation, Vector2.zero, new Vector2(700, 412), Paper); Centre(dialog);
        Label("Office", dialog, "OFICINA DE EMPLEO  /  REVISIÓN DEL EXPEDIENTE", 16, Pencil, new Vector2(38, -26), new Vector2(624, 28));
        Line(dialog, "Rule", new Vector2(38, -69), new Vector2(624, 2), Ink);
        menu.confirmationTitle = Label("Title", dialog, "¿BORRAR EL PROGRESO?", 30, Ink, new Vector2(38, -94), new Vector2(624, 54), true);
        menu.confirmationMessage = Label("Message", dialog, "", 24, Pencil, new Vector2(38, -170), new Vector2(624, 120));
        menu.confirmResetButton = Row("Confirm Reset", dialog, "", "Borrar progreso", "", new Vector2(38, -322), new Vector2(300, 54), true);
        menu.cancelResetButton = Row("Cancel Reset", dialog, "", "Cancelar", "", new Vector2(358, -322), new Vector2(304, 54));
        menu.confirmationAction = menu.confirmResetButton.transform.Find("Label").GetComponent<Text>();
        menu.resetConfirmation = confirmation.gameObject; confirmation.gameObject.SetActive(false);

        var controlsOverlay = Overlay("Controls overlay", canvas.transform);
        var controls = Sheet("Fine print", controlsOverlay, Vector2.zero, new Vector2(820, 872), Paper); Centre(controls);
        Stamp("Instructions stamp", controls, "COPIA PARA EL POSTULANTE", new Vector2(352, -35), new Vector2(428, 50), -3);
        Label("Instructions title", controls, "LA LETRA CHICA", 42, Ink, new Vector2(40, -116), new Vector2(740, 62), true);
        Label("Goal", controls, "Encuentra la única salida. Recoge monedas y compra mejoras para sobrevivir.", 23, Pencil, new Vector2(40, -198), new Vector2(740, 74));
        string[] actions = { "Mover", "Mirar", "Correr", "Saltar", "Rodar", "Tienda", "Pausa" };
        string[] keys = { "WASD / Flechas / Stick izquierdo", "Ratón / Stick derecho", "Shift / L3", "Espacio / A", "C / Ctrl / B del mando", "B / Select", "Esc" };
        for (int i = 0; i < actions.Length; i++)
        {
            Label("Action " + i, controls, actions[i], 23, Red, new Vector2(40, -298 - i * 44), new Vector2(160, 38), true);
            Label("Keys " + i, controls, keys[i], 23, Ink, new Vector2(220, -298 - i * 44), new Vector2(560, 38));
        }
        Label("Advice", controls, "El perseguidor acelera cada 30 s.\nCerca de las señoras, pasa rodando.\nDos derrotas generan otro laberinto.", 22, Pencil, new Vector2(40, -642), new Vector2(740, 108));
        menu.closeControlsButton = Row("Close controls", controls, "", "LEÍDO. ACEPTAR Y SEGUIR.", "", new Vector2(40, -778), new Vector2(740, 56), true);
        menu.controlsPanel = controlsOverlay.gameObject; controlsOverlay.gameObject.SetActive(false);
        menu.dialogs = new[] { dialog, controls };
        AddRanking(menu);
        var preview = menu.gameObject.AddComponent<StartupCharacterPortrait>();
        preview.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Player.prefab"); preview.photo = photo; preview.Build();
        foreach (var light in Object.FindObjectsByType<Light>())
            if (!light.transform.IsChildOf(preview.transform) && light.name != "Photo softbox") light.cullingMask &= ~(1 << 30);
        EditorUtility.SetDirty(preview);
    }
    public static void AddRanking(StartupMenu menu)
    { StartupRankingView.Build(menu); EditorUtility.SetDirty(menu); }
    static RectTransform Rect(string name, Transform parent, Vector2 p, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); r.gameObject.layer = 5;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = p; r.sizeDelta = size; return r;
    }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void Centre(RectTransform r) { r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f; r.anchoredPosition = Vector2.zero; }
    static StartupPaperArt Art(RectTransform r, StartupPaperArt.Drawing drawing, Color color)
    {
        if (!r.GetComponent<CanvasRenderer>()) r.gameObject.AddComponent<CanvasRenderer>();
        var art = r.gameObject.AddComponent<StartupPaperArt>(); art.drawing = drawing; art.color = color; art.raycastTarget = false;
        art.SetAllDirty(); return art;
    }
    static RectTransform Sheet(string name, Transform parent, Vector2 p, Vector2 size, Color color)
    { var r = Rect(name, parent, p, size); Art(r, StartupPaperArt.Drawing.Sheet, color); return r; }
    static RectTransform Box(string name, Transform parent, Color c, Vector2 p, Vector2 size)
    { var r = Rect(name, parent, p, size); var i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false; return r; }
    static void Line(Transform parent, string name, Vector2 p, Vector2 size, Color c) => Box(name, parent, c, p, size);
    static Text Label(string name, Transform parent, string text, int size, Color c, Vector2 p, Vector2 dimensions, bool bold = false)
    {
        var r = Rect(name, parent, p, dimensions); var t = r.gameObject.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.text = text; t.fontSize = size;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; t.color = c; t.raycastTarget = false;
        t.alignment = TextAnchor.MiddleLeft; return t;
    }
    static void Stamp(string name, Transform parent, string value, Vector2 p, Vector2 size, float rotation)
    {
        var r = Rect(name, parent, p, size); r.localRotation = Quaternion.Euler(0, 0, rotation); Art(r, StartupPaperArt.Drawing.Stamp, Red);
        var t = Label("Stamp", r, value, 24, Red, Vector2.zero, Vector2.zero, true); Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(10, 4); t.rectTransform.offsetMax = new Vector2(-10, -4); t.alignment = TextAnchor.MiddleCenter;
    }
    static RectTransform Overlay(string name, Transform canvas)
    { var r = Box(name, canvas, new Color(.025f, .045f, .038f, .79f), Vector2.zero, Vector2.zero); Stretch(r); r.GetComponent<Image>().raycastTarget = true; return r; }
    static Button Row(string name, Transform parent, string number, string title, string subtitle, Vector2 p, Vector2 dimensions, bool primary = false)
    {
        var r = Box(name, parent, Color.white, p, dimensions); var image = r.GetComponent<Image>(); image.raycastTarget = true;
        var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image;
        var colors = b.colors; Color normal = primary ? Red : new Color(.82f, .80f, .70f);
        colors.normalColor = normal; colors.highlightedColor = primary ? new Color(.73f, .26f, .17f) : new Color(.92f, .77f, .41f);
        colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(.68f, .58f, .35f);
        colors.disabledColor = new Color(.64f, .65f, .58f); colors.fadeDuration = .13f; b.colors = colors;
        bool compact = dimensions.y < 65;
        float inset = number.Length > 0 ? 82 : 22;
        if (number.Length > 0) Label("Number", r, number, 20, primary ? Paper : Pencil, new Vector2(22, -10), new Vector2(52, dimensions.y - 20));
        var t = Label("Label", r, title, compact ? 21 : 28, primary ? Paper : Ink,
            new Vector2(inset, compact ? -4 : -12), new Vector2(dimensions.x - inset - 24, compact ? dimensions.y - 8 : 36), true);
        if (!compact && subtitle.Length > 0) Label("Subtitle", r, subtitle, 18, primary ? Paper : Pencil,
            new Vector2(inset, -51), new Vector2(dimensions.x - inset - 26, 26));
        if (dimensions.y >= 80) Label("Arrow", r, "→", 30, primary ? Paper : Red, new Vector2(dimensions.x - 53, -18), new Vector2(42, 42));
        r.gameObject.AddComponent<StartupPaperButton>();
        return b;
    }
}
