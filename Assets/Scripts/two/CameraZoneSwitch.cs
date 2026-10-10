using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 判定区所在的镜头边缘：Horizontal = 画面右侧（左右切屏），Vertical = 画面上侧（上下切屏）。
/// </summary>
public enum SwitchAxis { Horizontal, Vertical }

/// <summary>
/// 场景内镜头切换判定区：玩家到达画面边缘的矩形判定区时，瞬间把镜头切到指定位置，
/// 并把玩家瞬移到指定落点，同时<b>不改变玩家的任何状态</b>（速度、朝向、动画状态全部保留）。<br/>
/// 玩家从反方向重新进入同一个判定区时，镜头与玩家切回原处——单区双向触发。<br/>
/// 用法：挂在带 BoxCollider2D(Is Trigger) 的空物体上，用空物体给相机/玩家指定落点。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CameraZoneSwitch : MonoBehaviour
{
    [Header("判定区")]
    [Tooltip("判定区所在的镜头边缘：Horizontal = 右侧，Vertical = 上侧")]
    public SwitchAxis axis = SwitchAxis.Horizontal;
    [Tooltip("沿该轴的速度绝对值小于此值时，改用玩家相对判定区中心的位置来判断方向")]
    public float minSpeedForDirection = 2f;

    [Header("切过去（去程）")]
    [Tooltip("切过去时相机要到达的位置（空物体）")]
    public Transform cameraIn;
    [Tooltip("切过去时玩家要到达的位置（空物体，按玩家 Transform 位置对齐）")]
    public Transform playerIn;

    [Header("回切（回程，留空则用初始位置）")]
    [Tooltip("回切时相机要到达的位置；留空 = 相机初始位置")]
    public Transform cameraOut;
    [Tooltip("回切时玩家要到达的位置；留空 = 进场前玩家的位置")]
    public Transform playerOut;

    [Header("目标相机（留空 = Camera.main）")]
    public Camera targetCamera;

    private Collider2D _zone;

    // 所有启用中的判定区。传送这类"直接把玩家瞬移过去"的操作绕过了触发回调，
    // 需要主动查询判定区来补切镜头，所以这里留一个注册表。
    private static readonly List<CameraZoneSwitch> All = new List<CameraZoneSwitch>();

    // 防抖：一次进入只切换一次，离开判定区后才重新武装
    private bool _armed = true;

    // 相机初始位置（cameraOut 留空时使用）
    private Vector3 _camOriginPos;
    private bool _hasCamOrigin;

    // 进场前的玩家位置（playerOut 留空时使用）
    private Vector3 _playerOriginPos;
    private bool _hasPlayerOrigin;

    private void Awake()
    {
        _zone = GetComponent<Collider2D>();

        if (_zone != null && !_zone.isTrigger)
        {
            Debug.LogWarning("CameraZoneSwitch: " + name + " 的 Collider2D 没有勾选 Is Trigger，无法作为镜头切换判定区。", this);
        }

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
        {
            _camOriginPos = targetCamera.transform.position;
            _hasCamOrigin = true;
        }
    }

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    /// <summary>
    /// 瞬移（墓碑传送等）之后调用：找出离 worldPos 最近的判定区，
    /// 按落点在它的哪一侧把相机直接吸到设计好的锚点上，并把该区重新武装。<br/>
    /// 用"最近"来消歧，是因为连续多个判定区（左屏/中屏/右屏）中只有紧挨落点的那一个
    /// 才知道落点这一屏的取景位。返回是否成功切换。
    /// </summary>
    public static bool SnapCameraToNearest(Vector3 worldPos)
    {
        CameraZoneSwitch nearest = null;
        float best = float.PositiveInfinity;

        for (int i = 0; i < All.Count; i++)
        {
            CameraZoneSwitch zone = All[i];
            if (zone == null) continue;

            float sqr = zone.SqrDistanceToZone(worldPos);
            if (sqr < best)
            {
                best = sqr;
                nearest = zone;
            }
        }

        return nearest != null && nearest.SnapFor(worldPos);
    }

    /// <summary>落点到本判定区矩形的平方距离（落在矩形内为 0）</summary>
    private float SqrDistanceToZone(Vector3 p)
    {
        Bounds b = _zone != null ? _zone.bounds : new Bounds(transform.position, Vector3.zero);
        float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
        float dy = Mathf.Max(b.min.y - p.y, 0f, p.y - b.max.y);
        return dx * dx + dy * dy;
    }

    /// <summary>按落点在哪一侧把相机吸到对应锚点，并重新武装本判定区</summary>
    private bool SnapFor(Vector3 worldPos)
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null || cameraIn == null)
            return false;

        Vector3 inPos  = cameraIn.position;
        Vector3 outPos = cameraOut != null ? cameraOut.position
                       : (_hasCamOrigin ? _camOriginPos : inPos);

        Vector3 center = (_zone != null) ? (Vector3)_zone.bounds.center : transform.position;

        float delta = (axis == SwitchAxis.Horizontal) ? worldPos.x - center.x : worldPos.y - center.y;
        float dirIn = (axis == SwitchAxis.Horizontal) ? inPos.x - outPos.x   : inPos.y - outPos.y;

        //落点落在"去程"那一侧就用 cameraIn，否则回到出发位
        bool inSide = dirIn * delta >= 0f;
        Vector3 target = inSide ? inPos : outPos;

        float z = targetCamera.transform.position.z;
        targetCamera.transform.position = new Vector3(target.x, target.y, z);

        //玩家已被瞬移出判定区，重新武装：否则 _armed 会一直停在 false，
        //之后正常走进走出也不会再触发切换
        _armed = true;
        return true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!_armed) return;

        Player player = collision.GetComponentInParent<Player>();
        if (player == null || player.rb == null) return;

        bool forward = EvaluateForward(player);
        Execute(player, forward);
        _armed = false;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // 玩家离开判定区，重新武装，允许下一次进入时再次切换
        if (collision.GetComponentInParent<Player>() == null) return;
        _armed = true;
    }

    /// <summary>正 = 切过去（去程），负 = 回切（回程）。速度太小则用玩家在判定区哪一侧判断。</summary>
    private bool EvaluateForward(Player player)
    {
        Vector2 v = player.rb.velocity;
        float axisSpeed = (axis == SwitchAxis.Horizontal) ? v.x : v.y;

        if (Mathf.Abs(axisSpeed) >= minSpeedForDirection)
            return axisSpeed > 0f;

        //速度过小时（例如被水平移动平台驮着穿过判定区，玩家自身速度接近 0）改用位置判断。
        //去程是朝 cameraIn 那一屏走，所以"从出发侧进入"才算去程：玩家的相对位置与去程方向异号。
        //不能写成 delta >= 0 —— 那是"落点已经在目标那一侧"的判断，只适用于瞬移落点（见 SnapFor）；
        //放到进入检测上正好相反，会把玩家按回出发的那一屏。
        Vector3 center = (_zone != null) ? (Vector3)_zone.bounds.center : transform.position;
        Vector3 p = player.transform.position;
        float delta = (axis == SwitchAxis.Horizontal) ? p.x - center.x : p.y - center.y;

        float dirIn = DirectionOfForward;
        if (Mathf.Abs(dirIn) < 0.0001f)
        {
            //cameraIn 与出发位在同一条线上，无法据锚点判断去程方向（一般是锚点没配）
            Debug.LogWarning("CameraZoneSwitch: " + name +
                " 的 cameraIn 与 cameraOut/相机初始位置在同一侧，无法用位置判断方向，暂按速度正负处理。", this);
            return axisSpeed >= 0f;
        }

        return delta * dirIn < 0f;
    }

    /// <summary>去程在轴上的方向：cameraIn 相对出发位（cameraOut，留空则相机初始位置）的位移符号</summary>
    private float DirectionOfForward
    {
        get
        {
            Vector3 inPos  = cameraIn != null ? cameraIn.position : transform.position;
            Vector3 outPos = cameraOut != null ? cameraOut.position
                           : (_hasCamOrigin ? _camOriginPos : inPos);
            return (axis == SwitchAxis.Horizontal) ? inPos.x - outPos.x : inPos.y - outPos.y;
        }
    }

    /// <summary>执行切换：先缓存进场前的玩家位置，再移动相机与玩家（不动速度 / 朝向 / 状态机）</summary>
    private void Execute(Player player, bool forward)
    {
        if (forward)
        {
            // 记录进场前的玩家位置，供 playerOut 留空时回切使用
            _playerOriginPos = player.transform.position;
            _hasPlayerOrigin = true;
        }

        MoveCamera(forward ? cameraIn : cameraOut, forward);
        MovePlayer(player, forward ? playerIn : playerOut, forward);
    }

    /// <summary>移动相机到指定锚点，保留原 Z（避免正交相机裁剪面异常）</summary>
    private void MoveCamera(Transform anchor, bool forward)
    {
        if (targetCamera == null) return;

        float z = targetCamera.transform.position.z;

        if (anchor != null)
            targetCamera.transform.position = new Vector3(anchor.position.x, anchor.position.y, z);
        else if (!forward && _hasCamOrigin)
            targetCamera.transform.position = new Vector3(_camOriginPos.x, _camOriginPos.y, z);
    }

    /// <summary>瞬移玩家到指定锚点，保留原 Z；不触碰 velocity / 朝向 / 状态机</summary>
    private void MovePlayer(Player player, Transform anchor, bool forward)
    {
        Vector3 target;

        if (anchor != null)
            target = new Vector3(anchor.position.x, anchor.position.y, player.transform.position.z);
        else if (!forward && _hasPlayerOrigin)
            target = new Vector3(_playerOriginPos.x, _playerOriginPos.y, player.transform.position.z);
        else
            return;

        player.rb.position = target;
        player.transform.position = target;
        Physics2D.SyncTransforms();
    }
}