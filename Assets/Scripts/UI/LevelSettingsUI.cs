using UnityEngine;

/// <summary>
/// 设置界面的交互逻辑。两种挂法都能用：<br/>
/// <list type="bullet">
/// <item><b>面板模式（推荐）</b>：挂在 LevelUI 预制体的 SettingsPanel 上。返回 = 关掉面板 + 恢复时间缩放，
/// 关卡原样继续（不切场景）。</item>
/// <item><b>独立场景模式（兜底）</b>：挂在 UI_settings 场景里。返回 = 跳回进来的那个关卡（会重开关卡）。</item>
/// </list>
/// 三个按钮：退出游戏 / 退出关卡 / 重新开始；左上角返回箭头。<br/>
/// 同时支持键盘 1 / 2 / 3。注意：暂停时 <c>Time.timeScale</c> = 0，但 <c>Input.GetKeyDown</c> 依然有效。
/// </summary>
public class LevelSettingsUI : MonoBehaviour
{
    [Header("键盘快捷键")]
    public bool enableHotKeys = true;
    public KeyCode quitGameKey = KeyCode.Alpha1;    // 退出游戏
    public KeyCode quitLevelKey = KeyCode.Alpha2;   // 退出关卡
    public KeyCode restartKey = KeyCode.Alpha3;     // 重新开始

    /// <summary>宿主关卡 UI；面板模式下非空，独立场景模式下为 null</summary>
    private LevelUI _owner;

    private void Awake()
    {
        // 面板模式：本物体是 LevelUI 预制体的子物体，向上能找到宿主；
        // 独立场景模式：UI_settings 场景里没有 LevelUI，返回 null，走老的跳场景逻辑。
        _owner = GetComponentInParent<LevelUI>(true);
    }

    private void Start()
    {
        LevelUiUtil.EnsureEventSystem();
    }

    private void Update()
    {
        // 本界面处于暂停状态，但仍要响应按键
        if (!enableHotKeys) return;

        if (Input.GetKeyDown(quitGameKey)) OnQuitGame();
        else if (Input.GetKeyDown(quitLevelKey)) OnQuitLevel();
        else if (Input.GetKeyDown(restartKey)) OnRestart();
    }

    /// <summary>按钮 1：退出游戏（立即生效）</summary>
    public void OnQuitGame()
    {
        Debug.Log("[设置界面] 退出游戏");
        LevelUiActions.QuitGame();
    }

    /// <summary>按钮 2：退出关卡 → 选关界面</summary>
    public void OnQuitLevel()
    {
        Debug.Log("[设置界面] 退出关卡 → " + LevelUiActions.ChooseScene);
        LevelUiActions.QuitToChoose();
    }

    /// <summary>按钮 3：重新开始 → 重载来源关卡</summary>
    public void OnRestart()
    {
        Debug.Log("[设置界面] 重新开始 → " + LevelUiActions.RememberedLevelScene);
        LevelUiActions.RestartCurrentLevel();
    }

    /// <summary>
    /// 左上角返回箭头：<br/>
    /// 面板模式 → 关掉面板并恢复时间缩放，关卡接着原来的进度继续；<br/>
    /// 独立场景模式 → 跳回来源关卡（会重新加载关卡）。
    /// </summary>
    public void OnBack()
    {
        if (_owner != null)
        {
            Debug.Log("[设置界面] 返回：关闭面板，关卡原样继续");
            _owner.CloseSettings();
            return;
        }

        Debug.Log("[设置界面] 返回关卡 → " + LevelUiActions.RememberedLevelScene);
        LevelUiActions.BackToLevel();
    }
}
