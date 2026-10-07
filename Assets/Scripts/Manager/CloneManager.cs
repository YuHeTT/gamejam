using System.Collections.Generic;
using UnityEngine;

/// <summary>「募」的复制体管理：生成、数量上限、连接开关。<br/>
/// 复制体以玩家为模板在运行时拼装（无需预制体），并用 Physics2D.IgnoreCollision<br/>
/// 让复制体与玩家、复制体之间互不碰撞，与地面仍然碰撞。</summary>
public class CloneManager : MonoBehaviour
{
    [Header("CloneInfo")]
    [Tooltip("场景中同时存在的复制体上限")]
    public int maxClones = 3;
    [Tooltip("玩家引用，留空则自动查找")]
    public Player player;

    private static CloneManager _instance;
    private readonly List<PlayerClone> clones = new List<PlayerClone>();
    private bool connected = true;

    /// <summary>场景实例；未手动放置时首次使用时自动创建</summary>
    public static CloneManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CloneManager>();
                if (_instance == null)
                    _instance = new GameObject(nameof(CloneManager)).AddComponent<CloneManager>();
            }
            return _instance;
        }
    }

    /// <summary>已存在的实例（不自动创建）</summary>
    public static CloneManager Existing => _instance;

    /// <summary>复制体是否与玩家保持连接（初始为「已连接」）</summary>
    public bool Connected => connected;
    public int CloneCount => clones.Count;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    #region 生成
    /// <summary>在玩家当前位置召唤一个复制体。达到上限或找不到玩家时返回 false。</summary>
    public bool TrySummon()
    {
        Player p = ResolvePlayer();
        if (p == null)
        {
            Debug.LogError("[募] 找不到 Player，无法生成复制体。");
            return false;
        }

        if (clones.Count >= maxClones)
        {
            Debug.Log($"[募] 复制体已达上限（{maxClones} 个），按 L 无效。");
            return false;
        }

        PlayerClone clone = BuildClone(p);
        if (clone == null) return false;

        clones.Add(clone);
        Debug.Log($"[募] 生成复制体 {clones.Count}/{maxClones}，连接状态：{(connected ? "已连接" : "已断开")}");
        return true;
    }

    /// <summary>以玩家为模板拼装复制体</summary>
    private PlayerClone BuildClone(Player p)
    {
        Animator srcAnim = p.anim;
        Transform srcGroundCheck = p.GroundCheck;
        if (srcAnim == null || srcGroundCheck == null)
        {
            Debug.LogError("[募] 玩家缺少 Animator 或 GroundCheck，无法生成复制体。", p);
            return null;
        }

        Transform srcVisual = srcAnim.transform;
        SpriteRenderer srcSr = srcAnim.GetComponent<SpriteRenderer>();
        Rigidbody2D srcRb = p.rb;
        BoxCollider2D srcCol = p.GetComponent<BoxCollider2D>();

        GameObject go = new GameObject("PlayerClone");
        go.layer = p.gameObject.layer;
        go.transform.SetPositionAndRotation(p.transform.position, p.transform.rotation);
        go.transform.localScale = p.transform.localScale;

        //刚体：与玩家一致
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        if (srcRb != null)
        {
            rb.bodyType               = srcRb.bodyType;
            rb.mass                   = srcRb.mass;
            rb.drag                   = srcRb.drag;
            rb.angularDrag            = srcRb.angularDrag;
            rb.gravityScale           = srcRb.gravityScale;
            rb.constraints            = srcRb.constraints;
            rb.collisionDetectionMode = srcRb.collisionDetectionMode;
            rb.interpolation          = srcRb.interpolation;
            rb.sharedMaterial         = srcRb.sharedMaterial;
        }

        //碰撞箱：与玩家一致
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        if (srcCol != null)
        {
            col.size           = srcCol.size;
            col.offset         = srcCol.offset;
            col.sharedMaterial = srcCol.sharedMaterial;
        }

        //视觉子物体：只保留 SpriteRenderer + Animator（不带玩家视觉子物体上依赖 Player 的脚本）
        GameObject visual = new GameObject(srcVisual.name);
        visual.layer = go.layer;
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = srcVisual.localPosition;
        visual.transform.localRotation = srcVisual.localRotation;
        visual.transform.localScale    = srcVisual.localScale;

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        if (srcSr != null)
        {
            sr.sprite         = srcSr.sprite;
            sr.color          = srcSr.color;
            sr.sortingLayerID = srcSr.sortingLayerID;
            sr.sortingOrder   = srcSr.sortingOrder;
            sr.sharedMaterial = srcSr.sharedMaterial;
            sr.flipX          = srcSr.flipX;
            sr.flipY          = srcSr.flipY;
        }

        Animator anim = visual.AddComponent<Animator>();
        anim.runtimeAnimatorController = srcAnim.runtimeAnimatorController;
        anim.applyRootMotion           = false;
        anim.cullingMode               = srcAnim.cullingMode;
        anim.updateMode                = srcAnim.updateMode;

        //地面检测点：位置与玩家一致
        GameObject ground = new GameObject(srcGroundCheck.name);
        ground.layer = go.layer;
        ground.transform.SetParent(go.transform, false);
        ground.transform.localPosition = srcGroundCheck.localPosition;
        ground.transform.localRotation = srcGroundCheck.localRotation;
        ground.transform.localScale    = srcGroundCheck.localScale;

        PlayerClone clone = go.AddComponent<PlayerClone>();
        clone.Initialize(ground.transform, p.GroundCheckDistance, p.GroundCheckDeviate, p.GroundMask,
                         p.moveSpeed, p.jumpSpeed, p.gravityScale, p.fallMultiplier);
        clone.facingDir   = p.facingDir;
        clone.facingRight = p.facingRight;
        clone.connected   = connected;

        //无碰撞体积：与玩家、与所有已有复制体互相忽略
        Collider2D playerCol = p.GetComponent<Collider2D>();
        if (playerCol != null) Physics2D.IgnoreCollision(col, playerCol, true);
        foreach (PlayerClone other in clones)
        {
            if (other == null) continue;
            Collider2D otherCol = other.GetComponent<Collider2D>();
            if (otherCol != null) Physics2D.IgnoreCollision(col, otherCol, true);
        }

        return clone;
    }
    #endregion

    #region 连接开关
    public void ToggleConnection() => SetConnected(!connected);

    /// <summary>设置所有复制体的连接状态（断开 = 停止镜像玩家输入，物理照常）</summary>
    public void SetConnected(bool value)
    {
        bool changed = connected != value;
        connected = value;

        foreach (PlayerClone clone in clones)
        {
            if (clone != null) clone.connected = value;
        }

        if (changed)
            Debug.Log($"[募] 复制体连接已{(value ? "启用（继续模仿）" : "断开（停止模仿）")}");
    }
    #endregion

    /// <summary>把所有已存在的复制体的动画组替换掉（玩家换装时调用）。<br/>
    /// 之后新生成的复制体会在拼装时直接继承玩家当前的动画组，无需再处理。</summary>
    public void ApplyAnimatorController(RuntimeAnimatorController controller)
    {
        for (int i = 0; i < clones.Count; i++)
        {
            PlayerClone clone = clones[i];
            if (clone == null || clone.anim == null) continue;
            if (clone.anim.runtimeAnimatorController != controller)
                clone.anim.runtimeAnimatorController = controller;
        }
    }

    private Player ResolvePlayer()
    {
        if (player == null && PlayerManager.instance != null)
            player = PlayerManager.instance.player;
        if (player == null)
            player = FindObjectOfType<Player>();
        return player;
    }
}