using UnityEngine;

/// <summary>
/// 机关互动的统一过滤：判断某个碰撞体是否算作踏板触发者 / 天平配重。<br/>
/// 规则：道具读 <see cref="Item"/> 上的开关；墓碑读全局 <see cref="TombstoneService"/> 上的开关；
/// 其余物体（玩家、箱子等）保持原有行为，一律计为可互动。<br/>
/// 两个开关默认关闭，所以墓碑与道具默认既不会触发踏板、也不会压动天平。
/// </summary>
public static class MechanismQuery
{
    /// <summary>该碰撞体是否可以触发踏板机关</summary>
    public static bool CanTriggerPedal(Collider2D col) => IsAllowed(col, true);

    /// <summary>该碰撞体是否可以作为天平配重</summary>
    public static bool CanWeighOnBalance(Collider2D col) => IsAllowed(col, false);

    private static bool IsAllowed(Collider2D col, bool forPedal)
    {
        if (col == null) return false;

        // 道具：读它自己的开关
        Item item = col.GetComponentInParent<Item>();
        if (item != null)
            return forPedal ? item.canTriggerPedal : item.canWeighOnBalance;

        // 墓碑：开关统一放在全局的 TombstoneService 上
        if (col.GetComponentInParent<TombstoneMarker>() != null)
        {
            TombstoneService service = TombstoneService.Instance;
            if (service == null) return false;
            return forPedal ? service.canTriggerPedal : service.canWeighOnBalance;
        }

        // 其它未标记物体（玩家、箱子等）：保持原有行为
        return true;
    }
}