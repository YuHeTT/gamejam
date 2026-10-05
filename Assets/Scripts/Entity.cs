using System.Collections;
using UnityEngine;

public class Entity : MonoBehaviour
{
    #region 组件
    public Animator anim { get ; private set ; }    
    public Rigidbody2D rb { get ; private set ; }
    public EntityFx fx { get ; private set ; }
    #endregion
    
    [Header("KnockBackInfo")]
    [SerializeField] protected Vector2 knockBackDir;
    [SerializeField] protected float knockBackDuration;
    [SerializeField] protected float knockBackDistance;
    protected bool isKnocked;
    private Coroutine hitKnockBackCoroutine;

    public float KnockBackDuration => knockBackDuration;

    [Header("CollisionInfo")]
    public Transform attackCheck;
    public AttackHitbox attackHitBox { get; private set; }
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
        fx = GetComponent<EntityFx>();

        if (attackCheck != null)
            attackHitBox = attackCheck.GetComponentInChildren<AttackHitbox>();
    }

    protected virtual void Update()
    {
        CollisionCheck();
        //限制最大下降速度
        if (rb.velocity.y < -maxFallSpeed)
            this.SetVelocity(rb.velocity.x, -maxFallSpeed);
    }
    #region  受伤检测
    public virtual void Damage(Vector2 attackerPosition)
    {
        if (fx != null)
        {
            fx.PlayFlash();
            Debug.Log(fx.gameObject.name + " was damaged!");
        }

        if (hitKnockBackCoroutine != null)
        {
            StopCoroutine(hitKnockBackCoroutine);
        }

        hitKnockBackCoroutine = StartCoroutine(HitKnockBack(attackerPosition));
    }

    public virtual bool CanCounter(AttackHitbox incomingAttack) => false;
    public virtual void OnCounterSuccess(Entity attacker) { }
    public virtual void OnCountered(Entity defender) { }
    public virtual bool CanClash(Entity opponent) => false;
    public virtual void OnClash(Entity opponent) { }
    
    #endregion

    #region 击退
    protected virtual IEnumerator HitKnockBack(Vector2 attackerPosition)
    {
        isKnocked = true;

        float direction;

        if (Mathf.Approximately(transform.position.x, attackerPosition.x))
        {
            direction = -facingDir;
        }
        else
        {
            direction = transform.position.x > attackerPosition.x ? 1 : -1;
            if(direction == facingDir)
            {
                Flip();
                transform.position = new Vector2(transform.position.x + direction * 1.2f * knockBackDistance,transform.position.y);
            }
            else
            {
                transform.position = new Vector2(transform.position.x + direction * knockBackDistance,transform.position.y);  
            }
        }

        rb.velocity = new Vector2(Mathf.Abs(knockBackDir.x) * direction,knockBackDir.y);

        yield return new WaitForSeconds(knockBackDuration);

        isKnocked = false;
        hitKnockBackCoroutine = null;
    }
    #endregion

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
        if (isKnocked)
        {
            return;
        }
        rb.velocity = new Vector2(_xVelocity,_yVelocity);
        FlipController(_xVelocity);
    }
    #endregion
}
