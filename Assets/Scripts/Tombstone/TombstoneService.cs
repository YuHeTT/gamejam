using System.Collections.Generic;
using UnityEngine;

/// <summary>全局唯一墓碑：放置、替换、查询传送点。场景中放一个并指定 prefab（含 TombstoneMarker）。<br/>
/// 附带两个 Inspector 开关：是否与玩家碰撞（实体/地面）、是否允许空中放置（悬停 + 粘附移动地面）。</summary>
public class TombstoneService : MonoBehaviour
{
    public static TombstoneService Instance { get; private set; }

    [Header("Prefab")]
    [Tooltip("须带 TombstoneMarker；美术由你在编辑器指定")]
    public GameObject tombstonePrefab;

    [Header("Placement")]
    [Tooltip("相对地面命中点的额外偏移（例如让碑底贴地）")]
    public Vector2 placementOffset = Vector2.zero;

    [Header("玩家碰撞")]
    [Tooltip("勾选：墓碑视为实体/地面，会挡住玩家且玩家可站在其上起跳（放到 Ground 层）；取消：玩家可穿过墓碑。")]
    public bool collideWithPlayer = false;

    [Header("空中放置")]
    [Tooltip("勾选：允许在空中放置墓碑。空中放置的墓碑会悬停在放置点；当有移动的地面碰到它时粘附到该平台并随之移动。")]
    public bool allowAirPlacement = false;
    [Tooltip("哪些层的物体算作「可粘附的移动地面」。留空(Nothing)时按名称解析为 Ground 层。")]
    public LayerMask movingGroundMask;

    public bool HasActiveTombstone => activeMarker != null;
    public TombstoneMarker ActiveMarker => activeMarker;

    private TombstoneMarker activeMarker;
    private GameObject activeInstance;

    #region 悬停 / 粘附状态
    //当前墓碑是否处于空中悬停
    private bool hovering;
    //是否已粘附到移动平台
    private bool attached;
    //悬停/粘附期间用于驱动位置的刚体
    private Rigidbody2D hoverRb;
    //粘附平台的 Transform
    private Transform attachAnchor;
    //墓碑在平台本地空间的偏移
    private Vector3 attachLocalOffset;

    //解析后的「移动地面」层掩码与查询过滤器
    private int movingMask;
    private ContactFilter2D movingFilter;

    //重叠查询结果缓存与上一物理帧的位置采样（用于判断"是否在移动"）
    private readonly Collider2D[] overlapBuffer = new Collider2D[16];
    private readonly Dictionary<Collider2D, Vector2> lastCenters = new Dictionary<Collider2D, Vector2>();
    private readonly List<Collider2D> staleKeys = new List<Collider2D>();

    //位移超过该平方阈值才认为"在移动"（约 0.001 米）
    private const float MoveEpsilonSqr = 1e-6f;
    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("场景中存在多个 TombstoneService，销毁重复项。", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ResolveMovingMask();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>解析「可粘附的移动地面」层掩码：优先用 Inspector 值，留空时按层名称取 Ground。</summary>
    private void ResolveMovingMask()
    {
        if (movingGroundMask.value != 0)
        {
            movingMask = movingGroundMask.value;
        }
        else
        {
            int ground = LayerMask.NameToLayer("Ground");
            movingMask = ground >= 0 ? 1 << ground : 0;
        }

        if (movingMask == 0)
        {
            Debug.LogWarning(
                "TombstoneService: 未指定 movingGroundMask，且找不到名为 'Ground' 的层，空中墓碑的粘附功能已关闭。", this);
        }

        movingFilter = new ContactFilter2D { useTriggers = false };
        movingFilter.SetLayerMask(movingMask);
    }

    /// <summary>在玩家脚下地面放置墓碑；若已有则销毁旧的。<br/>
    /// 玩家不在地面时：未开启空中放置则无效；开启则把墓碑悬停在玩家脚底高度。</summary>
    public bool TryPlaceAtPlayerFeet(Player player)
    {
        if (tombstonePrefab == null)
        {
            Debug.LogWarning("[墓] TombstoneService 未指定 tombstonePrefab。", this);
            return false;
        }

        if (player == null)
            return false;

        bool grounded = player.IsGroundDetected();

        if (!grounded && !allowAirPlacement)
        {
            Debug.Log("[墓] 空中放置未开启且玩家不在地面，放置无效。");
            return false;
        }

        Vector2 pos;

        if (grounded)
        {
            if (!player.TryGetGroundHit(out RaycastHit2D hit, ignoreTombstones: true)
                && !player.TryGetGroundHit(out hit))
            {
                hit = default;
            }

            pos = hit.collider != null
                ? hit.point + placementOffset
                : (Vector2)player.GroundCheck.position + placementOffset;
        }
        else
        {
            //空中放置：悬停在玩家脚底当前高度
            pos = (Vector2)player.GroundCheck.position + placementOffset;
        }

        ReplaceTombstone(pos, hover: !grounded);
        return true;
    }

    public Vector2 GetTeleportFeetPosition()
    {
        return activeMarker != null ? activeMarker.FeetWorldPosition : Vector2.zero;
    }

    private void ReplaceTombstone(Vector2 worldPosition, bool hover)
    {
        ClearHoverState();

        if (activeInstance != null)
            DestroyImmediate(activeInstance);

        activeMarker = null;
        activeInstance = Instantiate(tombstonePrefab, worldPosition, Quaternion.identity);
        activeMarker = activeInstance.GetComponent<TombstoneMarker>();
        if (activeMarker == null)
            activeMarker = activeInstance.AddComponent<TombstoneMarker>();

        int tombLayer = LayerMask.NameToLayer("Tombstone");
        if (tombLayer >= 0)
            SetLayerRecursively(activeInstance, tombLayer);

        //开启「与玩家碰撞」时把墓碑当作实体/地面：改到 Ground 层。
        //Ground↔Player 在碰撞矩阵中是开启的，玩家会被墓碑挡住、可站在其上并正常起跳
        //（玩家的地面射线只识别 Ground 层，放在 Tombstone 层则无法被判定为着地）。
        if (collideWithPlayer)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                SetLayerRecursively(activeInstance, groundLayer);
            else
                Debug.LogWarning("TombstoneService: 找不到名为 'Ground' 的层，无法让墓碑与玩家碰撞。", this);
        }

        if (hover)
            BeginHover();
    }

