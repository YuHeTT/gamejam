using UnityEngine;

/// <summary>
/// 踏板机关：当有效的物体（玩家等）压在上面时 isPlayerOnFloor = true。<br/>
/// 采用重叠检测轮询，而不是物理触发回调——本项目的踏板/墓碑/平台可能是 Static/Kinematic 且靠 transform 位移，
/// 这类物体之间不会产生 OnTrigger 回调。<br/>
/// 谁算作"有效物体"由 <see cref="MechanismQuery"/> 决定：道具与墓碑分别读各自的开关（默认关闭），其余物体照旧。<br/><br/>
/// 注意：轮询会返回**所有**相交的碰撞体，包括地形，所以必须显式排除"地形"，否则
/// isPlayerOnFloor 会永远是 true（踏板被误判成"一直有人踩着"）。地形有两类，都要排除：
/// <list type="number">
/// <item>普通地面/墙/装饰：<b>没有</b> Rigidbody2D（例如场景里的 Square）。</item>
/// <item>瓦片地图 ground：为了驱动 CompositeCollider2D，<b>必须</b>带一个 <b>Static</b> 刚体。
/// 只判"有没有刚体"是拦不住它的，必须再判 bodyType。</item>
/// </list>
/// 原 OnTriggerStay2D 版本天然免疫，因为触发器回调要求至少一方是<b>非静态</b>刚体——
/// 这里的判空 + 排除 Static，正是等价地恢复同一语义。
/// （同类判据见 <see cref="updown"/> 的 IsStaticObstacle：也是按 bodyType 分类。）
/// </summary>
public class triggerfloor : MonoBehaviour
{
    public bool isPlayerOnFloor = false;

    //用于取检测范围的碰撞体（通常是本物体上的 Trigger 碰撞体）
    private Collider2D _area;

    //复用的查询结果缓存，避免每帧分配
    private readonly Collider2D[] _buffer = new Collider2D[16];

    private ContactFilter2D _filter;

    private void Awake()
    {
        _area = GetComponentInChildren<Collider2D>();
        if (_area == null)
            Debug.LogWarning("triggerfloor: 本物体及其子物体上没有 Collider2D，无法检测踏板。", this);

        //不做图层过滤，只排除触发器（真实碰撞体都在非 Trigger 上）
        _filter = new ContactFilter2D();
        _filter.useTriggers = false;
        _filter.useLayerMask = false;
        _filter.useDepth = false;
        _filter.useNormalAngle = false;
    }

    private void FixedUpdate()
    {
        bool any = false;

        if (_area != null)
        {
            Bounds b = _area.bounds;
            int count = Physics2D.OverlapBox(b.center, b.size, 0f, _filter, _buffer);

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _buffer[i];
                if (col == null) continue;
                if (col == _area) continue;
                if (col.transform.IsChildOf(transform)) continue;

                // 关键：只认"会被物理驱动的非静态刚体"——玩家、箱子、复制体。
                // 轮询会返回所有相交的碰撞体，其中地形必须排除，而且有两类：
                //   1) 没有 Rigidbody2D：普通地面 / 墙 / 装饰（场景里的 Square）
                //   2) 带 Static Rigidbody2D：瓦片地图 ground —— 它为了 CompositeCollider2D
                //      必须挂一个 Static 刚体，所以只判空是拦不住它的（这就是门自己打开的根因）
                // 原 OnTriggerStay2D 版本不会踩这个坑：触发器回调要求至少一方是非静态刚体。
                Rigidbody2D body = col.attachedRigidbody;
                if (body == null) continue;                              // 无刚体：纯静态地形
                if (body.bodyType == RigidbodyType2D.Static) continue;    // Static 刚体：瓦片地图地形

                if (!MechanismQuery.CanTriggerPedal(col)) continue;

                any = true;
                break;
            }
        }

        isPlayerOnFloor = any;
    }
}