using UnityEngine;

/// <summary>
/// 全局暂停开关。关卡内的设置面板用它把游戏"真正"停住。<br/><br/>
/// <b>为什么不能只写 <c>Time.timeScale = 0</c>：</b>
/// Update / LateUpdate / <c>Input</c> / <c>yield return null</c> 的协程 / AudioSource 都不受
/// timeScale 影响，只有"乘 deltaTime 的逻辑"和物理会被冻住。实测本项目里这些代码在暂停时仍然活着：
/// <list type="bullet">
/// <item><c>PlayerItemController</c>：暂停中还能按 J 拾取/交换道具，按 I/L 使用道具</item>
/// <item><c>SummonItem</c>：暂停中还能按 S 切换复制体连接</item>
/// <item><c>PlayerGraveTeleportController</c>：暂停中还能按 W 传送</item>
/// <item><c>Player.CheckJumpInput</c> / <c>PlayerClone</c>：暂停中按 K 会写进跳跃缓冲，
/// 恢复的瞬间凭空起跳</item>
/// </list>
/// 所以暂停要同时做三件事：
/// <list type="number">
/// <item><c>Time.timeScale = 0</c> —— 冻结物理、动画、以及所有乘 deltaTime 的逻辑</item>
/// <item><c>AudioListener.pause</c> —— 冻结音频（可选，见 LevelUI 的 pauseAudioWhilePaused）</item>
/// <item><see cref="IsPaused"/> —— 给读输入的脚本一个统一的早退开关</item>
/// </list>
/// <b>恢复务必走 <see cref="Resume"/></b>，它会一次把三者复原，避免出现"面板关了但时间还是 0"的整场卡死。
/// </summary>
public static class GamePause
{
    /// <summary>当前是否处于暂停（设置面板打开）状态</summary>
    public static bool IsPaused { get; private set; }

    /// <summary>
    /// 暂停。
    /// </summary>
    /// <param name="alsoPauseAudio">
    /// 是否连音频一起停。<c>true</c> = 音乐/音效也暂停（需要音乐继续放就传 <c>false</c>）。
    /// </param>
    public static void Pause(bool alsoPauseAudio)
    {
        IsPaused = true;
        Time.timeScale = 0f;

        if (alsoPauseAudio)
            AudioListener.pause = true;
    }

    /// <summary>恢复：时间缩放、音频监听、暂停标记一起复原</summary>
    public static void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}
