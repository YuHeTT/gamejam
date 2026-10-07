using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 关卡内 UI：左上角一个"设置"按钮。<br/>
/// 挂在 12 个关卡场景里（通过 LevelUI.prefab 批量注入）。<br/><br/>
/// 点击后：暂停时间 → 记住当前关卡场景名 → 跳转到设置界面。<br/>
/// 设置界面里再决定"退出游戏 / 退出关卡 / 重新开始"。
/// </summary>
public class LevelUI : MonoBehaviour
{
    [Header("目标场景")]
    [Tooltip("点击设置按钮后进入的界面")]
    public string settingsScene = "UI_settings";

    [Header("设置按钮位置（屏幕像素，1920×1080，以左下为原点）")]
    [Tooltip("左上角")]
    public Vector2 buttonPosition = new Vector2(24f, 896f);
    public Vector2 buttonSize = new Vector2(160f, 160f);

    [Header("素材")]
    [Tooltip("齿轮图标；留空则显示一个纯色方块（仅用于占位）")]
    public Sprite buttonSprite;

    [Header("调试快捷键（在关卡里直接按）")]
    public bool enableHotKeys = true;
    public KeyCode quitGameKey = KeyCode.Alpha1;
    public KeyCode quitLevelKey = KeyCode.Alpha2;
    public KeyCode restartKey = KeyCode.Alpha3;

    private Button _button;

    private void Start()
    {
        LevelUiUtil.EnsureEventSystem();
        BuildButton();
    }

    private void Update()
    {
        if (!enableHotKeys) return;

        if (Input.GetKeyDown(quitGameKey)) LevelUiActions.QuitGame();
        else if (Input.GetKeyDown(quitLevelKey)) LevelUiActions.QuitToChoose();
        else if (Input.GetKeyDown(restartKey)) LevelUiActions.RestartCurrentLevel();
    }

    /// <summary>创建左上角设置按钮（预制体里已摆好，这里是运行时兜底）</summary>
    public void BuildButton()
    {
        if (_button != null) return;

        // 已经手工摆好的（预制体实例）优先复用
        Button existing = GetComponentInChildren<Button>(true);
        if (existing != null)
        {
            _button = existing;
            _button.onClick.AddListener(OpenSettings);
            return;
        }

        Canvas canvas = LevelUiUtil.EnsureCanvas();

        float x0 = buttonPosition.x;
        float y0 = buttonPosition.y;
        float x1 = x0 + buttonSize.x;
        float y1 = y0 + buttonSize.y;

        // 视觉：齿轮
        Image visual = LevelUiUtil.CreateImage(canvas.transform, "Btn_Settings", buttonSprite,
                                               x0, y0, x1, y1);
        if (buttonSprite == null)
            visual.color = new Color(0.35f, 0.35f, 0.35f, 0.9f);

        // 点击框：直接挂 Canvas（不挂在视觉节点下，避免被缩放/翻转影响）
        _button = LevelUiUtil.CreateHitBox(canvas.transform, "Btn_Settings", x0, y0, x1, y1);
        _button.onClick.AddListener(OpenSettings);
    }

    /// <summary>点击设置：暂停 + 记住来源关卡 + 进设置界面</summary>
    public void OpenSettings()
    {
        LevelUiActions.OpenSettings(settingsScene);
    }
}
