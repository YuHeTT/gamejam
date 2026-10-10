using UnityEngine;

/// <summary>道具「募」：持有期间按 L 在玩家原地召唤一个复制体（最多 n 个）；<br/>
/// 按 S 启用 / 断开复制体与玩家的连接。丢弃时立即断开连接，但复制体保留。<br/>
/// 该道具不会被消耗。</summary>
public class SummonItem : Item
{
    [Header("第二个复制体的警报音乐（可选）")]
    [Tooltip("勾选：玩家召唤出第 2 个复制体的瞬间，立刻中断背景音乐，并循环播放下面的音频（音量 = 背景音乐音量 × 2），" +
             "直到离开本场景（退出 / 过关 / 重开都算）")]
    public bool panicBgmOnSecondClone = false;
    [Tooltip("第二个复制体出现后，循环播放的音频片段")]
    public AudioClip secondCloneLoopClip;

    // 本实例只触发一次：第 3 个复制体出现时不再重播
    private bool _panicTriggered;

    /// <summary>「募」的使用键是 L</summary>
    public override KeyCode useKey => KeyCode.L;

    public override bool UseAbility(Player player)
    {
        bool summoned = CloneManager.Instance.TrySummon();

        // 第 2 个复制体刚出现 → 触发警报音乐
        if (summoned && panicBgmOnSecondClone && !_panicTriggered
            && CloneManager.Instance.CloneCount >= 2)
        {
            _panicTriggered = true;
            musicmanager.PlayInterruptLoop(secondCloneLoopClip, 2f);
        }

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