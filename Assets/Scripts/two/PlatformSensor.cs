using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 平台负载传感器：统计<b>真正压在平台顶面上</b>的所有有效刚体总质量。<br/>
/// 直接读 Rigidbody2D.mass，因此不需要额外的重量标记脚本：想改重量就改它自己的 Rigidbody2D.mass。<br/><br/>
/// 判定分两步，兼顾"容忍弹跳"和"排除浮空"：<br/>
/// <b>第一步（候选集）</b>：以平台碰撞体顶面为基准，向上 <see cref="detectHeightAboveTop"/>、
/// 向下 <see cref="detectDepthBelowTop"/> 组成一个大检测盒，盒内带刚体的物体先作为候选。
/// 盒子大是为了容忍乘客被 Kinematic 平台顶得弹离表面（用真实接触点会因接触断开而读数归零，
/// 天平目标被打成 0、进度被撤销，表现为来回抖动且速度越调越慢）。<br/>
/// <b>第二步（连通性）</b>：只有能从平台顶面出发、通过一串"竖直间隙不超过
/// <see cref="stackTolerance"/> 的相邻物体"连上来的候选才算数。这样：
/// <list type="bullet">
/// <item>压在上面 / 堆叠在上面的 → 计入；</item>
/// <item>悬在空中没有支撑的（浮空、正从上方掉落穿过盒子）→ <b>不计入</b>。</item>
/// </list>
/// 检测盒在 Scene 视图有蓝色线框可视化（选中物体即可看到）。<br/><br/>
/// 谁计入配重由 <see cref="MechanismQuery"/> 决定：道具与墓碑分别读各自的开关（默认关闭），其余刚体（玩家等）照旧。<br/><br/>
/// <b>执行顺序</b>：设为 <b>-100</b>，必须早于 <see cref="BalanceElevator"/>（后者为 100）。
/// 平台是在 BalanceElevator 的 FixedUpdate 里被挪动的，本传感器要在"挪动之前"读，
/// 这样读到的平台面与乘客位置都出自同一个物理步，不会产生一帧偏斜（否则实测间隙会比真实掉队量大一个
/// 渲染帧的位移，乘客会被误判成浮空、读数抖动）。
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlatformSensor : MonoBehaviour
{
    [Tooltip("是否为天平平台。勾选 = 计入天平；若把本脚本复用在地面压力板等场合可关掉")]
    public bool isActive = true;

    [Tooltip("平台顶面之上多大的范围作为候选（米）。要大一点以覆盖堆叠，浮空的会被连通性判定剔除")]
    public float detectHeightAboveTop = 2.5f;

    [Tooltip("平台顶面之下仍作为候选的深度（米），覆盖乘客轻微嵌入平台的情况")]
    public float detectDepthBelowTop = 0.1f;

    [Tooltip("竖直相邻容差（米）：物体底面与下方支撑面（平台顶面或已计入物体的顶面）的高差在这个值以内，才算“压在上面”")]
    public float stackTolerance = 0.3f;

    /// <summary>候选物体：刚体 + 它在世界空间里的竖直范围</summary>
    private struct BodyInfo
    {
        public Rigidbody2D body;
        public float minY;
        public float maxY;
        public bool accepted;
    }

    //当前压在平台上的刚体（最终结果，按刚体去重）
    private readonly List<Rigidbody2D> _tracked = new List<Rigidbody2D>();

    //复用的查询结果缓存，避免每帧分配
    private readonly Collider2D[] _buffer = new Collider2D[32];

    //候选表，同样复用避免分配（上限 = 查询缓存大小）
    private BodyInfo[] _candidates;

    //平台本体上的碰撞体：检测盒以它为基准（不是本脚本所在的那个 Trigger 子物体）
    private Collider2D _area;
    private ContactFilter2D _filter;

    private float _mass;

    /// <summary>当前平台上的总质量。每帧由 FixedUpdate 重算，其余脚本可直接读取</summary>
    public float Mass { get { return _mass; } }

    /// <summary>当前平台上计入配重的刚体数量（调试用）</summary>
    public int BodyCount { get { return _tracked.Count; } }

    // ================= 运行时诊断（只读，不参与任何判定）=================

    /// <summary>本步测得的平台顶面世界 Y</summary>
    public float PlatformTopY { get; private set; }

    /// <summary>
    /// 本步"已计入配重"的候选中，底面高出平台顶面最多的那个高差（米）。<br/>
    /// 静止贴地时应接近 0；平台下降时它会升到约 v²/(2a)（moveSpeed=4、a=49 时约 0.16）。
    /// 如果它接近甚至超过 <see cref="stackTolerance"/>，说明乘客已经跟不上了。
    /// </summary>
    public float MaxAcceptedGap { get; private set; }

    /// <summary>本步因超出 <see cref="stackTolerance"/> 被判为浮空而剔除的候选数量。<br/>
    /// 平稳压在台上时应为 0；频繁大于 0 说明读数在抖动。</summary>
    public int FloatingRejectedCount { get; private set; }

    private void Awake()
    {
        _candidates = new BodyInfo[_buffer.Length];

        ResolveArea();

        if (_area == null)
            Debug.LogWarning("PlatformSensor: 本物体及其子物体、父物体上都没有 Collider2D，无法统计负载。", this);

        //不做图层过滤，只排除触发器（乘客靠的都是非 Trigger 碰撞体）
        _filter = new ContactFilter2D();
        _filter.useTriggers = false;
        _filter.useLayerMask = false;
        _filter.useDepth = false;
        _filter.useNormalAngle = false;
    }

    /// <summary>
    /// 取"平台本体"上的碰撞体，用于推导检测盒。<br/>
    /// 本脚本通常挂在平台下的 Trigger 子物体上；Trigger 的 bounds 会被平台缩放影响、
    /// 还带自己的偏移，直接拿它当范围很容易算出很窄的窗口，速度一快乘客就会滑出去。
    /// 所以这里统一改用父物体（平台本体）上的非 Trigger 碰撞体。
    /// </summary>
    private Collider2D ResolveArea()
    {
        if (_area != null) return _area;

        Collider2D col = GetComponent<Collider2D>();
        if (col == null || col.isTrigger)
        {
            Collider2D parentCol = transform.parent != null ? transform.parent.GetComponent<Collider2D>() : null;
            if (parentCol != null) col = parentCol;
        }
        if (col == null) col = GetComponentInChildren<Collider2D>();

        if (col != null && col.isTrigger)
            Debug.LogWarning("PlatformSensor: 只找到 Trigger 碰撞体，检测盒会以触发器为基准，可能不准。" +
                             "建议把本脚本挂到平台本体上，或让父物体带一个非 Trigger 碰撞体。", this);

        _area = col;
        return _area;
    }

    /// <summary>当前检测盒（世界空间），供查询与 Gizmo 共用</summary>
    private bool GetDetectBox(out Vector2 center, out Vector2 size)
    {
        center = Vector2.zero;
        size = Vector2.zero;
        if (_area == null) return false;

        Bounds b = _area.bounds;
        float bottom = b.max.y - detectDepthBelowTop;
        float top = b.max.y + detectHeightAboveTop;

        center = new Vector2(b.center.x, (bottom + top) * 0.5f);
        size = new Vector2(b.size.x, Mathf.Max(0.01f, top - bottom));
        return true;
    }

    private void FixedUpdate()
    {
        if (!isActive)
        {
            if (_mass != 0f) _mass = 0f;
            if (_tracked.Count > 0) _tracked.Clear();
            MaxAcceptedGap = 0f;
            FloatingRejectedCount = 0;
            return;
        }

        _tracked.Clear();
        _mass = 0f;
        MaxAcceptedGap = 0f;
        FloatingRejectedCount = 0;

        if (_candidates == null) _candidates = new BodyInfo[_buffer.Length];

        if (GetDetectBox(out Vector2 center, out Vector2 size))
        {
            int count = Physics2D.OverlapBox(center, size, 0f, _filter, _buffer);

            // ---------- 第一步：收集候选（按刚体去重，并合并同一刚体多个碰撞体的竖直范围）----------
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                Collider2D col = _buffer[i];
                if (col == null) continue;
                if (col == _area) continue;
                if (col.transform.IsChildOf(transform)) continue;   //传感器自身或其子物体
                if (transform.IsChildOf(col.transform)) continue;   //传感器所属的平台

                if (!MechanismQuery.CanWeighOnBalance(col)) continue;

                Rigidbody2D body = FindBody(col);
                if (body == null) continue;

                //平台自身的刚体不算配重
                if (body.transform == transform) continue;
                if (transform.IsChildOf(body.transform)) continue;

                Bounds cb = col.bounds;

                int slot = -1;
                for (int k = 0; k < n; k++)
                {
                    if (_candidates[k].body == body) { slot = k; break; }
                }

                if (slot >= 0)
                {
                    //同一物体的其他碰撞体：并进已有的竖直范围
                    BodyInfo merged = _candidates[slot];
                    if (cb.min.y < merged.minY) merged.minY = cb.min.y;
                    if (cb.max.y > merged.maxY) merged.maxY = cb.max.y;
                    _candidates[slot] = merged;
                }
                else if (n < _candidates.Length)
                {
                    _candidates[n].body = body;
                    _candidates[n].minY = cb.min.y;
                    _candidates[n].maxY = cb.max.y;
                    _candidates[n].accepted = false;
                    n++;
                }
            }

            // ---------- 第二步：从平台顶面出发做竖直连通性判定 ----------
            // supportY = 当前"已经连上来的最高支撑面"。新物体底面只要落在 supportY + 容差以内，
            // 就算压在这个支撑面上，计入并把支撑面抬到它的顶面；够不着的（浮空/掉落中）自然被排除。
            float supportY = _area.bounds.max.y;
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int k = 0; k < n; k++)
                {
                    if (_candidates[k].accepted) continue;
                    if (_candidates[k].minY > supportY + stackTolerance) continue;

                    _candidates[k].accepted = true;
                    if (_candidates[k].maxY > supportY) supportY = _candidates[k].maxY;
                    grew = true;
                }
            }

            // ---------- 求和 ----------
            float total = 0f;
            for (int k = 0; k < n; k++)
            {
                if (!_candidates[k].accepted) continue;
                Rigidbody2D body = _candidates[k].body;
                if (body == null) continue;
                if (_tracked.Contains(body)) continue;

                _tracked.Add(body);
                total += body.mass;
            }
            _mass = total;

            // ---------- 诊断数据（不影响上面的判定）----------
            PlatformTopY = _area.bounds.max.y;
            float maxGap = 0f;
            int rejected = 0;
            for (int k = 0; k < n; k++)
            {
                float gap = _candidates[k].minY - PlatformTopY;
                if (_candidates[k].accepted)
                {
                    if (gap > maxGap) maxGap = gap;
                }
                else
                {
                    rejected++;
                }
            }
            MaxAcceptedGap = maxGap;
            FloatingRejectedCount = rejected;
        }
    }

    /// <summary>
    /// 取碰撞体所属的刚体：向上查找以兼容"刚体在父物体、碰撞体在子物体"的常见角色结构。<br/>
    /// 纯静态碰撞体（地形、装饰）没有刚体，返回 null 即自动不计重。
    /// </summary>
    private Rigidbody2D FindBody(Collider2D collision)
    {
        if (collision == null) return null;

        Rigidbody2D body = collision.attachedRigidbody;
        if (body == null)
            body = collision.GetComponentInParent<Rigidbody2D>();

        return body;
    }

    // ================= 调试可视化 =================

    private void OnDrawGizmos()
    {
        if (!isActive) return;

        // 检测盒：直接看出候选范围有多大
        ResolveArea();
        if (GetDetectBox(out Vector2 center, out Vector2 size))
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f),
                                new Vector3(size.x, size.y, 0.01f));

            // 平台顶面（连通性判定的起点）
            if (_area != null)
            {
                Bounds b = _area.bounds;
                Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
                Gizmos.DrawLine(new Vector3(b.min.x, b.max.y, 0f), new Vector3(b.max.x, b.max.y, 0f));
            }
        }

        // 被算进配重的刚体位置（黄点）；这里只在运行时才有数据
        Gizmos.color = _tracked.Count > 0 ? Color.yellow : new Color(1f, 1f, 1f, 0.25f);
        for (int i = 0; i < _tracked.Count; i++)
        {
            if (_tracked[i] == null) continue;
            Gizmos.DrawWireSphere(_tracked[i].worldCenterOfMass, 0.15f);
        }
    }
}
