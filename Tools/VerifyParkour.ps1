$ErrorActionPreference = 'Stop'
$sources = @('MazeGrid.cs', 'MazeObstacles.cs', 'MazeParkourLayout.cs') | ForEach-Object {
    (Get-Content -LiteralPath (Join-Path $PSScriptRoot "../Assets/01_Scripts/$_") -Raw) -replace '(?m)^using .*;\r?\n', ''
}
Add-Type -TypeDefinition ("using System;`nusing System.Collections.Generic;`n" + ($sources -join "`n") + @'
public static class ParkourChecks
{
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    public static string Run()
    {
        for (int seed = -500; seed < 500; seed++)
        {
            int w = 5 + Math.Abs(seed % 20), h = 3 + Math.Abs(seed % 17);
            var grid = new MazeGrid(w, h, seed, 1, .25f, (int)Math.Ceiling((w+h-2)*.75), 5);
            var twin = new MazeGrid(w, h, seed, 1, .25f, (int)Math.Ceiling((w+h-2)*.75), 5);
            Check(grid.ParkourCells.Count >= 3, "No parkour section");
            Check(grid.Exits.Count == 1, "Changed exit count");
            var distances = grid.DistancesFrom(0);
            for (int c=0; c<grid.Count; c++)
            {
                Check(distances[c] >= 0, "Parkour disconnected the maze");
                for (int d=0; d<4; d++)
                {
                    Check(grid.IsOpen(c,d)==twin.IsOpen(c,d), "Non-deterministic layout");
                    int n=grid.Neighbor(c,d);
                    if (n>=0) Check(grid.IsOpen(c,d)==grid.IsOpen(n,(d+2)%4), "Asymmetric edge");
                    if (grid.IsParkourPit(c) && d%2 != grid.ParkourDirection%2)
                        Check(!grid.IsOpen(c,d), "Side door bypasses platforms");
                }
            }
            for (int i=0; i<grid.ParkourCells.Count; i++)
            {
                int c=grid.ParkourCells[i];
                Check(c==twin.ParkourCells[i] && c!=0 && !grid.IsExitCell(c), "Unsafe parkour placement");
                if(i>0) Check(grid.Neighbor(grid.ParkourCells[i-1],grid.ParkourDirection)==c &&
                    grid.IsOpen(grid.ParkourCells[i-1],grid.ParkourDirection), "Wall splits parkour");
            }
            foreach(int cell in MazeObstacles.Select(grid,seed,10)) Check(!grid.IsParkourCell(cell), "Overlapping obstacles");
            for (int size=2; size<=6; size++)
            {
                var layout = new MazeParkourLayout((grid.ParkourCells.Count-2)*size,1.8f);
                Check(layout.Platforms.Count>=2 && layout.Gap>=1.25f && layout.Gap<=2.16f, "Unsafe gap");
                for (int i=1;i<layout.Platforms.Count;i++)
                {
                    var a=layout.Platforms[i-1]; var b=layout.Platforms[i];
                    Check(Math.Abs(b.Start-a.Start-a.Length-layout.Gap)<.001f, "Incorrect empty gap");
                    Check(a.Length>=.35f && b.Length>=.35f, "Unusable platform");
                }
            }
        }
        return "PASS: 1000 seeds; connected, deterministic parkour; one distant exit; clear platform gaps; no side bypasses or overlapping obstacles.";
    }
}
'@)
[ParkourChecks]::Run()
