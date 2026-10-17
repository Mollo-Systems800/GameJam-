$ErrorActionPreference = 'Stop'
$scripts = Join-Path $PSScriptRoot '..\Assets\01_Scripts'
$code = "using System;`nusing System.Collections.Generic;`n"
foreach ($name in @('MazeGrid','MazeObstacles','MazeEncounters','RunProgress')) {
    $code += (Get-Content -LiteralPath (Join-Path $scripts ($name + '.cs')) -Raw) -replace '(?m)^using .*;\r?\n', ''
    $code += "`n"
}
Add-Type -TypeDefinition ($code + @'
public static class RunFeatureChecks
{
    static void Check(bool condition, string reason)
    {
        if (!condition) throw new Exception(reason);
    }
    public static string Run()
    {
        var run = new RunProgress();
        Check(run.Health == 3 && run.Coins == 0 && run.Score == 10000, "Initial state");
        Check(!run.TryBuy(PowerUpKind.Speed) && !run.TryBuy(PowerUpKind.Heal), "Empty wallet purchase");
        run.AddCoins(20);
        Check(!run.TryBuy(PowerUpKind.Heal) && run.Coins == 20, "Healing full health charged coins");
        Check(!run.TryBuy((PowerUpKind)99) && run.Coins == 20, "Unknown purchase charged coins");
        Check(run.TryBuy(PowerUpKind.Speed) && run.Coins == 14 && run.SpeedMultiplier == 1.5f, "Speed purchase");
        Check(run.TryBuy(PowerUpKind.Shield) && run.Coins == 6, "Shield purchase");
        Check(!run.TakeDamage(3) && run.Health == 3, "Shield did not block boss damage");
        run.Advance(8.1f);
        Check(run.ShieldSeconds == 0 && run.SpeedSeconds > 0, "Buff durations");
        Check(run.TakeDamage(1) && run.Health == 2 && !run.TakeDamage(1), "Damage cooldown");
        Check(run.TryBuy(PowerUpKind.Heal) && run.Health == 3 && run.Coins == 2, "Healing purchase");
        Check(run.TotalCoins == 20, "Spending changed collection score");
        run.Advance(2);
        Check(run.SpeedSeconds == 0 && run.SpeedMultiplier == 1, "Speed did not expire");
        int before = run.Score;
        run.Advance(5);
        Check(run.Score < before && run.Coins == 2, "Score did not reward faster escape");
        Check(run.TakeDamage(3) && run.Health == 0 && !run.TakeDamage(1), "Lethal damage");
        float elapsed = run.Elapsed;
        run.Advance(float.NaN); run.Advance(float.PositiveInfinity); run.Advance(-5);
        run.AddCoins(-10);
        Check(run.Elapsed == elapsed && run.Coins == 2, "Invalid input mutated state");
        var reset = new RunProgress();
        Check(reset.Coins == 0 && reset.Health == 3 && reset.Elapsed == 0 && reset.SpeedSeconds == 0, "Run reset");
        var exact = new RunProgress();
        exact.AddCoins(6);
        Check(exact.TryBuy(PowerUpKind.Speed) && exact.Coins == 0 && !exact.TryBuy(PowerUpKind.Speed), "Exact balance/overspending");
        for (int seed = -500; seed < 500; seed++)
        {
            var grid = new MazeGrid(3 + Math.Abs(seed % 18), 3 + Math.Abs(seed % 17), seed, 4, 0.25f);
            var pits = MazeObstacles.Select(grid, seed, 10);
            int boss = 1;
            for (int c = 1; c < grid.Count; c++)
                if (!grid.IsExitCell(c) && !pits.Contains(c)) { boss = c; break; }
            var plan = new MazeEncounters(grid, seed, pits, boss, 6, 4, 36);
            var twin = new MazeEncounters(grid, seed, pits, boss, 6, 4, 36);
            var occupied = new HashSet<int>();
            var enemies = new List<int>(plan.Spades); enemies.AddRange(plan.Turrets);
            var distances = grid.DistancesFrom(0);
            Check(plan.Spades.Count <= 6 && plan.Turrets.Count <= 4 && plan.Coins.Count <= 36, "Spawn limits");
            foreach (int c in enemies)
            {
                Check(c != 0 && c != boss && !grid.IsExitCell(c) && !pits.Contains(c), "Unsafe enemy spawn");
                Check(distances[c] >= 4 && occupied.Add(c), "Enemy spawn distance/duplicate");
                for (int d = 0; d < 4; d++)
                    Check(!grid.IsExitCell(grid.Neighbor(c,d)) && !enemies.Contains(grid.Neighbor(c,d)), "Enemy crowding/exit obstruction");
            }
            foreach (int c in plan.Coins)
                Check(c != 0 && c != boss && !grid.IsExitCell(c) && !pits.Contains(c) && occupied.Add(c), "Unsafe/duplicate coin");
            Check(string.Join(",",plan.Spades) == string.Join(",",twin.Spades)
                && string.Join(",",plan.Turrets) == string.Join(",",twin.Turrets)
                && string.Join(",",plan.Coins) == string.Join(",",twin.Coins), "Non-deterministic encounters");
            var empty = new MazeEncounters(grid, seed, pits, boss, 0, 0, 0);
            Check(empty.Spades.Count == 0 && empty.Turrets.Count == 0 && empty.Coins.Count == 0, "Zero counts ignored");
        }
        return "PASS: shop balances, prices, healing, shield, timed speed, damage cooldown, score, restart state, invalid input; 1000 deterministic encounter layouts with safe enemy/coin placement.";
    }
}
'@)
[RunFeatureChecks]::Run()
