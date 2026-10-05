using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>卡住状态：保持压扁，左右移动播放行走动画但不产生位移，且无法跳跃</summary>
public class PlayerStuckState : PlayerState
{
    public PlayerStuckState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        player.isStuck = true;
        player.SetSquashed(true);       //保持压扁
        rb.gravityScale = player.gravityScale;
    }

    public override void Exit()
    {
        base.Exit();
        player.isStuck = false;
        ClearLocomotionAnim();
    }

    public override void Update()
    {
        base.Update();

        //原地踏步：水平速度强制为 0，但动画照常播放
        player.SetVelocity(0, rb.velocity.y);

        //朝向试图移动的方向（速度被清零，需单独翻转）
        player.FlipController(xInput);

        bool moving = xInput != 0;
        player.anim.SetBool("Idle", !moving);
        player.anim.SetBool("Move", moving);
        player.anim.SetBool("Jump", false);
    }

    private void ClearLocomotionAnim()
    {
        player.anim.SetBool("Idle", false);
        player.anim.SetBool("Move", false);
        player.anim.SetBool("Jump", false);
    }
}