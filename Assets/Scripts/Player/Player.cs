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
    public PlayerStuckState stuckState { get ; private set ; }
    #endregion

    #region 信息
    [Header("Move&JumpInfo")]
    public float gravityScale = 5f;
    public float moveSpeed = 8;
    public float jumpSpeed = 20;
    [Tooltip("跳跃力度倍率（由持有「蓦」等道具修改，默认 1）")]
    public float jumpSpeedMultiplier = 1f;
    [Tooltip("下落重力倍率：松手/下坠时实际重力 = gravityScale × 该值")]
    [Range(1f, 10f)]
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
    [Tooltip("是否允许冲刺：仅持有「蓦」时为 true")]
    public bool canDash;
    public float dashDir { get ; private set ; }

    [Header("FloatInfo")]
    [Tooltip("漂浮时的匀速下落速度：从 0 受重力加速到该速度后保持恒速下落")]
    [Range(0f, 20f)]
    public float floatFallSpeed = 4f;
    [Tooltip("漂浮时碰撞箱高度倍率（1 = 不压缩）")]
    [Range(0.1f, 1f)]
    public float squashMultiplier = 0.5f;
    public bool isFloating;
    public bool isStuck;

    private BoxCollider2D boxCol;
    private Vector2 origColSize;
    private Vector2 origColOffset;
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
        stuckState = new PlayerStuckState(this, stateMachine, "Idle");
    }

    protected override void Start()
    {
        base.Start();
        rb.gravityScale = gravityScale;
        stateMachine.Initialize(idleState);

        //缓存原始尺寸，供压扁/恢复使用
        boxCol = GetComponent<BoxCollider2D>();
        if (boxCol != null)
        {
            origColSize = boxCol.size;
            origColOffset = boxCol.offset;
        }
    }

    protected override void Update()
    {
        base.Update();
        CheckJumpInput();
        UpdateDashCooldown();
        stateMachine.currentState.Update();

        //漂浮是常驻标记：无论当前处于哪个状态，都正常受重力加速，并把下落速度限制为匀速
        if (isFloating)
        {
            rb.gravityScale = gravityScale;
            if (rb.velocity.y < -floatFallSpeed)
                rb.velocity = new Vector2(rb.velocity.x, -floatFallSpeed);
        }
    }

    public void AnimationTrigger() => stateMachine.currentState.AnimationFinishTrigger();

    #region 跳跃输入检测
    private void CheckJumpInput()
    {
        if(jumpBufferTimer > 0)
            jumpBufferTimer -= Time.deltaTime;

        //漂浮/卡住期间禁用跳跃
        if (isFloating || isStuck)
        {
            jumpBufferTimer = 0;
            return;
        }

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

    #region 冲刺
    private void UpdateDashCooldown()
    {
        if (dashCoolDownTimer >= 0)
            dashCoolDownTimer -= Time.deltaTime;
    }

    /// <summary>尝试冲刺（由持有「蓦」的道具按 L 时调用）。<br/>
    /// 未持有该道具（canDash=false）、漂浮/卡住、冷却中或空中已冲刺过时均失败。</summary>
    public bool TryDash()
    {
        if (!canDash) return false;
        if (isFloating || isStuck) return false;
        if (dashCoolDownTimer > 0 || hasDashedInAir) return false;

        dashCoolDownTimer = dashCoolDown;
        dashDir = PlayerState.xInput;
        if (dashDir == 0)
            dashDir = facingDir;
        stateMachine.ChangeState(dashState);
        return true;
    }
    #endregion

    #region 漂浮/卡住
    /// <summary>进入漂浮（拾取「幕」时调用）：压扁碰撞箱、限制下落为匀速，禁用跳跃/冲刺。<br/>
    /// 漂浮是常驻标记而非状态，因此下落、移动都不会丢失漂浮效果。</summary>
    public void EnterFloat()
    {
        isFloating = true;
        SetSquashed(true);

        //若正卡住，拾回道具即解除卡住
        if (stateMachine.currentState == stuckState)
            stateMachine.ChangeState(IsGroundDetected() ? (PlayerState)idleState : (PlayerState)airState);

        rb.gravityScale = gravityScale;
    }

    /// <summary>结束漂浮（丢弃「幕」时调用）：净空足够则恢复全高，否则卡住</summary>
    public void TryExitFloat()
    {
        if (!isFloating) return;

        isFloating = false;
        rb.gravityScale = gravityScale;

        if (HasHeadroom())
        {
            SetSquashed(false);
        }
        else
        {
            //头顶净空不足：保持压扁，进入卡住状态（原地踏步、无法跳跃）
            stateMachine.ChangeState(stuckState);
        }
    }

    /// <summary>碰撞箱高度压扁/恢复（底部锚定不动）。视觉动画不做缩放。</summary>
    public void SetSquashed(bool squashed)
    {
        if (boxCol == null) return;

        if (squashed)
        {
            float bottom = origColOffset.y - origColSize.y * 0.5f;
            float newHeight = origColSize.y * squashMultiplier;

            boxCol.size = new Vector2(origColSize.x, newHeight);
            boxCol.offset = new Vector2(origColOffset.x, bottom + newHeight * 0.5f);
        }
        else
        {
            boxCol.size = origColSize;
            boxCol.offset = origColOffset;
        }

        Physics2D.SyncTransforms();
    }

    /// <summary>检测头顶是否有足够净空恢复到全高（底部不动，只看新增的上半段）</summary>
    public bool HasHeadroom()
    {
        if (boxCol == null) return true;

        float bottom = origColOffset.y - origColSize.y * 0.5f;
        float squashedTop = bottom + origColSize.y * squashMultiplier;
        float fullTop = bottom + origColSize.y;

        Vector2 center = (Vector2)transform.position + new Vector2(origColOffset.x, (squashedTop + fullTop) * 0.5f);
        Vector2 size = new Vector2(origColSize.x, fullTop - squashedTop);

        Physics2D.SyncTransforms();
        return Physics2D.OverlapBox(center, size, 0f, whatIsGround) == null;
    }
    #endregion

    //水平移动控制
    public void HorizontalMoveController()
    {
        SetVelocity(PlayerState.xInput * moveSpeed, rb.velocity.y);
    }
}