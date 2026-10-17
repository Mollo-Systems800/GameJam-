$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Assets/01_Scripts/PlayerRollState.cs') -Raw
Add-Type -TypeDefinition ($source + @'
public static class RollChecks
{
    static void Check(bool result, string message)
    {
        if (!result) throw new Exception(message);
    }
    public static string Run()
    {
        foreach (float dt in new[] { 1f / 30, 1f / 60, 1f / 144 })
        {
            var roll = new PlayerRollState();
            roll.Request();
            Check(roll.Step(dt, true, 1.166667f, 0.6f) && roll.Active, "Grounded press did not start");
            roll.Step(dt, false, 1.166667f, 0.6f);
            Check(roll.Active, "A single missed ground frame cancelled the roll");
            roll.Step(dt, true, 1.166667f, 0.6f);
            Check(roll.Active && roll.Progress > 0, "Contact recovery did not resume animation");
            float previous = roll.Progress;
            int steps = 0;
            while (roll.Active && steps++ < 1000)
            {
                roll.Step(dt, true, 1.166667f, 0.6f);
                Check(roll.Progress >= previous && roll.Progress <= 1, "Animation time went backwards");
                previous = roll.Progress;
            }
            Check(!roll.Active && roll.Progress == 1, "Roll did not complete");
            roll.Request();
            Check(!roll.Step(dt, true, 1.166667f, 0.6f), "Cooldown allowed immediate repeat");
            for (int i = 0; i < 200; i++) roll.Step(dt, true, 1.166667f, 0.6f);
            Check(!roll.Active, "Expired buffered input started an unrequested roll");
            roll.Request();
            Check(roll.Step(dt, true, 1.166667f, 0.6f), "New roll did not start after cooldown");
            for (int i = 0; i < 30; i++) roll.Step(dt, false, 1.166667f, 0.6f);
            Check(!roll.Active, "Falling did not interrupt the roll");
            roll.Reset();
            roll.Request();
            Check(!roll.Step(dt, false, 1.166667f, 0.6f), "Roll started in the air");
            Check(roll.Step(dt, true, 1.166667f, 0.6f), "Press just before landing was lost");
            roll.Reset();
            Check(!roll.Active && roll.Progress == 0, "Restart did not reset action");
        }
        return "PASS: roll activation, buffered landing input, transient ground loss, full duration, cooldown, no automatic repeat, fall cancellation and restart at 30/60/144 FPS.";
    }
}
'@)
[RollChecks]::Run()
