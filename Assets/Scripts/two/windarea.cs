using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 风扇风区：进入触发器范围的玩家会受到持续向上的风场，被托着上升。<br/>
/// 作用方式：FixedUpdate 里给玩家刚体施加向上的力（AddForce），并把上升速度限制在 windSpeed 内，
/// 因此风是"托举"而不是"瞬移"；离开风区后交给重力自然下坠。<br/>
/// 风口随昼夜开闭：白天开启（吹风 + 播放风动画），黑夜关闭（不吹风、停止动画）。<br/>
/// 勾选 usePeriodicCycle 后忽略昼夜，改为按周期自动开 n 秒、关 m 秒（动画同步）。<br/><br/>
/// <b>视觉表现</b>：由子物体上的 Animator 参数 <c>iswind</c> 驱动（循环动画）。
/// 风口开 → <c>iswind = true</c>；风口关 → <c>iswind = false</c>，同时清掉残留帧以免画面留影。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class windarea : MonoBehaviour
{
    [Header("风区参数")]
    [Tooltip("风的上升速度上限（米/秒）：玩家在风区内最多被吹到这个上升速度")]
    public float windSpeed = 6f;
    [Tooltip("风的推力（米/秒²，与质量无关）：越大上升起动越快、越难被压下去")]
    public float windForce = 40f;

    [Header("作用对象")]
    [Tooltip("只对挂有 Player 组件的物体生效。取消勾选则对所有非静态刚体生效")]
    public bool onlyAffectPlayer = true;
    [Tooltip("要求玩家处于漂浮状态(isFloating)才生效：没拿「幕」道具时站在风区里不会被吹起")]
    public bool requireFloating = true;

    [Header("风动画（可留空：运行时会自动在子物体里查找 Animator）")]
    [Tooltip("用参数 iswind 控制：风口开=true 播放循环动画，风口关=false 停止")]
    public Animator animator;

    /// <summary>动画参数名（与 wind.controller 里的 bool 参数一致）</summary>
    private const string WindAnimParam = "iswind";

    [Header("周期开关（可选）")]
    [Tooltip("勾选后忽略昼夜，按固定周期自动开/关风口（动画同步）")]
    public bool usePeriodicCycle = false;
    [Tooltip("周期模式：每次开启持续 n 秒")]
    public float openDuration = 3f;
    [Tooltip("周期模式：每次关闭持续 m 秒")]
    public float closedDuration = 3f;

    // 需要施加风力的刚体（用列表兼容同时有多个物体进入）
    private readonly List<Rigidbody2D> _targets = new List<Rigidbody2D>();

    // 每个刚体当前的触发计数：同一物体的多个子碰撞体会重复触发，靠计数避免被一次 Exit 剔除
    private readonly Dictionary<Rigidbody2D, int> _stayCount = new Dictionary<Rigidbody2D, int>();

    // 复用的临时容器，避免每帧产生 GC
    private readonly List<Rigidbody2D> _staleKeys = new List<Rigidbody2D>();
    private readonly List<Collider2D> _bodyColliders = new List<Collider2D>();

    private Collider2D _zone;

    // 风口是否开启：白天开启，黑夜关闭
    private bool _ventOpen = true;

    // 周期开关模式
    private bool _periodicActive;           // 当前是否处于周期模式
    private bool _cycleOpenPhase = true;    // 周期模式下当前阶段（true = 开启中）
    private float _cycleTimer;              // 当前阶段已持续时间

    private void Awake()
    {
        _zone = GetComponent<Collider2D>();

        if (_zone != null && !_zone.isTrigger)
        {
            Debug.LogWarning("windarea: " + name + " 的 Collider2D 没有勾选 Is Trigger，无法作为风区触发范围。", this);
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
            Debug.LogWarning("windarea: " + name + " 没找到 Animator，风不会有动画表现（风力仍然生效）。", this);
    }

    private void OnEnable()
    {
        //昼夜切换时立即更新风口状态
        TimeOfDayManager.OnTimeChanged += HandleTimeChanged;

        _periodicActive = !usePeriodicCycle;   //先置反，保证 ApplyMode 一定执行一次
        ApplyMode(usePeriodicCycle);
    }

    private void OnDisable()
    {
        TimeOfDayManager.OnTimeChanged -= HandleTimeChanged;
    }

    private void HandleTimeChanged(bool isNight)
    {
        ApplyVentState(isNight);
    }

    private void Update()
    {
        //运行时切换开关也能立即生效
        if (usePeriodicCycle != _periodicActive)
            ApplyMode(usePeriodicCycle);

        if (usePeriodicCycle)
            TickPeriodicCycle();
    }

    /// <summary>切换/初始化风口驱动方式：周期模式 or 昼夜模式</summary>
    private void ApplyMode(bool periodic)
    {
        _periodicActive = periodic;

        if (periodic)
        {
            //周期模式：从"开启"阶段开始计时
            _cycleOpenPhase = true;
            _cycleTimer = 0f;
            SetVentOpen(true);
        }
        else
        {
            //非周期模式：回到原来的昼夜行为
            ApplyVentState(TimeOfDayManager.IsNight);
        }
    }

    /// <summary>周期计时：开 openDuration 秒 → 关 closedDuration 秒 → 如此循环</summary>
    private void TickPeriodicCycle()
    {
        float phaseLength = _cycleOpenPhase
            ? Mathf.Max(0.01f, openDuration)
            : Mathf.Max(0.01f, closedDuration);

        _cycleTimer += Time.deltaTime;
        if (_cycleTimer < phaseLength) return;

        _cycleTimer = 0f;
        _cycleOpenPhase = !_cycleOpenPhase;
        SetVentOpen(_cycleOpenPhase);
    }

    /// <summary>白天风口开启（吹风 + 播放动画），黑夜风口关闭（不吹风 + 停动画）。周期模式下由周期计时接管。</summary>
    private void ApplyVentState(bool isNight)
    {
        if (usePeriodicCycle) return;   //周期模式接管风口开关

        SetVentOpen(!isNight);
    }

    /// <summary>
    /// 设置风口开/关，并同步风动画。<br/>
    /// 开启：<c>iswind = true</c> 播放循环动画。<br/>
    /// 关闭：<c>iswind = false</c>；控制器若没有"空白状态"，动画会停在最后一帧，
    /// 所以这里额外把被动画驱动的精灵清空，保证画面不留影。
    /// </summary>
    private void SetVentOpen(bool open)
    {
        _ventOpen = open;

        if (animator == null) return;

        animator.SetBool(WindAnimParam, open);

        if (!open) ClearAnimationSprite();
    }

    /// <summary>清掉动画驱动的那张精灵，避免风口关闭后画面残留最后一帧</summary>
    private void ClearAnimationSprite()
    {
        if (animator == null) return;

        // animator 挂在被动画驱动的那个子物体上（wind.prefab 里的 6-1-7）
        SpriteRenderer sr = animator.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sprite = null;
    }

    private void FixedUpdate()
    {
        //黑夜风口关闭：不施加任何风（漂浮状态也没用）
        if (!_ventOpen) return;

        // 倒序遍历，便于安全移除已离开或已销毁的对象
        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            Rigidbody2D body = _targets[i];

            if (body == null)
            {
                // 对象已被销毁，移除目标；残留的计数项在循环后统一清理
                _targets.RemoveAt(i);
                continue;
            }

            if (!IsInsideZone(body))
            {
                _targets.RemoveAt(i);
                _stayCount.Remove(body);
                continue;
            }

            // 只有漂浮状态(isFloating)才吃风：没拿「幕」时站在风区里完全没效果
            if (CanBlow(body))
                ApplyWind(body);
        }

        PruneDestroyedKeys();
    }

    /// <summary>
    /// 判断某个刚体当前是否满足受风条件。<br/>
    /// 玩家从 PlayerManager 取（单例，全局唯一），取不到再退回自己找 Player 组件。
    /// </summary>
    private bool CanBlow(Rigidbody2D body)
    {
        if (body == null) return false;
        if (!requireFloating) return true;

        Player player = GetPlayer(body);
        if (player == null) return false;

        return player.isFloating;
    }

    /// <summary>取玩家对象：优先用 PlayerManager，避免依赖自己的父级查找</summary>
    private Player GetPlayer(Rigidbody2D body)
    {
        PlayerManager manager = PlayerManager.instance;
        if (manager != null && manager.player != null)
            return manager.player;

        // PlayerManager 缺失或没接线时，退回从刚体往上找 Player 组件
        return body.GetComponentInParent<Player>();
    }

    /// <summary>给单个刚体施加向上的风：力负责托举，速度上限负责匀速上升</summary>
    private void ApplyWind(Rigidbody2D body)
    {
        if (body == null) return;

        // 乘 mass ⇒ 无论物体多沉，获得的加速度都是 windForce（米/秒²），手感一致
        body.AddForce(Vector2.up * windForce * body.mass, ForceMode2D.Force);

        // 上升速度封顶：玩家自己跳得比风快时不压制，所以风里仍然能跳跃脱离
        Vector2 v = body.velocity;
        if (v.y > windSpeed)
        {
            v.y = windSpeed;
            body.velocity = v;
        }
    }

    /// <summary>
    /// 判定目标是否仍在风区内：用风区碰撞体的包围盒包含目标的碰撞体中心。<br/>
    /// 不用 OverlapPoint 之类的物理查询，是为了不受项目设置 queriesHitTriggers 的影响。
    /// </summary>
    private bool IsInsideZone(Rigidbody2D body)
    {
        if (_zone == null || body == null) return false;

        Bounds zoneBounds = _zone.bounds;

        _bodyColliders.Clear();
        body.GetComponentsInChildren(false, _bodyColliders);

        for (int i = 0; i < _bodyColliders.Count; i++)
        {
            Collider2D c = _bodyColliders[i];
            if (c == null || !c.enabled) continue;

            if (zoneBounds.Contains(c.bounds.center)) return true;
            if (_zone.IsTouching(c)) return true;
        }

        return false;
    }

    /// <summary>清掉计数表里已被销毁的刚体键（遍历中不能改字典，先收集再删）</summary>
    private void PruneDestroyedKeys()
    {
        if (_stayCount.Count == 0) return;

        _staleKeys.Clear();
        foreach (KeyValuePair<Rigidbody2D, int> pair in _stayCount)
        {
            // 用 == 判断：Unity 销毁后的对象会走重载，表现为 null
            if (pair.Key == null)
                _staleKeys.Add(pair.Key);
        }

        if (_staleKeys.Count == 0) return;

        for (int i = 0; i < _staleKeys.Count; i++)
            _stayCount.Remove(_staleKeys[i]);

        _staleKeys.Clear();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Register(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // 兜底：物体在风区内部被 Instantiate/启用时 Enter 可能不触发
        Register(collision);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        Rigidbody2D body = FindTarget(collision);
        if (body == null) return;

        if (_stayCount.ContainsKey(body))
        {
            _stayCount[body]--;
            if (_stayCount[body] > 0) return;
            _stayCount.Remove(body);
        }

        _targets.Remove(body);
        // 不做任何速度处理：离开风区后由重力自然接管
    }

    private void Register(Collider2D collision)
    {
        Rigidbody2D body = FindTarget(collision);
        if (body == null) return;

        if (_stayCount.ContainsKey(body))
            _stayCount[body]++;
        else
            _stayCount[body] = 1;

        if (!_targets.Contains(body))
            _targets.Add(body);
    }

    /// <summary>筛选可受风影响的刚体：纯静态碰撞体没有刚体，自动被忽略</summary>
    private Rigidbody2D FindTarget(Collider2D collision)
    {
        if (collision == null) return null;

        Rigidbody2D body = collision.attachedRigidbody;
        if (body == null)
            body = collision.GetComponentInParent<Rigidbody2D>();

        if (body == null) return null;
        if (body.bodyType == RigidbodyType2D.Static) return null;
        if (body.bodyType == RigidbodyType2D.Kinematic) return null;

        if (onlyAffectPlayer)
        {
            Player player = body.GetComponentInParent<Player>();
            if (player == null) return null;
        }

        return body;
    }

    private void OnDrawGizmosSelected()
    {
        // 示意风向：从风区中心向上画箭头
        Collider2D col = GetComponent<Collider2D>();
        Vector3 center = (col != null) ? col.bounds.center : transform.position;
        float h = (col != null) ? col.bounds.extents.y : 1f;

        Gizmos.color = new Color(0.4f, 0.8f, 1f, 1f);
        Vector3 tip = center + Vector3.up * (h + 1f);
        Gizmos.DrawLine(center, tip);
        Gizmos.DrawLine(tip, tip + new Vector3(-0.2f, -0.3f, 0f));
        Gizmos.DrawLine(tip, tip + new Vector3(0.2f, -0.3f, 0f));
    }
}
