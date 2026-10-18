using System;
using System.Collections.Generic;

// Only straight, non-adjacent corridor cells can become jumping sections.
public static class MazeObstacles
{
    public static List<int> Select(MazeGrid grid, int seed, int count, IEnumerable<int> reserved = null)
    {
        var candidates = new List<int>();
        var excluded = reserved == null ? new HashSet<int>() : new HashSet<int>(reserved);
        int[] distances = grid.DistancesFrom(0);
        for (int cell = 1; cell < grid.Count; cell++)
        {
            if (distances[cell] < 3 || grid.IsExitCell(cell) || grid.IsParkourCell(cell) || excluded.Contains(cell)) continue;
            bool nearExit = false;
            for (int d = 0; d < 4; d++) nearExit |= grid.IsExitCell(grid.Neighbor(cell, d)) || grid.IsParkourCell(grid.Neighbor(cell, d)) || excluded.Contains(grid.Neighbor(cell,d));
            if (nearExit) continue;
            bool vertical = grid.IsOpen(cell, 0) && grid.IsOpen(cell, 2) && !grid.IsOpen(cell, 1) && !grid.IsOpen(cell, 3);
            bool horizontal = grid.IsOpen(cell, 1) && grid.IsOpen(cell, 3) && !grid.IsOpen(cell, 0) && !grid.IsOpen(cell, 2);
            if (vertical || horizontal) candidates.Add(cell);
        }
        var random = new Random(seed ^ 137249);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int temp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = temp;
        }
        var selected = new List<int>();
        foreach (int cell in candidates)
        {
            if (selected.Count >= Math.Max(0, count)) break;
            bool adjacent = false;
            for (int d = 0; d < 4; d++) adjacent |= selected.Contains(grid.Neighbor(cell, d));
            if (!adjacent) selected.Add(cell);
        }
        return selected;
    }
}
