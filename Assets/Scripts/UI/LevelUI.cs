using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡内 UI：左上角一个"设置"按钮。<br/>
/// 挂在 12 个关卡场景里（通过 LevelUI.prefab 批量注入）。<br/><br/>
/// <b>推荐用法（面板模式）</b>：把设置界面做成本预制体的子物体（见菜单 Tools → UI → 把设置界面做成关卡内面板），
/// 点击齿轮只是 <c>settingsPanel.SetActive(true)</c> + <see cref="GamePause.Pause"/>，<b>不切场景</b>。
/// 因此返回时关卡原样继续：玩家位置、复制体、墓碑、道具、相机区域全都不动，
/// 不会再出现"返回 = 重新开始关卡"。<br/><br/>
/// <b>兜底用法（切场景模式）</b>：<see cref="settingsPanel"/> 为空时仍按老逻辑跳到
/// <see cref="settingsScene"/>，但那样返回只能重新加载关卡。
/// </summary>
public class LevelUI : MonoBehaviour
{
    [Header("设置面板（推荐）")]
    [Tooltip("设置界面的面板根物体；留空则退回「跳到设置场景」的老逻辑（返回会重开关卡）")]
    public GameObject settingsPanel;

    [Tooltip("暂停时是否连音频一起停。默认 false，保持以前“设置界面里音乐继续放”的听感")]
    public bool pauseAudioWhilePaused = false;

    [Header("兜底：设置场景")]
    [Tooltip("settingsPanel 为空时，点击设置按钮进入的界面")]
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

        // 面板必须从"关闭"状态开始；预制体里保存成启用态也不会漏出来
        if (settingsPanel != null && settingsPanel.activeSelf)
            settingsPanel.SetActive(false);
    }

    private void Update()
    {
        // 暂停中：1/2/3 交给设置面板自己处理，避免关卡与面板各响应一次
        if (GamePause.IsPaused) return;
        if (!enableHotKeys) return;

        if (Input.GetKeyDown(quitGameKey)) LevelUiActions.QuitGame();
        else if (Input.GetKeyDown(quitLevelKey)) LevelUiActions.QuitToChoose();
        else if (Input.GetKeyDown(restartKey)) LevelUiActions.RestartCurrentLevel();
    }

    private void OnDestroy()
    {
        // 兜底：万一带着"暂停"状态被销毁（有别的路径切了场景），把时间缩放交还回去，
        // 否则目标场景会整场卡死。正常路径下 LevelUiActions / CloseSettings 已经恢复过了。
        if (GamePause.IsPaused)
        {
            Debug.LogWarning("[LevelUI] 在暂停状态下被销毁，自动恢复 timeScale，避免目标场景卡死。", this);
            GamePause.Resume();
        }
    }

    /// <summary>创建左上角设置按钮（预制体里已摆好，这里是运行时兜底）</summary>
    public void BuildButton()
    {
        if (_button != null) return;

        // 已经手工摆好的（预制体实例）优先复用
        Button existing = FindGearButton();
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

    /// <summary>
    /// 找齿轮的点击框。<br/>
    /// 注意：设置面板也是本物体的子物体，里面同样有 Button，所以不能直接用
    /// <c>GetComponentInChildren&lt;Button&gt;</c>（会抢到面板的按钮），必须按名字精确找。
    /// </summary>
    private Button FindGearButton()
    {
        Transform byName = transform.Find("Btn_Settings_Hit");
        if (byName != null)
        {
            Button b = byName.GetComponent<Button>();
            if (b != null) return b;
        }

        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            if (settingsPanel != null && b.transform.IsChildOf(settingsPanel.transform))
                continue;   // 跳过设置面板里的按钮

            if (b.name != null && b.name.StartsWith("Btn_Settings"))
                return b;
        }

        // 最后兜底：任意一个不属于设置面板的按钮
        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            if (settingsPanel == null || !b.transform.IsChildOf(settingsPanel.transform))
                return b;
        }

        return null;
    }

    /// <summary>
    /// 点击设置：<b>暂停 + 显示面板</b>（推荐路径，关卡不重载）。<br/>
    /// 没有面板时退回"暂停 + 跳设置场景"的老逻辑。
    /// </summary>
    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            if (GamePause.IsPaused) return;   // 防连点 / 防重复触发

            // 面板模式没有"切场景"这一步，但"来源关卡"这个状态照样要记：
            // 设置界面里的「重新开始」靠它找关卡，不记的话会提示"没记住来源关卡"并跳到选关界面。
            string levelScene = gameObject.scene.name;
            if (!string.IsNullOrEmpty(levelScene))
                LevelUiActions.RememberedLevelScene = levelScene;

            GamePause.Pause(pauseAudioWhilePaused);
            settingsPanel.SetActive(true);
            SetGearInteractable(false);

            Debug.Log("[LevelUI] 打开设置面板：timeScale=0，关卡保留不重载。" +
                      "（来源关卡 " + LevelUiActions.RememberedLevelScene + "）", this);
            return;
        }

        LevelUiActions.OpenSettings(settingsScene);
    }

    /// <summary>
    /// 关闭设置面板并恢复时间缩放，关卡从原状态继续（返回键调这个）。
    /// </summary>
    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        SetGearInteractable(true);
        GamePause.Resume();

        Debug.Log("[LevelUI] 关闭设置面板：timeScale=1，关卡原样继续。", this);
    }

    private void SetGearInteractable(bool value)
    {
        if (_button != null)
            _button.interactable = value;
    }
}
