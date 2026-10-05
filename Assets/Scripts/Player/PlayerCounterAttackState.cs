public class PlayerCounterAttackState : PlayerState
{
    public PlayerCounterAttackState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = player.counterAttackDuration;
        player.anim.SetBool("SuccessfulCounterAttack",false);

        player.SetZeroVelocity();
        player.ClearAttackHitBox();
    }

    public override void Update()
    {
        base.Update();

        if(triggerCalled || stateTimer <= 0f)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }
    }
}
