using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class StartupRankingView
{
    static readonly Color Paper = new Color(.91f,.87f,.77f), Ink = new Color(.12f,.16f,.15f), Red = new Color(.63f,.20f,.14f), Pencil = new Color(.34f,.36f,.30f);
    public static void Ensure(StartupMenu menu)
    { if (!menu.rankingButton || !menu.rankingPanel || !menu.finalistsButton) Build(menu); }
    static void Remove(GameObject obj)
    { if (!obj) return; obj.SetActive(false); if(Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj); }
    public static void Build(StartupMenu menu)
    {
        var dialogs = new System.Collections.Generic.List<RectTransform>();
        foreach (var dialog in menu.dialogs ?? new RectTransform[0])
            if (dialog && (!menu.rankingPanel || !dialog.IsChildOf(menu.rankingPanel.transform))) dialogs.Add(dialog);
        var paper = menu.mainPanel.Find("Application form");
        Button[] existing = { menu.newGameButton, menu.loadGameButton, menu.controlsButton };
        for (int i = 0; i < existing.Length; i++)
        {
            var rect = existing[i].GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(52, -520 - i * 83); rect.sizeDelta = new Vector2(756, 78);
        }
        if (menu.rankingButton) Remove(menu.rankingButton.gameObject);
        if (menu.rankingPanel) Remove(menu.rankingPanel);
        menu.rankingButton = Row("Ranking", paper, "04", "RANKING", "Los que completaron el trámite", new Vector2(52, -769), new Vector2(756, 78));
        menu.checkpointStatus.rectTransform.anchoredPosition = new Vector2(54, -866);
        menu.feedback.rectTransform.anchoredPosition = new Vector2(54, -904);
        var overlay = Overlay("Ranking overlay", menu.menuCanvas.transform);
        var sheet = Sheet("Campaign records", overlay, Vector2.zero, new Vector2(820, 790), Paper); Centre(sheet);
        Label("Office", sheet, "OFICINA DE EMPLEO  /  REGISTRO DE CONTRATACIONES", 16, Pencil, new Vector2(40, -24), new Vector2(740, 30));
        Line(sheet, "Rule", new Vector2(40, -68), new Vector2(740, 2), Ink);
        Label("Heading", sheet, "RANKING", 46, Ink, new Vector2(40, -88), new Vector2(740, 64), true);
        Stamp("Stamp", sheet, "TRÁMITE COMPLETO", new Vector2(464, -101), new Vector2(315, 48), -4);
        Label("Name heading", sheet, "N.º   POSTULANTE", 19, Red, new Vector2(40, -182), new Vector2(500, 34), true);
        Label("Time heading", sheet, "TIEMPO TOTAL", 19, Red, new Vector2(565, -182), new Vector2(215, 34), true);
        menu.rankingNames = Label("Names", sheet, "", 24, Ink, new Vector2(40, -234), new Vector2(500, 366));
        menu.rankingTimes = Label("Times", sheet, "", 24, Ink, new Vector2(565, -234), new Vector2(215, 366));
        foreach (var text in new[] { menu.rankingNames, menu.rankingTimes })
        { text.alignment = TextAnchor.UpperLeft; text.lineSpacing = 1.25f; text.supportRichText = false; }
          menu.previousRankingPage = Row("Previous page", sheet, "", "ANTERIOR", "", new Vector2(40, -595), new Vector2(180, 38));
          menu.nextRankingPage = Row("Next page", sheet, "", "SIGUIENTE", "", new Vector2(600, -595), new Vector2(180, 38));
          menu.rankingPage = Label("Page", sheet, "", 18, Pencil, new Vector2(238, -600), new Vector2(350, 32));
          menu.rankingPage.alignment = TextAnchor.MiddleCenter;
          menu.rankingStatus = Label("Status", sheet, "", 19, Pencil, new Vector2(40, -646), new Vector2(740, 56));
        menu.closeRankingButton = Row("Close ranking", sheet, "", "VOLVER AL ESCRITORIO", "", new Vector2(40, -710), new Vector2(740, 54), true);
        menu.rankingPanel = overlay.gameObject; overlay.gameObject.SetActive(false);
        dialogs.Add(sheet); menu.dialogs = dialogs.ToArray();
          var plan = menu.mainPanel.Find("Plan B") as RectTransform;
          if (plan)
          {
              foreach (Transform child in plan.Cast<Transform>().ToArray()) Remove(child.gameObject);
              Label("Finalists heading", plan, "LOS QUE SALIERON", 30, Ink, new Vector2(27, -18), new Vector2(650, 46), true);
              Label("Finalists subtitle", plan, "RANKING  /  3 áreas · 9 niveles", 18, Red, new Vector2(27, -65), new Vector2(650, 30));
              menu.finalistsSummary = Label("Finalists summary", plan, "", 22, Ink, new Vector2(27, -109), new Vector2(500, 125));
              menu.finalistsSummary.supportRichText = false;
              var maze = Rect("Maze pencil sketch", plan, new Vector2(548, -105), new Vector2(140, 120));
              Art(maze, StartupPaperArt.Drawing.Maze, Pencil).variation = 2106;
              menu.finalistsButton = Row("View finalists", plan, "", "VER TODOS LOS FINALISTAS", "", new Vector2(27, -251), new Vector2(666, 55), true);
          }
    }
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
