using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 昼夜视觉：夜晚时在整个画面上盖一层暗色滤镜，白天完全透明（即"现在这样"）。<br/><br/>
/// 由 <see cref="TimeOfDayManager"/> 驱动，不需要手动摆放：第一次切到夜晚时自动创建一个用默认参数的实例。
/// 想逐关调暗度/颜色，就在场景里手动放一个本组件，会自动优先使用手动那份的参数。<br/><br/>
/// 实现是一个 Screen Space - Overlay 画布，滤镜用一张自下而上、由亮到暗的渐变贴图，
/// 做出"屏幕上方更暗、越往下越亮"的夜晚感；相机在多个小场景之间瞬移时滤镜始终铺满画面。
/// </summary>
[DisallowMultipleComponent]
public class NightOverlay : MonoBehaviour
{
    [Header("夜晚滤镜")]
    [Tooltip("夜晚盖在画面上的颜色，A 越大越暗（默认暗蓝黑，55% 不透明）")]
    public Color nightColor = new Color(0.05f, 0.06f, 0.16f, 0.55f);
    [Tooltip("白天↔夜晚的过渡时长（秒）。0 = 立即切换")]
    public float fadeDuration = 0.35f;
    [Tooltip("屏幕最底部的滤镜强度倍率，用来做上暗下亮的渐变：" +
             "1 = 和顶部一样暗（没有渐变），0 = 底部完全不压暗（最亮）")]
    [Range(0f, 1f)] public float bottomAlpha = 0.25f;

    [Header("层级")]
    [Tooltip("滤镜画布的 sortingOrder。调低到比暂停菜单等 UI 画布更小，滤镜就不会盖暗它们")]
    public int sortingOrder = 100;

    // 当前生效的实例：场景里手动放了就用手动那份，否则自动创建
    private static NightOverlay _active;

    private Canvas canvas;
    private Image image;
    private Coroutine fadeRoutine;
    private float currentAlpha;

    //运行时生成的渐变贴图（自己 new 的要自己销毁，否则会留下泄漏的纹理）
    private Sprite gradientSprite;
    private Texture2D gradientTexture;
    private float builtBottomAlpha = -1f;

    private void Awake()
    {
        _active = this;
        Build();
    }

    private void OnDestroy()
    {
        if (_active == this) _active = null;
        ReleaseGradientSprite();
    }

    /// <summary>由 <see cref="TimeOfDayManager"/> 调用：应用白天/夜晚的画面表现。</summary>
    public static void Apply(bool night)
    {
        NightOverlay target = Resolve();

        if (target == null)
        {
            //白天且场上还没有滤镜：直接跳过，避免白白建一份全透明的画布
            if (!night) return;
            target = CreateInstance();
        }

        target.SetNight(night);
    }

    private static NightOverlay Resolve()
    {
        if (_active != null) return _active;
        _active = FindObjectOfType<NightOverlay>(true);
        return _active;
    }

    private static NightOverlay CreateInstance()
    {
        //AddComponent 会立刻触发 Awake → 建好画布并把自己注册为当前实例
        GameObject go = new GameObject("NightOverlay");
        return go.AddComponent<NightOverlay>();
    }

    /// <summary>建立全屏画布 + 一张纯色 Image（Image 不挂 sprite 时会画成纯色矩形）</summary>
    private void Build()
    {
        if (image != null) return;

        GameObject canvasGo = new GameObject("NightOverlayCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        GameObject imageGo = new GameObject("Dark", typeof(Image));
        imageGo.transform.SetParent(canvasGo.transform, false);

        image = imageGo.GetComponent<Image>();
        image.raycastTarget = false;   //只是滤镜，不能挡住 UI 的点击
        image.sprite = EnsureGradientSprite();
        image.type   = Image.Type.Simple;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        currentAlpha = 0f;
        image.color = WithAlpha(nightColor, 0f);
    }

    private void SetNight(bool night)
    {
        //运行中改了渐变参数就地重建，省得每次调数值都要重进播放模式
        if (image != null) image.sprite = EnsureGradientSprite();

        float target = night ? Mathf.Clamp01(nightColor.a) : 0f;

        if (fadeDuration <= 0f || !isActiveAndEnabled)
        {
            StopFade();
            ApplyAlpha(target);
            return;
        }

        StopFade();
        fadeRoutine = StartCoroutine(FadeTo(target));
    }

    private IEnumerator FadeTo(float target)
    {
        float from = currentAlpha;
        float duration = Mathf.Max(0.0001f, fadeDuration);
        float t = 0f;

        //用 unscaledDeltaTime：暂停游戏时昼夜过渡不该被一并冻住
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            ApplyAlpha(Mathf.Lerp(from, target, t / duration));
            yield return null;
        }

        ApplyAlpha(target);
        fadeRoutine = null;
    }

    private void StopFade()
    {
        if (fadeRoutine == null) return;
        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    private void ApplyAlpha(float a)
    {
        currentAlpha = a;
        if (image != null)
            image.color = WithAlpha(nightColor, a);
    }

    private void OnValidate()
    {
        //编辑器里改颜色/透明度的实时预览（运行时才有效果）
        if (image != null)
            image.color = WithAlpha(nightColor, currentAlpha);
    }

    /// <summary>生成/复用渐变贴图：贴图内自上而下 alpha 由 1 递减到 <see cref="bottomAlpha"/>。
    /// 整体暗度仍由 Image 的颜色 alpha 控制，所以昼夜淡入淡出照旧。</summary>
    private Sprite EnsureGradientSprite()
    {
        if (gradientSprite != null && Mathf.Approximately(builtBottomAlpha, bottomAlpha))
            return gradientSprite;

        ReleaseGradientSprite();
        builtBottomAlpha = bottomAlpha;

        //1 像素宽就够：UI 上横向被拉满，纵向靠双线性插值做出平滑过渡
        const int height = 64;
        gradientTexture = new Texture2D(1, height, TextureFormat.RGBA32, false);
        gradientTexture.wrapMode   = TextureWrapMode.Clamp;
        gradientTexture.filterMode = FilterMode.Bilinear;

        //Texture2D 与 UI 贴图的 V 轴都是自下而上：y = 0 在屏幕底部
        for (int y = 0; y < height; y++)
        {
            float t = (float)y / (height - 1);
            float a = Mathf.Lerp(Mathf.Clamp01(bottomAlpha), 1f, t);
            gradientTexture.SetPixel(0, y, new Color(1f, 1f, 1f, a));
        }
        gradientTexture.Apply();

        //必须用 FullRect：1 像素宽的贴图走默认的 Tight 网格会退化
        gradientSprite = Sprite.Create(gradientTexture, new Rect(0f, 0f, 1f, height),
                                       new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return gradientSprite;
    }

    private void ReleaseGradientSprite()
    {
        if (gradientSprite   != null) Destroy(gradientSprite);
        if (gradientTexture  != null) Destroy(gradientTexture);
        gradientSprite  = null;
        gradientTexture = null;
    }

    private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
}