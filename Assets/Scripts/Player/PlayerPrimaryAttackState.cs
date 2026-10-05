using UnityEngine;

public class PlayerPrimaryAttackState : PlayerState
{   
    public PlayerPrimaryAttackState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        //可加，提高攻速
        //player.anim.speed = 1;
        stateTimer = .1f;
        if(player.comboCounter > 2 || Time.time > player.lastTimeAttack + player.comboWindow)
            player.comboCounter = 0;

        player.SetAttackHitBox(player.comboCounter);

        player.attackDir = xInput != 0 ? xInput : player.facingDir;

        player.SetVelocity(player.attackMovement[player.comboCounter].x * player.attackDir,player.attackMovement[player.comboCounter].y);
        player.anim.SetInteger("ComboCounter",player.comboCounter);
    }

    public override void Exit()
    {
        base.Exit();
        player.CloseClashWindow();
        player.ClearAttackHitBox();
        //可加，提高攻速
        //player.anim.speed = 1;
        //协程
        player.StartCoroutine(player.BusyFor(0.05f));
        player.comboCounter++;
        player.lastTimeAttack = Time.time;
    }

    public override void Update()
    {
        base.Update();
        if(stateTimer <= 0)
            player.SetZeroVelocity();
        if (triggerCalled)
        {
            stateMachine.ChangeState(player.idleState);
        }
    }
}
