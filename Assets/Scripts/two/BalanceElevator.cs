using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 天平电梯：一根绳跨过滑轮，两端各挂一个平台，依据两端总质量的差值升降。<br/>
/// 绳长固定 ⇒ 两平台位移永远大小相等、方向相反，因此内部只维护一个自由度 offset：
///   左平台 y = 左平台原始 y + offset
///   右平台 y = 右平台原始 y - offset
/// offset = 0 即水平平衡位，也是"两边等重"时自动回到的位置。<br/>
/// 符号：offset 取正 = 左平台上升、右平台下降；左边更重时应取负（左降右升）。<br/><br/>
/// <b>时间轴（关键）</b>：平台在 <c>FixedUpdate</c> 里用 <c>Time.fixedDeltaTime</c> 移动，
/// 不再在 <c>Update</c>（渲染帧）里移动。原因：平台是 Kinematic、靠 transform 位移，
/// 乘客（动态刚体）只在物理步里被"去穿透"推动。若平台按渲染帧走而物理按固定步长跑，
/// 两者之间就会出现"一帧偏斜"：
/// <list type="bullet">
/// <item>配重传感器读到的是"已挪动后的平台面"配"上一物理步的乘客位置"，实测间隙比真实掉队量大一个渲染帧的位移
/// （moveSpeed=4 时 0.163 变 0.230，30fps 变 0.296），于是乘客被判成浮空 → 读数抖动 → 天平来回震；</item>
/// <item>乘客的落地射线也会因此周期性落空。</item>
/// </list>
/// 放到 <c>FixedUpdate</c> 后，平台每物理步只移动 <c>moveSpeed × fixedDeltaTime</c>，
/// 与乘客的积分在同一时间轴上，偏斜消失。<br/><br/>
/// 配合 <see cref="DefaultExecutionOrder"/>：本脚本设为 <b>100</b>（靠后），
/// <see cref="PlatformSensor"/> 设为 <b>-100</b>（靠前）。于是每个物理步的顺序是：
/// 传感器先读（此时平台的 transform 还是上一步挪到的位置，正好是上一步物理模拟所用的一致状态）
/// → 再算目标、移动平台 → 物理模拟。这样传感器测到的平台面与乘客位置永远出自同一个物理步。
/// </summary>
[DefaultExecutionOrder(100)]
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

    [Header("读数容错")]
    [Tooltip("质量读数的指数平滑时间（秒）")]
    public float massSmoothingTime = 0.12f;
    [Tooltip("左右质量差小于此值时视为相等，并立即回到水平位")]
    public float massDeadZone = 0.08f;
    [Tooltip("改变升降方向前需要稳定超过死区的时间（秒）")]
    public float directionSwitchDelay = 0.08f;

    // offset：左平台相对水平平衡位的竖直偏移，正 = 左高右低（即左轻右重）
    private float offset;

    // 上一次有效读数的方向（-1 = 左重、+1 = 右重）
    private float _lastDir;
    private float _directionTimer;

    // 两个平台上表面的原始高度（水平平衡位）
    private float leftBaseY;
    private float rightBaseY;

    // 当前两侧总质量
    private float leftMass;
    private float rightMass;
    private float _filteredLeftMass;
    private float _filteredRightMass;

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

        // 初始摆位
        MovePlatforms();
    }

    /// <summary>
    /// 运动与配重判定都走物理步：与乘客的积分同一时间轴，也和 PlatformSensor 的执行顺序配套。
    /// </summary>
    private void FixedUpdate()
    {
        // 1) 读取两端总质量（传感器在本步更早执行，两侧都读的是同一个物理步的一致状态）
        leftMass  = (leftSensor  != null) ? leftSensor.Mass  : 0f;
        rightMass = (rightSensor != null) ? rightSensor.Mass : 0f;

        // 2) 先平滑质量，再计算方向。传感器本身也有接触滞后，
        //    这里再做一层轻量平滑，防止平台高速移动时方向在一两个物理帧内反复翻转。
        float blend = 1f - Mathf.Exp(-Time.fixedDeltaTime / Mathf.Max(0.001f, massSmoothingTime));
        _filteredLeftMass = Mathf.Lerp(_filteredLeftMass, leftMass, blend);
        _filteredRightMass = Mathf.Lerp(_filteredRightMass, rightMass, blend);

        // 左重时左端下降（offset 为负），右重时右端下降（offset 为正）。
        float netWeight = _filteredLeftMass - _filteredRightMass;
        if (Mathf.Abs(netWeight) <= Mathf.Max(0f, massDeadZone))
        {
            // 等重必须清除旧方向，目标回到 0，而不是继续沿旧方向走满行程。
            _lastDir = 0f;
            _directionTimer = 0f;
        }
        else
        {
            float desiredDir = netWeight > 0f ? -1f : 1f;
            if (!Mathf.Approximately(desiredDir, _lastDir))
            {
                _directionTimer += Time.fixedDeltaTime;
                if (_directionTimer >= Mathf.Max(0f, directionSwitchDelay))
                {
                    _lastDir = desiredDir;
                    _directionTimer = 0f;
                }
            }
            else
            {
                _directionTimer = 0f;
            }
        }

        float targetOffset = _lastDir * maxTravel;

        // 3) 严格匀速逼近目标：每物理步位移恒定，且不会过冲（碰到地面自然停住）
        offset = Mathf.MoveTowards(offset, targetOffset, moveSpeed * Time.fixedDeltaTime);

        // 兜底限位，防止 Inspector 参数填错导致平台跑出场景
        offset = Mathf.Clamp(offset, -maxTravel, maxTravel);

        // 4) 落到两个平台的 transform
        MovePlatforms();
    }

    /// <summary>绳子是纯视觉，按渲染帧刷新，避免看起来和平台脱节</summary>
    private void Update()
    {
        UpdateRope();
    }

    /// <summary>由绳长守恒同时写两个平台的位置</summary>
    private void MovePlatforms()
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

        // 本工程 Physics2D.autoSyncTransforms 是关闭的（ProjectSettings/Physics2DSettings），
        // 移动后立刻同步，让紧随其后的物理步就用上新位置。
        // 现在每物理步只同步一次（原来每渲染帧一次，开销也更低）。
        Physics2D.SyncTransforms();
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

    /// <summary>
    /// 两侧是否已配平：质量差落在 <see cref="massDeadZone"/> 内，
    /// 且天平已经停回水平位（offset 归零）。<br/><br/>
    /// 这是给外部（例如 <c>BalanceVictoryTrigger</c>）用的只读信号，
    /// 不参与也不改变本脚本的任何原有行为。
    /// </summary>
    public bool IsBalanced
    {
        get
        {
            return Mathf.Abs(_filteredLeftMass - _filteredRightMass) <= Mathf.Max(0f, massDeadZone)
                   && Mathf.Abs(offset) <= 0.02f;
        }
    }

    /// <summary>两侧的质量差（左 − 右，已平滑）</summary>
    public float WeightDifference { get { return _filteredLeftMass - _filteredRightMass; } }
}
