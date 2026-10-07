using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI 点击链路自检。菜单：Tools → UI → 检查当前场景的 UI 点击链路<br/>
/// 逐项检查：EventSystem / 输入模块 / Canvas / GraphicRaycaster / 所有按钮（位置、尺寸、
/// 是否在屏幕内、raycastTarget、onClick 绑定、目标场景是否在 Build Settings），
/// 并在 Scene 视图用方框标出所有按钮区域（绿色=正常，红色=有问题）。
/// </summary>
public static class MenuUIDiagnostics
{
    // 参考分辨率，用于把 anchoredPosition 换算成屏幕像素
    private const float UI_W = 1920f;
    private const float UI_H = 1080f;

    private static readonly List<Rect> _screenRects = new List<Rect>();
    private static readonly List<bool> _screenOk = new List<bool>();

    [MenuItem("Tools/UI/检查当前场景的 UI 点击链路")]
    public static void Check()
    {
        _screenRects.Clear();
        _screenOk.Clear();

        int problems = 0;

        // ---------- 1) EventSystem ----------
        EventSystem es = Object.FindObjectOfType<EventSystem>();
        if (es == null)
        {
            Debug.LogError("[UI自检] ✗ 场景里没有 EventSystem —— 这是点击完全失效的头号原因。");
            problems++;
        }
        else
        {
            Debug.Log("[UI自检] ✓ EventSystem 存在，物体名 = " + es.gameObject.name);

            BaseInputModule[] modules = es.GetComponents<BaseInputModule>();
            if (modules.Length == 0)
            {
                Debug.LogError("[UI自检] ✗ EventSystem 上没有输入模块（StandaloneInputModule）—— 点击不会生效。");
                problems++;
            }
            else
            {
                foreach (BaseInputModule m in modules)
                {
                    Debug.Log("[UI自检] ✓ 输入模块: " + m.GetType().Name +
                              "  enabled=" + m.enabled +
                              "  物体激活=" + m.gameObject.activeInHierarchy);
                    if (!m.enabled || !m.gameObject.activeInHierarchy)
                    {
                        Debug.LogError("[UI自检] ✗ 输入模块未启用，点击不会生效。");
                        problems++;
                    }
                }
            }

            if (!es.gameObject.activeInHierarchy)
            {
                Debug.LogError("[UI自检] ✗ EventSystem 所在物体未激活。");
                problems++;
            }
        }

        // ---------- 2) Canvas 与 GraphicRaycaster ----------
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
        if (canvases.Length == 0)
        {
            Debug.LogError("[UI自检] ✗ 场景里没有 Canvas。");
            problems++;
        }

        foreach (Canvas c in canvases)
        {
            GraphicRaycaster gr = c.GetComponent<GraphicRaycaster>();
            Debug.Log("[UI自检] Canvas '" + c.gameObject.name + "'  renderMode=" + c.renderMode +
                      "  GraphicRaycaster=" + (gr != null) +
                      (gr != null ? ("  enabled=" + gr.enabled) : ""));
            if (gr == null)
            {
                Debug.LogError("[UI自检] ✗ Canvas 上没有 GraphicRaycaster，点击不会被派发。");
                problems++;
            }
            else if (!gr.enabled)
            {
                Debug.LogError("[UI自检] ✗ GraphicRaycaster 被禁用了。");
                problems++;
            }

            CanvasScaler cs = c.GetComponent<CanvasScaler>();
            if (cs != null)
                Debug.Log("[UI自检] CanvasScaler  uiScaleMode=" + cs.uiScaleMode +
                          "  referenceResolution=" + cs.referenceResolution +
                          "  match=" + cs.matchWidthOrHeight);
        }

        // ---------- 3) 所有按钮 ----------
        Button[] buttons = Object.FindObjectsOfType<Button>();
        Debug.Log("[UI自检] 场景里共有 " + buttons.Length + " 个 Button。");

        if (buttons.Length == 0)
        {
            Debug.LogError("[UI自检] ✗ 没有任何 Button —— 说明生成器没有生成点击框，" +
                           "或者当前打开的不是生成后的场景。");
            problems++;
        }

        foreach (Button b in buttons)
        {
            problems += CheckButton(b);
        }

        // ---------- 结论 ----------
        SceneView.RepaintAll();
        if (problems == 0)
            Debug.Log("[UI自检] ===== 全部通过：点击链路看起来是完好的。=====\n" +
                      "若运行后仍然点不动，请再检查：① 是否在 Play 模式下点击；" +
                      "② 点击位置是否落在 Scene 视图里绿框范围内（见下一条日志的坐标）。");
        else
            Debug.LogError("[UI自检] ===== 共发现 " + problems + " 个问题，见上面 ✗ 标记的条目。=====");

        PrintButtonScreenRects();
    }

