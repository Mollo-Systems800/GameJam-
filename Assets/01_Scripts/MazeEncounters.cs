using System;
using System.Collections.Generic;

public sealed class MazeEncounters
{
    public readonly List<int> Spades = new List<int>();
    public readonly List<int> Turrets = new List<int>();
    public readonly List<int> Coins = new List<int>();

    public MazeEncounters(MazeGrid grid, int seed, IEnumerable<int> pits, int bossCell,
        int spadeCount, int turretCount, int coinCount)
    {
        var forbidden = new HashSet<int>(pits) { 0, bossCell };
        foreach (var exit in grid.Exits) forbidden.Add(exit.Cell);
        var distances = grid.DistancesFrom(0);
        var candidates = new List<int>();
        for (int c = 1; c < grid.Count; c++)
        {
            if (forbidden.Contains(c) || distances[c] < 4) continue;
            bool nextToExit = false;
            for (int d = 0; d < 4; d++) nextToExit |= grid.IsExitCell(grid.Neighbor(c, d));
            if (!nextToExit) candidates.Add(c);
        }
        var random = new Random(seed ^ 20271);
        Shuffle(candidates, random);
        var reserved = new HashSet<int>();
        foreach (int c in candidates)
        {
            if (Spades.Count >= Math.Max(0, spadeCount) && Turrets.Count >= Math.Max(0, turretCount)) break;
            if (reserved.Contains(c)) continue;
            if (Spades.Count < Math.Max(0, spadeCount)) Spades.Add(c);
            else Turrets.Add(c);
            forbidden.Add(c);
            reserved.Add(c);
            for (int d = 0; d < 4; d++) reserved.Add(grid.Neighbor(c, d));
        }
        candidates.Clear();
        for (int c = 1; c < grid.Count; c++) if (!forbidden.Contains(c)) candidates.Add(c);
        Shuffle(candidates, random);
        for (int i = 0; i < Math.Min(Math.Max(0, coinCount), candidates.Count); i++) Coins.Add(candidates[i]);
    }

    static void Shuffle(List<int> values, Random random)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int value = values[i]; values[i] = values[j]; values[j] = value;
        }
    }
}
