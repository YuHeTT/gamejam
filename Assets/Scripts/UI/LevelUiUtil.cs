using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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

    // ========================================================================
    //  按钮选中反馈（悬停变暗/变透明 + 放大 + 点击音效）
    //
    //  本工程所有按钮都是「整幅全屏美术图层 + 透明点击框」的结构：
    //  每个按钮的美术单独占一张全屏 PNG，其余区域 alpha = 0。
    //  所以把整幅图层的 Image 当作 Button 的 targetGraphic 做 ColorTint，
    //  视觉上就等于只给那一个按钮变色（透明处 0 乘任何系数还是 0）。
    //  已用像素包围盒逐一验证：没有一张图层里画了两个按钮。
    // ========================================================================

    /// <summary>悬停：亮度 ×0.88、alpha ×0.9（像纸片被掀起来一点）</summary>
    public static readonly Color HighlightColor = new Color(0.88f, 0.88f, 0.88f, 0.9f);

    /// <summary>按下：再暗一些</summary>
    public static readonly Color PressedColor = new Color(0.72f, 0.72f, 0.72f, 0.9f);

    /// <summary>不可用</summary>
    public static readonly Color DisabledColor = new Color(1f, 1f, 1f, 0.4f);

    /// <summary>悬停放大倍率</summary>
    public const float HoverScale = 1.03f;

    /// <summary>按下缩小倍率</summary>
    public const float PressedScale = 0.97f;

    /// <summary>变色渐变时长（秒）。Selectable 内部用的是 ignoreTimeScale，暂停时也照常渐变</summary>
    public const float FadeDuration = 0.08f;

    /// <summary>
    /// 给按钮接上「悬停变暗/变透明 + 放大 + 点击音效」。<br/>
    /// <paramref name="visual"/> = 该按钮对应的那张整幅美术图层的 Image。
    /// </summary>
    public static void WireHighlight(Button btn, Image visual)
    {
        if (btn == null) return;

        if (visual != null)
        {
            // 1) 变暗 / 变透明：用 Unity 原生 ColorTint
            btn.targetGraphic = visual;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = HighlightColor,
                pressedColor = PressedColor,
                selectedColor = HighlightColor,
                disabledColor = DisabledColor,
                colorMultiplier = 1f,
                fadeDuration = FadeDuration,
            };

            // 2) 把美术图层的 pivot 挪到"按钮中心"，这样放大是绕按钮原地变大，
            //    不会因为图层是全屏的而朝屏幕中心飘。
            //    （全屏图层 sizeDelta = {0,0}，offsetMin = anchoredPosition - sizeDelta*pivot，
            //      所以改 pivot 不会移动矩形，视觉位置不变。）
            SetPivotToButtonCenter((RectTransform)visual.transform, (RectTransform)btn.transform);

            // 3) 缩放与点击音效交给运行时组件
            UiButtonFx fx = btn.GetComponent<UiButtonFx>();
            if (fx == null) fx = btn.gameObject.AddComponent<UiButtonFx>();
            fx.popTarget = (RectTransform)visual.transform;
            fx.ResetScale();
        }

        // 4) 关掉键盘焦点。uGUI 的状态优先级是 Pressed > Selected > Highlighted，
        //    默认 navigation 下鼠标点一下按钮就会把它设成焦点并一直保持变暗（看起来像卡住）。
        //    全工程没有任何代码使用键盘焦点，所以直接关掉最干净。
        Navigation nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
    }

    /// <summary>
    /// 把美术图层的 pivot 挪到"按钮中心在该图层里的比例位置"，这样放大是绕按钮原地变大，
    /// 而不是因为图层是全屏的、按钮偏心而朝屏幕中心飘。<br/><br/>
    /// 比例用 <c>anchorMin/anchorMax</c> 逐级复合算出来，<b>不碰 transform 的世界坐标</b>：
    /// 预制体里的根 Canvas 其 RectTransform 序列化成了 <c>localScale = (0,0,0)</c>，
    /// 只要走一次 <c>TransformPoint</c> 就会得到 NaN。锚点复合与缩放无关，
    /// 所以也天然兼容左箭头那种 <c>scale.x = -1</c> 的镜像。<br/><br/>
    /// 比例与当前 pivot 无关，所以可以反复执行而不会漂移。
    /// </summary>
    /// <returns>算出来的 pivot（0~1）；算不出来时返回原值</returns>
    public static Vector2 SetPivotToButtonCenter(RectTransform visual, RectTransform button)
    {
        if (visual == null || button == null) return new Vector2(0.5f, 0.5f);

        RectTransform common = CommonAncestor(button, visual);
        if (common == null) return visual.pivot;

        Rect buttonRect, visualRect;
        if (!TryGetNormalizedRect(button, common, out buttonRect)) return visual.pivot;
        if (!TryGetNormalizedRect(visual, common, out visualRect)) return visual.pivot;

        if (visualRect.width <= 0.0001f || visualRect.height <= 0.0001f) return visual.pivot;

        Vector2 center = buttonRect.center;
        Vector2 pivot = new Vector2(
            Mathf.Clamp01((center.x - visualRect.x) / visualRect.width),
            Mathf.Clamp01((center.y - visualRect.y) / visualRect.height));

        visual.pivot = pivot;
        return pivot;
    }

    /// <summary>两个 RectTransform 最近的共同祖先（含自身）</summary>
    private static RectTransform CommonAncestor(RectTransform a, RectTransform b)
    {
        HashSet<RectTransform> chain = new HashSet<RectTransform>();
        for (RectTransform t = a; t != null; t = t.parent as RectTransform) chain.Add(t);
        for (RectTransform t = b; t != null; t = t.parent as RectTransform)
            if (chain.Contains(t)) return t;
        return null;
    }

    /// <summary>
    /// 把 <paramref name="rt"/> 的矩形表示成"相对祖先 <paramref name="frame"/> 的比例矩形"（0~1）。<br/>
    /// 沿父链复合 anchorMin/anchorMax，不使用 position / scale / rect —— 场景与预制体资源里结果一致。
    /// 只支持"锚点定位 + offset 为 0"的写法（本工程所有相关图层都如此）；否则返回 false。
    /// </summary>
    private static bool TryGetNormalizedRect(RectTransform rt, RectTransform frame, out Rect norm)
    {
        norm = new Rect(0f, 0f, 1f, 1f);
        if (rt == null || frame == null) return false;
        if (rt == frame) return true;

        List<RectTransform> chain = new List<RectTransform>();
        RectTransform cur = rt;
        while (cur != null && cur != frame)
        {
            chain.Add(cur);
            cur = cur.parent as RectTransform;
        }
        if (cur != frame) return false;      // frame 不是 rt 的祖先

        Rect acc = new Rect(0f, 0f, 1f, 1f);
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            RectTransform t = chain[i];
            if (t.offsetMin != Vector2.zero || t.offsetMax != Vector2.zero) return false;

            Vector2 amin = t.anchorMin;
            Vector2 amax = t.anchorMax;

            acc = new Rect(
                acc.x + amin.x * acc.width,
                acc.y + amin.y * acc.height,
                (amax.x - amin.x) * acc.width,
                (amax.y - amin.y) * acc.height);
        }

        norm = acc;
        return true;
    }
}
