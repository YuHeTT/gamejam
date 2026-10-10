using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 昼夜视觉：夜晚时在整个画面上盖一层暗色滤镜，白天完全透明（即"现在这样"）。<br/><br/>
/// 由 <see cref="TimeOfDayManager"/> 驱动，不需要手动摆放：第一次切到夜晚时自动创建一个用默认参数的实例。
/// 想逐关调暗度/颜色，就在场景里手动放一个本组件，会自动优先使用手动那份的参数。<br/><br/>
/// 实现是一个 Screen Space - Overlay 画布，所以相机在多个小场景之间瞬移时滤镜始终铺满画面。
/// </summary>
[DisallowMultipleComponent]
public class NightOverlay : MonoBehaviour
{
    [Header("夜晚滤镜")]
    [Tooltip("夜晚盖在画面上的颜色，A 越大越暗（默认暗蓝黑，55% 不透明）")]
    public Color nightColor = new Color(0.05f, 0.06f, 0.16f, 0.55f);
    [Tooltip("白天↔夜晚的过渡时长（秒）。0 = 立即切换")]
    public float fadeDuration = 0.35f;

    [Header("层级")]
    [Tooltip("滤镜画布的 sortingOrder。调低到比暂停菜单等 UI 画布更小，滤镜就不会盖暗它们")]
    public int sortingOrder = 100;

    // 当前生效的实例：场景里手动放了就用手动那份，否则自动创建
    private static NightOverlay _active;

    private Canvas canvas;
    private Image image;
    private Coroutine fadeRoutine;
    private float currentAlpha;

    private void Awake()
    {
        _active = this;
        Build();
    }

    private void OnDestroy()
    {
        if (_active == this) _active = null;
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

    private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
}