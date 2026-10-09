using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 关卡相关的动作集合（退出游戏 / 退出关卡 / 重新开始 / 返回关卡），
/// 供关卡内 UI 与设置界面共用。<br/><br/>
/// <b>打开设置</b>有两条路：<br/>
/// - 面板模式（推荐）：由 <see cref="LevelUI.OpenSettings"/> 直接显示面板 + <see cref="GamePause.Pause"/>，不经过这里；<br/>
/// - 切场景模式（兜底）：<see cref="OpenSettings"/> 暂停后跳到 UI_settings，返回只能重开关卡。<br/><br/>
/// <b>关于 timeScale</b>：从"暂停中的设置界面"跳场景前，必须先把
/// <see cref="Time.timeScale"/> 恢复为 1，否则目标场景会整场卡死。
/// 这里统一走 <see cref="GamePause.Resume"/>，它还会顺便解除 <c>AudioListener.pause</c>。
/// </summary>
public static class LevelUiActions
{
    /// <summary>记录"从哪个关卡进来的"。空字符串表示不是从关卡进来的。</summary>
    public static string RememberedLevelScene = "";

    public const string ChooseScene = "UI_choose";

    /// <summary>
    /// 兜底路径：进入设置场景（暂停时间 + 记住来源关卡）。<br/>
    /// 只有 <see cref="LevelUI"/> 没配 settingsPanel 时才会走到这里；它会把关卡场景卸载掉，
    /// 所以返回时只能重新加载关卡（= 重新开始）。
    /// </summary>
    public static void OpenSettings(string settingsScene)
    {
        Scene active = SceneManager.GetActiveScene();
        RememberedLevelScene = active.name;

        if (!Application.CanStreamedLevelBeLoaded(settingsScene))
        {
            Debug.LogError("[LevelUI] 场景 '" + settingsScene + "' 不在 Build Settings 里，无法打开设置界面。");
            return;
        }

        GamePause.Pause(false);   // 音乐继续放，与旧的设置场景听感一致

        Debug.Log("[LevelUI] 打开设置界面：" + settingsScene +
                  "（来源关卡 " + RememberedLevelScene + "，已暂停；此路径返回时会重开关卡）");

        SceneManager.LoadScene(settingsScene);
    }

    /// <summary>退出游戏：立即生效，不做确认</summary>
    public static void QuitGame()
    {
        GamePause.Resume();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>退出关卡：恢复时间并回到选关界面</summary>
    public static void QuitToChoose()
    {
        LoadChecked(ChooseScene);
    }

    /// <summary>
    /// 从"独立的设置场景"返回：有记录的来源关卡就回去，没有就回选关界面。<br/>
    /// 注意这条路径会重新加载关卡（关卡状态已经随场景卸载丢了）；
    /// 面板模式不走这里，面板用 <see cref="LevelUI.CloseSettings"/> 原地恢复。
    /// </summary>
    public static void BackToLevel()
    {
        string target = RememberedLevelScene;

        if (string.IsNullOrEmpty(target) || !Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogWarning("[LevelUI] 没有可返回的关卡，改为回到选关界面。");
            LoadChecked(ChooseScene);
            return;
        }

        Debug.Log("[LevelUI] 返回关卡：" + target);
        LoadChecked(target);
    }

    /// <summary>重新开始：重载"进来的那个关卡"；没有记录时退回选关界面</summary>
    public static void RestartCurrentLevel()
    {
        string target = RememberedLevelScene;

        if (string.IsNullOrEmpty(target))
        {
            Debug.LogWarning("[LevelUI] 没有记住来源关卡（不是从关卡进来的），改为回到选关界面。");
            LoadChecked(ChooseScene);
            return;
        }

        Debug.Log("[LevelUI] 重新开始关卡：" + target);
        LoadChecked(target);
    }

    /// <summary>
    /// 加载场景。恢复时间缩放放在"确认能加载"之后：<br/>
    /// 万一场景没进 Build Settings，就保持暂停状态让玩家还能按返回退出设置界面，
    /// 而不是出现"面板还盖着、游戏却已经跑起来"的半死状态。
    /// </summary>
    /// <returns>是否已发起加载</returns>
    private static bool LoadChecked(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("[LevelUI] 场景 '" + sceneName + "' 不在 Build Settings 里，无法加载。" +
                           "已保持在暂停状态，请按返回键退出设置界面。");
            return false;
        }

        GamePause.Resume();   // 必须在 LoadScene 之前恢复，否则目标场景整场卡死
        SceneManager.LoadScene(sceneName);
        return true;
    }
}
