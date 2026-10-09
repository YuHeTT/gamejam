using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 音量滑条：把 UI 上的 <see cref="Slider"/> 接到 <see cref="musicmanager"/> 的音量上。<br/>
/// UI_music 里三条滑条分别对应 总音量 / 背景音乐 / 音效。<br/><br/>
/// 值存在 <see cref="musicmanager"/> 的静态属性里，所以在"设置界面比 musicmanager 先出现"的
/// 情况下也不会丢：玩家在主菜单调完，进关卡时新生成的 musicmanager 会自己读同一份值。
/// </summary>
[RequireComponent(typeof(Slider))]
[DisallowMultipleComponent]
public class MusicVolumeSlider : MonoBehaviour
{
    public enum Channel
    {
        /// <summary>总增益：整体缩放背景音乐与音效</summary>
        Master = 0,
        /// <summary>背景音乐</summary>
        Bgm = 1,
        /// <summary>音效</summary>
        Shot = 2,
    }

    [Tooltip("这条滑条控制哪一路音量")]
    public Channel channel = Channel.Bgm;

    [Tooltip("拖动时在 Console 打印当前音量，排查用")]
    public bool logOnChange = false;

    private Slider _slider;

    private void Start()
    {
        _slider = GetComponent<Slider>();
        if (_slider == null)
        {
            Debug.LogError("[MusicVolumeSlider] 物体上没有 Slider 组件：" + name, this);
            return;
        }

        _slider.minValue = 0f;
        _slider.maxValue = 1f;
        _slider.wholeNumbers = false;

        // 先设初值、再挂监听：否则这一下会被当成"玩家改动"再写回去
        _slider.value = Value;

        _slider.onValueChanged.AddListener(OnValueChanged);
    }

    /// <summary>读/写本滑条对应的那一路音量</summary>
    private float Value
    {
        get
        {
            switch (channel)
            {
                case Channel.Master: return musicmanager.MasterVolume;
                case Channel.Bgm: return musicmanager.BgmVolume;
                default: return musicmanager.ShotVolume;
            }
        }
        set
        {
            switch (channel)
            {
                case Channel.Master: musicmanager.MasterVolume = value; break;
                case Channel.Bgm: musicmanager.BgmVolume = value; break;
                default: musicmanager.ShotVolume = value; break;
            }
        }
    }

    private void OnValueChanged(float v)
    {
        Value = v;

        if (logOnChange)
        {
            Debug.Log(string.Format(
                "[音量] {0} = {1:F2}   →  实际 bgm = {2:F2} / shot = {3:F2}",
                channel, v, musicmanager.EffectiveBgmVolume, musicmanager.EffectiveShotVolume), this);
        }
    }
}
