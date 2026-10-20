using UnityEngine;
using UnityEngine.UI;

// All references belong to the existing UI/Stats Canvas in BetaMaze.
public sealed class MazeCanvasHUD : MonoBehaviour
{
    public MazeBetaGame game;
    public MazeAudioSettingsPanel audioSettings;
    [Header("Vida y monedas")]
    public Image[] hearts;
    public Image coinIcon;
    public Text coinCount;
    [Header("Panel Stats")]
    public Text levelText, timeText, attemptsText, hintText, effectsText;
    public Button shopButton;
    [Header("Avisos y tienda del mismo Canvas")]
    public GameObject resultPanel, shopPanel;
    public Text resultTitle, resultDetails, primaryLabel, shopBalance;
    public Button primaryButton, closeShopButton;
    public Button returnToMenuButton;
    public Button[] purchaseButtons;

    void Awake()
    {
        if (!game) game = FindAnyObjectByType<MazeBetaGame>();
        audioSettings=MazeAudioSettingsView.Ensure(null,this);game.AudioSettings=audioSettings;
        MazeInterfaceDesign.Apply(this);
        shopButton.onClick.AddListener(() => game.SetShopOpen(true));
        closeShopButton.onClick.AddListener(() => game.SetShopOpen(false));
        primaryButton.onClick.AddListener(Continue);
        if (returnToMenuButton) returnToMenuButton.onClick.AddListener(StartupMenu.ReturnToStart);
        for (int i = 0; i < purchaseButtons.Length; i++)
        {
            var kind = (PowerUpKind)i;
            purchaseButtons[i].onClick.AddListener(() => game.BuyPowerUp(kind));
        }
        Refresh();
    }

    void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (!game || game.Progress == null) return;
        var progress = game.Progress;
        int health = progress.Health;
        for (int i = 0; i < hearts.Length; i++)
            if (hearts[i]) hearts[i].gameObject.SetActive(i < health);
        coinCount.text = progress.Coins.ToString();
        levelText.text = $"{game.ActiveArea.areaName} · Nivel {game.CurrentLevelInArea + 1}/3";
        timeText.text = $"{CampaignRanking.Format(game.CampaignSeconds)}   |   Score {progress.Score}";
        attemptsText.text = $"Vidas: {progress.Health}/3 · Sin vidas vuelves al nivel 1.";
        hintText.text = game.RestartNotice ?? (game.IsPlaying && game.Player && game.RequiresRollNear(game.Player.transform.position, .6f)
            ? "C / Ctrl: pasa rodando cerca de la señora." : "Shift: correr   ·   Espacio: saltar   ·   C / Ctrl: rodar   ·   B: tienda");
        effectsText.text = (progress.SpeedSeconds > 0 ? $"Velocidad {progress.SpeedSeconds:0}s   " : "")
            + (progress.ShieldSeconds > 0 ? $"Escudo {progress.ShieldSeconds:0}s   " : "")
            + (progress.DoubleJumpSeconds > 0 ? $"Doble salto {progress.DoubleJumpSeconds:0}s" : "");
        shopButton.gameObject.SetActive(game.IsPlaying);
        shopPanel.SetActive(game.ShopOpen);
        if (game.ShopOpen)
        {
            shopBalance.text = $"TU PRESUPUESTO: {progress.Coins} MONEDAS    /    Elige lo que necesitas para seguir.";
            for (int i = 0; i < purchaseButtons.Length; i++) purchaseButtons[i].interactable = progress.CanBuy((PowerUpKind)i);
        }
        bool result = game.WaitingForRetry || game.HasWon || game.IsPaused;
        resultPanel.SetActive(result && !game.ShopOpen && !game.IsCinematicPlaying);
        // A defeat has no buttons: the countdown automatically starts the next attempt.
        primaryButton.gameObject.SetActive(result && !game.WaitingForRetry);
        if (returnToMenuButton) returnToMenuButton.gameObject.SetActive(result && !game.WaitingForRetry && !game.ShopOpen);
        if (game.WaitingForRetry)
        {
            resultTitle.text = game.IsFinalDefeat ? "SIN VIDAS · FIN DE LA FUGA" : "LA SOLICITUD TE ATRAPÓ";
            string action = game.IsFinalDefeat
                ? "Volverás al nivel 1 con 3 vidas."
                : $"Nuevo laberinto en este nivel · {progress.Health} vidas restantes.";
            resultDetails.text = $"{game.DefeatReason}\n\n{action}\n\nReinicio automático en {Mathf.CeilToInt(game.RetrySecondsRemaining)} s";
        }
        else if (game.HasWon)
        {
            resultTitle.text = game.CampaignComplete ? "¡CAMPAÑA COMPLETADA!" : "¡NIVEL COMPLETADO!";
            resultDetails.text = $"Tiempo: {progress.Elapsed:0.0} s\nScore: {progress.Score}";
            if (game.CampaignComplete)
                resultDetails.text = $"Campaña: {CampaignRanking.Format(game.CampaignSeconds)}\n3 áreas · 9 niveles completados\n{game.RecordFeedback ?? (game.HasPendingCampaignRecord ? (game.IsCampaignBest ? "¡Nuevo récord!" : "¡Ya formas parte del ranking!") : game.CampaignClock.Finished ? "¡Encontraste la salida del último trámite!" : "Para el ranking, empieza una campaña desde el primer nivel.")}";
            resultDetails.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, game.HasPendingCampaignRecord ? 94 : 158);
            primaryLabel.text = game.CampaignComplete ? "Nueva campaña" : "Siguiente nivel";
        }
        else if (game.IsPaused)
        {
            resultTitle.text = "PAUSA";
            resultDetails.text = $"{game.ActiveArea.areaName} · Nivel {game.CurrentLevelIndex+1} de 9\nTu fuga puede esperar un momento. Esc para continuar.";
            primaryLabel.text = "Continuar";
        }
        if (!game.HasWon) resultDetails.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, game.IsPaused?84:158);
    }

    void Continue()
    {
        if (game.WaitingForRetry || game.IsCinematicPlaying) return;
        if (game.HasWon)
        {
            if (game.CampaignComplete) game.StartNewCampaign(System.Guid.NewGuid().GetHashCode());
            else game.AdvanceLevel();
        }
        else game.ResumeGame();
    }
}
