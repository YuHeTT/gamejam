using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerGroundedState : PlayerState
{
    public PlayerGroundedState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = player.coyoteTime;
        player.hasDashedInAir = false;
        player.hasDoubleJumped = false;
    }

    public override void Exit()
    {
        base.Exit();
    }

    protected bool IsCurrentState() => stateMachine.currentState == this;
    public override void Update()
    {
        base.Update();
        //土狼时间清零才判定为离地
        if(stateTimer <= 0 && !player.IsGroundDetected())
        {
            stateMachine.ChangeState(player.airState);
            return;
        }

        //地面上发动反击的条件：缓冲大于零且冷却已好
        if (player.counterAttackBufferTimer > 0f && player.counterAttackCoolDownTimer < 0)
        {
            player.counterAttackBufferTimer = 0f;
            player.counterAttackCoolDownTimer = player.counterAttackCoolDownTime;

            stateMachine.ChangeState(player.counterAttackState);
            return;
        }
        //跳跃
        if (player.jumpBufferTimer > 0)
        {
            player.jumpBufferTimer = 0;
            stateMachine.ChangeState(player.jumpState);
        }
    }
}