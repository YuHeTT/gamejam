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
    [SerializeField] protected Transform wallCheck;
    [SerializeField] protected float wallCheckDistance;
    [Space]
    [SerializeField] protected LayerMask whatIsGround;
    [SerializeField] protected float groundCheckDeviate = 0.36f;
    public int facingDir = 1;
    public bool facingRight = true;
    public float maxFallSpeed = 25f;

    protected virtual void Awake()
    {
        
    }

    protected virtual void Start()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    protected virtual void Update()
    {
        CollisionCheck();
        //限制最大下降速度
        if (rb.velocity.y < -maxFallSpeed)
            this.SetVelocity(rb.velocity.x, -maxFallSpeed);
    }

    #region 碰撞&墙体检测
    protected virtual void OnDrawGizmos()
    {   
        //地面检测
        Gizmos.color = Color.red;
        Vector2 pos = groundCheck.position;
        Gizmos.DrawLine(pos, pos + Vector2.down * groundCheckDistance);
        Gizmos.DrawLine(pos + Vector2.left * groundCheckDeviate, pos + Vector2.left * groundCheckDeviate + Vector2.down * groundCheckDistance);
        Gizmos.DrawLine(pos + Vector2.right * groundCheckDeviate, pos + Vector2.right * groundCheckDeviate + Vector2.down * groundCheckDistance);
        //墙体检测
        Gizmos.color = Color.green;
        Gizmos.DrawLine(wallCheck.position,new Vector2(wallCheck.position.x + wallCheckDistance * facingDir ,wallCheck.position.y));
    }
    protected virtual void CollisionCheck()
    {
        Vector2 pos = groundCheck.position;
        centerCheck = Physics2D.Raycast(pos, Vector2.down, groundCheckDistance, whatIsGround);
        leftCheck = Physics2D.Raycast(pos + Vector2.left * groundCheckDeviate, Vector2.down, groundCheckDistance, whatIsGround);
        rightCheck = Physics2D.Raycast(pos + Vector2.right * groundCheckDeviate, Vector2.down, groundCheckDistance, whatIsGround);
    }
    public virtual bool IsWallDetected() => Physics2D.Raycast(wallCheck.position,Vector2.right * facingDir,wallCheckDistance,whatIsGround);
    public virtual bool IsGroundDetected() => centerCheck || leftCheck || rightCheck;
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