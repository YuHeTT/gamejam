using UnityEngine;

/// <summary>道具「幕」：拾取后玩家进入漂浮状态，丢弃后结束漂浮（在低矮处丢弃会卡住）</summary>
public class MuItem : Item
{
    public override void OnPickUp(Player player)
    {
        player.EnterFloat();
    }

    public override void OnDrop(Player player, Vector2 worldPos)
    {
        player.TryExitFloat();
    }
}