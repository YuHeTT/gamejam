using System;
using UnityEngine;

/// <summary>玩家道具栏：J 拾取/交换，I 使用。挂在 Player 上。<br/>
/// 不改动 Player.cs，独立组件自带 Update。</summary>
public class PlayerItemController : MonoBehaviour
{
    [Header("Refs")]
    public Player player;                // 留空则自动向上查找
    public Transform pickUpCheck;        // 检测中心，留空用自身

    [Header("PickUpInfo")]
    public float pickUpRadius = 1.2f;
    public LayerMask whatIsItem = ~0;    // 建议设为 Item 层；留 Everything 也能靠类型过滤工作
    public float pickUpCooldown = 0.15f; // 拾取/交换后的再拾取冷却
    private float pickUpCooldownTimer;

    [Header("Keys")]
    public KeyCode pickUpKey = KeyCode.J;
    public KeyCode useKey    = KeyCode.I;

    /// <summary>当前持有的道具</summary>
    public Item CurrentItem { get; private set; }
    public bool HasItem => CurrentItem != null;

    /// <summary>槽位变化广播，未来接 UI 直接订阅</summary>
    public event Action<Item> OnItemChanged;

    private void Awake()
    {
        if (player == null)      player      = GetComponentInParent<Player>();
        if (pickUpCheck == null) pickUpCheck = transform;
    }

    private void Update()
    {
        if (pickUpCooldownTimer > 0) pickUpCooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(pickUpKey)) TryPickUp();
        if (Input.GetKeyDown(useKey))    UseCurrentItem();
    }

    /// <summary>拾取/交换。返回是否真的发生了交互</summary>
    public bool TryPickUp()
    {
        if (pickUpCooldownTimer > 0) return false;

        Item target = FindNearestItem();
        if (target == null || target == CurrentItem) return false;

        if (CurrentItem != null)          // 交换：先把旧道具放回世界
        {
            Item old = CurrentItem;
            CurrentItem = null;
            old.ExitCarriedState(player, old.GetDropPosition(player.transform.position));
        }

        CurrentItem = target;
        target.EnterCarriedState(player);
        OnItemChanged?.Invoke(target);
        pickUpCooldownTimer = pickUpCooldown;
        return true;
    }

    /// <summary>使用当前道具的能力</summary>
    public bool UseCurrentItem()
    {
        if (CurrentItem == null) return false;

        bool consumed = CurrentItem.UseAbility(player);   // 能力实现在这里被调用
        if (consumed)
        {
            Destroy(CurrentItem.gameObject);
            CurrentItem = null;
            OnItemChanged?.Invoke(null);
        }
        return true;
    }

    /// <summary>找最近的可拾取道具</summary>
    private Item FindNearestItem()
    {
        Vector2 center = pickUpCheck.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, pickUpRadius, whatIsItem);

        Item nearest  = null;
        float bestSqr = float.MaxValue;
        foreach (Collider2D hit in hits)
        {
            Item item = hit.GetComponentInParent<Item>();
            if (item == null || item.IsCarried) continue;   // 类型过滤同时排除玩家自身碰撞体

            float sqr = ((Vector2)item.transform.position - center).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; nearest = item; }
        }
        return nearest;
    }

    private void OnDrawGizmosSelected()   // 与 Entity 的 Gizmo 风格一致，便于调半径
    {
        Gizmos.color = Color.yellow;
        Vector3 c = pickUpCheck != null ? pickUpCheck.position : transform.position;
        Gizmos.DrawWireSphere(c, pickUpRadius);
    }
}
