using System.Collections;
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
    public PlayerDoubleJumpState doubleJumpState { get ; private set ; }
    public PlayerWallSlideState wallSlideState { get ; private set ; }
    public PlayerWallJumpState wallJumpState { get ; private set ; }
    public PlayerPrimaryAttackState primaryAttackState { get ; private set ; }
    public PlayerCounterAttackState counterAttackState { get ; private set ; }
    public PlayerSuccessfulCounterAttackState successFullCounterAttackState { get ; private set ; }
    
    #endregion

    #region 信息
    [Header("Move&JumpInfo")]
    public float gravityScale = 5f;
    public float moveSpeed = 8;
    public bool canDoubleJump;
    public bool hasDoubleJumped;
    public float jumpSpeed = 20;
    public float doubleJumpSpeed = 18;
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

    [Header("WallSlideInfo")]
    public float freeWallSlideSpeed = 2.5f;
    public float fromWallSpeedx = 8.0f;
    public float fromWallSpeedy = 18.0f;
    public float wallJumpBuffer = 0.15f;
    public float wallJumpBufferTimer;
    public float wallJumpDurationTime = 0.12f;

    [Header("AttackInfo")]
    public int comboCounter;
    public float attackBuffer = 0.1f;
    public float attackBufferTimer;
    public float lastTimeAttack;
    public float comboWindow = 0.5f;
    public float attackDir;
    public Vector2[] attackMovement;
    public AttackHitbox[] attackHitboxes;
    public AttackHitbox currentHitBox { get ; private set ; }

    public float counterAttackDuration = 0.2f;
    public float counterAttackBuffer = 0.1f;
    public float counterAttackBufferTimer;
    public float counterAttackCoolDownTime;
    public float counterAttackCoolDownTimer;
    public bool isBusy { get ; private set ; }

    private bool clashWindowOpen;
    private Coroutine counterSuccessRoutine;

    public SkillManager skill;

    #endregion
    protected override void Awake()
    {
        base.Awake();
        stateMachine = new PlayerStateMachine();
        
        idleState = new PlayerIdleState(this, stateMachine, "Idle");
        moveState = new PlayerMoveState(this, stateMachine, "Move");
        jumpState = new PlayerJumpState(this, stateMachine, "Jump");
        doubleJumpState = new PlayerDoubleJumpState(this,stateMachine, "Jump");

        airState  = new PlayerAirState (this, stateMachine, "Jump");
        dashState = new PlayerDashState(this, stateMachine, "Dash");

        wallSlideState = new PlayerWallSlideState(this, stateMachine, "WallSlide");
        wallJumpState = new PlayerWallJumpState(this,stateMachine,"Jump");

        primaryAttackState = new PlayerPrimaryAttackState(this,stateMachine, "Attack");

        counterAttackState = new PlayerCounterAttackState(this,stateMachine,"CounterAttack");
        successFullCounterAttackState = new PlayerSuccessfulCounterAttackState(this,stateMachine,"SuccessfulCounterAttack");
        //......
    }

    protected override void Start()
    {
        base.Start();
        skill = SkillManager.instance;
        rb.gravityScale = gravityScale;
        stateMachine.Initialize(idleState);
    }

    protected override void Update()
    {
        base.Update();
        CheckJumpInput();
        CheckDashInput();
        CheckWallJumpInput();
        CheckAttackInput();
        CheckCounterAttackInput();
        stateMachine.currentState.Update();
    }
    //协程
    public IEnumerator BusyFor(float _seconds)
    {
        isBusy = true;
        yield return new WaitForSeconds(_seconds);
        isBusy = false;
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

    #region 蹭墙跳输入检测
    private void CheckWallJumpInput()
    {
        if(wallJumpBufferTimer >= 0)
        {
            wallJumpBufferTimer -= Time.deltaTime;
        }
        if (Input.GetKeyDown(KeyCode.Space) && !IsGroundDetected())
        {
            wallJumpBufferTimer = wallJumpBuffer;
        }
    }

    #endregion

    #region 攻击输入检测
    private void CheckAttackInput()
    {
        if(attackBufferTimer > 0)
            attackBufferTimer -= Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            attackBufferTimer = attackBuffer;
        }
    }
    //连击判断
    public void SetAttackHitBox(int index)
    {
        ClearAttackHitBox();

        if(attackHitboxes == null || index < 0 || index >= attackHitboxes.Length || attackHitboxes[index] == null)
        {
            return;
        }
        currentHitBox = attackHitboxes[index];
    }

    public void ClearAttackHitBox()
    {
        if (currentHitBox != null)
        {
            currentHitBox.DisableHitbox();
            currentHitBox = null;
        }
    }

    #endregion

    #region 拼刀检测
    public void OpenClashWindow()
    {
        if (stateMachine.currentState == primaryAttackState)
        {
            clashWindowOpen = true;
        }
    }

    public void CloseClashWindow() => clashWindowOpen = false;

    public override bool CanClash(Entity opponent)
    {
        return clashWindowOpen &&
            stateMachine.currentState == primaryAttackState &&
            opponent is Enemy;
    }

    public override void OnClash(Entity opponent)
    {
        CloseClashWindow();
    }
    #endregion

    #region 反击检测
    public override bool CanCounter(AttackHitbox incomingAttack)
    {
        return incomingAttack.CanBeCountered &&
            incomingAttack.Owner is Enemy &&
            stateMachine.currentState == counterAttackState;
    }

    public override void OnCounterSuccess(Entity attacker)
    {
        if (counterSuccessRoutine == null)
        {
            counterSuccessRoutine = StartCoroutine(
                CompleteCounterAttack(attacker));
        }
    }

    private IEnumerator CompleteCounterAttack(Entity attacker)
    {
        while (TimeManager.IsFrameFrozen)
        {
            yield return null;
        }

        counterSuccessRoutine = null;

        if (stateMachine.currentState == counterAttackState)
        {
            stateMachine.ChangeState(successFullCounterAttackState);
        }

        attacker?.OnCountered(this);
    }
    private void CheckCounterAttackInput()
    {
        if(counterAttackCoolDownTimer >= 0)
            counterAttackCoolDownTimer -= Time.deltaTime;
            
        if (counterAttackBufferTimer > 0f)
        {
            counterAttackBufferTimer -= Time.deltaTime;
        }

        if (Input.GetKeyDown(KeyCode.Mouse1))
        {
            counterAttackBufferTimer = counterAttackBuffer;
        }
    }
    #endregion

    //水平移动控制
    public void HorizontalMoveController()
    {
        SetVelocity(PlayerState.xInput * moveSpeed, rb.velocity.y);
    }

}
