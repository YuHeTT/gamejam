using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMoveState : PlayerGroundedState
{
    //进入移动状态后稍等一小段时间再播第一步，避免按键瞬间就响。
    private const float WalkSoundStartDelay = 0.2f;
    private const float WalkSoundInterval = 0.35f;
    private float walkSoundTimer;

    public PlayerMoveState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        walkSoundTimer = WalkSoundStartDelay;
    }

    public override void Exit()
    {
        //走路状态结束时立刻停止专用走路音源，避免松开方向键后仍残留播放。
        musicmanager.StopWalkSound();
        walkSoundTimer = 0f;
        base.Exit();
    }

    public override void Update()
    {
        base.Update();
        if(!IsCurrentState()) return;
        player.HorizontalMoveController();

        if(xInput == 0)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        walkSoundTimer -= Time.deltaTime;
        if (walkSoundTimer <= 0f)
        {
            musicmanager.PlayWalkSound();
            walkSoundTimer = WalkSoundInterval;
        }
    }
}
