using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerWallSlideState : PlayerState
{
    public PlayerWallSlideState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }
    public override void Enter()
    {
        player.hasDashedInAir = false;
        player.hasDoubleJumped = false;
        base.Enter();
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();
        player.SetVelocity(0,-player.freeWallSlideSpeed);

        if(xInput != 0 && player.facingDir == -xInput)
        {
            stateMachine.ChangeState(player.airState);
            return;
        }

        if (player.wallJumpBufferTimer > 0)
        {   
            player.wallJumpBufferTimer = 0;
            stateMachine.ChangeState(player.wallJumpState);
            return;
        }

        if (player.IsGroundDetected())
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }
    }

}
