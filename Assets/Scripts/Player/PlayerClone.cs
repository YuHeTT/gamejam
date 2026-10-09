using UnityEngine;

/// <summary>「募」召唤出的玩家复制体：碰撞箱 / 图像 / 动画与玩家一致，<br/>
/// 镜像玩家的 A/D（左右）与 K（跳跃）输入；与玩家、与其它复制体之间没有碰撞体积。</summary>
public class PlayerClone : Entity
{
    [Header("CloneInfo")]
    public float moveSpeed = 8f;
    public float jumpSpeed = 20f;
    public float gravityScale = 5f;
    public float fallMultiplier = 3f;

    /// <summary>是否与玩家保持连接：连接时镜像玩家的输入，断开时视作没有输入（物理照常）</summary>
    public bool connected = true;

    private static readonly int IdleId = Animator.StringToHash("Idle");
    private static readonly int MoveId = Animator.StringToHash("Move");
    private static readonly int JumpId = Animator.StringToHash("Jump");
    private static readonly int YVelocityId = Animator.StringToHash("yVelocity");

    /// <summary>由 CloneManager 在生成后立即调用（必须早于 Entity.Start 解析地面检测）</summary>
    public void Initialize(Transform groundCheckSource,
                           float groundCheckDistance, float groundCheckDeviate, int groundMask,
                           float moveSpeed, float jumpSpeed, float gravityScale, float fallMultiplier)
    {
        groundCheck              = groundCheckSource;
        this.groundCheckDistance = groundCheckDistance;
        this.groundCheckDeviate  = groundCheckDeviate;
        whatIsGround             = groundMask;   //与玩家一致，避免 Start 中因掩码不一致而告警
        this.moveSpeed           = moveSpeed;
        this.jumpSpeed           = jumpSpeed;
        this.gravityScale        = gravityScale;
        this.fallMultiplier      = fallMultiplier;
    }

    protected override void Update()
    {
        base.Update();   //地面检测 + 最大下落速度限制

        //断开连接时视作没有输入
        float x = connected ? PlayerState.xInput : 0f;

        //左右移动（与玩家一致：设速度并翻转朝向）
        rb.velocity = new Vector2(x * moveSpeed, rb.velocity.y);
        FlipController(x);

        //跳跃（暂停时不接受输入，避免恢复的瞬间凭空起跳）
        if (connected && !GamePause.IsPaused && IsGroundDetected() && Input.GetKeyDown(KeyCode.K))
            rb.velocity = new Vector2(rb.velocity.x, jumpSpeed);

        //复刻玩家的「长按 K 跳更高」
        bool holdingJump = connected && !GamePause.IsPaused && Input.GetKey(KeyCode.K);
        rb.gravityScale = (rb.velocity.y > 0f && !holdingJump)
            ? gravityScale * fallMultiplier
            : gravityScale;

        UpdateAnim(x);
    }

    /// <summary>与玩家状态机一致地互斥设置 Idle / Move / Jump</summary>
    private void UpdateAnim(float x)
    {
        bool grounded = IsGroundDetected();
        bool moving   = Mathf.Abs(x) > 0.01f;

        anim.SetBool(IdleId, grounded && !moving);
        anim.SetBool(MoveId, grounded && moving);
        anim.SetBool(JumpId, !grounded);
        anim.SetFloat(YVelocityId, rb.velocity.y);
    }
}