    #region 悬停与粘附移动地面
    /// <summary>切换为空中悬停：Kinematic + 零重力，停在放置点不下落</summary>
    private void BeginHover()
    {
        hoverRb = activeInstance.GetComponent<Rigidbody2D>();
        if (hoverRb == null)
            hoverRb = activeInstance.AddComponent<Rigidbody2D>();

        hoverRb.bodyType     = RigidbodyType2D.Kinematic;
        hoverRb.gravityScale = 0f;
        hoverRb.velocity     = Vector2.zero;

        hovering = true;
        attached = false;
        lastCenters.Clear();
    }

    private void FixedUpdate()
    {
        if (!hovering) return;

        if (activeInstance == null || hoverRb == null)
        {
            ClearHoverState();
            return;
        }

        if (attached)
        {
            //平台被销毁 → 退化为原地悬停
            if (attachAnchor == null)
            {
                attached = false;
                return;
            }

            hoverRb.position = attachAnchor.TransformPoint(attachLocalOffset);
            return;
        }

        TryAttachToMovingGround();
    }

    /// <summary>用重叠检测找出「正在移动的地面」并粘附。<br/>
    /// 不依赖碰撞回调：本项目里的移动平台都是用 transform.position 逐帧平移的（Kinematic 或干脆无刚体），
    /// 这类物体之间不会产生 OnCollision/OnTrigger 事件。</summary>
    private void TryAttachToMovingGround()
    {
        if (movingMask == 0) return;

        Collider2D self = activeInstance.GetComponent<Collider2D>();
        if (self == null) return;

        Bounds b = self.bounds;
        int count = Physics2D.OverlapBox(b.center, b.size, 0f, movingFilter, overlapBuffer);

        //第一遍：只读取上一物理帧的采样，判断谁在移动
        for (int i = 0; i < count; i++)
        {
            Collider2D c = overlapBuffer[i];
            if (!IsValidCandidate(c, self)) continue;

            Vector2 center = c.bounds.center;
            if (lastCenters.TryGetValue(c, out Vector2 prev)
                && (center - prev).sqrMagnitude > MoveEpsilonSqr)
            {
                AttachTo(c);
                return;
            }
        }

        //第二遍：写回本帧采样，并清理已销毁的键
        for (int i = 0; i < count; i++)
        {
            Collider2D c = overlapBuffer[i];
            if (!IsValidCandidate(c, self)) continue;
            lastCenters[c] = c.bounds.center;
        }

        staleKeys.Clear();
        foreach (KeyValuePair<Collider2D, Vector2> pair in lastCenters)
        {
            if (pair.Key == null)
                staleKeys.Add(pair.Key);
        }
        for (int i = 0; i < staleKeys.Count; i++)
            lastCenters.Remove(staleKeys[i]);
    }

    /// <summary>候选：不是墓碑自己、也不是墓碑的子物体</summary>
    private bool IsValidCandidate(Collider2D c, Collider2D self)
    {
        if (c == null) return false;
        if (c == self) return false;
        if (c.transform.IsChildOf(activeInstance.transform)) return false;
        return true;
    }

    /// <summary>粘附到命中的移动地面：记录平台与本地偏移，之后每物理帧跟随平台位置</summary>
    private void AttachTo(Collider2D c)
    {
        Transform platform = c.attachedRigidbody != null ? c.attachedRigidbody.transform : c.transform;

        attachAnchor      = platform;
        attachLocalOffset = platform.InverseTransformPoint(hoverRb.position);
        attached          = true;
        lastCenters.Clear();

        Debug.Log($"[墓] 墓碑已粘附到移动地面：{platform.name}");
    }

    private void ClearHoverState()
    {
        hovering     = false;
        attached     = false;
        hoverRb      = null;
        attachAnchor = null;
        lastCenters.Clear();
    }
    #endregion

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}