using UnityEngine;

/// <summary>道具「暮」：持有期间按 L 在白天 / 黑夜之间切换。<br/>
/// 拾取与丢弃本身不会切换时间。该道具不会被消耗。</summary>
public class DuskItem : Item
{
    /// <summary>「暮」的使用键是 L</summary>
    public override KeyCode useKey => KeyCode.L;

    public override bool UseAbility(Player player)
    {
        TimeOfDayManager.SetNight(!TimeOfDayManager.IsNight);
        return false;   //不消耗，保持持有
    }
}