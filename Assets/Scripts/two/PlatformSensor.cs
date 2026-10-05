using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 平台负载传感器：挂在平台下的 Trigger 子物体上，统计"压在这个平台上"的所有刚体总质量。<br/>
/// 直接读 Rigidbody2D.mass，因此不需要额外的重量标记脚本：
/// 想改重量就改它自己的 Rigidbody2D.mass，销毁物体也会自动剔除。
/// </summary>
public class PlatformSensor : MonoBehaviour
{
    [Tooltip("是否为天平平台。勾选 = 计入天平；若把本脚本复用在地面压力板等场合可关掉")]
    public bool isActive = true;

    // 当前压在本平台上的刚体（按刚体去重，避免一个物体的多个子碰撞体被算成多份）
    private readonly List<Rigidbody2D> _tracked = new List<Rigidbody2D>();

    // 遍历求和用的临时缓存，避免每帧产生 GC
    private readonly List<Rigidbody2D> _buffer = new List<Rigidbody2D>();

    private float _mass;

    /// <summary>当前平台上的总质量。每帧由 Update 重算，其余脚本可直接读取</summary>
    public float Mass { get { return _mass; } }

    /// <summary>当前平台上的刚体数量（调试用）</summary>
    public int BodyCount { get { return _tracked.Count; } }

    private void Update()
    {
        if (!isActive)
        {
            if (_mass != 0f) _mass = 0f;
            if (_tracked.Count > 0) _tracked.Clear();
            return;
        }

        // 清掉已被销毁的物体（列表里会变成 null）
        _tracked.RemoveAll(item => item == null);

        // 重新求和：物体在平台上时质量被修改也能立刻反映出来
        float total = 0f;
        for (int i = 0; i < _tracked.Count; i++)
        {
            Rigidbody2D body = _tracked[i];
            if (body == null) continue;
            total += body.mass;
        }
        _mass = total;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryAdd(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // 兜底：物体在传感器内部被 Instantiate/启用时，Enter 可能不触发
        TryAdd(collision);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        Rigidbody2D body = FindBody(collision);
        if (body == null) return;

        _tracked.Remove(body);
    }

    private void TryAdd(Collider2D collision)
    {
        if (!isActive) return;

        Rigidbody2D body = FindBody(collision);
        if (body == null) return;

        if (!_tracked.Contains(body))
            _tracked.Add(body);
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
