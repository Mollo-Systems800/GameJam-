$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../Assets/01_Scripts/MazeCampaignProgress.cs')
for ($seed = -500; $seed -lt 500; $seed++) {
    $progress = [MazeCampaignProgress]::new()
    $seeds = [System.Collections.Generic.HashSet[int]]::new()
    for ($level = 0; $level -lt 9; $level++) {
        if ($progress.LevelIndex -ne $level -or $progress.AreaIndex -ne [Math]::Floor($level / 3) -or $progress.LevelInArea -ne ($level % 3)) { throw 'Incorrect 3x3 progression' }
        $derived = [MazeCampaignProgress]::SeedForLevel($seed, $level)
        if (!$seeds.Add($derived) -or $derived -ne [MazeCampaignProgress]::SeedForLevel($seed, $level)) { throw 'Non-deterministic or repeated level seed' }
        if ($progress.Advance() -ne ($level -lt 8)) { throw 'Incorrect campaign completion' }
    }
    if (!$progress.IsLastLevel -or $progress.Advance() -or $progress.LevelIndex -ne 8) { throw 'Advanced beyond campaign' }
}
if ([MazeCampaignProgress]::new(-1).LevelIndex -ne 0 -or [MazeCampaignProgress]::new(99).LevelIndex -ne 8) { throw 'Invalid start level clamp' }
'PASS: 1000 campaigns, 3 areas x 3 levels, deterministic distinct seeds, safe start and final level.'
