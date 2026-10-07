using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 点击后跳转到指定名称的场景。<br/>
/// 场景必须已加入 Build Settings（File → Build Settings），否则 LoadScene 会失败。
/// </summary>
public class MenuNavigation : MonoBehaviour
{
    [Tooltip("目标场景名（不含 .unity 后缀）")]
    public string targetScene = "UI";

    [Tooltip("点击时在 Console 打印一条日志，用于排查点击是否命中")]
    public bool logOnClick = true;

    public void Go()
    {
        if (logOnClick)
            Debug.Log("[MenuNavigation] 点击生效：" + name + " → 目标场景 '" + targetScene + "'", this);

        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogWarning("MenuNavigation: targetScene 为空，未跳转。", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError("MenuNavigation: 场景 '" + targetScene +
                           "' 不在 Build Settings 里，无法加载。请把它加入 File → Build Settings。", this);
            return;
        }

        SceneManager.LoadScene(targetScene);
    }
}
