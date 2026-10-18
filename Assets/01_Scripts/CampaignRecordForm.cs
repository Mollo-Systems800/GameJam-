using UnityEngine;
using UnityEngine.UI;

public sealed class CampaignRecordForm : MonoBehaviour
{
    public MazeCanvasHUD hud;
    public GameObject form;
    public InputField playerName;
    public Button saveButton;
    public Text message;
    bool wasVisible;
    void Awake() { saveButton.onClick.AddListener(Save); form.SetActive(false); }
    void LateUpdate()
    {
        bool visible = hud && hud.game && hud.game.HasPendingCampaignRecord && hud.resultPanel.activeInHierarchy;
        form.SetActive(visible);
        if (visible && !wasVisible) { playerName.text = ""; playerName.Select(); playerName.ActivateInputField(); }
        if (visible) message.text = hud.game.RecordFeedback ?? (hud.game.IsCampaignBest ? "Nuevo récord. Firma tu marca:" : "Campaña completa. Escribe tu nombre para el ranking:");
        wasVisible = visible;
    }
    public void Save()
    {
        if (hud && hud.game && hud.game.SaveCampaignRecord(playerName.text))
        {
            playerName.DeactivateInputField(); form.SetActive(false);
            if (UnityEngine.EventSystems.EventSystem.current)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(hud.primaryButton.gameObject);
        }
    }
}
