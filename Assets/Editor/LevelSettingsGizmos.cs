using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置界面上的布局可视化工具。<br/>
/// 菜单：Tools → UI → 诊断 → 设置界面：显示/隐藏布局辅助框（默认开）<br/>
/// 在 Scene 视图用彩色方框标出「退出游戏 / 退出关卡 / 重新开始」三个按钮的实际位置，
/// 对照 Art/设置-1等5项文件/设置-1.png 的画稿就能一眼看出坐标差多少，不用反复生成试错。
/// </summary>
[InitializeOnLoad]
public static class LevelSettingsGizmos
{
    private const string PrefKey = "LevelSettingsGizmos.Show";
    private const string ScenePath = "Assets/Scenes/UI_settings.unity";

    private static bool Enabled
    {
        get { return EditorPrefs.GetBool(PrefKey, true); }
        set { EditorPrefs.SetBool(PrefKey, value); }
    }

    private static readonly Color[] Palette =
    {
        new Color(1f, 0.2f, 0.2f, 1f),      // 红 = 退出游戏
        new Color(0.2f, 0.6f, 1f, 1f),      // 蓝 = 退出关卡
        new Color(0.1f, 0.85f, 0.2f, 1f),   // 绿 = 重新开始
        new Color(1f, 0.6f, 0.1f, 1f),      // 橙 = 返回
    };

    private static readonly string[] Names =
    {
        "Btn_QuitGame_Hit", "Btn_QuitLevel_Hit", "Btn_Restart_Hit", "Btn_Back_Hit",
    };

    static LevelSettingsGizmos()
    {
        SceneView.duringSceneGui -= OnSceneGui;
        SceneView.duringSceneGui += OnSceneGui;
    }

    [MenuItem("Tools/UI/诊断/设置界面：显示或隐藏布局辅助框")]
    private static void Toggle()
    {
        Enabled = !Enabled;
        SceneView.RepaintAll();
        Debug.Log("[设置界面布局] 辅助框显示：" + (Enabled ? "开" : "关"));
    }

    // 说明：曾经的「打印三个按钮的实际矩形」已删除 —— 那是一次性标定用的，
    // 现在四个点击框的位置已经稳定（见 MenuUIBuilder.BuildSettingsScene 的注释），
    // 需要复核时用上面的辅助框在 Scene 视图里直接看即可。

    private static void OnSceneGui(SceneView view)
    {
        if (!Enabled) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath) return;

        for (int i = 0; i < Names.Length; i++)
        {
            GameObject go = GameObject.Find(Names[i]);
            if (go == null) continue;

            RectTransform rt = go.transform as RectTransform;
            if (rt == null) continue;

            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);

            // 1920×1080 设计空间 → Scene 视图 GUI 坐标
            Vector2 p0 = HandleUtility.WorldToGUIPoint(c[0]);
            Vector2 p1 = HandleUtility.WorldToGUIPoint(c[2]);

            Rect gui = new Rect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y),
                                Mathf.Abs(p1.x - p0.x), Mathf.Abs(p1.y - p0.y));

            Color col = Palette[i % Palette.Length];
            Handles.BeginGUI();
            Handles.DrawSolidRectangleWithOutline(gui,
                new Color(col.r, col.g, col.b, 0.10f), col);
            Handles.EndGUI();
        }
    }
}
