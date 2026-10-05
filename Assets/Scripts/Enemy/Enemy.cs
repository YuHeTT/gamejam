using UnityEngine;

public class Enemy : Entity
{
    [Header("PlayerDetectionInfo")]
    [SerializeField] protected LayerMask whatIsPlayer;
    public float detectDistance = 10f;
    [Header("MoveInfo")]
    public float moveSpeed;
    public float idleTime;

    [Header("AttackInfo")]
    public float battleTime;
    public float attackDistance;
    public float attackCoolDown;
    public float lastTimeAttack;

    [Header("StunnedInfo")]
    public float stunnedTime = 1f;
    public float stunnedKnockTime = 0.4f;
    public Vector2 stunnedDir;

    public EnemyStateMachine stateMachine { get ; private set ;}

    protected override void Awake()
    {
        base.Awake();
        stateMachine = new EnemyStateMachine();
    }

    protected override void Update()
    {
        base.Update();
        stateMachine.currentState.Update();
    }

    public virtual void AnimationFinishTrigger() => stateMachine.currentState.AnimationFinishTrigger();
    public virtual RaycastHit2D IsPlayerDetected() => Physics2D.Raycast(transform.position,Vector2.right * facingDir,detectDistance,whatIsPlayer);

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position,new Vector2(transform.position.x + attackDistance * facingDir,transform.position.y));
    }

    public bool CanAttack()
    {
        return Time.time >= lastTimeAttack + attackCoolDown;
    }
}
