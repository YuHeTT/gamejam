using UnityEngine;

public class PlayerAnimationTriggers : MonoBehaviour
{
    private Player player => GetComponentInParent<Player>();

    //结束攻击动画
    private void AnimationTrigger()
    {
        if (player.currentHitBox != null)
        {
            player.currentHitBox.DisableHitbox();
        }

        player.CloseClashWindow();
        player.AnimationTrigger();
    }

    //检测攻击碰撞箱
    private void AttackTrigger()
    {
        if (player.currentHitBox != null)
        {
            player.currentHitBox.EnableHitbox();
        }
    }
    //关闭攻击碰撞箱
    private void AttackEndTrigger()
    {
        if (player.currentHitBox != null)
        {
            player.currentHitBox.DisableHitbox();
        }
    }

    private void OpenClashWindow() => player.OpenClashWindow();
    private void CloseClashWindow() => player.CloseClashWindow();
}
