using UnityEngine;

/// <summary>
/// 组合门：**两个条件同时满足**才开门，任一个失效后延迟 <see cref="closeDelay"/> 秒关门。<br/>
/// 专为 game5 的 door_botton 设计：条件是"按钮 botton(1) 被踩住"+"踏板 triggerfloor 被踩住"。<br/><br/>
/// 与 <see cref="doorwithtrigger"/> 的关系：同样是"条件满足则向上开 moveDistance 米"、
/// 用 MoveTowards 匀速移动；区别是它支持<b>延迟关门</b>，且关门延迟可被"重新满足条件"取消。<br/>
/// 与 <see cref="botton"/> 的关系：botton 用 Invoke("closedoor", 1f) 做延迟，但 Invoke 一旦排程
/// <b>无法取消</b>——离开后又立刻回来，1 秒后门照样会关。这里改用显式计时器解决该问题。<br/><br/>
/// 摆放约定：物体当前位置 = 关门位，向上 moveDistance 米 = 开门位（与 doorwithtrigger 一致）。
/// </summary>
public class ComboDoor : MonoBehaviour
{
    [Header("两个条件（都为真才开门）")]
    [Tooltip("按钮：踩住 = 满足")]
    public triggerfloor button;
    [Tooltip("踏板：踩住 = 满足")]
    public triggerfloor plate;

    [Header("运动参数（与 doorwithtrigger 一致：向上开）")]
    [Tooltip("开门时向上移动的距离（米）")]
    public float moveDistance = 4f;
    [Tooltip("移动速度（米/秒）。MoveTowards 保证严格匀速、不会过冲")]
    public float moveSpeed = 8f;

    [Header("延迟")]
    [Tooltip("两个条件不再同时满足后，等多少秒才关门")]
    public float closeDelay = 1f;

    [Header("调试")]
    [Tooltip("在 Scene 视图画出开门位/关门位（仅选中时显示）")]
    public bool drawGizmos = true;

    // 关门位 / 开门位
    private Vector3 closedPosition;
    private Vector3 openedPosition;

    // 当前目标位置
    private Vector3 targetPosition;

    // 关门倒计时：条件满足时重置为 closeDelay
    private float _closeTimer;

    // 未接线提示只打一次
    private bool _warnedMissing;

    // 只用于 Gizmos 显示"开门位"落点
    private bool _ready;

    private void Start()
    {
        closedPosition = transform.position;                    // 摆放位置 = 关门位
        openedPosition = closedPosition + Vector3.up * moveDistance;
        targetPosition = closedPosition;
        _closeTimer = 0f;
        _ready = true;
    }

    private void Update()
    {
        bool buttonPressed = ReadSwitch(button);
        bool platePressed = ReadSwitch(plate);

        if (buttonPressed && platePressed)
        {
            // 两个条件都满足：开，并把计时器重置
            // ⇒ 延迟期间只要任一条件重新满足，关门就被取消（这是不用 Invoke 的原因）
            targetPosition = openedPosition;
            _closeTimer = closeDelay;
        }
        else
        {
            // 任一个失效：开始（或继续）倒计时，归零才关门
            _closeTimer -= Time.deltaTime;
            if (_closeTimer <= 0f)
                targetPosition = closedPosition;
        }

        // 用 MoveTowards 实现匀速移动，避免用 Lerp 造成的先快后慢
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime);
    }

    /// <summary>读一个机关开关。字段没接线时视为"不满足"，并给出一次提示</summary>
    private bool ReadSwitch(triggerfloor sw)
    {
        if (sw == null)
        {
            if (!_warnedMissing) WarnMissing();
            return false;
        }

        return sw.isPlayerOnFloor;
    }

    private void WarnMissing()
    {
        _warnedMissing = true;
        Debug.LogWarning("ComboDoor: button / plate 没有接线，门将永远打不开。", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        // 编辑模式下也按"当前位置 = 关门位"预览行程
        Vector3 closed = _ready ? closedPosition : transform.position;
        Vector3 opened = closed + Vector3.up * moveDistance;

        Gizmos.color = new Color(0.4f, 0.8f, 1f, 1f);   // 蓝：关门位
        Gizmos.DrawWireCube(closed, new Vector3(0.3f, 0.1f, 0.01f));

        Gizmos.color = new Color(0.4f, 1f, 0.5f, 1f);   // 绿：开门位
        Gizmos.DrawWireCube(opened, new Vector3(0.3f, 0.1f, 0.01f));

        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawLine(closed, opened);
    }
}
