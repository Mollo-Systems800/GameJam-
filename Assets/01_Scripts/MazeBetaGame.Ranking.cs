using UnityEngine;

public sealed partial class MazeBetaGame
{
    public CampaignRunClock CampaignClock { get; private set; } = new CampaignRunClock();
    public float CampaignSeconds => CampaignClock.Seconds;
    public bool HasPendingCampaignRecord => CampaignComplete && pendingRecord > 0;
    public string RecordFeedback { get; private set; }
    public bool IsCampaignBest { get; private set; }
    float pendingRecord;
    string recordKey = CampaignRanking.StorageKey;
    void FinishCampaignLevel()
    {
        CampaignClock.CompleteLevel(CurrentLevelIndex);
        ConsiderCampaignRecord();
    }
    void ConsiderCampaignRecord(string storageKey = CampaignRanking.StorageKey)
    {
        if (!recordResults || !CampaignComplete || !CampaignClock.Finished) return;
        IsCampaignBest = CampaignRanking.IsNewRecord(CampaignSeconds, storageKey);
        recordKey = storageKey; pendingRecord = CampaignSeconds;
    }
    public bool SaveCampaignRecord(string name)
    {
        if (!HasPendingCampaignRecord || !CampaignClock.Finished || !recordResults) return false;
        if (CampaignRanking.CleanName(name).Length == 0) { RecordFeedback = "Escribe tu nombre para guardar la marca."; return false; }
        if (!CampaignRanking.TrySave(name, pendingRecord, recordKey)) { RecordFeedback = "No se pudo guardar la marca. Inténtalo de nuevo."; return false; }
        RecordFeedback = "Marca guardada. Puedes verla en Ranking, en el inicio.";
        pendingRecord = 0; return true;
    }
    public void SaveCampaignCheckpoint()
    {
        if (!checkpointEnabled || !recordResults || CampaignComplete) return;
        int level = CurrentLevelIndex + (HasWon ? 2 : 1);
        int mazeSeed = HasWon ? MazeCampaignProgress.SeedForLevel(campaignSeed, level - 1) : ActiveSeed;
        MazeCampaignSave.Store(campaignSeed, level, mazeSeed, CampaignSeconds, CampaignClock.CompletedLevels, CampaignClock.Eligible);
    }
    void OnApplicationPause(bool value) { if (value) SaveCampaignCheckpoint(); }
    void OnApplicationQuit() => SaveCampaignCheckpoint();
}
