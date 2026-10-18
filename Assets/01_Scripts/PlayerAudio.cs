using UnityEngine;

[RequireComponent(typeof(Player))]
public sealed class PlayerAudio : MonoBehaviour
{
    Player player;
    MazeBetaGame game;
    Vector3 previousPosition;
    float travelled,airtime,breathTime;
    bool wasGrounded,wasRolling,wasActive;
    float speedSeconds,shieldSeconds,doubleSeconds;
    void Awake(){player=GetComponent<Player>();previousPosition=transform.position;}
    void LateUpdate()
    {
        game=player.Game;
        bool active=game&&game.IsPlaying&&player.CanMove;
        if(!active){previousPosition=transform.position;travelled=airtime=0;wasActive=false;return;}
        bool grounded=player.IsGrounded;
        if(!wasActive){wasGrounded=grounded;wasRolling=player.IsRolling;previousPosition=transform.position;wasActive=true;}
        float dt=Time.deltaTime;
        if(player.IsRolling&&!wasRolling){MazeAudioDirector.Play(MazeSound.Roll);travelled=0;}
        if(!grounded)airtime+=dt;
        if(grounded&&!wasGrounded&&airtime>.16f){MazeAudioDirector.Play(MazeSound.Land,null,.7f);airtime=travelled=0;}
        Vector3 step=Vector3.ProjectOnPlane(transform.position-previousPosition,Vector3.up);
        if(grounded&&!player.IsRolling&&player.PlanarSpeed>.3f&&step.magnitude<1)
        {
            travelled+=step.magnitude;
            if(travelled>=(player.IsSprinting?.78f:.65f))
            {travelled=0;MazeAudioDirector.Play(MazeSound.Footstep,null,player.IsSprinting?.95f:.65f);}
        }
        else if(player.PlanarSpeed<.2f)travelled=0;
        breathTime+=dt;
        if(breathTime>(player.IsSprinting?3.4f:7.5f))
        {breathTime=0;MazeAudioDirector.Play(player.IsSprinting?MazeSound.SprintBreath:MazeSound.Breath,null,player.IsSprinting?.42f:.12f);}
        var progress=game.Progress;
        if((speedSeconds>0&&progress.SpeedSeconds<=0)||(shieldSeconds>0&&progress.ShieldSeconds<=0)||(doubleSeconds>0&&progress.DoubleJumpSeconds<=0))
            MazeAudioDirector.Play(MazeSound.PowerExpired);
        speedSeconds=progress.SpeedSeconds;shieldSeconds=progress.ShieldSeconds;doubleSeconds=progress.DoubleJumpSeconds;
        previousPosition=transform.position;wasGrounded=grounded;wasRolling=player.IsRolling;
    }
}
