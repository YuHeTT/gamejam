using UnityEngine;

/// <summary>道具「墓」：持有时按 S 在脚下地面放置/替换墓碑（空中无效，不消耗道具）。传送见 PlayerGraveTeleportController（W）。</summary>
public class GraveItem : Item
{
    [Header("拒绝拾取（假道具）")]
    [Tooltip("勾选：本实例被拾取时动画照播，但播完不会生效，而是被随机抛向左右 —— " +
             "玩家可以反复拾取，却始终拿不到「墓」的能力")]
    public bool refusePickUp = false;
    [Tooltip("抛出时的水平速度范围（每次拾取在这两个值之间随机取一个），方向左右随机")]
    public Vector2 throwSpeedX = new Vector2(4f, 7f);
    [Tooltip("抛出时附加的向上速度。0 就是严格的平抛（初速纯水平）")]
    public float throwSpeedY = 3f;

    public override KeyCode useKey => KeyCode.S;

    /// <summary>「墓」的假道具模式：动画播完后不生效，而是被随机平抛出去，玩家永远拿不到它的能力。</summary>
    public override bool TryRejectPickUp(Player player)
    {
        if (!refusePickUp) return false;

        Vector2 from = transform.position;              //此刻道具正举在玩家头顶
        float direction = Random.value < 0.5f ? -1f : 1f;   //左右随机
        float speedX    = Random.Range(throwSpeedX.x, throwSpeedX.y);

        ReturnToWorld(from);                            //恢复物理与碰撞、把道具交还给世界

        //ReturnToWorld 会把 X 轴一起冻住（普通道具是为了落直、不沿平台滑走），
        //但抛出去必须解锁 X，否则刚给的初速会被约束吃掉、道具原地掉下来。
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.velocity    = new Vector2(direction * speedX, throwSpeedY);
        }

        return true;
    }

    public override bool UseAbility(Player player)
    {
        if (TombstoneService.Instance == null)
        {
            Debug.LogWarning("[墓] 场景中缺少 TombstoneService。", player);
            return false;
        }

        //只有实际放置成功才播放音效；空中放置未开启等失败情况不应误响。
        if (TombstoneService.Instance.TryPlaceAtPlayerFeet(player))
            musicmanager.PlayShotSound(musicmanager.ShotIndexPlaceTombstone);

        return false;
    }
}
