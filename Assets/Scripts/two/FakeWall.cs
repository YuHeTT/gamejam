using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 假墙体：外观与真墙一致，但满足"指定数量的推动者同时朝墙推"时可以被推动。<br/><br/>
///
/// <b>为什么不用"高摩擦 + 大质量"来挡人</b><br/>
/// 那种做法要挡住单人，就必须让墙咬死地面（质量 × 重力倍率 × 摩擦系数）。
/// 但玩家是**速度控制**的（每帧写 rb.velocity），顶住墙时穿透量会不断累积，
/// 求解器只好周期性施加一次大的去穿透修正，把墙"嘣"地弹开，然后接触断开、摩擦重新咬住 —— 
/// 这个"咬住 → 弹开 → 再咬住"的循环每秒重复几十次，就是肉眼看到的震动（stick-slip）。
/// 而且重力倍率抬高后，落地的穿透修正会让墙**静止时也抖**。<br/><br/>
///
/// <b>本脚本改用【约束门控】</b><br/>
/// 推动者不足时把刚体设为 <see cref="RigidbodyConstraints2D.FreezeAll"/>（X / Y / 旋转全锁）：
/// 墙在物理上**完全不能移动**，求解器不需要做任何位置修正，对玩家来说与 Static 真墙完全一致 ——
/// 单人推 0 位移、0 抖动，连插值渲染出来的微米级残留都没有。<br/>
/// 只锁 X 是不够的：那样墙仍被重力压在"与地面的竖直接触"上，每个物理步都要解一次这个接触，
/// 残留的位置修正经过插值渲染就是可见的轻微抖动。<br/>
/// 推动者达标时放开 X（默认 Y 仍锁住），墙变成只会水平滑动的 Dynamic 物体；推动者离开后再就地全锁。<br/>
/// 这样"必须两个人"是脚本明确判定的，而不是靠摩擦力硬扛出来的，抖动从根上消失。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class FakeWall : MonoBehaviour
{
    [Header("推动条件")]
    [Tooltip("需要多少个玩家/复制体同时朝墙推，墙才会被推动")]
    public int requiredPushers = 2;

    [Header("检测")]
    [Tooltip("可以从哪一侧推。-1 = 只有左侧，1 = 只有右侧，0 = 两侧都可以")]
    [Range(-1, 1)]
    public int allowedSide = 0;
    [Tooltip("检测盒厚度（米）：从墙面往外延伸多远仍算“贴住墙”")]
    public float probeThickness = 0.35f;
    [Tooltip("检测盒在墙面上下的外扩（米）")]
    public float probeMarginY = 0.15f;
    [Tooltip("推动者必须与墙面有这么多竖直重叠，避免站在墙顶的角色被算成推动者（米）")]
    public float minVerticalOverlap = 0.05f;

    [Header("冻结恢复")]
    [Tooltip("推动者不足后，连续满足这么久才重新冻结，避免边界抖动（秒）")]
    public float refreezeDelay = 0.2f;
    [Tooltip("解除固定后是否继续锁住 Y 轴。勾上 = 墙只做水平滑动、不会被推下平台、" +
             "也不会因竖直接触产生抖动（推荐，零抖动）；取消 = 恢复自由落体，可以被推下平台")]
    public bool keepYLockedWhenReleased = true;

    [Header("刚体自动配置")]
    [Tooltip("在 Awake 里把刚体参数修正为推荐值，避免手工漏改导致震动。关掉则完全使用 Inspector 里的值")]
    public bool autoConfigureRigidbody = true;
    [Tooltip("推荐重力倍率。抬高它会让落地穿透修正和摩擦力量级失控，是震动的主因之一")]
    public float recommendedGravityScale = 1f;
    [Tooltip("推荐线性阻尼。0 阻尼时求解器产生的振荡不会衰减")]
    public float recommendedLinearDrag = 4f;

    [Header("调试")]
    public bool debugLog = false;

    // ===== 运行时只读状态（Inspector 的 Debug 模式可看）=====
    /// <summary>本物理步有多少个"正在朝墙推"的玩家/复制体</summary>
    public int CurrentPushers { get; private set; }
    /// <summary>是否处于"已解除固定、可被推动"状态</summary>
    public bool IsReleased { get; private set; }

    private Rigidbody2D _rb;
    private Collider2D _col;
    private ContactFilter2D _filter;
    private readonly Collider2D[] _buffer = new Collider2D[16];
    private readonly List<Rigidbody2D> _seen = new List<Rigidbody2D>();
    private float _idleTimer;

    // 冻结态：X / Y / 旋转全锁（FreezeAll）。
    //
    // 只锁 X 时墙仍然会被重力压在"与地面的竖直接触"上，求解器每个物理步都要解一次这个接触，
    // 残留的微米级位置修正再经过插值渲染，就是那点"轻微抖动"。
    // 全锁之后刚体在物理上完全不能移动，求解器不需要做任何位置修正 ——
    // 抖动从"被压小"变成"不存在"，表现与 Static 真墙完全一致。
    private const RigidbodyConstraints2D FrozenConstraints = RigidbodyConstraints2D.FreezeAll;

    // 解除态（默认）：只放开 X，Y 继续锁住。
    // 墙只会左右滑动，不会因为重力/地面接触产生竖直抖动，也不会被推下平台掉落（真墙本来就不会掉）。
    private const RigidbodyConstraints2D ReleasedConstraintsKeepY =
        RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;

    // 解除态（可选）：只锁旋转，Y 也放开 —— 可以让它自由落体 / 被推下平台
    private const RigidbodyConstraints2D ReleasedConstraintsFreeY =
        RigidbodyConstraints2D.FreezeRotation;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();

        if (_rb == null)
        {
            Debug.LogError("FakeWall: " + name + " 上没有 Rigidbody2D，无法工作。", this);
            enabled = false;
            return;
        }
        if (_col == null || _col.isTrigger)
        {
            Debug.LogError("FakeWall: " + name + " 上需要一个非 Trigger 的 Collider2D 才能检测推动者。", this);
            enabled = false;
            return;
        }

        if (autoConfigureRigidbody)
        {
            // 这些取值都是为了"不抖"：
            //   gravityScale=1   —— 不给它超常的法向力，落地不穿透、摩擦量级正常
            //   drag=4           —— 推动结束后自然收住，振荡能衰减
            //   Interpolate      —— 与玩家一致，消除"玩家平滑、墙跳格"的视觉差
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = recommendedGravityScale;
            _rb.drag = recommendedLinearDrag;
            _rb.angularDrag = 0.05f;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        _filter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = false,
            useDepth = false,
            useNormalAngle = false
        };

        Freeze();
    }

    private void FixedUpdate()
    {
        CurrentPushers = CountPushers();

        if (CurrentPushers >= Mathf.Max(1, requiredPushers))
        {
            _idleTimer = 0f;
            if (!IsReleased) Release();
        }
        else
        {
            if (!IsReleased) return;

            _idleTimer += Time.fixedDeltaTime;
            if (_idleTimer >= Mathf.Max(0f, refreezeDelay)) Freeze();
        }
    }

    /// <summary>解除固定：恢复成普通 Dynamic 物体，可以被推走</summary>
    private void Release()
    {
        IsReleased = true;
        _idleTimer = 0f;
        _rb.constraints = keepYLockedWhenReleased ? ReleasedConstraintsKeepY : ReleasedConstraintsFreeY;

        if (debugLog)
            Debug.Log("[FakeWall] " + name + " 解除固定（推动者 " + CurrentPushers + "/" + requiredPushers + "）", this);
    }

    /// <summary>重新固定：X/Y 全锁，物理上完全不能移动，单推时与真墙一致</summary>
    private void Freeze()
    {
        IsReleased = false;
        _idleTimer = 0f;
        _rb.constraints = FrozenConstraints;
        _rb.velocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        if (debugLog)
            Debug.Log("[FakeWall] " + name + " 重新固定", this);
    }

    private int CountPushers()
    {
        if (_rb == null || _col == null) return 0;

        Bounds wallBounds = _col.bounds;
        _seen.Clear();

        // 同一个刚体可能同时落在两侧检测盒里（比如身位正好跨在墙角），用 _seen 去重
        if (allowedSide <= 0) CountInProbe(wallBounds, false);
        if (allowedSide >= 0) CountInProbe(wallBounds, true);

        return _seen.Count;
    }

    /// <summary>在墙的一侧开检测盒，统计“正在朝墙推”的玩家/复制体</summary>
    private void CountInProbe(Bounds wallBounds, bool rightSide)
    {
        float thickness = Mathf.Max(0.01f, probeThickness);
        float x0 = rightSide ? wallBounds.max.x : wallBounds.min.x - thickness;
        float x1 = rightSide ? wallBounds.max.x + thickness : wallBounds.min.x;

        Vector2 center = new Vector2((x0 + x1) * 0.5f, wallBounds.center.y);
        Vector2 size = new Vector2(Mathf.Abs(x1 - x0),
                                   wallBounds.size.y + Mathf.Max(0f, probeMarginY) * 2f);

        int count = Physics2D.OverlapBox(center, size, 0f, _filter, _buffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D col = _buffer[i];
            if (!IsValidPusher(col, wallBounds, rightSide)) continue;

            Rigidbody2D body = col.attachedRigidbody;
            if (body == null || _seen.Contains(body)) continue;
            _seen.Add(body);
        }
    }

    private bool IsValidPusher(Collider2D col, Bounds wallBounds, bool rightSide)
    {
        if (col == null || col.isTrigger) return false;
        if (col == _col) return false;
        if (col.transform.IsChildOf(transform) || transform.IsChildOf(col.transform)) return false;

        // 只认"会被物理驱动的非静态刚体"：排除没有刚体的纯地形，以及瓦片地图那种 Static 刚体
        Rigidbody2D body = col.attachedRigidbody;
        if (body == null) return false;
        if (body.bodyType == RigidbodyType2D.Static) return false;

        // 只认玩家与复制体：道具、墓碑、箱子都不算推墙的人
        bool isActor = col.GetComponentInParent<Player>() != null
                    || col.GetComponentInParent<PlayerClone>() != null;
        if (!isActor) return false;

        // 必须与墙面有足够竖直重叠：站在墙顶上的角色竖直重叠为 0，会被排除
        float vOverlap = Mathf.Min(col.bounds.max.y, wallBounds.max.y)
                       - Mathf.Max(col.bounds.min.y, wallBounds.min.y);
        if (vOverlap < Mathf.Max(0f, minVerticalOverlap)) return false;

        // 必须在朝墙的方向上按着方向键。
        // 复制体（PlayerClone.cs）读的就是 PlayerState.xInput，所以玩家和分身共用同一个值，
        // 表现为"玩家按方向键、分身跟着一起推"。
        float input = PlayerState.xInput;
        if (Mathf.Abs(input) < 0.01f) return false;
        return rightSide ? input < 0f : input > 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D c = _col != null ? _col : GetComponent<Collider2D>();
        if (c == null) return;

        Bounds b = c.bounds;
        float thickness = Mathf.Max(0.01f, probeThickness);
        float extraY = Mathf.Max(0f, probeMarginY);

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
        if (allowedSide <= 0)
            Gizmos.DrawWireCube(new Vector3(b.min.x - thickness * 0.5f, b.center.y, 0f),
                                new Vector3(thickness, b.size.y + extraY * 2f, 0.01f));
        if (allowedSide >= 0)
            Gizmos.DrawWireCube(new Vector3(b.max.x + thickness * 0.5f, b.center.y, 0f),
                                new Vector3(thickness, b.size.y + extraY * 2f, 0.01f));
    }
}
