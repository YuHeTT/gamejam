using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 设置界面（UI_settings）的交互逻辑。<br/>
/// 三个按钮：退出游戏 / 退出关卡 / 重新开始；左上角返回箭头回到进来的那个关卡。<br/>
/// 同时支持键盘 1 / 2 / 3。注意：本界面 <see cref="Time.timeScale"/> = 0，
/// 但 <c>Input.GetKeyDown</c> 依然有效，所以按键可以正常工作。
/// </summary>
public class LevelSettingsUI : MonoBehaviour
{
    [Header("键盘快捷键")]
    public bool enableHotKeys = true;
    public KeyCode quitGameKey = KeyCode.Alpha1;    // 退出游戏
    public KeyCode quitLevelKey = KeyCode.Alpha2;   // 退出关卡
    public KeyCode restartKey = KeyCode.Alpha3;     // 重新开始

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

    /// <summary>左上角返回箭头：回到来源关卡并恢复时间</summary>
    public void OnBack()
    {
        Debug.Log("[设置界面] 返回关卡 → " + LevelUiActions.RememberedLevelScene);
        LevelUiActions.BackToLevel();
    }
}
