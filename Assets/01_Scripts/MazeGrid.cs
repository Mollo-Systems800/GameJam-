using System;
using System.Collections.Generic;

// DFS builds connectivity, then extra internal passages provide alternate routes.
public sealed class MazeGrid
{
    public readonly int Width;
    public readonly int Height;
    public readonly int Seed;
    public int ExtraConnectionCount { get; private set; }
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
    readonly List<int> parkourCells = new List<int>();
    public IReadOnlyList<int> ParkourCells => parkourCells;
    public int ParkourDirection { get; private set; }

    public MazeGrid(int width, int height, int seed, int exitCount = 1, float alternativeRoutes = 0, int minimumExitDistance = 0, int parkourLength = 0)
    {
        Width = Math.Max(3, width);
        Height = Math.Max(3, height);
        Seed = seed;
        passages = new int[Width * Height];
        // System.Random treats +n and -n identically; mix the sign into the seed first.
        var random = new Random(seed ^ (int)((uint)seed >> 16));
        ReserveParkour(random, parkourLength);
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
                if (next >= 0 && !visited[next] && AllowsParkourEdge(cell, next, d)) choices[count++] = d;
            }
            if (count == 0) { stack.Pop(); continue; }
            int direction = choices[random.Next(count)];
            int neighbor = Neighbor(cell, direction);
            passages[cell] |= 1 << direction;
            passages[neighbor] |= 1 << ((direction + 2) % 4);
            visited[neighbor] = true;
            stack.Push(neighbor);
        }
        // DFS can reach the two ends independently. Open the complete reserved
        // chain afterwards so no wall can split the jumping sequence in half.
        for (int i = 1; i < parkourCells.Count; i++)
        {
            int from = parkourCells[i - 1], to = parkourCells[i];
            if (!IsOpen(from, ParkourDirection)) ExtraConnectionCount++;
            passages[from] |= 1 << ParkourDirection;
            passages[to] |= 1 << ((ParkourDirection + 2) % 4);
        }
        if (!float.IsNaN(alternativeRoutes) && alternativeRoutes > 0)
        {
            var walls = new List<int>();
            for (int cell = 1; cell < Count; cell++)
                for (int d = 0; d < 4; d++)
                {
                    int next = Neighbor(cell, d);
                    if (next > cell && !IsOpen(cell, d) && AllowsParkourEdge(cell, next, d)) walls.Add(cell * 4 + d);
                }
            for (int i = walls.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int wall = walls[i]; walls[i] = walls[j]; walls[j] = wall;
            }
            int openings = Math.Min(walls.Count, (int)Math.Ceiling((Count - 1) * Math.Min(1, alternativeRoutes)));
            for (int i = 0; i < openings; i++)
            {
                int cell = walls[i] / 4, direction = walls[i] % 4;
                int next = Neighbor(cell, direction);
                passages[cell] |= 1 << direction;
                passages[next] |= 1 << ((direction + 2) % 4);
                ExtraConnectionCount++;
            }
        }
        // Spread exits across distinct boundary cells after creating alternative routes.
        int[] distances = DistancesFrom(0);
        var candidates = new List<Exit>();
        minimumExitDistance = Math.Max(0, Math.Min(Width + Height - 2, minimumExitDistance));
        for (int cell = 0; cell < passages.Length; cell++)
        {
            // Physical distance as well as route length: a long detour must not
            // put the door just beside the spawn on the other side of a wall.
            if (cell == 0 || cell % Width + cell / Width < minimumExitDistance) continue;
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

    void ReserveParkour(Random random, int requested)
    {
        if (requested < 3 || Math.Max(Width, Height) < 5) return;
        bool horizontal = Width >= 5 && (Height < 5 || random.Next(2) == 0);
        ParkourDirection = horizontal ? 1 : 0;
        int length = Math.Min(Math.Max(3, requested), (horizontal ? Width : Height) - 2);
        int along = random.Next(1, (horizontal ? Width : Height) - length);
        int across = random.Next(1, (horizontal ? Height : Width) - 1);
        int cell = horizontal ? across * Width + along : along * Width + across;
        for (int i = 0; i < length; i++)
        {
            parkourCells.Add(cell);
            cell = Neighbor(cell, ParkourDirection);
        }
    }

    public bool IsParkourCell(int cell) => parkourCells.Contains(cell);
    public bool IsParkourPit(int cell)
    {
        int index = parkourCells.IndexOf(cell);
        return index > 0 && index < parkourCells.Count - 1;
    }

    bool AllowsParkourEdge(int cell, int next, int direction) =>
        direction % 2 == ParkourDirection % 2 || (!IsParkourPit(cell) && !IsParkourPit(next));

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

    public List<int> FindPath(int start, int goal, ISet<int> blocked = null, bool allowPartial = false)
    {
        var result = new List<int>();
        if (start < 0 || goal < 0 || start >= Count || goal >= Count) return result;
        var parent = new int[Count];
        for (int i = 0; i < Count; i++) parent[i] = -1;
        var queue = new Queue<int>();
        queue.Enqueue(start);
        parent[start] = start;
        int closest = start;
        int closestDistance = Math.Abs(start % Width - goal % Width) + Math.Abs(start / Width - goal / Width);
        while (queue.Count > 0 && parent[goal] < 0)
        {
            int cell = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int next = Neighbor(cell, d);
                if (next < 0 || !IsOpen(cell, d) || parent[next] >= 0 || (blocked != null && blocked.Contains(next))) continue;
                parent[next] = cell;
                queue.Enqueue(next);
                if (allowPartial)
                {
                    int distance = Math.Abs(next % Width - goal % Width) + Math.Abs(next / Width - goal / Width);
                    if (distance < closestDistance) { closest = next; closestDistance = distance; }
                }
            }
        }
        if (parent[goal] < 0)
        {
            if (!allowPartial) return result;
            goal = closest;
        }
        for (int cell = goal; cell != start; cell = parent[cell]) result.Add(cell);
        result.Reverse();
        return result;
    }
}
