$ErrorActionPreference = 'Stop'
$taskClockSource = Get-Content (Join-Path $PSScriptRoot '../Assets/01_Scripts/CampaignRunClock.cs') -Raw
Add-Type -TypeDefinition ($taskClockSource + @'
public static class CampaignClockChecks
{
    public static string Run()
    {
        var run = new CampaignRunClock();
        for (int level = 0; level < 9; level++)
        {
            run.Advance(10); run.Advance(2); // failed-attempt time remains counted
            if (run.Finished) throw new Exception("Ranking unlocked before the ninth exit");
            run.CompleteLevel(level);
        }
        if (!run.Finished || run.Seconds != 108) throw new Exception("Nine-level total time lost");
        run.Advance(999); if (run.Seconds != 108) throw new Exception("Finished clock keeps running");
        var resumed = new CampaignRunClock(5, 48, 4, true);
        for (int level = 4; level < 9; level++) { resumed.Advance(12); resumed.CompleteLevel(level); }
        if (!resumed.Finished || resumed.Seconds != 108) throw new Exception("Resume loses campaign time/eligibility");
        foreach (int first in new[] {2, 4, 7, 9})
        {
            var skip = new CampaignRunClock(first);
            for (int level = first - 1; level < 9; level++) skip.CompleteLevel(level);
            if (skip.Finished) throw new Exception("Skipped levels accepted into ranking");
        }
        var duplicate = new CampaignRunClock(); duplicate.CompleteLevel(0); duplicate.CompleteLevel(0);
        if (duplicate.Eligible) throw new Exception("Duplicate victory advances ranking");
        if (new CampaignRunClock(5, 48, 4, false).Eligible) throw new Exception("Legacy checkpoint becomes eligible");
        foreach (float invalid in new[] {float.NaN, float.PositiveInfinity, -1})
            if (new CampaignRunClock(1, invalid).Eligible) throw new Exception("Invalid time accepted");
        var clock = new CampaignRunClock(); clock.Advance(float.NaN); clock.Advance(-1);
        if (clock.Seconds != 0) throw new Exception("Invalid delta corrupts clock");
        return "PASS: full nine-level campaign, retries, resume, skipped/duplicate levels, legacy checkpoints and invalid times.";
    }
}
'@)
[CampaignClockChecks]::Run()
