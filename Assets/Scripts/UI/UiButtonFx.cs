using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 按钮的「悬停/按下放大 + 点击音效」。<br/><br/>
/// 变暗/变透明由 <see cref="Button"/> 自己的 ColorTint 负责（见 <see cref="LevelUiUtil.WireHighlight"/>），
/// 这里只补两件 Unity 原生管不了的事：缩放、点击音效。<br/><br/>
/// <b>缩放为什么不会把按钮带跑</b>：缩放是绕 transform 的 pivot 进行的，而整幅美术图层是全屏尺寸、
/// 按钮在图层里是偏心的，所以接线时已经把图层的 pivot 挪到了"按钮中心"（<see cref="LevelUiUtil.SetPivotToButtonCenter"/>）。
/// <br/><br/>
/// <b>为什么用 unscaledDeltaTime</b>：设置面板本身处于 <c>Time.timeScale = 0</c>，
/// 用 <c>deltaTime</c> 的话按钮在暂停界面里会一动不动。<br/><br/>
/// 注意：<see cref="popTarget"/> 的初始 <c>localScale</c> 会被完整保留（左箭头是 <c>scale.x = -1</c> 的镜像，
/// 直接赋 <c>Vector3.one * s</c> 会把它翻回去），缩放是在初始值上乘倍率。
/// </summary>
[DisallowMultipleComponent]
public class UiButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                         IPointerDownHandler, IPointerUpHandler
{
    [Header("放大")]
    [Tooltip("要缩放的视觉（那张整幅美术图层）。留空则自动取父物体上的 Image")]
    public RectTransform popTarget;

    [Tooltip("悬停时的倍率")]
    public float hoverScale = LevelUiUtil.HoverScale;

    [Tooltip("按下时的倍率")]
    public float pressedScale = LevelUiUtil.PressedScale;

    [Tooltip("跟随速度，越大越快")]
    public float popSpeed = 14f;

    [Header("点击音效")]
    public bool playClickSound = true;

    [Tooltip("播放 shotclips 的哪一项（Inspector 里的 Element 序号，5 = 点击音效）")]
    public int clickShotIndex = musicmanager.ShotIndexUiClick;

    private Button _button;
    private bool _inside;

    // 悬停放大总开关（变暗不受它影响）。给"按钮挤在一起、放大后会互相压住"之类的场合留的后路。
    private bool _hoverPopEnabled = true;

    // 初始缩放（含左箭头那种 scale.x = -1 的镜像），缩放时在它基础上乘倍率
    private Vector3 _baseScale = Vector3.one;

    private float _scale = 1f;
    private float _targetScale = 1f;

    private void Awake()
    {
        _button = GetComponent<Button>();

        if (popTarget == null)
        {
            // 兜底：点击框通常就是那张整幅美术图层的子物体
            Transform p = transform.parent;
            if (p != null)
            {
                Image img = p.GetComponent<Image>();
                if (img != null) popTarget = img.rectTransform;
            }
        }

        if (popTarget != null) _baseScale = popTarget.localScale;
        _scale = _targetScale = 1f;

        if (playClickSound && _button != null)
            _button.onClick.AddListener(PlayClickSound);
    }

    private void OnDisable()
    {
        // 面板被关掉/物体被停用时把缩放归位，免得下次出现时是放大或缩小状态
        // （注意不要动 _hoverPopEnabled —— 那是外部设的开关，不是临时状态）
        _inside = false;
        _scale = _targetScale = 1f;
        Apply();
    }

    private void OnDestroy()
    {
        if (playClickSound && _button != null)
            _button.onClick.RemoveListener(PlayClickSound);
    }

    private void Update()
    {
        if (Mathf.Approximately(_scale, _targetScale)) return;

        // 指数趋近：与帧率无关，且 timeScale = 0 时照样推进
        float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, popSpeed) * Time.unscaledDeltaTime);
        _scale = Mathf.Lerp(_scale, _targetScale, k);
        Apply();
    }

    private void Apply()
    {
        if (popTarget == null) return;

        // 在初始缩放上乘倍率 —— 保留镜像用的负号
        popTarget.localScale = new Vector3(_baseScale.x * _scale, _baseScale.y * _scale, _baseScale.z);
    }

    /// <summary>把缩放归位。接线时（编辑期）调一次，避免把放大状态存进场景。</summary>
    public void ResetScale()
    {
        if (popTarget != null) _baseScale = popTarget.localScale;
        _scale = _targetScale = 1f;
        Apply();
    }

    /// <summary>
    /// 允许/禁止悬停放大（变暗不受影响）。<br/>
    /// 给"多个按钮挤在一起、放大后会互相压住"之类的场合留一个开关，默认开启。
    /// </summary>
    public void SetHoverPopEnabled(bool enabled)
    {
        _hoverPopEnabled = enabled;
        if (!enabled) _targetScale = 1f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _inside = true;
        if (_hoverPopEnabled) _targetScale = hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _inside = false;
        _targetScale = 1f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_hoverPopEnabled) _targetScale = pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _targetScale = (_hoverPopEnabled && _inside) ? hoverScale : 1f;
    }

    private void PlayClickSound()
    {
        musicmanager.PlayShotSound(clickShotIndex);
    }
}
