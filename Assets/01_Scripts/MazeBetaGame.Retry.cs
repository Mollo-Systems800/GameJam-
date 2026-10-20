using UnityEngine;

public sealed partial class MazeBetaGame
{
    [Header("Reintento automatico")]
    [Min(1)] public float defeatRestartDelay = 4;
    public int LossesOnCurrentMaze { get; private set; }
    public bool WaitingForRetry { get; private set; }
    public bool RegenerateOnRetry => WaitingForRetry;
    public bool IsFinalDefeat => !HasWon && Progress != null && Progress.Health == 0;
    public float RetrySecondsRemaining { get; private set; }
    public bool IsPaused => paused;
    public bool IsRunning => running;
    public string RestartNotice { get; private set; }
    float restartNoticeUntil;

    void ResetDefeatCycle()
    {
        LossesOnCurrentMaze = 0;
        WaitingForRetry = false;
        RetrySecondsRemaining = 0;
        RestartNotice = null;
    }

    void ScheduleDefeatRestart()
    {
        LossesOnCurrentMaze++;
        WaitingForRetry = true;
        RetrySecondsRemaining = Mathf.Max(1, defeatRestartDelay);
        RestartNotice = null;
    }

    void TickDefeatRestart()
    {
        if (!WaitingForRetry)
        {
            if (Time.unscaledTime > restartNoticeUntil) RestartNotice = null;
            return;
        }
        RetrySecondsRemaining = Mathf.Max(0, RetrySecondsRemaining - Time.unscaledDeltaTime);
        if (RetrySecondsRemaining > 0) return;
        if (IsFinalDefeat)
        {
            StartCampaign(NewSeed());
            RestartNotice = "Nueva campaña: nivel 1 · 3 vidas.";
        }
        else
        {
            RetryWithNewMaze();
            RestartNotice = $"Nuevo laberinto · {Progress.Health} vidas restantes.";
        }
        restartNoticeUntil = Time.unscaledTime + 6;
    }

    public void RetryWithNewMaze()
    {
        if (!WaitingForRetry || IsCinematicPlaying) return;
        if (IsFinalDefeat) { StartCampaign(NewSeed()); return; }
        int nextSeed = NewSeed();
        if (nextSeed == ActiveSeed) nextSeed = unchecked(ActiveSeed + 1);
        Begin(nextSeed);
        MazeAudioDirector.Play(MazeSound.NewMaze);
    }

    public void ResumeGame()
    {
        if (!running) return;
        if (ShopOpen) SetShopOpen(false);
        paused = false;
        RefreshActivity();
    }
}
