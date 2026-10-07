using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerState
{
    protected PlayerStateMachine stateMachine;
    protected Player player;
    protected Rigidbody2D rb;

    static public float xInput;
    static public float yInput;
    private string animBoolName;
    protected float stateTimer;
    protected bool triggerCalled;
    public PlayerState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName)
    {
        this.player = _player;
        this.stateMachine = _stateMachine;
        this.animBoolName = _animBoolName;
    }

    /// <summary>本状态的动画布尔参数名（换动画组会重置 Animator 参数，需要按当前状态重新置位）</summary>
    public string AnimBoolName => animBoolName;

    public virtual void Enter()
    {
        player.anim.SetBool(animBoolName,true);
        rb = player.rb;
        triggerCalled = false;
    }

    public virtual void Update()
    {   
        if(stateTimer > 0)
            stateTimer -= Time.deltaTime;
        xInput = Input.GetAxisRaw("Horizontal");
        yInput = Input.GetAxisRaw("Vertical");
        player.anim.SetFloat("yVelocity",rb.velocity.y);
    }

    public virtual void Exit()
    {
        player.anim.SetBool(animBoolName,false);
    }

    public virtual void AnimationFinishTrigger()
    {
        triggerCalled = true;
    }
}
