using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkeletonIdleState : SkeletonGroundedState
{
    public SkeletonIdleState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, EnemySkeleton enemy) : base(_enemyBase, _stateMachine, _animBoolName, enemy)
    {
    }

    public override void Enter()
    {
        base.Enter();
        enemy.SetZeroVelocity();
        stateTimer = enemy.idleTime;
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();
        if (!enemy.IsGroundDetected())
        {
            stateMachine.ChangeState(enemy.airState);
            return;
        }
        if (stateMachine.currentState != this)
        {
            return;
        }

        if(stateTimer < 0)
        {
            stateMachine.ChangeState(enemy.moveState);
            return;
        }
    }
}
