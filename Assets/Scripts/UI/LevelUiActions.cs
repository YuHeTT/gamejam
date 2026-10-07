using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 关卡相关的动作集合（退出游戏 / 退出关卡 / 重新开始），供关卡内 UI 与设置界面共用。<br/><br/>
/// <b>关于 timeScale</b>：从"暂停中的设置界面"跳场景前，必须先把 <see cref="Time.timeScale"/>
/// 恢复为 1，否则目标场景会整场卡死（时间不流动）。
/// </summary>
public static class LevelUiActions
{
    /// <summary>记录"从哪个关卡进来的"。空字符串表示不是从关卡进来的。</summary>
    public static string RememberedLevelScene = "";

    public const string ChooseScene = "UI_choose";

    /// <summary>进入设置界面：暂停时间 + 记住来源关卡</summary>
    public static void OpenSettings(string settingsScene)
    {
        RememberedLevelScene = SceneManager.GetActiveScene().name;
        Time.timeScale = 0f;

        Debug.Log("[LevelUI] 打开设置界面：" + settingsScene + "（来源关卡 " + RememberedLevelScene + "，已暂停）");

        if (!Application.CanStreamedLevelBeLoaded(settingsScene))
        {
            Debug.LogError("[LevelUI] 场景 '" + settingsScene + "' 不在 Build Settings 里，无法打开设置界面。");
            return;
        }

        SceneManager.LoadScene(settingsScene);
    }

    /// <summary>退出游戏：立即生效，不做确认</summary>
    public static void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>退出关卡：恢复时间并回到选关界面</summary>
    public static void QuitToChoose()
    {
        Time.timeScale = 1f;
        LoadChecked(ChooseScene);
    }

    /// <summary>重新开始：重载"进来的那个关卡"；没有记录时退回选关界面</summary>
    public static void RestartCurrentLevel()
    {
        Time.timeScale = 1f;

        string target = RememberedLevelScene;

        if (string.IsNullOrEmpty(target))
        {
            Debug.LogWarning("[LevelUI] 没有记住来源关卡（不是从关卡进来的），改为回到选关界面。");
            LoadChecked(ChooseScene);
            return;
        }

        LoadChecked(target);
    }

    /// <summary>返回"进来的那个关卡"，并恢复时间</summary>
    public static void BackToLevel()
    {
        Time.timeScale = 1f;

        string target = RememberedLevelScene;

        if (string.IsNullOrEmpty(target) || !Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogWarning("[LevelUI] 没有可返回的关卡，改为回到选关界面。");
            LoadChecked(ChooseScene);
            return;
        }

        LoadChecked(target);
    }

    private static void LoadChecked(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("[LevelUI] 场景 '" + sceneName + "' 不在 Build Settings 里，无法加载。");
        }
    }
}
