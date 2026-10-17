using UnityEngine;

public sealed partial class MazeBetaGame
{
    // Paleta tomada del propio juego: letreros SALTA (amarillo), SALIDA (verde) y HUD gris azulado.
    static readonly Color[] MenuAccent =
    {
        new Color(0.85f, 0.72f, 0.35f),
        new Color(0.36f, 0.80f, 0.42f),
        new Color(0.86f, 0.42f, 0.34f)
    };
    static readonly Color MenuPanelColor = new Color(0.13f, 0.17f, 0.19f, 0.90f);
    static readonly Color MenuSecondaryColor = new Color(0.20f, 0.25f, 0.28f, 0.95f);
    static readonly Color MenuBorder = new Color(0.33f, 0.40f, 0.43f, 1f);
    static readonly Color MenuInk = new Color(0.90f, 0.92f, 0.92f);
    static readonly Color MenuSlate = new Color(0.68f, 0.74f, 0.76f);
    static readonly Color MenuGold = new Color(0.85f, 0.72f, 0.35f);
    static readonly Color MenuDark = new Color(0.08f, 0.10f, 0.12f);
    static readonly Color[] MenuConfetti = { MenuGold };

    // --- Estilos legacy (por si otros archivos del partial los usan, p. ej. la tienda) ---
    static Texture2D menuRound;
    static GUIStyle menuPanel, menuTitleStyle, menuSubStyle, menuBtnSecondary;
    static readonly GUIStyle[] menuBtnPrimary = new GUIStyle[3];
    int menuState = -1;
    float menuShownAt;