    private static int CheckButton(Button b)
    {
        int problems = 0;
        string name = b.gameObject.name;

        if (!b.interactable)
        {
            Debug.LogError("[UI自检] ✗ " + name + "：interactable = false，点不动。");
            problems++;
        }

        if (!b.gameObject.activeInHierarchy)
        {
            Debug.LogError("[UI自检] ✗ " + name + "：物体未激活。");
            problems++;
        }

        RectTransform rt = b.transform as RectTransform;
        if (rt == null)
        {
            Debug.LogError("[UI自检] ✗ " + name + "：不是 RectTransform。");
            return problems + 1;
        }

        Graphic target = b.targetGraphic;
        if (target == null)
        {
            Debug.LogError("[UI自检] ✗ " + name + "：targetGraphic 为空。");
            problems++;
        }
        else if (!target.raycastTarget)
        {
            Debug.LogError("[UI自检] ✗ " + name + "：targetGraphic 的 raycastTarget = false，点不中。");
            problems++;
        }

        // 点击框必须有一个 raycastTarget=true 的 Graphic，且 RectTransform 尺寸 > 0
        if (rt.rect.width < 1f || rt.rect.height < 1f)
        {
            Debug.LogError("[UI自检] ✗ " + name + "：点击框尺寸异常 " + rt.rect.size);
            problems++;
        }

        // 屏幕矩形（以 1920×1080 参考分辨率为准）
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);          // Overlay 模式下世界坐标 = 屏幕像素坐标
        Rect screen = new Rect(corners[0].x, corners[0].y,
                               corners[2].x - corners[0].x, corners[2].y - corners[0].y);

        bool inside = screen.xMax > 0 && screen.xMin < UI_W &&
                      screen.yMax > 0 && screen.yMin < UI_H;

        _screenRects.Add(screen);
        _screenOk.Add(inside && problems == 0);

        // onClick 绑定
        int persistent = b.onClick.GetPersistentEventCount();
        string bound = "";
        for (int i = 0; i < persistent; i++)
        {
            Object t = b.onClick.GetPersistentTarget(i);
            bound += (t != null ? t.GetType().Name : "null") + "." + b.onClick.GetPersistentMethodName(i) + " ";
        }

        if (persistent == 0)
        {
            Debug.LogError("[UI自检] ✗ " + name + "：onClick 没有任何绑定，点了也不会跳转。");
            problems++;
        }

