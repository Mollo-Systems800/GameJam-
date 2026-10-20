using System;
using UnityEngine;

// A checkpoint is the beginning of a level, including its current random maze.
public static class MazeCampaignSave
{
    const string Prefix = "MazeBeta.CampaignSave.";
    public readonly struct Checkpoint
    {
        public readonly int CampaignSeed, Level, MazeSeed;
        public readonly float Seconds;
        public readonly int CompletedLevels, Lives;
        public readonly bool RankingEligible;
        public Checkpoint(int seed, int level, int mazeSeed, float seconds = 0, int completedLevels = 0, bool rankingEligible = false, int lives = RunProgress.MaxHealth)
        { CampaignSeed = seed; Level = level; MazeSeed = mazeSeed; Seconds = seconds; CompletedLevels = completedLevels; RankingEligible = rankingEligible; Lives = Mathf.Clamp(lives, 1, RunProgress.MaxHealth); }
    }
    static Checkpoint? pendingLaunch;
    static bool pendingNewCampaign;
    public static bool HasSave => PlayerPrefs.GetInt(Prefix + "Exists", 0) == 1;
    public static bool HasRecords => PlayerPrefs.HasKey("MazeBeta.BestTime") || PlayerPrefs.HasKey("MazeBeta.BestScore") || CampaignRanking.Read().Count > 0;

    public static void Store(int seed, int level, int mazeSeed, float seconds = 0, int completedLevels = 0, bool rankingEligible = false, int lives = RunProgress.MaxHealth)
    {
        level = Mathf.Clamp(level, 1, MazeCampaignProgress.LevelCount);
        PlayerPrefs.SetInt(Prefix + "Lives", Mathf.Clamp(lives, 1, RunProgress.MaxHealth));
        PlayerPrefs.SetInt(Prefix + "Seed", seed);
        PlayerPrefs.SetInt(Prefix + "Level", level);
        PlayerPrefs.SetInt(Prefix + "MazeSeed", mazeSeed);
        PlayerPrefs.SetInt(Prefix + "Exists", 1);
        PlayerPrefs.SetFloat(Prefix + "Seconds", seconds);
        PlayerPrefs.SetInt(Prefix + "CompletedLevels", completedLevels);
        PlayerPrefs.SetInt(Prefix + "RankingEligible", rankingEligible ? 1 : 0);
        PlayerPrefs.Save();
    }
    public static bool TryLoad(out Checkpoint checkpoint)
    {
        checkpoint = default;
        if (!HasSave) return false;
        int level = PlayerPrefs.GetInt(Prefix + "Level", 1);
        if (level < 1 || level > MazeCampaignProgress.LevelCount) return false;
        int seed = PlayerPrefs.GetInt(Prefix + "Seed", 0);
        checkpoint = new Checkpoint(seed, level, PlayerPrefs.GetInt(Prefix + "MazeSeed", MazeCampaignProgress.SeedForLevel(seed, level - 1)),
            PlayerPrefs.GetFloat(Prefix + "Seconds", 0), PlayerPrefs.GetInt(Prefix + "CompletedLevels", 0), PlayerPrefs.GetInt(Prefix + "RankingEligible", 0) == 1, PlayerPrefs.GetInt(Prefix + "Lives", RunProgress.MaxHealth));
        return true;
    }
    public static void RequestNew()
    {
        int seed = Guid.NewGuid().GetHashCode();
        pendingLaunch = new Checkpoint(seed, 1, MazeCampaignProgress.SeedForLevel(seed, 0), 0, 0, true);
        pendingNewCampaign = true;
    }
    public static bool RequestContinue()
    {
        pendingNewCampaign = false;
        if (!TryLoad(out var checkpoint)) { pendingLaunch = null; return false; }
        pendingLaunch = checkpoint; return true;
    }
    public static bool ConsumeLaunch(out Checkpoint checkpoint)
        => ConsumeLaunch(out checkpoint, out _);
    public static bool ConsumeLaunch(out Checkpoint checkpoint, out bool newCampaign)
    {
        checkpoint = pendingLaunch.GetValueOrDefault();
        bool requested = pendingLaunch.HasValue;
        newCampaign = requested && pendingNewCampaign;
        pendingLaunch = null;
        pendingNewCampaign = false;
        return requested;
    }
    public static void Complete()
    {
        PlayerPrefs.SetInt(Prefix + "Exists", 0); PlayerPrefs.Save();
    }
    public static void Reset()
    {
        pendingLaunch = null;
        pendingNewCampaign = false;
        foreach (string key in new[] { "Lives", "Exists", "Seed", "Level", "MazeSeed", "Seconds", "CompletedLevels", "RankingEligible" }) PlayerPrefs.DeleteKey(Prefix + key);
        PlayerPrefs.DeleteKey(CampaignRanking.StorageKey);
        PlayerPrefs.DeleteKey("MazeBeta.BestTime"); PlayerPrefs.DeleteKey("MazeBeta.BestScore");
        PlayerPrefs.Save();
    }
}
