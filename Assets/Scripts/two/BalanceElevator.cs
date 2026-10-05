using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 天平电梯：一根绳跨过滑轮，两端各挂一个平台，依据两端总质量的差值升降。<br/>
/// 绳长固定 ⇒ 两平台位移永远大小相等、方向相反，因此内部只维护一个自由度 offset：
///   左平台 y = 左平台原始 y + offset
///   右平台 y = 右平台原始 y - offset
/// offset = 0 即水平平衡位，也是"两边等重"时自动回到的位置。<br/>
/// 符号：offset 取正 = 左平台上升、右平台下降；左边更重时应取负（左降右升）。
/// </summary>
public class BalanceElevator : MonoBehaviour
{
    [Header("平台（需要 Rigidbody2D 设为 Kinematic）")]
    public Transform leftPlatform;
    public Transform rightPlatform;

    [Header("负载传感器（挂在平台下的 Trigger 子物体上）")]
    public PlatformSensor leftSensor;
    public PlatformSensor rightSensor;

    [Header("滑轮与绳子")]
    [Tooltip("滑轮物体的 Transform，绳子在此处折角")]
    public Transform pulley;
    [Tooltip("滑轮上的 LineRenderer，用于画绳（可留空则不画）")]
    public LineRenderer rope;

    [Header("运动参数")]
    [Tooltip("升降速度（米/秒）。MoveTowards 保证严格匀速、且不会过冲")]
    public float moveSpeed = 1.5f;
    [Tooltip("单侧最大行程（米）。现实中由地面自然限位，这里只做兜底防止穿模")]
    public float maxTravel = 2f;

    [Header("重量规则")]
    [Tooltip("是否允许玩家自身质量计入天平（读刚体质量，_GetRigidbodyMass）")]
    public bool allowPlayerAsWeight = true;

    // offset：左平台相对水平平衡位的竖直偏移，正 = 左高右低（即左轻右重）
    private float offset;

    // 两个平台上表面的原始高度（水平平衡位）
    private float leftBaseY;
    private float rightBaseY;

    // 当前两侧总质量
    private float leftMass;
    private float rightMass;

    private void Awake()
    {
        if (leftPlatform == null || rightPlatform == null)
        {
            Debug.LogError("BalanceElevator: 没有指定 leftPlatform / rightPlatform!", this);
            enabled = false;
            return;
        }

        // 记录两平台的水平平衡位
        leftBaseY = leftPlatform.position.y;
        rightBaseY = rightPlatform.position.y;

        // 兜底：平台必须是 Kinematic，否则 Physics2D 会与脚本写 transform 的行为互相打架
        SetKinematic( leftPlatform);
        SetKinematic(rightPlatform);

        if (rope != null)
        {
            // 绳子用世界坐标手动喂点，不能让 LineRenderer 自己跟随物体
            rope.useWorldSpace = true;
            rope.positionCount = 4;
        }

        UpdatePositions();
    }

    private void Update()
    {
        // 1) 读取两端总质量（传感器每帧自行维护，物体被销毁也会自动剔除）
        leftMass  = (leftSensor  != null) ? leftSensor.Mass  : 0f;
        rightMass = (rightSensor != null) ? rightSensor.Mass : 0f;

        // 2) 质量差决定目标偏移；相等 ⇒ 目标为 0，即回到水平平衡位
        //    正质量差（左重）⇒ 左平台下降、右平台上升 ⇒ offset 取负（offset 正 = 左高右低）
        float netWeight = leftMass - rightMass;
        float targetOffset = 0f;
        if (netWeight > 0f)
            targetOffset = -maxTravel;
        else if (netWeight < 0f)
            targetOffset = maxTravel;

        // 3) 严格匀速逼近目标：每帧位移恒定，且不会过冲（碰到地面自然停住）
        offset = Mathf.MoveTowards(offset, targetOffset, moveSpeed * Time.deltaTime);

        // 兜底限位，防止 Inspector 参数填错导致平台跑出场景
        offset = Mathf.Clamp(offset, -maxTravel, maxTravel);

        // 4) 落到两个平台的 transform 与绳子渲染
        UpdatePositions();
    }

    /// <summary>由绳长守恒同时写两个平台的位置</summary>
    private void UpdatePositions()
    {
        if (leftPlatform != null)
        {
            Vector3 p = leftPlatform.position;
            p.y = leftBaseY + offset;
            leftPlatform.position = p;
        }

        if (rightPlatform != null)
        {
            Vector3 p = rightPlatform.position;
            p.y = rightBaseY - offset;
            rightPlatform.position = p;
        }

        UpdateRope();
    }

    /// <summary>绳子路径：左平台 → 滑轮 → 右平台（中间重复一个点做出折角）</summary>
    private void UpdateRope()
    {
        if (rope == null) return;
        if (leftPlatform == null || rightPlatform == null) return;
        if (pulley == null)
        {
            // 没有滑轮就直连两点
            rope.positionCount = 2;
            rope.SetPosition(0, leftPlatform.position);
            rope.SetPosition(1, rightPlatform.position);
            return;
        }

        rope.positionCount = 4;
        rope.SetPosition(0, leftPlatform.position);
        rope.SetPosition(1, pulley.position);
        rope.SetPosition(2, pulley.position);
        rope.SetPosition(3, rightPlatform.position);
    }

    /// <summary>确保平台是 Kinematic 刚体，避免物理引擎与脚本抢位置</summary>
    private void SetKinematic(Transform platform)
    {
        if (platform == null) return;

        Rigidbody2D rb = platform.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogWarning("BalanceElevator: " + platform.name +
                " 上没有 Rigidbody2D。平台虽然能移动，但玩家站上去的触发检测可能失效。", platform);
            return;
        }

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    // ================= 对外查询 =================

    /// <summary>左平台当前总质量</summary>
    public float LeftMass { get { return leftMass; } }

    /// <summary>右平台当前总质量</summary>
    public float RightMass { get { return rightMass; } }

    /// <summary>当前偏移：正 = 左高右低（左轻右重），负 = 左低右高（左重右轻），0 = 水平平衡</summary>
    public float Offset { get { return offset; } }

    /// <summary>天平是否处于水平位</summary>
    public bool IsLevel { get { return Mathf.Approximately(offset, 0f); } }
}
