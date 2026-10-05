public class SkeletonKnockBackState : EnemyState
{
    private EnemySkeleton enemy;

    public SkeletonKnockBackState(Enemy _enemyBase,EnemyStateMachine _stateMachine,string _animBoolName,EnemySkeleton _enemy): base(_enemyBase, _stateMachine, _animBoolName)
    {
        enemy = _enemy;
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = enemy.KnockBackDuration;
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0 && triggerCalled)
        {
            stateMachine.ChangeState(enemy.battleState);
        }
    }
}
