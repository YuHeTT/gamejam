using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 升降梯：与 <see cref="doorwithtrigger"/> 原理相同、触发条件相反——<br/>
/// 玩家踩上 triggerfloors 时降下来（closedPosition），离开后升上去（openedPosition）。<br/>
/// 勾选 useLoopMode 后无视触发机关：自动在两端之间循环往复，并在每端停留 loopEndPause 秒。<br/>
/// 勾选 useHorizontalMove 后改为左右移动（沿 X 平移 moveDistance，方向由 horizontalMoveToLeft 决定），而不是上下；
/// 水平模式下不做压扁（压扁/限位都是按竖直方向设计的），只做平移。<br/><br/>
/// 新增机制：下降途中压到箱子（标签 box）时，箱子会**随着下降逐渐压扁**——
/// 压扁进度由升降梯压入箱子的深度决定，升降梯是匀速下降的，
/// 所以压扁推进速度天然与下降速度相关（想更快就调小 squashHeight）。<br/>
/// 压到 squashHeight 米时压扁完成（长 ×2、高 ×0.5），之后升降梯停在箱子顶面上。<br/>
/// 压扁会让箱子变宽，若因此挤到墙，会把箱子沿水平方向挪到不撞墙的位置。<br/>
/// 障碍判定**不依赖图层**：带 Dynamic 刚体的都不算墙，所以箱子自己、升降梯、
/// 地面上的动态物体都不会被误判（箱子在 Ground 层也照样工作）。
/// </summary>
public class updown : MonoBehaviour
{
    [Header("触发")]
    public triggerfloor[] triggerfloors;

    [Header("运动参数")]
    public float moveDistance = 5f;
    public float moveSpeed = 12f;

    [Header("循环模式（可选）")]
    [Tooltip("勾选后无视触发机关，自动在两个端点之间循环往复移动")]
    public bool useLoopMode = false;
    [Tooltip("循环模式下到达任意一端后的停留时间（秒）")]
    public float loopEndPause = 1f;

    [Header("移动方向（可选）")]
    [Tooltip("勾选后改为左右移动（沿 X 平移 moveDistance），而不是上下移动；水平模式下不做压扁")]
    public bool useHorizontalMove = false;
    [Tooltip("仅水平模式生效：勾选=向左（-X），不勾选=向右（+X）")]
    public bool horizontalMoveToLeft = false;

    [Header("压扁箱子")]
    [Tooltip("压扁后的横向倍率（长 ×2）")]
    public float squashWidthScale = 2f;
    [Tooltip("压扁后的纵向倍率（高 ×0.5）")]
    public float squashHeightScale = 0.5f;
    [Tooltip("升降梯底面进入箱子顶部多少米内算“压到”，开始渐进压扁")]
    public float crushThreshold = 0.05f;
    [Tooltip("压扁完成所需的压入深度（米）：越小压得越快。升降梯速度为 moveSpeed，所以压扁过程约持续 moveSpeed/squashHeight 秒")]
    public float squashHeight = 0.6f;
    [Tooltip("箱子被挪开墙边时，最多允许的水平位移（米）")]
    public float maxShift = 1.5f;
    [Tooltip("箱子所在层的掩码。不确定就保持 Everything")]
    public LayerMask boxMask = ~0;
    [Tooltip("可选：额外的墙体层掩码。保持 Nothing 即可，脚本会按刚体类型自动把静态碰撞体当墙")]
    public LayerMask wallMask = 0;
    [Tooltip("升降梯停在箱子顶面之上留的间隙，避免抖动")]
    public float surfaceOffset = 0.02f;

    private Vector3 closedPosition;
    private Vector3 openedPosition;
    private Vector3 targetPosition;

    // 升降梯底面的局部偏移（相对 transform 原点），用于把"底面"对齐到箱子顶面
    private Collider2D _elevatorCollider;
    private float _bottomOffset;
    private float _elevatorHalfWidth = 0.5f;
    private bool _bottomOffsetReady;

    /// <summary>正在被压的箱子（渐进压扁需要跨帧保存原始数据）</summary>
    private class SquashHandle
    {
        public BoxCollider2D box;
        public Vector2 originalSize;        // 碰撞体在本地空间的尺寸
        public Vector2 originalOffset;      // 碰撞体本地偏移
        public Vector3 originalScale;       // 原始 Transform 缩放（压扁就是按比例改它）
        public float anchorBottomY;         // 压扁时保持不动的底边世界 Y
        public float originalTopY;          // 接触瞬间的顶面世界 Y，用来算压入深度
        public float ratio;                 // 当前压扁进度 0~1（只增不减）
    }

