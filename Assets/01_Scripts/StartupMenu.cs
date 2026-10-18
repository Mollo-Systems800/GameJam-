using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StartupMenu : MonoBehaviour
{
    public MazeAudioSettingsPanel audioSettings;
    [Header("Ilustracion y Canvas recibidos")]
    public Texture2D backgroundTexture;
    public Image backgroundDisplay;
    public Canvas menuCanvas;
    public RectTransform mainPanel;
    public CanvasGroup panelGroup;
    public RectTransform[] dialogs;
    [Header("Acciones")]
    public Button newGameButton, loadGameButton, resetButton, controlsButton, exitButton;
    public Text checkpointStatus, recordStatus, feedback;
    [Header("Confirmacion")]
    public GameObject resetConfirmation;
    public Text confirmationTitle, confirmationMessage, confirmationAction;
    public Button confirmResetButton, cancelResetButton;
    [Header("Controles")]
    public GameObject controlsPanel;
    public Button closeControlsButton;
    [Header("Ranking de campanas completas")]
    public Button rankingButton, closeRankingButton;
    public GameObject rankingPanel;
    public Text rankingNames, rankingTimes, rankingStatus;
    public Button previousRankingPage, nextRankingPage, finalistsButton;
    public Text rankingPage, finalistsSummary;
    int shownRankingPage;
    [Header("Escena jugable")]
    public string gameScene = "BetaMaze";
    public bool IsLoading { get; private set; }
    bool confirmingNew;
    Sprite runtimeBackground;
    AsyncOperation loading;
    float shownAt;
    Vector2 previousSize;

    void Awake()
    {
        StartupRankingView.Ensure(this);
        audioSettings=MazeAudioSettingsView.Ensure(this,null);
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (backgroundDisplay && backgroundTexture && !backgroundDisplay.sprite)
        {
            runtimeBackground = Sprite.Create(backgroundTexture, new Rect(0, 0, backgroundTexture.width, backgroundTexture.height), Vector2.one * .5f);
            backgroundDisplay.sprite = runtimeBackground;
        }
        newGameButton.onClick.AddListener(StartNewGame);
        loadGameButton.onClick.AddListener(LoadGame);
        resetButton.onClick.AddListener(ShowResetConfirmation);
        controlsButton.onClick.AddListener(ShowControls);
        exitButton.onClick.AddListener(Exit);
        confirmResetButton.onClick.AddListener(ConfirmReset);
        cancelResetButton.onClick.AddListener(CancelResetConfirmation);
        closeControlsButton.onClick.AddListener(CloseControls);
        if (rankingButton) rankingButton.onClick.AddListener(ShowRanking);
        if (closeRankingButton) closeRankingButton.onClick.AddListener(CloseRanking);
        if (previousRankingPage) previousRankingPage.onClick.AddListener(() => ChangeRankingPage(-1));
        if (nextRankingPage) nextRankingPage.onClick.AddListener(() => ChangeRankingPage(1));
        if (finalistsButton) finalistsButton.onClick.AddListener(ShowRanking);
        if (rankingPanel) rankingPanel.SetActive(false);
        resetConfirmation.SetActive(false); controlsPanel.SetActive(false);
        shownAt = Time.unscaledTime;
        RefreshMenuState(); FitPanel();
        Select(MazeCampaignSave.HasSave ? loadGameButton : newGameButton);
    }

    void Update()
    {
        if (panelGroup) panelGroup.alpha = Mathf.SmoothStep(0, 1, (Time.unscaledTime - shownAt) / .3f);
        if (((RectTransform)menuCanvas.transform).rect.size != previousSize) FitPanel();
        bool back = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
            || Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        if (back && !IsLoading)
        {
            if (audioSettings&&audioSettings.IsOpen)audioSettings.Close();
            else if (rankingPanel && rankingPanel.activeSelf) CloseRanking();
            else if (controlsPanel.activeSelf) CloseControls();
            else if (resetConfirmation.activeSelf) CancelResetConfirmation();
        }
    }

    public void FitPanel()
    {
        var rect = (RectTransform)menuCanvas.transform;
        previousSize = rect.rect.size;
        float scale = Mathf.Min(1, (previousSize.x - 64) / mainPanel.sizeDelta.x, (previousSize.y - 64) / mainPanel.sizeDelta.y);
        scale = Mathf.Max(.1f, scale);
        mainPanel.localScale = Vector3.one * scale;
        float left = (previousSize.x - mainPanel.sizeDelta.x * scale) * .5f;
        mainPanel.anchoredPosition = new Vector2(left, 0);
        foreach (var dialog in dialogs)
            dialog.localScale = Vector3.one * Mathf.Min(1, (previousSize.x - 64) / dialog.sizeDelta.x, (previousSize.y - 64) / dialog.sizeDelta.y);
    }

    public void StartNewGame()
    {
        if (IsLoading) return;
        if (MazeCampaignSave.HasSave)
        {
            confirmingNew = true;
            confirmationTitle.text = "¿EMPEZAR DE NUEVO?";
            confirmationMessage.text = "La nueva partida reemplazará el nivel guardado. Tus récords se conservarán.";
            confirmationAction.text = "Nueva partida";
            ShowConfirmation();
        }
        else BeginNew();
    }
    void BeginNew()
    {
        if (!CanLoad()) return;
        MazeCampaignSave.RequestNew(); StartCoroutine(LoadScene());
    }
    public void LoadGame()
    {
        if (IsLoading || !CanLoad() || !MazeCampaignSave.RequestContinue()) return;
        StartCoroutine(LoadScene());
    }
    bool CanLoad()
    {
        if (Application.CanStreamedLevelBeLoaded(gameScene)) return true;
        feedback.text = "No se encuentra la escena del juego. Revisa Build Profiles.";
        return false;
    }
    IEnumerator LoadScene()
    {
        IsLoading = true; resetConfirmation.SetActive(false); controlsPanel.SetActive(false);
        if (rankingPanel) rankingPanel.SetActive(false);
        if (rankingButton) rankingButton.interactable = false;
        newGameButton.interactable = loadGameButton.interactable = resetButton.interactable = controlsButton.interactable = exitButton.interactable = false;
        loading = SceneManager.LoadSceneAsync(gameScene);
        while (loading != null && !loading.isDone)
        {
            feedback.text = $"Abriendo expediente… {Mathf.RoundToInt(Mathf.Clamp01(loading.progress / .9f) * 100)}%";
            yield return null;
        }
    }
    public void ShowResetConfirmation()
    {
        if (IsLoading) return;
        confirmingNew = false;
        confirmationTitle.text = "¿BORRAR EL PROGRESO?";
        confirmationMessage.text = "Se borrarán el nivel guardado, tus récords y el ranking local. Esta acción no se puede deshacer.";
        confirmationAction.text = "Borrar progreso";
        ShowConfirmation();
    }
    void ShowConfirmation()
    {
        panelGroup.interactable = false; resetConfirmation.SetActive(true); Select(cancelResetButton);
    }
    public void ConfirmReset()
    {
        if (IsLoading) return;
        if (confirmingNew) { BeginNew(); return; }
        MazeCampaignSave.Reset(); CancelResetConfirmation(); RefreshMenuState(); Select(newGameButton);
    }
    public void CancelResetConfirmation()
    {
        resetConfirmation.SetActive(false); panelGroup.interactable = true;
        Select(confirmingNew ? newGameButton : resetButton);
    }
    public void ShowControls()
    {
        if (IsLoading) return;
        panelGroup.interactable = false; controlsPanel.SetActive(true); Select(closeControlsButton);
    }
    public void CloseControls()
    {
        controlsPanel.SetActive(false); panelGroup.interactable = true; Select(controlsButton);
    }
    public void RefreshMenuState()
    {
        bool hasSave = MazeCampaignSave.TryLoad(out var save);
        loadGameButton.interactable = hasSave;
        resetButton.interactable = hasSave || MazeCampaignSave.HasRecords;
        checkpointStatus.text = hasSave ? $"EXPEDIENTE ABIERTO  /  ÁREA {(save.Level - 1) / 3 + 1}/3 · NIVEL {(save.Level - 1) % 3 + 1}/3"
            : "SIN EXPEDIENTE ABIERTO";
        var records = CampaignRanking.Read();
        recordStatus.text = records.Count > 0 ? $"MEJOR CAMPAÑA   {CampaignRanking.Format(records[0].seconds)}" : "3 ÁREAS  /  9 NIVELES  /  1 SALIDA";
        if (finalistsSummary)
        {
            var summary = new System.Text.StringBuilder();
            for (int i = 0; i < Mathf.Min(3, records.Count); i++)
                summary.AppendLine($"{i + 1:00}   {records[i].name}   /   {CampaignRanking.Format(records[i].seconds)}");
            finalistsSummary.text = records.Count == 0 ? "El primer puesto sigue vacante.\nCompleta los 9 niveles y deja tu nombre." : summary.ToString();
        }
        feedback.text = "Tu avance se guarda al empezar cada nivel.";
    }
    public void ShowRanking()
    {
        if (IsLoading || !rankingPanel) return;
        shownRankingPage = 0;
        RefreshRanking();
        panelGroup.interactable = false; rankingPanel.SetActive(true); Select(closeRankingButton);
    }
    public void ChangeRankingPage(int direction)
    {
        shownRankingPage += direction; RefreshRanking();
    }
    void RefreshRanking()
    {
        var rows = CampaignRanking.Read();
        int pages = Mathf.Max(1, Mathf.CeilToInt(rows.Count / (float)CampaignRanking.PageSize));
        shownRankingPage = Mathf.Clamp(shownRankingPage, 0, pages - 1);
        var names = new System.Text.StringBuilder(); var times = new System.Text.StringBuilder();
        for (int i = shownRankingPage * CampaignRanking.PageSize; i < Mathf.Min(rows.Count, (shownRankingPage + 1) * CampaignRanking.PageSize); i++)
        { names.AppendLine($"{i + 1:00}   {rows[i].name}"); times.AppendLine(CampaignRanking.Format(rows[i].seconds)); }
        rankingNames.text = names.ToString(); rankingTimes.text = times.ToString();
        rankingStatus.text = rows.Count == 0 ? "Todavía no hay marcas.\nCompleta las 3 áreas y sus 9 niveles para firmar la primera."
            : "Solo campañas completas. Menor tiempo, mejor posición.\nLos tiempos incluyen los intentos fallidos.";
        if (rankingPage) rankingPage.text = $"{shownRankingPage + 1} / {pages}   ·   {rows.Count} finalistas";
        if (previousRankingPage) previousRankingPage.interactable = shownRankingPage > 0;
        if (nextRankingPage) nextRankingPage.interactable = shownRankingPage + 1 < pages;
    }
    public void CloseRanking()
    {
        if (rankingPanel) rankingPanel.SetActive(false);
        panelGroup.interactable = true; Select(rankingButton);
    }
    static void Select(Button button)
    {
        if (EventSystem.current && button && button.interactable) EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
    void Exit()
    {
#if UNITY_EDITOR
        feedback.text = "Salir cierra el juego en la versión compilada.";
#else
        Application.Quit();
#endif
    }
    public static void ReturnToStart()
    {
        var game = FindAnyObjectByType<MazeBetaGame>(); if (game) game.SaveCampaignCheckpoint();
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        SceneManager.LoadScene("Inicio");
    }
    void OnDestroy() { if (runtimeBackground) Destroy(runtimeBackground); }
}
