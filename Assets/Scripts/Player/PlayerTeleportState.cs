using UnityEngine;

/// <summary>墓碑传送期间：不读输入，由 PlayerTombstoneTeleport 协程驱动。</summary>
public class PlayerTeleportState : PlayerState
{
    public PlayerTeleportState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
        : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        player.SetZeroVelocity();
        player.TombstoneTeleport.StartSequenceFromState();
    }

    public override void Update()
    {
        if (stateTimer > 0)
            stateTimer -= Time.deltaTime;

        xInput = 0f;
        yInput = 0f;

        if (player.anim != null)
            player.anim.SetFloat("yVelocity", 0f);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
