using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 风扇风区：进入触发器范围的玩家会受到持续向上的风场，被托着上升。<br/>
/// 作用方式：FixedUpdate 里给玩家刚体施加向上的力（AddForce），并把上升速度限制在 windSpeed 内，
/// 因此风是"托举"而不是"瞬移"；离开风区后交给重力自然下坠。<br/>
/// 只在白天吹风（TimeOfDayManager.IsNight == false）；黑夜时风与粒子一并停掉，完全没有效果。
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
    [Tooltip("isFloating=false 时是否连粒子一起停掉，让风扇看起来像没在工作")]
    public bool stopParticlesWhenInactive = true;

    [Header("粒子特效（可留空：运行时会自动在自身和子物体里查找）")]
    public ParticleSystem windParticles;
    [Tooltip("勾选后粒子发射框会自动匹配本物体 Collider2D 的尺寸")]
    public bool syncParticlesWithTrigger = true;

    // 需要施加风力的刚体（用列表兼容同时有多个物体进入）
    private readonly List<Rigidbody2D> _targets = new List<Rigidbody2D>();

    // 每个刚体当前的触发计数：同一物体的多个子碰撞体会重复触发，靠计数避免被一次 Exit 剔除
    private readonly Dictionary<Rigidbody2D, int> _stayCount = new Dictionary<Rigidbody2D, int>();

    // 复用的临时容器，避免每帧产生 GC
    private readonly List<Rigidbody2D> _staleKeys = new List<Rigidbody2D>();
    private readonly List<Collider2D> _bodyColliders = new List<Collider2D>();

    private Collider2D _zone;
    private bool _particlesActive;

    private void Awake()
    {
        _zone = GetComponent<Collider2D>();

        if (_zone != null && !_zone.isTrigger)
        {
            Debug.LogWarning("windarea: " + name + " 的 Collider2D 没有勾选 Is Trigger，无法作为风区触发范围。", this);
        }

        if (windParticles == null)
            windParticles = GetComponentInChildren<ParticleSystem>();

        ApplyParticleShape();
    }

    private void FixedUpdate()
    {
        // 风口只在白天生效：黑夜时既不施力、也不显示粒子
        bool dayTime = !TimeOfDayManager.IsNight;
        bool blow = false;

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

            // 只有白天且满足受风条件（如漂浮状态）时才吃风
            if (dayTime && CanBlow(body))
            {
                ApplyWind(body);
                blow = true;
            }
        }

        PruneDestroyedKeys();
        SetParticlesActive(dayTime && (!stopParticlesWhenInactive || blow));
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

    /// <summary>粒子只在状态切换时 Play/Stop，避免每帧调用</summary>
    private void SetParticlesActive(bool active)
    {
        if (windParticles == null) return;
        if (_particlesActive == active) return;

        _particlesActive = active;

        if (active)
            windParticles.Play();
        else
            windParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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

    /// <summary>把粒子发射框对齐到风区碰撞体尺寸，改碰撞体大小后粒子范围自动跟着变</summary>
    private void ApplyParticleShape()
    {
        if (!syncParticlesWithTrigger) return;
        if (windParticles == null || _zone == null) return;

        ParticleSystem.ShapeModule shape = windParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        BoxCollider2D box = _zone as BoxCollider2D;
        if (box != null)
        {
            // BoxCollider2D 用 size/offset；其他形状退化为用包围盒尺寸
            shape.scale = new Vector3(box.size.x, box.size.y, 1f);
            shape.position = new Vector3(box.offset.x, box.offset.y, 0f);
        }
        else
        {
            Vector3 size = _zone.bounds.size;
            shape.scale = new Vector3(size.x, size.y, 1f);
            shape.position = Vector3.zero;
        }
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
