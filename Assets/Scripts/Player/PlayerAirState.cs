using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAirState : PlayerState
{
    public PlayerAirState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();
        //空中也能控制移动
        player.HorizontalMoveController();
        if (player.IsWallDetected())
        {
            stateMachine.ChangeState(player.wallSlideState);
            return;
        }

        if(player.IsGroundDetected())
        {            
            stateMachine.ChangeState(player.idleState);
            return;    
        }
        if (player.canDoubleJump)
            if(player.jumpBufferTimer > 0 && !player.hasDoubleJumped)
            {
                player.jumpBufferTimer = 0;
                stateMachine.ChangeState(player.doubleJumpState);
                return;
            }
    }
}
