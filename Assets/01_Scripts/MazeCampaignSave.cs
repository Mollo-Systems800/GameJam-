using System;
using UnityEngine;

// A checkpoint is the beginning of a level, including its current random maze.
public static class MazeCampaignSave
{
    const string Prefix = "MazeBeta.CampaignSave.";
    public readonly struct Checkpoint
    {
        public readonly int CampaignSeed, Level, MazeSeed;
        public Checkpoint(int seed, int level, int mazeSeed)
        { CampaignSeed = seed; Level = level; MazeSeed = mazeSeed; }
    }
    static Checkpoint? pendingLaunch;
    public static bool HasSave => PlayerPrefs.GetInt(Prefix + "Exists", 0) == 1;
    public static bool HasRecords => PlayerPrefs.HasKey("MazeBeta.BestTime") || PlayerPrefs.HasKey("MazeBeta.BestScore");

    public static void Store(int seed, int level, int mazeSeed)
    {
        level = Mathf.Clamp(level, 1, MazeCampaignProgress.LevelCount);
        PlayerPrefs.SetInt(Prefix + "Seed", seed);
        PlayerPrefs.SetInt(Prefix + "Level", level);
        PlayerPrefs.SetInt(Prefix + "MazeSeed", mazeSeed);
        PlayerPrefs.SetInt(Prefix + "Exists", 1);
        PlayerPrefs.Save();
    }
    public static bool TryLoad(out Checkpoint checkpoint)
    {
        checkpoint = default;
        if (!HasSave) return false;
        int level = PlayerPrefs.GetInt(Prefix + "Level", 1);
        if (level < 1 || level > MazeCampaignProgress.LevelCount) return false;
        int seed = PlayerPrefs.GetInt(Prefix + "Seed", 0);
        checkpoint = new Checkpoint(seed, level, PlayerPrefs.GetInt(Prefix + "MazeSeed", MazeCampaignProgress.SeedForLevel(seed, level - 1)));
        return true;
    }
    public static void RequestNew()
    {
        int seed = Guid.NewGuid().GetHashCode();
        pendingLaunch = new Checkpoint(seed, 1, MazeCampaignProgress.SeedForLevel(seed, 0));
    }
    public static bool RequestContinue()
    {
        if (!TryLoad(out var checkpoint)) return false;
        pendingLaunch = checkpoint; return true;
    }
    public static bool ConsumeLaunch(out Checkpoint checkpoint)
    {
        checkpoint = pendingLaunch.GetValueOrDefault();
        bool requested = pendingLaunch.HasValue;
        pendingLaunch = null;
        return requested;
    }
    public static void Complete()
    {
        PlayerPrefs.SetInt(Prefix + "Exists", 0); PlayerPrefs.Save();
    }
    public static void Reset()
    {
        pendingLaunch = null;
        foreach (string key in new[] { "Exists", "Seed", "Level", "MazeSeed" }) PlayerPrefs.DeleteKey(Prefix + key);
        PlayerPrefs.DeleteKey("MazeBeta.BestTime"); PlayerPrefs.DeleteKey("MazeBeta.BestScore");
        PlayerPrefs.Save();
    }
}
