using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 平台负载传感器：挂在平台下的 Trigger 子物体上，统计"压在这个平台上"的所有有效刚体总质量。<br/>
/// 直接读 Rigidbody2D.mass，因此不需要额外的重量标记脚本：想改重量就改它自己的 Rigidbody2D.mass。<br/>
/// 采用重叠检测轮询，而不是触发回调——本项目的平台多为 Kinematic/Static 且靠 transform 位移，回调不可靠。<br/>
/// 谁计入配重由 <see cref="MechanismQuery"/> 决定：道具与墓碑分别读各自的开关（默认关闭），其余刚体（玩家等）照旧。
/// </summary>
public class PlatformSensor : MonoBehaviour
{
    [Tooltip("是否为天平平台。勾选 = 计入天平；若把本脚本复用在地面压力板等场合可关掉")]
    public bool isActive = true;

    //当前压在本平台上的刚体（按刚体去重，避免一个物体的多个子碰撞体被算成多份）
    private readonly List<Rigidbody2D> _tracked = new List<Rigidbody2D>();

    //复用的查询结果缓存，避免每帧分配
    private readonly Collider2D[] _buffer = new Collider2D[16];

    //取检测范围的碰撞体（本物体上的 Trigger）
    private Collider2D _area;
    private ContactFilter2D _filter;

    private float _mass;

    /// <summary>当前平台上的总质量。每帧由 FixedUpdate 重算，其余脚本可直接读取</summary>
    public float Mass { get { return _mass; } }

    /// <summary>当前平台上的刚体数量（调试用）</summary>
    public int BodyCount { get { return _tracked.Count; } }

    private void Awake()
    {
        _area = GetComponent<Collider2D>();
        if (_area == null) _area = GetComponentInChildren<Collider2D>();
        if (_area == null)
            Debug.LogWarning("PlatformSensor: 本物体及其子物体上没有 Collider2D，无法统计负载。", this);

        //不做图层过滤，只排除触发器（物体压上来靠的都是非 Trigger 碰撞体）
        _filter = new ContactFilter2D();
        _filter.useTriggers = false;
        _filter.useLayerMask = false;
        _filter.useDepth = false;
        _filter.useNormalAngle = false;
    }

    private void FixedUpdate()
    {
        if (!isActive)
        {
            if (_mass != 0f) _mass = 0f;
            if (_tracked.Count > 0) _tracked.Clear();
            return;
        }

        _tracked.Clear();

        if (_area != null)
        {
            Bounds b = _area.bounds;
            int count = Physics2D.OverlapBox(b.center, b.size, 0f, _filter, _buffer);

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _buffer[i];
                if (col == null) continue;
                if (col == _area) continue;
                if (col.transform.IsChildOf(transform)) continue;   //传感器自身或其子物体
                if (transform.IsChildOf(col.transform)) continue;   //传感器所属的平台（本脚本挂在平台上）

                if (!MechanismQuery.CanWeighOnBalance(col)) continue;

                Rigidbody2D body = FindBody(col);
                if (body == null) continue;

                //平台自身的刚体不算配重
                if (body.transform == transform) continue;
                if (transform.IsChildOf(body.transform)) continue;

                if (!_tracked.Contains(body))
                    _tracked.Add(body);
            }
        }

        //重新求和：物体在平台上时质量被修改也能立刻反映出来
        float total = 0f;
        for (int i = 0; i < _tracked.Count; i++)
        {
            Rigidbody2D body = _tracked[i];
            if (body == null) continue;
            total += body.mass;
        }
        _mass = total;
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

        Gizmos.color = _tracked.Count > 0 ? Color.yellow : new Color(1f, 1f, 1f, 0.25f);

        // 画出被统计的物体位置，方便在 Scene 视图确认谁被算进了天平
        for (int i = 0; i < _tracked.Count; i++)
        {
            if (_tracked[i] == null) continue;
            Gizmos.DrawWireSphere(_tracked[i].worldCenterOfMass, 0.15f);
        }
    }
}