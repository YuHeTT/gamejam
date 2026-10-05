using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDoubleJumpState : PlayerState
{
    public PlayerDoubleJumpState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        player.hasDoubleJumped = true;
        player.SetVelocity(rb.velocity.x,player.doubleJumpSpeed);
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();
        player.HorizontalMoveController();
        player.JumpHeightController();
        
        if(rb.velocity.y <= 0)
            stateMachine.ChangeState(player.airState);
    }
}
