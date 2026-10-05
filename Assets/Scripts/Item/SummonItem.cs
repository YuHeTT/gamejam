using UnityEngine;

/// <summary>道具「募」：持有期间按 L 在玩家原地召唤一个复制体（最多 n 个）；<br/>
/// 按 S 启用 / 断开复制体与玩家的连接。丢弃时立即断开连接，但复制体保留。<br/>
/// 该道具不会被消耗。</summary>
public class SummonItem : Item
{
    /// <summary>「募」的使用键是 L</summary>
    public override KeyCode useKey => KeyCode.L;

    public override bool UseAbility(Player player)
    {
        CloneManager.Instance.TrySummon();
        return false;   //不消耗，保持持有
    }

    public override void OnCarriedUpdate(Player player)
    {
        if (Input.GetKeyDown(KeyCode.S) && CloneManager.Existing != null)
            CloneManager.Existing.ToggleConnection();
    }

    public override void OnDrop(Player player, Vector2 worldPos)
    {
        //丢弃道具 → 立刻断开连接（复制体保留），需重新拾取并按 S 才能恢复模仿
        if (CloneManager.Existing != null)
            CloneManager.Existing.SetConnected(false);
    }
}