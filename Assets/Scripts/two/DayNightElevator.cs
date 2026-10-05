using UnityEngine;

/// <summary>
/// 昼夜电梯：与「暮」能力联动。<br/>
/// 白天（TimeOfDayManager.IsNight == false）→ 停在最高点；黑夜 → 降到最低点。<br/>
/// 摆放时把平台放在最高点，travelDistance 即为下降到最低点的行程。<br/>
/// 与 <see cref="BalanceElevator"/> 一致：平台需为 Kinematic 刚体，用 MoveTowards 严格匀速逼近目标、不会过冲。
/// </summary>
public class DayNightElevator : MonoBehaviour
{
    [Header("平台（需要 Rigidbody2D 设为 Kinematic）")]
    [Tooltip("实际升降的物体。留空则用自身")]
    public Transform platform;

    [Header("运动参数")]
    [Tooltip("升降速度（米/秒）。MoveTowards 保证严格匀速、且不会过冲")]
    public float moveSpeed = 1.5f;
    [Tooltip("从最高点下降到最低点的行程（米，正数）")]
    public float travelDistance = 4f;

    // 最高点 / 最低点的世界 Y：最高点 = 物体摆放时的初始 Y
    private float topY;
    private float bottomY;

    /// <summary>白天 = 最高点，黑夜 = 最低点</summary>
    private float TargetY => TimeOfDayManager.IsNight ? bottomY : topY;

    private void Awake()
    {
        if (platform == null) platform = transform;

        SetKinematic(platform);

        // 物体摆放位置即最高点，向下 travelDistance 得到最低点
        topY    = platform.position.y;
        bottomY = topY - travelDistance;
    }

    private void Update()
    {
        // 严格匀速逼近目标，到达端点自然停住
        Vector3 p = platform.position;
        p.y = Mathf.MoveTowards(p.y, TargetY, moveSpeed * Time.deltaTime);
        platform.position = p;
    }

    /// <summary>确保平台是 Kinematic 刚体，避免物理引擎与脚本抢位置</summary>
    private void SetKinematic(Transform target)
    {
        if (target == null) return;

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogWarning("DayNightElevator: " + target.name +
                " 上没有 Rigidbody2D。平台虽然能移动，但玩家站上去的触发检测可能失效。", target);
            return;
        }

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Transform t = platform != null ? platform : transform;
        float top    = t.position.y;
        float bottom = top - travelDistance;

        // 黄点 = 最高点（白天），蓝点 = 最低点（黑夜）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(t.position.x, top, t.position.z), 0.15f);

        Gizmos.color = new Color(0.35f, 0.55f, 1f, 1f);
        Gizmos.DrawWireSphere(new Vector3(t.position.x, bottom, t.position.z), 0.15f);

        Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
        Gizmos.DrawLine(new Vector3(t.position.x, top, t.position.z),
                        new Vector3(t.position.x, bottom, t.position.z));
    }
}