using UnityEngine;

/// <summary>
/// 点击后结束游戏：编辑器中停止 Play 模式，打包后退出程序。
/// </summary>
public class MenuQuit : MonoBehaviour
{
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
