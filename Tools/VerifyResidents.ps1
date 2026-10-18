$ErrorActionPreference = 'Stop'
$sources=@('MazeGrid.cs','MazeObstacles.cs','MazeSeatedResidents.cs','MazeEncounters.cs') | ForEach-Object {
    (Get-Content -LiteralPath (Join-Path $PSScriptRoot "../Assets/01_Scripts/$_") -Raw) -replace '(?m)^using .*;\r?\n',''
}
Add-Type -TypeDefinition ("using System;`nusing System.Collections.Generic;`n"+($sources -join "`n")+@'
public static class ResidentChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static string Run()
    {
        for(int seed=-500;seed<500;seed++)
        {
            var grid=new MazeGrid(12,12,seed,1,.25f,17,5);
            var residents=MazeSeatedResidents.Select(grid,seed,3,null);
            var repeat=MazeSeatedResidents.Select(grid,seed,3,null);
            Check(residents.Count>=3,"Less than three women");
            var pits=MazeObstacles.Select(grid,seed,10,residents);
            for(int i=0;i<residents.Count;i++)
            {
                int c=residents[i];
                Check(c==repeat[i] && c!=0 && !grid.IsExitCell(c) && !grid.IsParkourCell(c) && !pits.Contains(c),"Unsafe seat");
            }
            var encounters=new MazeEncounters(grid,seed,pits,1,6,4,36,residents);
            foreach(int c in residents)
                Check(!encounters.Coins.Contains(c) && !encounters.Spades.Contains(c) && !encounters.Turrets.Contains(c),"Encounter overlaps a woman");
        }
        Check(MazeSeatedResidents.EntryFraction(-3,0,6,0,-1,1,-1,1)<.34f,"Fast movement bypasses zone");
        Check(MazeSeatedResidents.EntryFraction(3,0,-6,0,-1,1,-1,1)<.34f,"Reverse movement bypasses zone");
        Check(MazeSeatedResidents.EntryFraction(0,0,.1f,0,-1,1,-1,1)==0,"Walking allowed inside zone");
        Check(MazeSeatedResidents.EntryFraction(-3,3,6,0,-1,1,-1,1)==1,"Unrelated corridor blocked");
        return "PASS: 1000 layouts; >=3 deterministic seated women; no spawn/exit/pit/encounter overlap; swept proximity blocks both directions and fast movement.";
    }
}
'@)
[ResidentChecks]::Run()
