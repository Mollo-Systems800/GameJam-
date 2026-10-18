using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StartupMenu : MonoBehaviour
{
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
            if (controlsPanel.activeSelf) CloseControls();
            else if (resetConfirmation.activeSelf) CancelResetConfirmation();
        }
    }

    public void FitPanel()
    {
        var rect = (RectTransform)menuCanvas.transform;
        previousSize = rect.rect.size;
        float scale = Mathf.Min(1, (previousSize.x - 64) / 620, (previousSize.y - 80) / 860);
        scale = Mathf.Max(.1f, scale);
        mainPanel.localScale = Vector3.one * scale;
        float left = previousSize.x / previousSize.y > 1.3f ? Mathf.Max(32, previousSize.x * .075f)
            : (previousSize.x - 620 * scale) * .5f;
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
        newGameButton.interactable = loadGameButton.interactable = resetButton.interactable = controlsButton.interactable = exitButton.interactable = false;
        loading = SceneManager.LoadSceneAsync(gameScene);
        while (loading != null && !loading.isDone)
        {
            feedback.text = $"Preparando tu salida… {Mathf.RoundToInt(Mathf.Clamp01(loading.progress / .9f) * 100)}%";
            yield return null;
        }
    }
    public void ShowResetConfirmation()
    {
        if (IsLoading) return;
        confirmingNew = false;
        confirmationTitle.text = "¿BORRAR EL PROGRESO?";
        confirmationMessage.text = "Se borrarán el nivel guardado y tus récords. Esta acción no se puede deshacer.";
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
        checkpointStatus.text = hasSave ? $"ÁREA {(save.Level - 1) / 3 + 1}/3   ·   NIVEL {(save.Level - 1) % 3 + 1}/3 guardado"
            : "Tu próximo comienzo está aquí.";
        recordStatus.text = MazeCampaignSave.HasRecords ? $"RÉCORD   {PlayerPrefs.GetInt("MazeBeta.BestScore", 0)} puntos" : "3 áreas   /   9 niveles   /   una salida";
        feedback.text = "El progreso se guarda al comenzar cada nivel.";
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
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        SceneManager.LoadScene("Inicio");
    }
    void OnDestroy() { if (runtimeBackground) Destroy(runtimeBackground); }
}
