using UnityEngine;

/// <summary>世界中的道具：可被拾取的物理物体，同时承载该道具的能力逻辑。<br/>
/// 组件 Rigidbody2D、Collider2D、SpriteRenderer 需与本脚本挂在同一个 GameObject 上。</summary>
public class Item : MonoBehaviour
{
    [Header("ItemInfo")]
    public string itemName = "未命名道具";
    public Sprite icon;                       // 预留：UI 用，暂不接入

    [Header("DropInfo")]
    public Vector2 dropOffset = Vector2.zero; // 从玩家身上放回世界时的偏移

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
    }

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
        OnDrop(player, worldPos);
    }

    // 关闭物理与渲染、但保留 GameObject 激活（保证协程/特效生成仍然可用）
    protected void SetWorldPresence(bool present)
    {
        if (rb  != null) rb.simulated = present;
        if (col != null) col.enabled  = present;
        if (sr  != null) sr.enabled   = present;
    }
    #endregion
}
