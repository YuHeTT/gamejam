using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 巡逻平台：在左右两个 X 坐标之间往返移动，到端点停留指定时间后返程。<br/>
/// 只改 X，Y 与 Z 保持不动；用 MoveTowards 保证严格匀速、不会过冲。<br/><br/>
/// 摆放约定：物体<b>当前 X</b> 默认作为左端点，右端点由 <see cref="rightBound"/> 指定。
/// 如果要用明确的绝对范围（例如左 30、右 37），勾上 <see cref="useCustomBounds"/> 再填两个值。<br/><br/>
/// 勾选 <see cref="carryRiders"/> 后，会把站在平台顶面上的物体（主要是玩家）一起带着走，
/// 物体跳跃或走出平台即自动脱离（与 <see cref="updown"/> 的水平承载逻辑一致）。
/// </summary>
[DisallowMultipleComponent]
public class movestair : MonoBehaviour
{
    [Header("巡逻范围（世界 X 坐标）")]
    [Tooltip("勾选后使用下面两个绝对值；不勾选则用“初始位置 X 作为左端点、rightBound 作为右端点”")]
    public bool useCustomBounds = false;
    [Tooltip("左端点 X（仅 useCustomBounds = true 时生效）")]
    public float leftBound = 30f;
    [Tooltip("右端点 X")]
    public float rightBound = 37f;

    [Header("范围含义")]
    [Tooltip("勾选：leftBound / rightBound 指平台【中心点】的 X。\n" +
             "不勾选：指平台碰撞体的【左右边缘】碰到的位置")]
    public bool boundsAreCenter = true;

    [Header("运动")]
    [Tooltip("移动速度（米/秒）。MoveTowards 保证严格匀速")]
    public float moveSpeed = 1.5f;
    [Tooltip("到达每个端点后停留的秒数")]
    public float waitTime = 1f;

    [Header("承载（吸附玩家）")]
    [Tooltip("勾选后平台移动时会把站在平台顶面上的物体（主要是玩家）一起带着走；物体跳跃或走出平台后自动脱离")]
    public bool carryRiders = true;
    [Tooltip("承载探测盒的厚度（米）：平台顶面以上这个厚度内、且脚底贴面的物体会被带走")]
    public float carryDetectHeight = 0.4f;
    [Tooltip("脚底低于平台顶面多少以内算“站在上面”（米），用于排除从上方飞过的物体")]
    public float carryFootTolerance = 0.15f;
    [Tooltip("承载对象的层掩码，保持 Everything 即可（脚本会自行过滤掉静态/无刚体对象）")]
    public LayerMask carryMask = ~0;

    [Header("调试")]
    [Tooltip("在 Scene 视图画出巡逻起止位置")]
    public bool drawGizmos = true;

    // 实际使用的两个端点 X
    private float _xMin;
    private float _xMax;

    // 当前是否在端点停留、剩余停留时间、目标端点
    private bool _waiting;
    private float _waitTimer;
    private float _targetX;

    // 当前目标的"中心 X"，用于判断该往哪一侧加半宽（不能用 transform.position.x，
    // 因为它会随着移动持续靠近目标，导致加减号在接近时翻转）
    private float _targetCenterX;

    // 承载探测：复用的查询容器，避免每帧分配
    private Collider2D _platformCollider;
    private readonly Collider2D[] _riderBuffer = new Collider2D[8];
    private ContactFilter2D _riderFilter;
    private readonly List<Rigidbody2D> _processedRiders = new List<Rigidbody2D>();

    private void Awake()
    {
        _platformCollider = GetComponent<Collider2D>();

        _riderFilter = new ContactFilter2D();
        _riderFilter.useTriggers = false;
        _riderFilter.useLayerMask = true;
        _riderFilter.layerMask = carryMask;

        // 默认：物体当前 X = 左端点，rightBound = 右端点
        _xMin = useCustomBounds ? leftBound : transform.position.x;
        _xMax = rightBound;

        // 防御：把两个端点排好序，避免写反导致平台乱跑
        if (_xMax < _xMin)
        {
            float t = _xMin;
            _xMin = _xMax;
            _xMax = t;
        }

        // 从当前位置出发，朝较远的那一端走
        _targetCenterX = transform.position.x;   // 先给个参考值，供 ToTransformX 判断方向
        _targetX = Mathf.Abs(transform.position.x - _xMin) >= Mathf.Abs(transform.position.x - _xMax)
            ? _xMin
            : _xMax;
        RefreshTargetCenter();
    }

    /// <summary>返回另一端的 X</summary>
    private float Flip(float from)
    {
        return from > (_xMin + _xMax) * 0.5f ? _xMin : _xMax;
    }

    private void Update()
    {
        // 两端重合：没有可走的路，直接不动（避免来回抖动）
        if (_xMax - _xMin < 0.0001f) return;

        if (_waiting)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer > 0f) return;

