using System.Collections;
using UnityEngine;

/// <summary>墓碑传送：错切压扁倒下 → 瞬移到墓碑（不可见）→ 从头往下显现 → 恢复控制。</summary>
[DisallowMultipleComponent]
public class PlayerTombstoneTeleport : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private PlayerShearVisual shearVisual;

    [Header("倒下（错切压扁）")]
    [Tooltip("从站立错切并压成一条线的时长（秒）")]
    public float collapseDuration = 0.5f;

    [Header("显现（从头到脚）")]
    [Tooltip("从头顶往脚底揭开完整身体的速度：1 = 每秒揭开全身。例如 2.5 约 0.4 秒揭完。")]
    public float revealSpeed = 1.25f;
    [Tooltip("相对墓碑脚底锚点的显现起点。Y 为负表示从碑下方开始，显现过程中会匀速升到墓碑位置。")]
    public Vector2 emergeStartOffset = new Vector2(0f, -2f);

    public bool IsTeleporting { get; private set; }

    private BoxCollider2D boxCol;
    private Coroutine routine;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<Player>();
        if (shearVisual == null)
            shearVisual = GetComponent<PlayerShearVisual>();
        if (shearVisual == null)
            shearVisual = gameObject.AddComponent<PlayerShearVisual>();

        boxCol = player != null ? player.GetComponent<BoxCollider2D>() : null;
    }

    public bool TryBeginTeleport()
    {
        if (IsTeleporting || player == null)
            return false;

        if (TombstoneService.Instance == null || !TombstoneService.Instance.HasActiveTombstone)
            return false;

        if (player.stateMachine.currentState == player.teleportState)
            return false;

        player.stateMachine.ChangeState(player.teleportState);
        return true;
    }

    public void StartSequenceFromState()
    {
        if (routine != null)
            return;

        IsTeleporting = true;
        Vector2 feetTarget = TombstoneService.Instance.GetTeleportFeetPosition();
        routine = StartCoroutine(TeleportRoutine(feetTarget));
    }

    private IEnumerator TeleportRoutine(Vector2 feetTarget)
    {
        Rigidbody2D rb = player.rb;
        RigidbodyType2D origBodyType = rb.bodyType;
        float origGravity = rb.gravityScale;
        Vector2 collapsePos = rb.position;

        // 错切期间：碰撞箱留在原地，角色不因重力/速度移动
        rb.velocity = Vector2.zero;
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (boxCol != null)
            boxCol.enabled = true;

        ClearLocomotionAnim();
        shearVisual.BeginEffectMode();
        shearVisual.SetCollapse(0f);

        float collapseTimer = 0f;
        float collapseLen = Mathf.Max(0.01f, collapseDuration);
        while (collapseTimer < collapseLen)
        {
            collapseTimer += Time.deltaTime;
            rb.velocity = Vector2.zero;
            rb.position = collapsePos;
            player.transform.position = collapsePos;
            shearVisual.SetCollapse(Mathf.Clamp01(collapseTimer / collapseLen));
            yield return null;
        }

        shearVisual.SetCollapse(1f);
        shearVisual.SetHidden();

        if (boxCol != null)
            boxCol.enabled = false;
        rb.simulated = false;

        Vector2 emergeStart = feetTarget + emergeStartOffset;
        AlignFeetTo(emergeStart);

        float reveal = 0f;
        float speed = Mathf.Max(0.01f, revealSpeed);
        while (reveal < 1f)
        {
            reveal = Mathf.Min(1f, reveal + speed * Time.deltaTime);
            shearVisual.SetReveal(reveal);
            AlignFeetTo(Vector2.Lerp(emergeStart, feetTarget, reveal));
            yield return null;
        }

        shearVisual.SetReveal(1f);
        AlignFeetTo(feetTarget);
        shearVisual.EndEffectMode();

        if (boxCol != null)
            boxCol.enabled = true;
        rb.bodyType = origBodyType;
        rb.simulated = true;
        rb.velocity = Vector2.zero;
        rb.gravityScale = origGravity != 0f ? origGravity : player.gravityScale;

        player.CollisionCheckPublic();

        routine = null;
        IsTeleporting = false;

        if (player.IsGroundDetected())
            player.stateMachine.ChangeState(player.idleState);
        else
            player.stateMachine.ChangeState(player.airState);
    }

    private void AlignFeetTo(Vector2 worldFeet)
    {
        if (player.GroundCheck == null)
            return;

        Vector2 delta = worldFeet - (Vector2)player.GroundCheck.position;
        player.transform.position += (Vector3)delta;
        Physics2D.SyncTransforms();
    }

    private void ClearLocomotionAnim()
    {
        if (player.anim == null)
            return;

        player.anim.SetBool("Idle", false);
        player.anim.SetBool("Move", false);
        player.anim.SetBool("Jump", false);
        player.anim.SetBool("Dash", false);
        player.anim.SetFloat("yVelocity", 0f);
    }
}