        // 若绑定的是 MenuNavigation，检查目标场景是否在 Build Settings
        MenuNavigation nav = b.GetComponent<MenuNavigation>();
        if (nav != null && !string.IsNullOrEmpty(nav.targetScene))
        {
            bool inBuild = false;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.enabled && s.path.EndsWith("/" + nav.targetScene + ".unity")) { inBuild = true; break; }
            }
            if (!inBuild)
            {
                Debug.LogError("[UI自检] ✗ " + name + "：目标场景 '" + nav.targetScene +
                               "' 不在 Build Settings 里，LoadScene 会失败。");
                problems++;
            }
        }

        Debug.Log(string.Format(
            "[UI自检] {0} {1}：屏幕矩形 x[{2:F0},{3:F0}] y[{4:F0},{5:F0}]  尺寸 {6:F0}x{7:F0}  " +
            "在屏内={8}  onClick=[{9}]  目标场景={10}",
            problems == 0 ? "✓" : "✗", name,
            screen.xMin, screen.xMax, screen.yMin, screen.yMax,
            screen.width, screen.height, inside, bound.Trim(),
            nav != null ? nav.targetScene : "—"));

        return problems;
    }

    private static void PrintButtonScreenRects()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("[UI自检] 各按钮在 1920×1080 下的屏幕矩形（左下为原点）：\n");
        for (int i = 0; i < _screenRects.Count; i++)
        {
            Rect r = _screenRects[i];
            sb.Append("  ").Append(_screenOk[i] ? "✓" : "✗").Append("  ")
              .AppendFormat("x[{0:F0},{1:F0}] y[{2:F0},{3:F0}]", r.xMin, r.xMax, r.yMin, r.yMax)
              .Append('\n');
        }
        sb.Append("（Y 轴以屏幕左下为原点，向上为正）");
        Debug.Log(sb.ToString());
    }
}

/// <summary>
/// 在 Scene 视图实时画出 UI 点击框与 1920×1080 屏幕边界，方便肉眼核对
/// "点击框是否和画面上的文字重合"。<br/>
/// 菜单：Tools → UI → 开关：Scene 视图显示点击框<br/>
/// 蓝色 = 1920×1080 屏幕边界；绿色 = 点击框（可点）；红色 = 有问题。
/// </summary>
[InitializeOnLoad]
public static class MenuUIGizmos
{
    private const string PrefKey = "MenuUIBuilder.ShowHitBoxes";

    private static bool Enabled
    {
        get { return EditorPrefs.GetBool(PrefKey, true); }
        set { EditorPrefs.SetBool(PrefKey, value); }
    }

    static MenuUIGizmos()
    {
        SceneView.duringSceneGui -= OnSceneGui;
        SceneView.duringSceneGui += OnSceneGui;
    }

    [MenuItem("Tools/UI/开关：Scene 视图显示点击框")]
    private static void Toggle()
    {
        Enabled = !Enabled;
        SceneView.RepaintAll();
        Debug.Log("[UI自检] Scene 视图点击框显示：" + (Enabled ? "开" : "关"));
    }

    private static void OnSceneGui(SceneView view)
    {
        if (!Enabled) return;

        // 1920×1080 屏幕边界（Overlay Canvas 的世界坐标 = 画布像素坐标，原点在屏幕左下）
        DrawRect(new Rect(0f, 0f, 1920f, 1080f), new Color(0.2f, 0.6f, 1f, 1f), new Color(0.2f, 0.6f, 1f, 0.04f));

        Button[] buttons = Object.FindObjectsOfType<Button>();
        foreach (Button b in buttons)
        {
            RectTransform rt = b.transform as RectTransform;
            if (rt == null) continue;

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Rect r = new Rect(corners[0].x, corners[0].y,
                              corners[2].x - corners[0].x, corners[2].y - corners[0].y);

            bool ok = b.interactable && b.gameObject.activeInHierarchy &&
                      b.targetGraphic != null && b.targetGraphic.raycastTarget;

            DrawRect(r,
                ok ? new Color(0.1f, 1f, 0.2f, 1f) : new Color(1f, 0.2f, 0.2f, 1f),
                ok ? new Color(0.1f, 1f, 0.2f, 0.12f) : new Color(1f, 0.2f, 0.2f, 0.18f));
        }
    }

    private static void DrawRect(Rect worldRect, Color outline, Color fill)
    {
        Vector2 p0 = HandleUtility.WorldToGUIPoint(new Vector3(worldRect.xMin, worldRect.yMin, 0f));
        Vector2 p1 = HandleUtility.WorldToGUIPoint(new Vector3(worldRect.xMax, worldRect.yMax, 0f));

        Rect gui = new Rect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y),
                            Mathf.Abs(p1.x - p0.x), Mathf.Abs(p1.y - p0.y));

        Handles.BeginGUI();
        Handles.DrawSolidRectangleWithOutline(gui, fill, outline);
        Handles.EndGUI();
    }
}