            _waiting = false;
            _targetX = Flip(_targetX);          // 停留结束，掉头
            RefreshTargetCenter();
        }

        // 目标端点换算成"transform 应该去的 X"
        float targetTransformX = ToTransformX(_targetX);

        Vector3 pos = transform.position;
        pos.x = Mathf.MoveTowards(pos.x, targetTransformX, moveSpeed * Time.deltaTime);

        // 先把站在平台顶面上的物体按同样的位移带走，再移动平台本身（相对位置保持不变）。
        // 只按逐帧位移搬运：物体跳跃/走出平台后下一帧就探测不到，自然脱离，无需额外解绑。
        Vector3 delta = pos - transform.position;
        if (carryRiders && delta.sqrMagnitude > 0f)
            CarryRiders(delta);

        transform.position = pos;

        // 到达端点：开始停留
        if (Mathf.Abs(pos.x - targetTransformX) <= 0.0001f)
        {
            _waiting = true;
            _waitTimer = waitTime;
        }
    }

    /// <summary>
    /// 把"端点 X"换算成"transform 应该去的 X"。<br/>
    /// <see cref="boundsAreCenter"/> = true 时端点就是中心；否则端点是碰撞体边缘，
    /// 需要按目标在"当前目标中心"的哪一侧来加减半宽。
    /// </summary>
    private float ToTransformX(float boundX)
    {
        if (boundsAreCenter) return boundX;

        float halfWidth = GetHalfWidth();
        if (halfWidth <= 0f) return boundX;

        // _targetCenterX == 0 表示还没初始化：用 transform 当前位置判断方向
        float reference = _targetCenterX != 0f ? _targetCenterX : transform.position.x;

        return boundX + (boundX > reference ? halfWidth : -halfWidth);
    }

    /// <summary>根据目标端点刷新"目标中心"，保证加减半宽的方向稳定</summary>
    private void RefreshTargetCenter()
    {
        _targetCenterX = ToTransformX(_targetX);
    }

    /// <summary>
    /// 把"站在平台顶面上的物体"按同样的位移一起带走（主要是玩家）。<br/>
    /// 只处理带 Dynamic 刚体的物体（玩家、箱子等），平台自己与静态/运动学物体不动。<br/>
    /// 因为搬运量就是平台本帧的位移，所以玩家用自身速度跳跃或走出平台后，
    /// 下一帧便不再落在探测盒内 → 自动脱离，不需要额外的解绑逻辑。<br/>
    /// 注意：玩家速度由输入直接赋值（<see cref="Player.HorizontalMoveController"/>），
    /// 单纯给速度加平台分量会被覆盖，所以这里改的是刚体位置而不是速度。
    /// </summary>
    private void CarryRiders(Vector3 delta)
    {
        if (!TryGetPlatformBounds(out Bounds pb)) return;

        float h = Mathf.Max(0.05f, carryDetectHeight);
        float top = pb.max.y;

        // 探测盒：紧贴平台顶面之上，宽度与平台一致
        Vector2 center = new Vector2(pb.center.x, top + h * 0.5f);
        Vector2 size = new Vector2(Mathf.Max(0.05f, pb.size.x), h);

        int count = Physics2D.OverlapBox(center, size, 0f, _riderFilter, _riderBuffer);

        _processedRiders.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _riderBuffer[i];
            if (col == null || col == _platformCollider) continue;

            Rigidbody2D body = col.attachedRigidbody;
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic) continue;
            if (body.transform.IsChildOf(transform)) continue;      // 平台自己的子物体不搬
            if (_processedRiders.Contains(body)) continue;          // 同一刚体的多个碰撞体只搬一次

            // 脚底必须贴着平台顶面，才算"站在上面"（排除从上方飞过的物体）
            if (col.bounds.min.y > top + carryFootTolerance) continue;

            _processedRiders.Add(body);
            body.position += (Vector2)delta;
        }
    }

    /// <summary>取平台碰撞体的世界包围盒；没有碰撞体时退回 SpriteRenderer 的包围盒</summary>
    private bool TryGetPlatformBounds(out Bounds bounds)
    {
        if (_platformCollider != null)
        {
            bounds = _platformCollider.bounds;
            return true;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            bounds = sr.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    /// <summary>取碰撞体半宽（世界单位）；没有碰撞体时用 SpriteRenderer 半宽兜底，再不行用 0</summary>
    private float GetHalfWidth()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) return col.bounds.extents.x;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) return sr.bounds.extents.x;

        return 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        float halfWidth = GetHalfWidth();

        // 编辑模式下也要能预览：按当前设置算出两个端点
        float a = useCustomBounds ? leftBound : transform.position.x;
        float b = useCustomBounds ? rightBound : rightBound;
        if (b < a) { float t = a; a = b; b = t; }

        // 端点指中心时，把平台的左右边缘也画出来，便于核对视觉位置
        float leftEdge = boundsAreCenter ? a - halfWidth : a;
        float rightEdge = boundsAreCenter ? b + halfWidth : b;

        float y = transform.position.y;

        Gizmos.color = new Color(0.4f, 0.9f, 1f, 1f);
        Gizmos.DrawLine(new Vector3(leftEdge, y, 0f), new Vector3(rightEdge, y, 0f));
        Gizmos.DrawWireSphere(new Vector3(a, y, 0f), 0.12f);
        Gizmos.DrawWireSphere(new Vector3(b, y, 0f), 0.12f);
        Gizmos.DrawWireCube(new Vector3(a, y, 0f), new Vector3(halfWidth * 2f, 0.1f, 0.01f));
        Gizmos.DrawWireCube(new Vector3(b, y, 0f), new Vector3(halfWidth * 2f, 0.1f, 0.01f));
    }
}
