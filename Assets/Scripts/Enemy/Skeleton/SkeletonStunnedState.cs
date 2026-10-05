using UnityEngine;

public class SkeletonStunnedState : EnemyState
{
    private EnemySkeleton enemy;
    private float stunnedKnockTime;
    private float i;

    public SkeletonStunnedState(Enemy _enemyBase,EnemyStateMachine _stateMachine,string _animBoolName,EnemySkeleton _enemy): base(_enemyBase, _stateMachine, _animBoolName)
    {
        enemy = _enemy;
    }

    public override void Enter()
    {
        base.Enter();
        i = 0;
        stateTimer = enemy.stunnedTime;
        stunnedKnockTime = enemy.stunnedKnockTime;
        enemy.fx.InvokeRepeating("RedColorBlink",0,0.15f);
    }

    public override void Exit()
    {
        base.Exit();
        enemy.fx.CancelBlink();
    }

    public override void Update()
    {
        base.Update();
        if(i <= stunnedKnockTime)
        {
            i += Time.deltaTime;
            float t = i / stunnedKnockTime;

            float ease = 1f - t * t;
            Vector2 baseVelocity = new Vector2(-enemy.facingDir * enemy.stunnedDir.x,enemy.stunnedDir.y);
            rb.velocity = baseVelocity * ease;
        }

        else if(i > stunnedKnockTime)
        {
            rb.velocity = new Vector2(0,rb.velocity.y);
        }

        if(stateTimer < 0)
        {
            stateMachine.ChangeState(enemy.idleState);
            return;
        }
    }
}
