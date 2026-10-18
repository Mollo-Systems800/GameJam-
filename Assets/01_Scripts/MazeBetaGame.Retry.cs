using UnityEngine;

public sealed partial class MazeBetaGame
{
    [Header("Reintento automatico")]
    [Min(1)] public float defeatRestartDelay = 4;
    public int LossesOnCurrentMaze { get; private set; }
    public bool WaitingForRetry { get; private set; }
    public bool RegenerateOnRetry => WaitingForRetry && LossesOnCurrentMaze >= 2;
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
        bool regenerate = RegenerateOnRetry;
        int nextSeed = ActiveSeed;
        if (regenerate)
        {
            nextSeed = NewSeed();
            if (nextSeed == ActiveSeed) nextSeed = unchecked(ActiveSeed + 1);
        }
        Begin(nextSeed);
        if(regenerate)MazeAudioDirector.Play(MazeSound.NewMaze);
        RestartNotice = regenerate ? "Nuevo laberinto generado tras 2 derrotas."
            : "Segundo intento: el laberinto se mantiene. Otra derrota generará uno nuevo.";
        restartNoticeUntil = Time.unscaledTime + 6;
    }

    public void ResumeGame()
    {
        if (!running) return;
        if (ShopOpen) SetShopOpen(false);
        paused = false;
        RefreshActivity();
    }
}
