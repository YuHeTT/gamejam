using UnityEngine;

public class EnemySketetonAnimationTriggers : MonoBehaviour
{
    private EnemySkeleton enemy => GetComponentInParent<EnemySkeleton>();

    /// <summary>
    /// 动画结束回调 —— 由 Animation Event 调用
    /// </summary>
    private void AnimationTrigger()
    {
        if (enemy.attackHitBox != null)
        {
            enemy.attackHitBox.DisableHitbox();
        }

        enemy.AnimationFinishTrigger();
    }

    /// <summary>
    /// 攻击判定 —— 由 Animation Event 调用
    /// </summary>


    private void AttackTrigger()
    {
        if (enemy.attackHitBox != null)
        {
            enemy.attackHitBox.EnableHitbox();
        }
    }

    private void AttackEndTrigger()
    {
        if (enemy.attackHitBox != null)
        {
            enemy.attackHitBox.DisableHitbox();
        }
    }
}
