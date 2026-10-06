using UnityEngine;

/// <summary>
/// 踏板机关：当有效的物体（玩家等）压在上面时 isPlayerOnFloor = true。<br/>
/// 采用重叠检测轮询，而不是物理触发回调——本项目的踏板/墓碑/平台可能是 Static/Kinematic 且靠 transform 位移，
/// 这类物体之间不会产生 OnTrigger 回调。<br/>
/// 谁算作"有效物体"由 <see cref="MechanismQuery"/> 决定：道具与墓碑分别读各自的开关（默认关闭），其余物体照旧。
/// </summary>
public class triggerfloor : MonoBehaviour
{
    [HideInInspector] public bool isPlayerOnFloor = false;

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
                if (!MechanismQuery.CanTriggerPedal(col)) continue;

                any = true;
                break;
            }
        }

        isPlayerOnFloor = any;
    }
}