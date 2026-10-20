$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '../Assets/01_Scripts/MazeLevelDifficulty.cs') -Raw
$grid = (Get-Content (Join-Path $PSScriptRoot '../Assets/01_Scripts/MazeGrid.cs') -Raw) -replace '(?m)^using .*;\r?\n', ''
Add-Type -TypeDefinition ("using System.Collections.Generic;`n" + $source + $grid + @'
public static class MazeDifficultyChecks
{
    public static string Run()
    {
        var previous = new MazeLevelDifficulty(0,10,10,10,6,4,.25f,2.2f,5);
        int[] widths={10,12,12,14,14,16,16,18,18};
        int[] heights={10,10,12,12,14,14,16,16,18};
        for(int level=0;level<9;level++)
        {
            var current = new MazeLevelDifficulty(level,10,10,10,6,4,.25f,2.2f,5);
            if(current.Width!=widths[level] || current.Height!=heights[level] ||
                current.Obstacles!=10+2*level || current.Spades!=6+level || current.Turrets!=4+level/2)
                throw new Exception("Incorrect level scaling");
            if(level>0 && (current.Width*current.Height<=previous.Width*previous.Height || current.BossSpeed<=previous.BossSpeed || current.BossGracePeriod>=previous.BossGracePeriod || current.AlternativeRoutes>=previous.AlternativeRoutes))
                throw new Exception("Difficulty is not progressive");
            previous=current;
        }
        var capped = new MazeLevelDifficulty(8,39,40,10,6,4,.25f,2.2f,5);
        if(capped.Width!=40 || capped.Height!=40)throw new Exception("Size cap exceeded");
        var sides=new int[4];
        for(int seed=0;seed<4000;seed++)
        {
            var maze=new MazeGrid(12,12,seed,1,.25f,17,5);
            sides[maze.ExitDirection]++;
            if(maze.Exits.Count!=1 || maze.ExitCell==0 || maze.FindPath(0,maze.ExitCell).Count==0)
                throw new Exception("Unsafe exit");
        }
        foreach(int count in sides)if(count<800 || count>1200)throw new Exception("Biased exit sides");
        return "PASS: levels 1..9 grow 10x10 to 18x18, alternating rectangular/square maps with strictly increasing area; increasing hazards/enemies/boss pressure; size cap 40; 4000 reachable exits with side counts N/E/S/W: "+string.Join(", ",sides);
    }
}
'@)
[MazeDifficultyChecks]::Run()
