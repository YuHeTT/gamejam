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
    }

    public override void Exit()
    {
        base.Exit();
    }

    protected bool IsCurrentState() => stateMachine.currentState == this;
    public override void Update()
    {
        base.Update();   // stateTimer 在这里递减

        // 只要还贴着地面就把土狼时间续期。
        // 原先 stateTimer 只在 Enter 时赋值一次，等它耗尽后（正常走路 0.1 秒后就耗尽了）判定变成
        // "射线一落空就立刻切 Air"，土狼时间实际上只在刚落地那一下有效。
        // 现在把它变成"持续刷新的最近还在支撑上"容差：站在移动平台上时，平台是 Kinematic 且靠
        // transform 逐帧位移，偶尔会比玩家跑得快一点、落地射线短暂落空；有这段容差就不会被误判成
        // 离地，也就不会掉进没有跳跃分支的 PlayerAirState（表现为"移动平台上无法跳跃"）。
        if (player.IsGroundDetected())
            stateTimer = player.coyoteTime;

        //土狼时间清零才判定为离地
        if (stateTimer <= 0)
        {
            stateMachine.ChangeState(player.airState);
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