    private readonly List<SquashHandle> _squashHandles = new List<SquashHandle>();

    // 复用的查询容器，避免每帧分配
    // 注意：ContactFilter2D 是结构体，字段不能声明为 readonly，否则无法在 Awake 里赋值
    private readonly Collider2D[] _boxBuffer = new Collider2D[8];
    private readonly Collider2D[] _wallBuffer = new Collider2D[8];
    private ContactFilter2D _boxFilter;
    private ContactFilter2D _wallFilter;

    // 完全压扁后，升降梯能到达的最低 Y
    private float _descentLimitY = float.NegativeInfinity;

    private bool canOpen = false;
    private bool _descending;        // 目标在下方（打算下降）
    private bool _pressingDown;      // 正在向下压（含被箱子顶住、实际位移为 0 的情况）

    // 循环模式：是否朝 openedPosition 走
    private bool _loopGoingUp = true;
    // 循环模式：端点停留剩余时间
    private float _loopPauseTimer;

    private void Awake()
    {
        _elevatorCollider = GetComponent<Collider2D>();
        if (_elevatorCollider != null)
            _elevatorHalfWidth = _elevatorCollider.bounds.extents.x;

        if (_elevatorCollider == null)
            Debug.LogError("updown: 本物体没有 Collider2D，无法计算底面位置，探测盒会失效。", this);

        _boxFilter = new ContactFilter2D();
        _boxFilter.useTriggers = false;
        _boxFilter.useLayerMask = true;
        _boxFilter.layerMask = boxMask;

        _wallFilter = new ContactFilter2D();
        _wallFilter.useTriggers = false;
        _wallFilter.useLayerMask = wallMask.value != 0;
        if (wallMask.value != 0)
            _wallFilter.layerMask = wallMask;

        if (boxMask.value == 0)
            Debug.LogWarning("updown: boxMask 是 Nothing，将无法识别箱子。", this);
    }

    private void Start()
    {
        closedPosition = transform.position;
        // 默认向上移动 moveDistance 米；开启水平模式后改为向左或向右移动
        Vector3 moveAxis = useHorizontalMove
            ? (horizontalMoveToLeft ? Vector3.left : Vector3.right)
            : Vector3.up;
        openedPosition = closedPosition + moveAxis * moveDistance;
        targetPosition = closedPosition;
        _bottomOffset = FindElevatorBottomOffset();
        _bottomOffsetReady = true;
    }

    private void Update()
    {
        if (useLoopMode)
        {
            // 1a) 循环模式：无视触发机关，自动往复（端点停留 loopEndPause 秒）
            UpdateLoopTarget();
        }
        else
        {
            // 1b) 触发判定（与门相反：踩上去才降）
            canOpen = true;
            for (int i = 0; i < triggerfloors.Length; i++)
            {
                if (triggerfloors[i] != null && triggerfloors[i].isPlayerOnFloor)
                {
                    canOpen = false;
                    break;
                }
            }

            if (canOpen)
                targetPosition = openedPosition;
            else
                targetPosition = closedPosition;
        }

        // 2) 本帧想往哪走（先算出来，压扁推进要用它判断"是否在往下压"）
        Vector3 next = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime);

        // 2a) 水平模式：只做左右平移。压扁/限位都是按竖直方向设计的，这里直接跳过
        if (useHorizontalMove)
        {
            transform.position = next;
            Physics2D.SyncTransforms();
            return;
        }

        // 3) 判定下降意图。这里**必须用目标位置**，不能用"上一帧有没有实际位移"：
        //    被箱子顶面限位时实际位移为 0，用位移判断会让下一帧直接跳过压扁推进，压扁就永远停在起点。
        _descending = targetPosition.y < transform.position.y - 0.0001f;
        _pressingDown = _descending || next.y < transform.position.y - 0.0001f;

        // 4) 先提交位移（允许本帧压进箱子一点），再探测推进压扁、最后用箱子新顶面限位。
        //    只有让升降梯真正压进去，压入深度才会增长，压扁才会逐帧推进。
        transform.position = next;
        Physics2D.SyncTransforms();

        if (_pressingDown)
        {
            ProbeAndSquash();                              // 内部会更新 _descentLimitY
        }
        else
        {
            _descentLimitY = float.NegativeInfinity;       // 上升或静止：解除停靠限制
        }

