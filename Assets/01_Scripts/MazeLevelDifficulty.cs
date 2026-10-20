using System;

// Derived from the scene's base settings on every generation, never from the previous level.
public readonly struct MazeLevelDifficulty
{
    public readonly int Width, Height, Obstacles, Spades, Turrets;
    public readonly float AlternativeRoutes, BossSpeed, BossGracePeriod;

    public MazeLevelDifficulty(int levelIndex, int width, int height, int obstacles,
        int spades, int turrets, float routes, float bossSpeed, float bossGracePeriod)
    {
        int step = Math.Max(0, Math.Min(8, levelIndex));
        float progress = step / 8f;
        // Grow one axis at a time: rectangular and square layouts alternate.
        Width = Math.Max(3, Math.Min(40, width + ((step + 1) / 2) * 2));
        Height = Math.Max(3, Math.Min(40, height + (step / 2) * 2));
        Obstacles = Math.Max(0, obstacles) + step * 2;
        Spades = Math.Max(0, spades) + step;
        Turrets = Math.Max(0, turrets) + step / 2;
        AlternativeRoutes = Math.Max(0, routes) * (1 - .6f * progress);
        BossSpeed = Math.Max(.1f, bossSpeed) * (1 + .4f * progress);
        BossGracePeriod = Math.Max(0, bossGracePeriod) * (1 - .4f * progress);
    }
}
