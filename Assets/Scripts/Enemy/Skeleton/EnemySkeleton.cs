using UnityEngine;

public class EnemySkeleton : Enemy
{
    #region 状态
    public SkeletonIdleState idleState { get ; private set ; }
    public SkeletonMoveState moveState { get ; private set ; }
    public SkeletonBattleState battleState { get ; private set ; }
    public SkeletonAttackState attackState { get ; private set ; }
    public SkeletonKnockBackState knockBackState { get ; private set ; }
    public SkeletonAirState airState { get ; private set ; }
    public SkeletonStunnedState stunnedState { get ; private set ; }
    #endregion

    protected override void Awake()
    {
        base.Awake();

        idleState = new SkeletonIdleState(this,stateMachine,"Idle",this);
        moveState = new SkeletonMoveState(this,stateMachine,"Move",this);
        battleState = new SkeletonBattleState(this,stateMachine,"Move",this);
        attackState = new SkeletonAttackState(this,stateMachine,"Attack",this);
        knockBackState = new SkeletonKnockBackState(this,stateMachine,"Hit",this);
        airState = new SkeletonAirState(this,stateMachine,"Idle",this);
        stunnedState = new SkeletonStunnedState(this,stateMachine,"Stunned",this);
    }

    protected override void Start()
    {
        base.Start();
        stateMachine.Initialize(idleState);
    }

    public override void Damage(Vector2 attackerPosition)
    {
        base.Damage(attackerPosition);
        // if(stateMachine.currentState != stunnedState)
        //     stateMachine.ChangeState(knockBackState);
    }

    public override void OnCountered(Entity defender)
    {
        if (stateMachine.currentState != stunnedState)
        {
            stateMachine.ChangeState(stunnedState);
        }
    }
}
