using System;
using UnityEngine;

public enum MazeSound
{
    Footstep, Jump, DoubleJump, Land, Roll, Breath, SprintBreath, Hurt, ShieldHit,
    Coin, Pills, Heal, Shield, Speed, DoubleJumpPower, PowerExpired,
    UIHover, UIClick, ShopOpen, ShopClose, Purchase, Denied, PrinterFire, PaperImpact,
    ShovelMove, ShovelHit, BossRustle, BossAlert, BossAccelerate, LevelStart, LevelWin,
    AreaWin, Victory, DefeatBoss, DefeatHealth, PageTurn, Stamp, RetryTick, NewMaze
}

[Serializable]
public sealed class MazeAudioCue
{
    public MazeSound sound;
    public AudioClip[] variations;
    [Range(0,1)] public float volume=.65f;
    [Range(0,.3f)] public float pitchVariation=.05f;
    [Min(0)] public float minimumInterval=.06f;
}

[CreateAssetMenu(menuName="Maze/Office audio library")]
public sealed class MazeAudioLibrary : ScriptableObject
{
    public AudioClip menuMusic, normalMusic, threatMusic;
    public AudioClip[] areaTracks;
    public AudioClip introMusic, areaMusic, victoryMusic, bossDefeatMusic, healthDefeatMusic;
    public MazeAudioCue[] cues;
    public AudioClip StoryMusic(MazeStoryBeat kind)
    {
        switch(kind)
        {
            case MazeStoryBeat.AreaComplete:return areaMusic;
            case MazeStoryBeat.CampaignComplete:return victoryMusic;
            case MazeStoryBeat.BossDefeat:return bossDefeatMusic;
            case MazeStoryBeat.HealthDefeat:return healthDefeatMusic;
            default:return introMusic;
        }
    }
}
