using System;
using UnityEngine;

[Serializable]
public sealed class MazeAreaTheme
{
    public string areaName = "Area";
    public GameObject wallPrefab, floorPrefab, wallCorePrefab, wallCapPrefab;
    public GameObject exitFloorPrefab, exitMarkerPrefab, pitVisualPrefab;
    public GameObject platformPrefab, jumpTakeoffPrefab, jumpLandingPrefab;
    public GameObject spikePrefab, spikeFloorPrefab;
    public GameObject chairPrefab, storePrefab;
    [UnityEngine.Serialization.FormerlySerializedAs("shopkeeperPrefab")]
    public GameObject seatedWomanPrefab;
    public TextMesh worldLabelPrefab;
    public Color wallColor = new Color(0.25f, 0.3f, 0.35f);
    public Color floorColor = new Color(0.35f, 0.38f, 0.4f);
}

public sealed partial class MazeBetaGame
{
    [Header("Campana: 3 areas de 3 niveles")]
    [Range(1, 9)] public int startLevel = 1;
    [Range(0.5f, 1)] public float minimumExitDistanceRatio = 0.75f;
    public MazeAreaTheme[] areas = {
        new MazeAreaTheme { areaName = "Oficinas" },
        new MazeAreaTheme { areaName = "Archivo", wallColor = new Color(0.38f, 0.25f, 0.16f), floorColor = new Color(0.42f, 0.34f, 0.25f) },
        new MazeAreaTheme { areaName = "Patio", wallColor = new Color(0.16f, 0.32f, 0.24f), floorColor = new Color(0.28f, 0.37f, 0.29f) }
    };
    MazeCampaignProgress campaign = new MazeCampaignProgress();
    int campaignSeed;
    static readonly MazeAreaTheme FallbackArea = new MazeAreaTheme();
    public int CurrentLevelIndex => campaign.LevelIndex;
    public int CurrentAreaIndex => campaign.AreaIndex;
    public int CurrentLevelInArea => campaign.LevelInArea;
    public bool CampaignComplete => HasWon && campaign.IsLastLevel;
    public MazeAreaTheme ActiveArea => areas != null && campaign.AreaIndex < areas.Length && areas[campaign.AreaIndex] != null
        ? areas[campaign.AreaIndex] : FallbackArea;

    static GameObject Pick(GameObject themed, GameObject fallback) => themed ? themed : fallback;
    GameObject EnvWall => Pick(ActiveArea.wallPrefab, wallPrefab);
    GameObject EnvFloor => Pick(ActiveArea.floorPrefab, floorPrefab);
    GameObject EnvCore => Pick(ActiveArea.wallCorePrefab, wallCorePrefab);
    GameObject EnvCap => Pick(ActiveArea.wallCapPrefab, wallCapPrefab);
    GameObject EnvExitFloor => Pick(ActiveArea.exitFloorPrefab, exitFloorPrefab ? exitFloorPrefab : EnvFloor);
    GameObject EnvExitMarker => Pick(ActiveArea.exitMarkerPrefab, exitMarkerPrefab);
    GameObject EnvPit => Pick(ActiveArea.pitVisualPrefab, pitVisualPrefab);
    GameObject EnvPlatform => Pick(ActiveArea.platformPrefab, platformPrefab);
    GameObject EnvSpike => Pick(ActiveArea.spikePrefab, spikePrefab);
    GameObject EnvSpikeFloor => Pick(ActiveArea.spikeFloorPrefab, spikeFloorPrefab ? spikeFloorPrefab : EnvPit);
    GameObject EnvTakeoff => Pick(ActiveArea.jumpTakeoffPrefab, jumpTakeoffPrefab ? jumpTakeoffPrefab : EnvFloor);
    GameObject EnvLanding => Pick(ActiveArea.jumpLandingPrefab, jumpLandingPrefab ? jumpLandingPrefab : EnvFloor);
    GameObject EnvChair => Pick(ActiveArea.chairPrefab, chairPrefab);
    GameObject EnvSeatedWoman => Pick(ActiveArea.seatedWomanPrefab, seatedWomanPrefab);
    GameObject EnvStore => Pick(ActiveArea.storePrefab, storePrefab);
    TextMesh EnvLabel => ActiveArea.worldLabelPrefab ? ActiveArea.worldLabelPrefab : worldLabelPrefab;

    public void StartCampaign(int newSeed, int firstLevel = 1)
    {
        ResetDefeatCycle();
        campaignSeed = newSeed;
        campaign = new MazeCampaignProgress(Mathf.Clamp(firstLevel, 1, MazeCampaignProgress.LevelCount) - 1);
        Begin(MazeCampaignProgress.SeedForLevel(campaignSeed, campaign.LevelIndex));
    }

    public bool AdvanceLevel()
    {
        if (!HasWon || !campaign.Advance()) return false;
        ResetDefeatCycle();
        Begin(MazeCampaignProgress.SeedForLevel(campaignSeed, campaign.LevelIndex));
        return true;
    }

    void ApplyAreaTheme()
    {
        wallMaterial.color = ActiveArea.wallColor;
        floorMaterial.color = ActiveArea.floorColor;
    }

    void OnValidate()
    {
        exitCount = 1;
        startLevel = Mathf.Clamp(startLevel, 1, MazeCampaignProgress.LevelCount);
        if (areas == null) areas = new MazeAreaTheme[MazeCampaignProgress.AreaCount];
        else if (areas.Length != MazeCampaignProgress.AreaCount) Array.Resize(ref areas, MazeCampaignProgress.AreaCount);
        for (int i = 0; i < areas.Length; i++)
            if (areas[i] == null) areas[i] = new MazeAreaTheme { areaName = "Area " + (i + 1) };
    }
}
