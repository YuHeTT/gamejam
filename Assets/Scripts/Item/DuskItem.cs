using UnityEngine;

/// <summary>道具「暮」：拾取后把时间切换到黑夜，丢弃后切回白天。<br/>
/// 该道具为被动效果，按 I 不会有任何作用，也不会被消耗。</summary>
public class DuskItem : Item
{
    public override void OnPickUp(Player player)
    {
        TimeOfDayManager.SetNight(true);
    }

    public override void OnDrop(Player player, Vector2 worldPos)
    {
        TimeOfDayManager.SetNight(false);
    }
}