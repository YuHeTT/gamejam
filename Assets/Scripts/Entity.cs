using UnityEngine;

public class Entity : MonoBehaviour
{
    #region 组件
    public Animator anim { get ; private set ; }    
    public Rigidbody2D rb { get ; private set ; }
    #endregion

    [Header("CollisionInfo")]
    [SerializeField] protected Transform groundCheck;
    [SerializeField] protected float groundCheckDistance;
    protected bool centerCheck;
    protected bool leftCheck;
    protected bool rightCheck;
    [Space]
    [Tooltip("地面所在层的名称。代码按名称解析，避免层未命名时掩码被清成 0。")]
    [SerializeField] protected string groundLayerName = "Ground";
    [Tooltip("备用掩码，仅当上面的层名称不存在时才会用到。")]
    [SerializeField] protected LayerMask whatIsGround;
    [SerializeField] protected float groundCheckDeviate = 0.36f;
    public int facingDir = 1;
    public bool facingRight = true;
    public float maxFallSpeed = 25f;

    /// <summary>运行时解析出的地面层掩码，供射线检测使用。</summary>
    protected int groundMask;

    protected virtual void Awake()
    {
        // 提前解析常用组件：让子类能在 Awake 里安全使用 anim / rb。
        // 原先只在 Start 里赋值，子类若在 Awake 初始化状态机（Player.Awake → stateMachine.Initialize）
        // 就会拿到 null 的 anim / rb。Start 里会再解析一次，保持兼容。
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    protected virtual void Start()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();

        ResolveGroundMask();

        if (groundCheck == null)
        {
            Debug.LogError($"{name} 未指定 groundCheck，地面检测将失效。", this);
            enabled = false;
        }
    }

    /// <summary>
    /// 按层名称解析地面掩码。
    /// 说明：LayerMask 在 Inspector 中只能勾选"已命名"的层，
    /// 层未命名时会被 Unity 静默序列化成 0（Nothing），
    /// 导致地面检测永远失败、角色表现为永久浮空。
    /// 因此这里以"层名称"为唯一事实来源，避免该问题复现。
    /// </summary>
    protected virtual void ResolveGroundMask()
    {
        int layer = LayerMask.NameToLayer(groundLayerName);

        if (layer >= 0)
        {
            groundMask = 1 << layer;

            // 若 Inspector 中的掩码与层名称不一致，以层名称为准并给出提示
            if (whatIsGround.value != groundMask)
            {
                Debug.LogWarning(
                    $"{name}: whatIsGround 掩码({whatIsGround.value}) 与层 '{groundLayerName}'(值 {groundMask}) 不一致，" +
                    $"已按层名称自动修正。", this);
            }
            return;
        }

        // 找不到层名称时退回 Inspector 掩码
        groundMask = whatIsGround.value;

        if (groundMask == 0)
        {
            Debug.LogError(
                $"{name}: 找不到名为 '{groundLayerName}' 的层，且 whatIsGround 掩码为 0(Nothing)，" +
                $"地面检测将永远失败。请在 Project Settings → Tags and Layers 中添加该层。", this);
        }
    }

    protected virtual void Update()
    {
        CollisionCheck();
        //限制最大下降速度
        if (rb.velocity.y < -maxFallSpeed)
            this.SetVelocity(rb.velocity.x, -maxFallSpeed);
    }

    #region 碰撞检测
    protected virtual void OnDrawGizmos()
    {   
        if (groundCheck == null) return;

        //检测到地面=绿色，未检测到=红色，便于在 Scene 视图快速定位问题
        Gizmos.color = IsGroundDetected() ? Color.green : Color.red;
        Vector2 pos = groundCheck.position;
        Gizmos.DrawLine(pos, pos + Vector2.down * groundCheckDistance);
        Gizmos.DrawLine(pos + Vector2.left * groundCheckDeviate, pos + Vector2.left * groundCheckDeviate + Vector2.down * groundCheckDistance);
        Gizmos.DrawLine(pos + Vector2.right * groundCheckDeviate, pos + Vector2.right * groundCheckDeviate + Vector2.down * groundCheckDistance);
    }
    protected virtual void CollisionCheck()
    {
        Vector2 pos = groundCheck.position;
        centerCheck = Physics2D.Raycast(pos, Vector2.down, groundCheckDistance, groundMask);
        leftCheck = Physics2D.Raycast(pos + Vector2.left * groundCheckDeviate, Vector2.down, groundCheckDistance, groundMask);
        rightCheck = Physics2D.Raycast(pos + Vector2.right * groundCheckDeviate, Vector2.down, groundCheckDistance, groundMask);
    }
    public virtual bool IsGroundDetected() => centerCheck || leftCheck || rightCheck;

    /// <summary>地面检测参数（只读），供复制体等以玩家为模板拷贝</summary>
    public float GroundCheckDistance => groundCheckDistance;
    public float GroundCheckDeviate => groundCheckDeviate;
    public int GroundMask => groundMask;

    /// <summary>供道具/传送等使用的地面检测点</summary>
    public Transform GroundCheck => groundCheck;

    /// <summary>从脚底向下射线，命中 Ground 层时返回 true</summary>
    public bool TryGetGroundHit(out RaycastHit2D hit, bool ignoreTombstones = false)
    {
        hit = default;
        if (groundCheck == null)
            return false;

        float dist = groundCheckDistance + 1f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(groundCheck.position, Vector2.down, dist, groundMask);
        foreach (RaycastHit2D candidate in hits)
        {
            if (candidate.collider == null)
                continue;
            if (ignoreTombstones && candidate.collider.GetComponentInParent<TombstoneMarker>() != null)
                continue;
            hit = candidate;
            return true;
        }

        return false;
    }

    /// <summary>传送协程中需手动刷新地面射线时使用</summary>
    public void CollisionCheckPublic() => CollisionCheck();
    #endregion

    #region 翻转控制
    public virtual void Flip()
    {
        facingDir = -facingDir;
        facingRight = !facingRight;
        transform.Rotate(0, 180, 0);
    }

    public virtual void FlipController(float _x)
    {
        if ((_x > 0 && !facingRight) || (_x < 0 && facingRight))
        {
            Flip();
        }
    }
    #endregion

    #region 速度设置
    public virtual void SetZeroVelocity() => SetVelocity(0,0);
    public virtual void SetVelocity(float _xVelocity,float _yVelocity)
    {
        rb.velocity = new Vector2(_xVelocity,_yVelocity);
        FlipController(_xVelocity);
    }
    #endregion
}