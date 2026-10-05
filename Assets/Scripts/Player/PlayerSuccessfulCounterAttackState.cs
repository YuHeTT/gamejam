using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSuccessfulCounterAttackState : PlayerState
{
    public PlayerSuccessfulCounterAttackState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = player.counterAttackDuration;
        player.SetZeroVelocity();
        CameraShaker.Instance?.RequestShake(1f,0.08f);
        TimeManager.FrameFreeze(0.08f);
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        player.SetZeroVelocity();
        if (triggerCalled || stateTimer <= 0f)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }
    }
}

