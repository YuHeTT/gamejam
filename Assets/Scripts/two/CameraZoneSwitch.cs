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

    /// <summary>正 = 切过去（去程），负 = 回切（回程）。速度太小则用玩家相对判定区中心的位置判断。</summary>
    private bool EvaluateForward(Player player)
    {
        Vector2 v = player.rb.velocity;
        float axisSpeed = (axis == SwitchAxis.Horizontal) ? v.x : v.y;

        if (Mathf.Abs(axisSpeed) >= minSpeedForDirection)
            return axisSpeed > 0f;

        Vector3 center = (_zone != null) ? (Vector3)_zone.bounds.center : transform.position;
        Vector3 p = player.transform.position;
        float delta = (axis == SwitchAxis.Horizontal) ? p.x - center.x : p.y - center.y;

        return delta >= 0f;
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