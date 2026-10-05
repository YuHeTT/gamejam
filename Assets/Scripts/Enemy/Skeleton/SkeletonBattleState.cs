using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

public class SkeletonBattleState : EnemyState
{
    private Transform player;
    private EnemySkeleton enemy;
    private int moveDir;
    public SkeletonBattleState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName,EnemySkeleton enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        base.Enter();
        player = PlayerManager.instance.player.transform;
        stateTimer = enemy.battleTime;
        enemy.anim.SetBool("Idle", false);
    }

    public override void Exit()
    {
        base.Exit();
        enemy.anim.SetBool("Idle", false);
    }

    public override void Update()
    {
        base.Update();

        RaycastHit2D playerDetected = enemy.IsPlayerDetected();

        if (playerDetected)
        {
            stateTimer = enemy.battleTime;
        }
        else if (stateTimer < 0 || Vector2.Distance(player.position, enemy.transform.position) > 15f)
        {
            enemy.SetZeroVelocity();
            stateMachine.ChangeState(enemy.idleState);
            return;
        }

        if (!enemy.CanAttack())
        {
            enemy.SetZeroVelocity();
            enemy.anim.SetBool("Move", false);
            enemy.anim.SetBool("Idle", true);
            return;
        }

        enemy.anim.SetBool("Idle", false);

        if (playerDetected && playerDetected.distance < enemy.attackDistance)
        {
            enemy.SetZeroVelocity();
            enemy.anim.SetBool("Move", false);
            stateMachine.ChangeState(enemy.attackState);
            return;
        }

        enemy.anim.SetBool("Move", true);

        if(player.position.x > enemy.transform.position.x)
        {
            moveDir = 1;
        }
        else if(player.position.x < enemy.transform.position.x)
        {
            moveDir = -1;
        }
        enemy.SetVelocity(enemy.moveSpeed * 3f * moveDir,rb.velocity.y);
    }
}
