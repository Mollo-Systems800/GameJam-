using System;

public sealed class MazeCampaignProgress
{
    public const int AreaCount = 3;
    public const int LevelsPerArea = 3;
    public const int LevelCount = AreaCount * LevelsPerArea;
    public int LevelIndex { get; private set; }
    public int AreaIndex => LevelIndex / LevelsPerArea;
    public int LevelInArea => LevelIndex % LevelsPerArea;
    public bool IsLastLevel => LevelIndex == LevelCount - 1;

    public MazeCampaignProgress(int index = 0) { LevelIndex = Math.Max(0, Math.Min(LevelCount - 1, index)); }
    public bool Advance()
    {
        if (IsLastLevel) return false;
        LevelIndex++;
        return true;
    }
    public static int SeedForLevel(int campaignSeed, int index)
    {
        if (index == 0) return campaignSeed;
        unchecked
        {
            uint value = (uint)campaignSeed + 0x9e3779b9u * (uint)index;
            value = (value ^ (value >> 16)) * 0x85ebca6bu;
            value = (value ^ (value >> 13)) * 0xc2b2ae35u;
            return (int)(value ^ (value >> 16));
        }
    }
}
