using UnityEngine;

public class Player : Entity
{   
    #region 状态
    public PlayerStateMachine stateMachine { get ; private set; }
    public PlayerIdleState idleState { get ; private set ; }
    public PlayerMoveState moveState { get ; private set ; }
    public PlayerJumpState jumpState { get ; private set ; }
    public PlayerAirState airState { get ; private set ; }
    public PlayerDashState dashState { get ; private set ; }
    #endregion

    #region 信息
    [Header("Move&JumpInfo")]
    public float gravityScale = 5f;
    public float moveSpeed = 8;
    public float jumpSpeed = 20;
    public float fallMultiplier = 3f;
    public float coyoteTime = 0.1f;
    public float jumpBuffer = 0.1f;
    public float jumpBufferTimer;

    [Header("DashInfo")]
    public float dashDuration = 0.3f;
    public float dashSpeed = 15;
    public float dashCoolDown = 0.5f;
    public float dashCoolDownTimer;
    public bool hasDashedInAir = false;
    public float dashBuffer = 0.1f;
    public float dashBufferTimer;
    public float dashDir { get ; private set ; }
    #endregion

    protected override void Awake()
    {
        base.Awake();
        stateMachine = new PlayerStateMachine();
        
        idleState = new PlayerIdleState(this, stateMachine, "Idle");
        moveState = new PlayerMoveState(this, stateMachine, "Move");
        jumpState = new PlayerJumpState(this, stateMachine, "Jump");

        airState  = new PlayerAirState (this, stateMachine, "Jump");
        dashState = new PlayerDashState(this, stateMachine, "Dash");
    }

    protected override void Start()
    {
        base.Start();
        rb.gravityScale = gravityScale;
        stateMachine.Initialize(idleState);
    }

    protected override void Update()
    {
        base.Update();
        CheckJumpInput();
        CheckDashInput();
        stateMachine.currentState.Update();
    }

    public void AnimationTrigger() => stateMachine.currentState.AnimationFinishTrigger();

    #region 跳跃输入检测
    private void CheckJumpInput()
    {
        if(jumpBufferTimer > 0)
            jumpBufferTimer -= Time.deltaTime;
            
        if(Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferTimer = jumpBuffer;
        }
    }
    #endregion

    #region 跳跃高度控制
    public void JumpHeightController()
    {
        if (rb.velocity.y > 0 && !Input.GetKey(KeyCode.Space))
        {
            rb.gravityScale = gravityScale * fallMultiplier;
        }
        else
        {
            rb.gravityScale = gravityScale;
        }
    }
    #endregion

    #region 冲刺输入检测
    private void CheckDashInput()
    {   
        if(dashCoolDownTimer >= 0)
            dashCoolDownTimer -= Time.deltaTime;
        if(dashBufferTimer >= 0)
            dashBufferTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.LeftShift))
            dashBufferTimer = dashBuffer;

        if (dashBufferTimer > 0 && dashCoolDownTimer < 0 && !hasDashedInAir)
        {
            dashCoolDownTimer = dashCoolDown;
            dashBufferTimer = 0;
            dashDir = PlayerState.xInput;
            if(dashDir == 0)
            {
                dashDir = facingDir;
            }
            stateMachine.ChangeState(dashState);
        }       
    }
    #endregion

    //水平移动控制
    public void HorizontalMoveController()
    {
        SetVelocity(PlayerState.xInput * moveSpeed, rb.velocity.y);
    }
}