    static Texture2D Rounded(Color color, int size = 32, int radius = 4)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0);
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0);
                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
            }
        tex.Apply();
        return tex;
    }

    static GUIStyle MenuButton(Color normal)
    {
        var style = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            border = new RectOffset(4, 4, 4, 4)
        };
        style.normal.background = Rounded(normal);
        style.hover.background = Rounded(Color.Lerp(normal, Color.white, 0.12f));
        style.active.background = Rounded(Color.Lerp(normal, Color.black, 0.25f));
        style.focused.background = style.normal.background;
        style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = MenuInk;
        return style;
    }

    static void EnsureMenuStyles()
    {
        if (menuRound && menuPanel != null) return;
        menuRound = Rounded(MenuPanelColor, 32, 4);
        menuPanel = new GUIStyle { border = new RectOffset(4, 4, 4, 4) };
        menuPanel.normal.background = menuRound;
        menuTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter
        };
        menuTitleStyle.normal.textColor = MenuInk;
        menuSubStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.UpperCenter,
            wordWrap = true
        };
        menuSubStyle.normal.textColor = MenuSlate;
        menuBtnSecondary = MenuButton(MenuSecondaryColor);
        for (int i = 0; i < menuBtnPrimary.Length; i++)
            menuBtnPrimary[i] = MenuButton(Color.Lerp(MenuSecondaryColor, MenuAccent[i], 0.30f));
    }

    // --- Menu de pausa / victoria / derrota con el estilo del HUD ---
    static GUIStyle mnTitle, mnSub, mnLine, mnBtnText, mnFoot;

    readonly float[] mnHover = new float[4];
    float mnAlpha = 1f, mnLastTime, mnK = 1f, mnOx, mnOy;
    int mnFocus, mnPressed = -1;
    Vector2 mnLastMouse;

    static GUIStyle MnLabelStyle(int size, TextAnchor anchor, bool wrap = false)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = FontStyle.Normal,
            alignment = anchor,
            wordWrap = wrap,
            clipping = TextClipping.Overflow,
            padding = new RectOffset(0, 0, 0, 0)
        };
        s.normal.textColor = Color.white;
        return s;
    }

    static void EnsureMenuAssets()
    {
        if (mnTitle != null) return;
        mnTitle = MnLabelStyle(22, TextAnchor.MiddleCenter);
        mnSub = MnLabelStyle(14, TextAnchor.UpperCenter, true);
        mnLine = MnLabelStyle(14, TextAnchor.MiddleLeft);
        mnBtnText = MnLabelStyle(16, TextAnchor.MiddleCenter);
        mnFoot = MnLabelStyle(12, TextAnchor.MiddleCenter);
    }

    static Color MnA(Color c, float a)
    {
        c.a = a;
        return c;
    }

    static void MnFit(GUIStyle s, string text, float maxW, int max, int min)
    {
        var content = new GUIContent(text);
        int size = max;
        for (; size > min; size -= 2)
        {
            s.fontSize = size;
            if (s.CalcSize(content).x <= maxW) break;
        }
        s.fontSize = size;
    }

    int MnPx(float size) => Mathf.Max(9, Mathf.RoundToInt(size * mnK));

    Rect MnR(float x, float y, float w, float h) => new Rect(mnOx + x * mnK, mnOy + y * mnK, w * mnK, h * mnK);

    void MnFill(Rect r, Color c, float radius)
    {
        c.a *= mnAlpha;
        if (c.a <= 0.001f) return;
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c,
            0f, Mathf.Min(radius * mnK, Mathf.Min(r.width, r.height) * 0.5f));
    }

    void MnRing(Rect r, Color c, float width, float radius)
    {
        c.a *= mnAlpha;
        if (c.a <= 0.001f) return;
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c,
            Mathf.Max(1f, width * mnK), Mathf.Min(radius * mnK, Mathf.Min(r.width, r.height) * 0.5f));
    }

    void MnText(Rect r, string s, GUIStyle style, Color c)
    {
        c.a *= mnAlpha;
        if (c.a <= 0.001f) return;
        GUI.color = c;
        GUI.Label(r, s, style);
        GUI.color = Color.white;
    }

    void MnButton(int i, Rect r, string label, bool primary, Color accent,
                  bool mouseMoved, bool ready, float dt, ref int choice)
    {
        Event ev = Event.current;
        bool over = r.Contains(ev.mousePosition);
        if (over && mouseMoved) mnFocus = i;
        if (ev.rawType == EventType.MouseDown && ev.button == 0 && over) mnPressed = i;
        else if (ev.rawType == EventType.MouseUp) mnPressed = -1;

        bool focused = over || mnFocus == i;
        mnHover[i] += ((focused ? 1f : 0f) - mnHover[i]) * (1f - Mathf.Exp(-dt * 16f));
        float h = mnHover[i];
        bool pressed = mnPressed == i && over;

        Color baseColor = primary ? Color.Lerp(MenuSecondaryColor, accent, 0.28f) : MenuSecondaryColor;
        Color c = Color.Lerp(baseColor, Color.white, 0.10f * h);
        if (pressed) c = Color.Lerp(c, Color.black, 0.18f);

        MnFill(r, c, 2f);
        MnRing(r, Color.Lerp(MenuBorder, accent, h), Mathf.Lerp(1f, 2f, h), 2f);
        MnFill(new Rect(r.x + 1f * mnK, r.y + 1f * mnK, 4f * mnK, r.height - 2f * mnK),
            Color.Lerp(MnA(accent, primary ? 0.8f : 0.25f), accent, h), 0f);
        MnText(r, label, mnBtnText, MenuInk);

        if (GUI.Button(r, GUIContent.none, GUIStyle.none) && ready) choice = i;
    }

    void DrawMenuPanel(float screenWidth, float screenHeight)
    {
        if ((running && !paused) || ShopOpen) { menuState = -1; return; }
        EnsureMenuStyles();
        EnsureMenuAssets();

        int state = paused ? 0 : HasWon ? 1 : 2;
        float now = Time.realtimeSinceStartup;
        if (state != menuState)
        {
            menuState = state;
            menuShownAt = now;
            mnLastTime = now;
            mnFocus = 0;
            mnPressed = -1;
            for (int i = 0; i < mnHover.Length; i++) mnHover[i] = 0f;
        }
        float dt = Mathf.Clamp(now - mnLastTime, 0f, 0.05f);
        mnLastTime = now;
        float since = now - menuShownAt;
        float fade = Mathf.Clamp01(since / 0.15f);
        bool ready = since > 0.2f;
        Color accent = MenuAccent[state];
        Event ev = Event.current;

        string title, subtitle, footer, line2Label, line2Value;
        string[] options;
        string timeText = $"{Elapsed:0.0}s";

        if (state == 0)
        {
            title = "PAUSA";
            subtitle = "Pulsa Esc para continuar.";
            options = new[] { "Continuar", "Reiniciar nivel", "Nuevo laberinto" };
            line2Label = "Semilla";
            line2Value = $"{ActiveSeed}";
            footer = "Esc: continuar   |   W/S o flechas: elegir   |   Enter: aceptar";
        }
        else if (state == 1)
        {
            title = CampaignComplete ? "COMPLETASTE LAS 3 AREAS" : "NIVEL COMPLETADO";
            subtitle = CampaignComplete ? "Cruzaste todos los laberintos." : "Llegaste a la salida a tiempo.";
            options = new[]
            {
                CampaignComplete ? "Nueva campana desde el area 1"
                    : CurrentLevelInArea == 2 ? "Continuar a la siguiente area" : "Siguiente nivel",
                "Repetir este nivel"
            };
            line2Label = "Score final";
            line2Value = $"{Progress.Score:N0}";
            footer = "W/S o flechas: elegir   |   Enter: aceptar";
        }
        else
        {
            title = DefeatReason;
            subtitle = "Prueba otra ruta y usa monedas en la tienda (B).";
            options = new[] { "Reintentar este nivel", "Nuevo laberinto" };
            line2Label = "Semilla";
            line2Value = $"{ActiveSeed}";
            footer = "W/S o flechas: elegir   |   Enter: aceptar";
        }

        const float w = 460f, pad = 20f, btnH = 40f, gap = 8f;
        int n = options.Length;
        float buttonsY = 150f;
        float buttonsH = n * (btnH + gap) - gap;
        float height = buttonsY + buttonsH + 14f + 16f + 12f;
        mnK = Mathf.Clamp(Mathf.Min((screenHeight - 20f) / height, (screenWidth - 20f) / w), 0.5f, 1f);

        mnOx = (screenWidth - w * mnK) * 0.5f;
        mnOy = (screenHeight - height * mnK) * 0.5f;
        Rect panel = MnR(0f, 0f, w, height);

        mnSub.fontSize = MnPx(14f);
        mnLine.fontSize = MnPx(14f);
        mnBtnText.fontSize = MnPx(16f);
        mnFoot.fontSize = MnPx(12f);

        int choice = -1;
        if (ready && ev.type == EventType.KeyDown)
        {
            KeyCode key = ev.keyCode;
            if (key == KeyCode.DownArrow || key == KeyCode.S) { mnFocus = (mnFocus + 1) % n; ev.Use(); }
            else if (key == KeyCode.UpArrow || key == KeyCode.W) { mnFocus = (mnFocus + n - 1) % n; ev.Use(); }
            else if (key == KeyCode.Return || key == KeyCode.KeypadEnter) { choice = mnFocus; ev.Use(); }
        }

        // Fondo oscurecido simple
        GUI.color = new Color(0f, 0f, 0f, 0.50f * fade);
        GUI.DrawTexture(new Rect(0, 0, screenWidth, screenHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        mnAlpha = fade;

        // Panel estilo HUD
        MnFill(panel, MenuPanelColor, 3f);
        MnRing(panel, MenuBorder, 1f, 3f);
        MnFill(MnR(0f, 0f, w, 3f), accent, 0f);

        // Titulo centrado + separador
        string t = (title ?? "").ToUpperInvariant();
        MnFit(mnTitle, t, (w - pad * 2f) * mnK, MnPx(22f), MnPx(14f));
        MnText(MnR(0f, 10f, w, 32f), t, mnTitle, MenuInk);
        MnFill(MnR(pad, 48f, w - pad * 2f, 1f), MnA(MenuBorder, 0.8f), 0f);

        MnText(MnR(pad, 58f, w - pad * 2f, 24f), subtitle, mnSub, MenuSlate);

        // Datos como lineas del HUD
        MnText(MnR(pad, 92f, w - pad * 2f, 22f), $"Tiempo: {timeText}", mnLine, MenuInk);
        MnText(MnR(pad, 114f, w - pad * 2f, 22f), $"{line2Label}: {line2Value}", mnLine, state == 1 ? accent : MenuInk);
        MnFill(MnR(pad, 142f, w - pad * 2f, 1f), MnA(MenuBorder, 0.5f), 0f);

        bool mouseMoved = (ev.mousePosition - mnLastMouse).sqrMagnitude > 1f;
        for (int i = 0; i < n; i++)
        {
            Rect r = MnR(pad, buttonsY + 8f + i * (btnH + gap), w - pad * 2f, btnH);
            MnButton(i, r, options[i], i == 0, accent, mouseMoved, ready, dt, ref choice);
        }
        MnText(MnR(0f, buttonsY + 8f + buttonsH + 12f, w, 16f), footer, mnFoot, MnA(MenuSlate, 0.6f));

        GUI.color = Color.white;
        mnLastMouse = ev.mousePosition;
        if (choice < 0) return;

        if (state == 0)
        {
            if (choice == 0) { paused = false; RefreshActivity(); }
            else if (choice == 1) Begin(ActiveSeed);
            else Begin(NewSeed());
        }
        else if (state == 1)
        {
            if (choice == 0) { if (CampaignComplete) StartCampaign(NewSeed()); else AdvanceLevel(); }
            else Begin(ActiveSeed);
        }
        else if (choice == 0) Begin(ActiveSeed);
        else Begin(NewSeed());
    }
}