using UnityEngine;

public sealed partial class MazeBetaGame
{
    [Header("Cinematicas de historia")]
    public bool playNewGameIntro = true;
    [Range(5,20)] public float newGameIntroDuration = 9;
    public bool playOutcomeCinematics = true;
    [Range(5,20)] public float areaCinematicDuration = 7;
    [Range(5,20)] public float victoryCinematicDuration = 11;
    [Range(5,20)] public float defeatCinematicDuration = 6;
    NewGameCinematic intro;
    public bool IsCinematicPlaying => intro && intro.IsActive;
    public bool IsIntroPlaying => IsCinematicPlaying && intro.Kind == MazeStoryBeat.NewGame;
    public void StartNewCampaign(int newSeed)
    { StartCampaign(newSeed); PlayNewGameIntro(); }
    public void PlayNewGameIntro()
    {
        if (!playNewGameIntro || !IsPlaying || Grid == null || !Player || !Boss || !view || IsCinematicPlaying) return;
        if (!intro) intro = gameObject.AddComponent<NewGameCinematic>();
        intro.Play(this,view,followCamera,newGameIntroDuration);
        RefreshActivity();
    }
    public void SkipNewGameIntro() { if (intro) intro.Skip(); }
    public void SkipCinematic() { if (intro) intro.Skip(); }
    void PlayOutcomeCinematic()
    {
        if (!playOutcomeCinematics || !Player || !Boss || !view) return;
        MazeStoryBeat beat;
        float seconds;
        if (HasWon)
        {
            if (CampaignComplete) { beat=MazeStoryBeat.CampaignComplete;seconds=victoryCinematicDuration; }
            else if (CurrentLevelInArea==2) { beat=MazeStoryBeat.AreaComplete;seconds=areaCinematicDuration; }
            else return;
        }
        else { beat=DefeatedByBoss?MazeStoryBeat.BossDefeat:MazeStoryBeat.HealthDefeat;seconds=defeatCinematicDuration; }
        if (!intro) intro=gameObject.AddComponent<NewGameCinematic>();
        intro.Play(this,view,followCamera,seconds,beat);
        RefreshActivity();
    }
    internal void IntroFinished()
    { if (Player && Boss) RefreshActivity(); }
}
