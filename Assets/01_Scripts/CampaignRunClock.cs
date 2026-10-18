using System;

// Campaign time survives level changes and retries; only active gameplay advances it.
public sealed class CampaignRunClock
{
    public float Seconds { get; private set; }
    public int CompletedLevels { get; private set; }
    public bool Eligible { get; private set; }
    public bool Finished => Eligible && CompletedLevels == 9;
    public CampaignRunClock(int firstLevel = 1, float seconds = 0, int completed = 0, bool eligible = true)
    {
        bool valid = !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds >= 0;
        Seconds = valid ? seconds : 0;
        CompletedLevels = Math.Max(0, Math.Min(9, completed));
        Eligible = eligible && valid && firstLevel == completed + 1 && firstLevel >= 1 && firstLevel <= 9;
    }
    public void Advance(float delta)
    {
        if (delta > 0 && !float.IsInfinity(delta) && !float.IsNaN(delta) && CompletedLevels < 9) Seconds += delta;
    }
    public void CompleteLevel(int index)
    {
        if (index != CompletedLevels || index < 0 || index >= 9) { Eligible = false; return; }
        CompletedLevels++;
    }
}