        // 5) 用箱子"压扁后的新顶面"限位，防止穿透。下一帧从"贴着箱面"开始，压扁继续推进
        if (_pressingDown && _descentLimitY > float.NegativeInfinity)
        {
            Vector3 clamped = transform.position;
            if (clamped.y < _descentLimitY)
            {
                clamped.y = _descentLimitY;
                transform.position = clamped;
            }
        }
    }

    /// <summary>
    /// 循环模式：在 closedPosition / openedPosition 两端之间往复。<br/>
    /// 到达任意一端后停留 loopEndPause 秒再折返；停留期间保持停在端点不动。
    /// </summary>
    private void UpdateLoopTarget()
    {
        if (_loopPauseTimer > 0f)
        {
            _loopPauseTimer -= Time.deltaTime;
            return;     //停留中：保持刚到达的那一端
        }

        targetPosition = _loopGoingUp ? openedPosition : closedPosition;

        //到达端点 → 开始停留，并记录下一段的方向
        if (Vector3.Distance(transform.position, targetPosition) <= 0.001f)
        {
            _loopPauseTimer = Mathf.Max(0f, loopEndPause);
            _loopGoingUp = !_loopGoingUp;
        }
    }

    /// <summary>
    /// 探测升降梯底面下方的箱子并推进压扁。<br/>
    /// 用重叠盒而不是物理碰撞回调，因为升降梯是 Kinematic + transform 位移，碰撞回调不可靠。
    /// </summary>
    private void ProbeAndSquash()
    {
        float bottom = GetElevatorBottomY();

        // 本帧能到达的最低 Y（只有完全压扁后才需要限位）
        float limitY = float.NegativeInfinity;

        // ---------- 第一步：已登记、还在升降梯下方的箱子，每帧无条件继续推进 ----------
        // 不能要求它每帧都重新出现在那个很薄的探测盒里：箱子是 dynamic 的，
        // 被升降梯推一下就会掉出薄盒，压扁就会停在那里（这是之前"建了 handle 却不动"的原因）。
        for (int i = 0; i < _squashHandles.Count; i++)
        {
            SquashHandle handle = _squashHandles[i];
            if (handle == null || handle.box == null) continue;

            // 已经被升降梯越过去了（例如被挤到旁边），不再继续压
            if (handle.box.bounds.max.y < bottom - squashHeight - crushThreshold) continue;

            AdvanceHandle(handle, bottom, ref limitY);
        }

        // ---------- 第二步：用更宽松的探测盒找还没登记的箱子（首次接触） ----------
        float probeHeight = Mathf.Max(crushThreshold, squashHeight * 0.5f) * 2f;
        Vector2 probeCenter = new Vector2(transform.position.x, bottom - probeHeight * 0.5f);
        Vector2 probeSize = new Vector2(Mathf.Max(0.05f, _elevatorHalfWidth * 2f), probeHeight);

        int count = Physics2D.OverlapBox(probeCenter, probeSize, 0f, _boxFilter, _boxBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _boxBuffer[i];
            if (!IsBox(col)) continue;

            BoxCollider2D box = col as BoxCollider2D;
            if (box == null) continue;
            if (HasHandle(box)) continue;               // 已在第一步处理过

            // 箱子顶面必须已经贴到升降梯底面附近，才算"压到"
            float boxTop = col.bounds.max.y;
            if (bottom - boxTop > crushThreshold) continue;

            SquashHandle handle = GetOrCreateHandle(box);
            if (handle == null) continue;

            AdvanceHandle(handle, bottom, ref limitY);
        }

        _descentLimitY = (limitY > float.NegativeInfinity)
            ? limitY + surfaceOffset - _bottomOffset
            : float.NegativeInfinity;
    }

    /// <summary>推进单个箱子的压扁进度，并在完全压扁后提供限位高度</summary>
    private void AdvanceHandle(SquashHandle handle, float bottom, ref float limitY)
    {
        BoxCollider2D box = handle.box;
        if (box == null) return;

        // 由"升降梯底面压进箱子多少"直接决定箱子该被压成什么样：
        // 底面推着箱子顶面往下走，压扁进度自然随下降增长。
        // 关键：**不能用"箱子当前顶面"给升降梯限位**——那会让压入深度无法增长，
        // 压扁推进和限位互相锁死（实测就是卡在 ratio≈0.3 再也不动）。
        float depth = handle.originalTopY - bottom;
        float targetRatio = Mathf.Clamp01(depth / Mathf.Max(0.01f, squashHeight));

        // 只增不减：箱子一旦被压就不会弹回来
        handle.ratio = Mathf.Max(handle.ratio, targetRatio);
        if (handle.ratio <= 0f) return;

        ApplySquash(handle);

        // 只有"完全压扁"之后才需要给升降梯限位，让它停在压扁的箱子顶面上。
        // ratio<1 期间不限位，否则压扁无法继续推进。
        // 限位前提：箱子仍在升降梯正下方——箱子被挤开/移走后就不能再挡着，
        // 否则升降梯会永远停在压扁时的高度，回不到最底部（closedPosition）。
        if (handle.ratio >= 1f && IsUnderElevator(box))
        {
            float currentTop = box.bounds.max.y;
            if (currentTop > limitY) limitY = currentTop;
        }
    }

    /// <summary>
    /// 箱子是否仍在升降梯正下方（水平方向有重叠）。<br/>
    /// 箱子被移出升降梯下方后不再限位，升降梯才能继续下压到原本的最底部。
    /// </summary>
    private bool IsUnderElevator(BoxCollider2D box)
    {
        if (box == null || _elevatorCollider == null) return false;

        Bounds elevator = _elevatorCollider.bounds;
        Bounds boxBounds = box.bounds;

        return boxBounds.min.x < elevator.max.x && boxBounds.max.x > elevator.min.x;
    }

    /// <summary>按 handle.ratio 更新箱子的缩放与位置（底边固定）</summary>
    private void ApplySquash(SquashHandle handle)
    {
        BoxCollider2D box = handle.box;
        if (box == null) return;

        Transform t = box.transform;
        float r = Mathf.Clamp01(handle.ratio);
        float widthScale = Mathf.Lerp(1f, squashWidthScale, r);
        float heightScale = Mathf.Lerp(1f, squashHeightScale, r);

        // 关键：改 **Transform 缩放** 而不是碰撞体 size。
        // 只改 size 时碰撞体确实扁了，但 SpriteRenderer 不会跟着变，所以"碰撞体压扁了、形状没变"。
        // 缩放 Transform 会同时带动贴图和碰撞体，比例天然一致。
        Vector3 newScale = new Vector3(
            handle.originalScale.x * widthScale,
            handle.originalScale.y * heightScale,
            handle.originalScale.z);

        t.localScale = newScale;

        // 底边固定：由世界空间的底边反推 transform 的位置
        // （碰撞体在本地空间尺寸不变，世界高度 = 本地高度 × 缩放）
        float worldHeight = Mathf.Abs(handle.originalSize.y * newScale.y);
        float centerOffsetY = handle.originalOffset.y * newScale.y;

        Vector3 pos = t.position;
        pos.y = handle.anchorBottomY + worldHeight * 0.5f + centerOffsetY;

        // 变宽后可能挤到墙：沿水平方向挪开
        if (r > 0.01f)
            pos = ResolveHorizontal(box, pos, handle.originalSize, newScale, centerOffsetY, handle.anchorBottomY);

        t.position = pos;
        Physics2D.SyncTransforms();     // 让后面的 bounds 立即反映新尺寸
    }

    /// <summary>
    /// 在底边、竖直位置不变的前提下，给箱子找一个不撞墙的水平位置。<br/>
    /// 优先保持在原位；被挡就往两侧逐渐挪（限制在 maxShift 内），再不行挪到升降梯正下方。
    /// </summary>
    private Vector2 ResolveHorizontal(BoxCollider2D box, Vector3 basePos, Vector2 localSize,
                                      Vector3 scale, float centerOffsetY, float anchorBottomY)
    {
        float worldHeight = Mathf.Abs(localSize.y * scale.y);
        float centerY = anchorBottomY + worldHeight * 0.5f + centerOffsetY;
        float baseCenterX = basePos.x + box.offset.x * scale.x;

        if (IsAreaClear(box, baseCenterX, centerY, localSize, scale))
            return basePos;

        float[] candidates =
        {
            -maxShift * 0.25f, maxShift * 0.25f,
            -maxShift * 0.5f,  maxShift * 0.5f,
            -maxShift * 0.75f, maxShift * 0.75f,
            -maxShift,         maxShift,
            transform.position.x - baseCenterX        // 兜底：挪到升降梯正下方
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            float testCenterX = baseCenterX + candidates[i];
            if (!IsAreaClear(box, testCenterX, centerY, localSize, scale)) continue;

            return new Vector2(testCenterX - box.offset.x * scale.x, basePos.y);
        }

        // 实在挪不开：保持原位（宁可和墙重叠一点，也不要瞬移出关卡）
        return basePos;
    }

    /// <summary>该位置的箱子是否不与静态障碍（墙/地面）重叠。玩家、其他动态箱子都不算障碍</summary>
    private bool IsAreaClear(BoxCollider2D box, float centerX, float centerY, Vector2 localSize, Vector3 scale)
    {
        Vector2 worldSize = new Vector2(
            Mathf.Abs(localSize.x * scale.x),
            Mathf.Abs(localSize.y * scale.y));

        int count = Physics2D.OverlapBox(new Vector2(centerX, centerY), worldSize, 0f, _wallFilter, _wallBuffer);

        for (int i = 0; i < count; i++)
        {
            if (IsStaticObstacle(_wallBuffer[i], box)) return false;
        }

        return true;
    }

    /// <summary>
    /// 是否是静态障碍（墙、地面）。<br/>
    /// 关键：**带 Dynamic 刚体的一律不算**——箱子自己、其他箱子、站在箱上的玩家、
    /// 升降梯都会被排除。否则箱子压扁变宽后与玩家/地面重叠，会被反复判定"撞墙"，
    /// 表现为箱子每帧被推来推去（抖动或乱滑）。
    /// </summary>
    private bool IsStaticObstacle(Collider2D col, BoxCollider2D selfBox)
    {
        if (col == null || selfBox == null) return false;
        if (col == selfBox) return false;                        // 自己
        if (col == _elevatorCollider) return false;              // 升降梯
        if (col.gameObject == selfBox.gameObject) return false;  // 同物体上的其他碰撞体
        if (col.transform.IsChildOf(selfBox.transform)) return false;
        if (selfBox.transform.IsChildOf(col.transform)) return false;

        Rigidbody2D rb = col.attachedRigidbody;
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) return false;

        return true;
    }

    private SquashHandle GetOrCreateHandle(BoxCollider2D box)
    {
        for (int i = 0; i < _squashHandles.Count; i++)
        {
            if (_squashHandles[i].box == box) return _squashHandles[i];
        }

        SquashHandle handle = new SquashHandle();
        handle.box = box;
        handle.originalSize = box.size;
        handle.originalOffset = box.offset;
        handle.originalScale = box.transform.localScale;
        handle.originalTopY = box.bounds.max.y;
        handle.anchorBottomY = box.bounds.min.y;
        handle.ratio = 0f;

        _squashHandles.Add(handle);
        return handle;
    }

    /// <summary>升降梯底面当前的世界 Y</summary>
    private float GetElevatorBottomY()
    {
        if (!_bottomOffsetReady)
        {
            _bottomOffset = FindElevatorBottomOffset();
            _bottomOffsetReady = true;
        }

        return transform.position.y + _bottomOffset;
    }

    /// <summary>
    /// 箱子判定：**只认 box 标签**（按用户要求，后续压死玩家也用标签区分）。<br/>
    /// 不要求 Dynamic 刚体——静态/运动学的箱子同样能被压扁。<br/>
    /// 容错：若碰撞体自身没打标签（标签在父物体上的预制体结构），往父级找一次。
    /// </summary>
    private bool IsBox(Collider2D col)
    {
        if (col == null) return false;
        if (col.CompareTag("box")) return true;

        Transform parent = col.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag("box")) return true;
            parent = parent.parent;
        }

        return false;
    }

    /// <summary>已经在压扁中的箱子</summary>
    private bool HasHandle(BoxCollider2D box)
    {
        for (int i = 0; i < _squashHandles.Count; i++)
        {
            if (_squashHandles[i] != null && _squashHandles[i].box == box) return true;
        }

        return false;
    }

    private float FindElevatorBottomOffset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return 0f;

        return col.bounds.min.y - transform.position.y;
    }

    private void OnDrawGizmosSelected()
    {
        // 把探测区画出来，方便在 Scene 视图确认它正好在升降梯底面下方
        float bottom = GetElevatorBottomY();
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 1f);
        Gizmos.DrawWireCube(
            new Vector3(transform.position.x, bottom - crushThreshold * 0.5f, 0f),
            new Vector3(Mathf.Max(0.05f, _elevatorHalfWidth * 2f), crushThreshold, 0.01f));
    }
}
