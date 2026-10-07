using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 选关按钮：点击后跳转到 "game" + <see cref="levelNumber"/> 场景。<br/>
/// 例如 levelNumber = 5 ⇒ 加载 <c>game5</c>。场景需已加入 Build Settings。
/// </summary>
public class MenuChooseLevel : MonoBehaviour
{
    [Tooltip("关卡号，对应场景名 game{关卡号}")]
    public int levelNumber = 1;

    [Tooltip("点击时在 Console 打印一条日志，用于排查点击是否命中")]
    public bool logOnClick = true;

    public void Go()
    {
        if (levelNumber <= 0)
        {
            Debug.LogWarning("MenuChooseLevel: levelNumber 非法（" + levelNumber + "），未跳转。", this);
            return;
        }

        string sceneName = "game" + levelNumber;

        if (logOnClick)
            Debug.Log("[MenuChooseLevel] 点击生效：关卡 " + levelNumber + " → 场景 '" + sceneName + "'", this);

        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("MenuChooseLevel: 场景 '" + sceneName +
                           "' 不在 Build Settings 里，无法加载。请把它加入 File → Build Settings。", this);
        }
    }
}
