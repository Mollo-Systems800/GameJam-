$ErrorActionPreference = 'Stop'
$mazeSource = Join-Path $PSScriptRoot '..\Assets\01_Scripts\MazeGrid.cs'
$obstacleSource = Join-Path $PSScriptRoot '..\Assets\01_Scripts\MazeObstacles.cs'
$obstacleCode = (Get-Content -LiteralPath $obstacleSource -Raw) -replace '(?m)^using .*;\r?\n', ''
Add-Type -TypeDefinition ((Get-Content -LiteralPath $mazeSource -Raw) + $obstacleCode + @'
public static class MazeTopologyChecks
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    public static string Run()
    {
        var signatures = new HashSet<string>();
        for (int seed = -500; seed < 500; seed++)
        {
            int w = 3 + Math.Abs(seed % 38), h = 3 + Math.Abs(seed % 37);
            var maze = new MazeGrid(w, h, seed);
            var twin = new MazeGrid(w, h, seed);
            var distances = maze.DistancesFrom(0);
            int edges = 0, exits = 0, longestBoundary = 0;
            for (int c = 0; c < maze.Count; c++)
            {
                Check(distances[c] >= 0, "Disconnected maze: " + seed);
                for (int d = 0; d < 4; d++)
                {
                    int neighbor = maze.Neighbor(c, d);
                    if (neighbor < 0) longestBoundary = Math.Max(longestBoundary, distances[c]);
                    Check(maze.IsOpen(c, d) == twin.IsOpen(c, d), "Non-deterministic seed");
                    if (!maze.IsOpen(c, d)) continue;
                    if (neighbor < 0) { exits++; continue; }
                    Check(maze.IsOpen(neighbor, (d + 2) % 4), "Non-reciprocal passage");
                    edges++;
                }
            }
            Check(exits == 1, "Exit count: " + exits);
            Check(edges / 2 == maze.Count - 1, "Maze has loops or disconnected cells");
            Check(distances[maze.ExitCell] == longestBoundary, "Exit is not at farthest boundary");
            Check(maze.Neighbor(maze.ExitCell, maze.ExitDirection) < 0, "Exit is not exterior");
            // Check paths in both directions, including same-cell and invalid requests.
            int[] targets = { 0, maze.ExitCell, maze.Count / 2, maze.Count - 1 };
            foreach (int target in targets)
            {
                int current = 0;
                var path = maze.FindPath(0, target);
                foreach (int next in path)
                {
                    bool valid = false;
                    for (int d = 0; d < 4; d++)
                        valid |= maze.Neighbor(current, d) == next && maze.IsOpen(current, d);
                    Check(valid, "BFS crosses wall");
                    current = next;
                }
                Check(current == target && path.Count == distances[target], "Invalid BFS distance");
                var reverse = maze.FindPath(target, 0);
                Check(reverse.Count == path.Count, "Asymmetric BFS distance");
            }
            Check(maze.FindPath(-1, 0).Count == 0 && maze.FindPath(0, maze.Count).Count == 0, "Invalid path request");
            var sample = new MazeGrid(12, 12, seed);
            var signature = new System.Text.StringBuilder();
            for (int c = 0; c < sample.Count; c++)
                for (int d = 0; d < 4; d++) signature.Append(sample.IsOpen(c, d) ? '1' : '0');
            signatures.Add(signature.ToString());
            int wantedExits = 1 + Math.Abs(seed % 5);
            var multi = new MazeGrid(w, h, seed, wantedExits);
            int openingCount = 0;
            var exitCells = new HashSet<int>();
            foreach (var exit in multi.Exits)
            {
                Check(exit.Cell != 0 && exitCells.Add(exit.Cell), "Unsafe or duplicate exit cell");
                Check(multi.Neighbor(exit.Cell, exit.Direction) < 0 && multi.IsOpen(exit.Cell, exit.Direction), "Invalid exit");
                Check(multi.FindPath(0, exit.Cell).Count > 0, "Unreachable exit");
            }
            for (int c = 0; c < multi.Count; c++)
                for (int d = 0; d < 4; d++)
                    if (multi.Neighbor(c, d) < 0 && multi.IsOpen(c, d)) openingCount++;
            Check(openingCount == wantedExits && multi.Exits.Count == wantedExits, "Incorrect multiple exit count");
            var obstacles = MazeObstacles.Select(multi, seed, 10);
            var repeated = MazeObstacles.Select(multi, seed, 10);
            Check(obstacles.Count <= 10 && repeated.Count == obstacles.Count, "Obstacle count mismatch");
            Check(MazeObstacles.Select(multi, seed, 0).Count == 0, "Zero obstacles ignored");
            for (int i = 0; i < obstacles.Count; i++)
            {
                int c = obstacles[i];
                Check(c == repeated[i] && c != 0 && !multi.IsExitCell(c), "Unsafe or non-deterministic obstacle");
                int open = 0;
                for (int d = 0; d < 4; d++)
                {
                    if (multi.IsOpen(c, d)) open++;
                    int n = multi.Neighbor(c, d);
                    Check(!multi.IsExitCell(n) && !obstacles.Contains(n), "Obstacle beside exit or obstacle");
                }
                Check(open == 2 && multi.IsOpen(c, 0) == multi.IsOpen(c, 2), "Obstacle is not in straight corridor");
            }
        }
        Check(signatures.Count > 990, "Insufficient variation across seeds");
        return "PASS: 1000 seeds, dimensions 3..40; single and multiple exits (1..5), connectivity, BFS, reproducibility, variation, safe deterministic non-adjacent jumping sections.";
    }
}
'@)
[MazeTopologyChecks]::Run()
