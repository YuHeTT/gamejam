using UnityEngine;

/// <summary>道具「蓦」：持有期间跳跃力度变为 n 倍，并获得冲刺能力（按 L 朝面向方向冲刺）。<br/>
/// 该道具不会被消耗；丢弃后跳跃倍率与冲刺能力一并移除。</summary>
public class MoItem : Item
{
    [Header("蓦 Info")]
    [Tooltip("持有期间跳跃力度倍率")]
    public float jumpMultiplier = 1.8f;

    /// <summary>该道具的使用键是 L，而非默认的 I</summary>
    public override KeyCode useKey => KeyCode.L;

    public override void OnPickUp(Player player)
    {
        player.jumpSpeedMultiplier = jumpMultiplier;
        player.canDash = true;
    }

    public override bool UseAbility(Player player)
    {
        player.TryDash();
        return false;   // 不消耗，保持持有
    }

    public override void OnDrop(Player player, Vector2 worldPos)
    {
        player.jumpSpeedMultiplier = 1f;
        player.canDash = false;
    }
}