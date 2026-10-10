using System;
using UnityEngine;

/// <summary>一套角色动画槽位（静止/移动/跳跃/冲刺），以及该道具专属的视觉修正。<br/>
/// 由道具提供，持有该道具时运行时自动套用到角色的 Animator 上。</summary>
[Serializable]
public class ItemAnimationSet
{
    [Tooltip("静止动画")]
    public AnimationClip idle;
    [Tooltip("移动动画")]
    public AnimationClip move;
    [Tooltip("跳跃动画（跳跃状态是 yVelocity 混合树，上升与下落共用这一段）")]
    public AnimationClip jump;
    [Tooltip("冲刺动画（仅「蓦」这类有冲刺能力的道具需要填；留空沿用默认）")]
    public AnimationClip dash;

    [Header("变身后的视觉修正（本道具专属）")]
    [Tooltip("持有本道具时对角色视觉施加的缩放。道具素材大小不一，逐个道具微调以对齐体型，(1,1) 表示不缩放")]
    public Vector2 visualScale = new Vector2(2f, 2f);
    [Tooltip("持有本道具时对角色视觉追加的偏移（本地空间：x 随角色朝向自动镜像，y 上下）。" +
             "缩放后脚底/头顶没对齐时才需要调，(0,0) 表示不偏移")]
    public Vector2 visualOffset = Vector2.zero;
}

/// <summary>世界中的道具：可被拾取的物理物体，同时承载该道具的能力逻辑。<br/>
/// 组件 Rigidbody2D、Collider2D、SpriteRenderer 需与本脚本挂在同一个 GameObject 上。</summary>
public class Item : MonoBehaviour
{
    [Header("ItemInfo")]
    public string itemName = "未命名道具";
    public Sprite icon;                       // 预留：UI 用，暂不接入

    [Header("动画组（持有本道具时替换）")]
    [Tooltip("给这个道具拖入对应的 静止/移动/跳跃 动画；留空的那一项沿用角色的默认动画")]
    public ItemAnimationSet animationClips = new ItemAnimationSet();

    [Header("DropInfo")]
    public Vector2 dropOffset = Vector2.zero; // 从玩家身上放回世界时的偏移

    [Header("机关影响")]
    [Tooltip("勾选：本道具落在踏板上时能触发踏板机关（triggerfloor）")]
    public bool canTriggerPedal = false;
    [Tooltip("勾选：本道具压在天平平台上时计入配重，影响两侧平衡")]
    public bool canWeighOnBalance = false;

    protected SpriteRenderer sr;
    protected Rigidbody2D rb;
    protected Collider2D col;

    /// <summary>是否正被玩家持有</summary>
    public bool IsCarried { get; private set; }
    /// <summary>当前持有者</summary>
    public Player Holder { get; private set; }

    protected virtual void Awake()
    {
        sr  = GetComponentInChildren<SpriteRenderer>();
        rb  = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        //道具不与箱子发生物理碰撞（玩家与箱子照常碰）
        IgnoreBoxCollisions();
    }

    #region 与箱子的碰撞豁免
    /// <summary>让本道具的碰撞体与场景里所有 box 标签物体互不碰撞。<br/>
    /// 每次调用都重新扫描：场景重载（死亡重置）后缓存会失效，而道具数量很少，开销可忽略。</summary>
    private void IgnoreBoxCollisions()
    {
        if (col == null) return;

        Collider2D[] all = FindObjectsOfType<Collider2D>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Collider2D box = all[i];
            if (box == null || box == col) continue;
            if (!IsBoxTagged(box.transform)) continue;
            Physics2D.IgnoreCollision(col, box, true);
        }
    }

    /// <summary>箱子标签可能挂在碰撞体自身或它的父物体上</summary>
    private static bool IsBoxTagged(Transform t)
    {
        if (t == null) return false;
        if (t.CompareTag("box")) return true;
        return t.parent != null && t.parent.CompareTag("box");
    }
    #endregion

    #region 能力扩展点（子类在此填写具体能力）
    /// <summary>进入道具栏时触发</summary>
    public virtual void OnPickUp(Player player) { }

    /// <summary>按 I 使用能力。返回 true 表示该道具被消耗（从槽位移除并销毁）</summary>
    public virtual bool UseAbility(Player player) => false;

    /// <summary>离开道具栏时触发（交换/丢弃）</summary>
    public virtual void OnDrop(Player player, Vector2 worldPos) { }

    /// <summary>放回世界的位置，子类可重写</summary>
    public virtual Vector2 GetDropPosition(Vector2 playerPos) => playerPos + dropOffset;

    /// <summary>该道具在道具栏中的使用按键，默认 I；子类可重写成其他键（如 L）</summary>
    public virtual KeyCode useKey => KeyCode.I;

    /// <summary>持有时每帧调用，道具可在此处理自己的额外按键（如 S）</summary>
    public virtual void OnCarriedUpdate(Player player) { }
    #endregion

    #region 世界/持有状态切换（由 PlayerItemController 调用，子类不要直接调用）
    public void EnterCarriedState(Player player)
    {
        IsCarried = true;
        Holder    = player;
        SetWorldPresence(false);
        OnPickUp(player);
    }

    public void ExitCarriedState(Player player, Vector2 worldPos)
    {
        ReturnToWorld(worldPos);
        OnDrop(player, worldPos);
    }

    /// <summary>把道具放回世界：恢复物理与渲染并落到指定位置。不触发 OnPickUp / OnDrop。</summary>
    public void ReturnToWorld(Vector2 worldPos)
    {
        IsCarried = false;
        Holder    = null;
        if (rb != null)
        {
            //自由落体：冻结 X 轴与旋转，落直且不会滑下平台边缘
            rb.constraints     = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
            rb.gravityScale    = 1f;
            rb.velocity        = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position        = worldPos;   // 必须走 rb.position，物理查询才会用到位姿
        }
        transform.position = worldPos;
        SetWorldPresence(true);
    }

    #region 拾取动画（预览 → 生效）
    /// <summary>拾取动画开始：立即标记为"已持有"（避免被拾取检测重复命中），但暂不触发 OnPickUp；
    /// 关闭物理与碰撞、保留图像显示，供玩家"举在头顶"。</summary>
    public void BeginPickUpPreview(Player player)
    {
        IsCarried = true;
        Holder    = player;
        if (rb  != null) rb.simulated = false;
        if (col != null) col.enabled  = false;
        if (sr  != null) sr.enabled   = true;
    }

    /// <summary>拾取动画结束：正式生效，触发 OnPickUp（图像转回"持有中"的隐藏状态）</summary>
    public void CompletePickUp(Player player)
    {
        SetWorldPresence(false);
        OnPickUp(player);
    }
    #endregion

    // 关闭物理与渲染、但保留 GameObject 激活（保证协程/特效生成仍然可用）
    protected void SetWorldPresence(bool present)
    {
        if (rb  != null) rb.simulated = present;
        if (col != null) col.enabled  = present;
        if (sr  != null) sr.enabled   = present;

        //碰撞体禁用再启用后忽略关系会丢，放回世界时补一次
        if (present) IgnoreBoxCollisions();
    }
    #endregion
}
