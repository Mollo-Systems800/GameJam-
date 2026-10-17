using System;
using System.Collections.Generic;

// Pure grid topology, independent of rendering. DFS builds a connected spanning tree.
public sealed class MazeGrid
{
    public readonly int Width;
    public readonly int Height;
    public readonly int Seed;
    public readonly struct Exit
    {
        public readonly int Cell;
        public readonly int Direction;
        public Exit(int cell, int direction) { Cell = cell; Direction = direction; }
    }
    readonly List<Exit> exits = new List<Exit>();
    public IReadOnlyList<Exit> Exits => exits;
    public int ExitCell => exits[0].Cell;
    public int ExitDirection => exits[0].Direction;
    public static readonly int[] DX = { 0, 1, 0, -1 };
    public static readonly int[] DY = { 1, 0, -1, 0 };
    readonly int[] passages;

    public MazeGrid(int width, int height, int seed, int exitCount = 1)
    {
        Width = Math.Max(3, width);
        Height = Math.Max(3, height);
        Seed = seed;
        passages = new int[Width * Height];
        // System.Random treats +n and -n identically; mix the sign into the seed first.
        var random = new Random(seed ^ (int)((uint)seed >> 16));
        var visited = new bool[passages.Length];
        var stack = new Stack<int>();
        var choices = new int[4];
        visited[0] = true;
        stack.Push(0);
        while (stack.Count > 0)
        {
            int cell = stack.Peek(), count = 0;
            for (int d = 0; d < 4; d++)
            {
                int next = Neighbor(cell, d);
                if (next >= 0 && !visited[next]) choices[count++] = d;
            }
            if (count == 0) { stack.Pop(); continue; }
            int direction = choices[random.Next(count)];
            int neighbor = Neighbor(cell, direction);
            passages[cell] |= 1 << direction;
            passages[neighbor] |= 1 << ((direction + 2) % 4);
            visited[neighbor] = true;
            stack.Push(neighbor);
        }
        // Spread exits across distinct boundary cells; keep the first one farthest from spawn.
        int[] distances = DistancesFrom(0);
        var candidates = new List<Exit>();
        for (int cell = 0; cell < passages.Length; cell++)
        {
            if (cell == 0) continue;
            for (int d = 0; d < 4; d++)
                if (Neighbor(cell, d) < 0)
                {
                    candidates.Add(new Exit(cell, d));
                    break;
                }
        }
        var nearestExit = new int[Count];
        for (int i = 0; i < Count; i++) nearestExit[i] = int.MaxValue;
        for (int i = 0; i < Math.Min(Math.Max(1, exitCount), candidates.Count); i++)
        {
            int best = -1, score = -1;
            for (int c = 0; c < candidates.Count; c++)
            {
                if (IsExitCell(candidates[c].Cell)) continue;
                int value = Math.Min(distances[candidates[c].Cell], nearestExit[candidates[c].Cell]);
                if (value > score) { best = c; score = value; }
            }
            if (best < 0) break;
            Exit exit = candidates[best];
            exits.Add(exit);
            passages[exit.Cell] |= 1 << exit.Direction;
            int[] fromExit = DistancesFrom(exit.Cell);
            for (int c = 0; c < Count; c++) nearestExit[c] = Math.Min(nearestExit[c], fromExit[c]);
        }
    }

    public bool IsExitCell(int cell)
    {
        foreach (Exit exit in exits) if (exit.Cell == cell) return true;
        return false;
    }

    public int Count => passages.Length;
    public bool IsOpen(int cell, int direction) => (passages[cell] & (1 << direction)) != 0;
    public int Neighbor(int cell, int direction)
    {
        int x = cell % Width + DX[direction], y = cell / Width + DY[direction];
        return x < 0 || y < 0 || x >= Width || y >= Height ? -1 : y * Width + x;
    }

    public int[] DistancesFrom(int start)
    {
        int[] distances = new int[Count];
        for (int i = 0; i < Count; i++) distances[i] = -1;
        var queue = new Queue<int>();
        queue.Enqueue(start);
        distances[start] = 0;
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int next = Neighbor(cell, d);
                if (next < 0 || !IsOpen(cell, d) || distances[next] >= 0) continue;
                distances[next] = distances[cell] + 1;
                queue.Enqueue(next);
            }
        }
        return distances;
    }

    public List<int> FindPath(int start, int goal)
    {
        var result = new List<int>();
        if (start < 0 || goal < 0 || start >= Count || goal >= Count) return result;
        var parent = new int[Count];
        for (int i = 0; i < Count; i++) parent[i] = -1;
        var queue = new Queue<int>();
        queue.Enqueue(start);
        parent[start] = start;
        while (queue.Count > 0 && parent[goal] < 0)
        {
            int cell = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int next = Neighbor(cell, d);
                if (next < 0 || !IsOpen(cell, d) || parent[next] >= 0) continue;
                parent[next] = cell;
                queue.Enqueue(next);
            }
        }
        if (parent[goal] < 0) return result;
        for (int cell = goal; cell != start; cell = parent[cell]) result.Add(cell);
        result.Reverse();
        return result;
    }
}
