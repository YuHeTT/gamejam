using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 运行时创建 UI 的公共工具。<br/>
/// 这里固化了本项目已经验证过的正确做法：
/// <list type="bullet">
/// <item>用「比例锚点」定位，不用像素换算（美术画布与设计分辨率比例不同，硬算必然偏移）</item>
/// <item>点击框直接挂在 Canvas 下，不挂在会被翻转/缩放的节点下（否则触发器会跟着镜像）</item>
/// <item>场景没有 EventSystem 时自动补一个，避免"按钮点不动"</item>
/// </list>
/// </summary>
public static class LevelUiUtil
{
    public const float DesignW = 1920f;
    public const float DesignH = 1080f;

    /// <summary>确保场景里有可用的 EventSystem（没有就创建一个）</summary>
    public static EventSystem EnsureEventSystem()
    {
        if (EventSystem.current != null) return EventSystem.current;

        EventSystem existing = Object.FindObjectOfType<EventSystem>();
        if (existing != null) return existing;

        GameObject go = new GameObject("EventSystem",
            typeof(EventSystem), typeof(StandaloneInputModule));
        return go.GetComponent<EventSystem>();
    }

    /// <summary>
    /// 确保有一个用于 UI 的 Canvas（优先复用场景里已有的）。<br/>
    /// 参考分辨率 1920×1080，缩放方式与主界面一致。
    /// </summary>
    public static Canvas EnsureCanvas()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            EnsureScaler(canvas.gameObject);
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        GameObject go = new GameObject("LevelUI_Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        EnsureScaler(go);
        return canvas;
    }

    private static void EnsureScaler(GameObject go)
    {
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = go.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(DesignW, DesignH);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    /// <summary>
    /// 用「屏幕像素」矩形（1920×1080，以左下为原点）设置 RectTransform 的比例锚点。
    /// </summary>
    public static void SetAnchorRectScreen(RectTransform rt,
                                           float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(Mathf.Clamp01(Mathf.Min(x0, x1) / DesignW),
                                   Mathf.Clamp01(Mathf.Min(y0, y1) / DesignH));
        rt.anchorMax = new Vector2(Mathf.Clamp01(Mathf.Max(x0, x1) / DesignW),
                                   Mathf.Clamp01(Mathf.Max(y0, y1) / DesignH));
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 创建一个图片节点（屏幕像素定位）。<paramref name="sprite"/> 为 null 时是纯色块。
    /// </summary>
    public static Image CreateImage(Transform parent, string name, Sprite sprite,
                                    float x0, float y0, float x1, float y1)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        SetAnchorRectScreen((RectTransform)go.transform, x0, y0, x1, y1);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = false;
        return img;
    }

    /// <summary>
    /// 创建一个完全透明的点击框（Image alpha=0 + Button），挂在 Canvas 下。
    /// </summary>
    public static Button CreateHitBox(Transform parent, string name,
                                      float x0, float y0, float x1, float y1)
    {
        GameObject go = new GameObject(name + "_Hit",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        SetAnchorRectScreen((RectTransform)go.transform, x0, y0, x1, y1);

        Image img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = true;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
        return btn;
    }
}
