using UnityEngine;

/// <summary>道具「墓」：持有时按 S 在脚下地面放置/替换墓碑（空中无效，不消耗道具）。传送见 PlayerGraveTeleportController（W）。</summary>
public class GraveItem : Item
{
    public override KeyCode useKey => KeyCode.S;

    public override bool UseAbility(Player player)
    {
        if (TombstoneService.Instance == null)
        {
            Debug.LogWarning("[墓] 场景中缺少 TombstoneService。", player);
            return false;
        }

        TombstoneService.Instance.TryPlaceAtPlayerFeet(player);
        return false;
    }
}
