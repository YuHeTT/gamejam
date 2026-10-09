using UnityEngine;

public class musicmanager : MonoBehaviour
{

    public static musicmanager instance;
    public AudioSource bgm;
    public AudioSource shot;

    public AudioClip[] bgmclips;
    public AudioClip[] shotclips;

    private AudioClip currentbgmclip;

    // ========================================================================
    //  音量
    //  设置界面（UI_music）有三条滑条：总音量 / 背景音乐 / 音效。
    //  实际写进 AudioSource 的是  总音量 × 分项 ：
    //      bgm.volume  = MasterVolume * BgmVolume
    //      shot.volume = MasterVolume * ShotVolume
    //  这样"总音量"拉到 1 时，两条分项的值就等于实际音量，不会莫名其妙减半。
    //
    //  值放在 static 里（而不是只挂在实例上），因为设置界面可能先于 musicmanager 存在：
    //  玩家在主菜单调完音量再进关卡时，关卡里新建的 musicmanager 会读到同一份值。
    // ========================================================================

    public const float DefaultMasterVolume = 1f;
    public const float DefaultBgmVolume = 0.5f;
    public const float DefaultShotVolume = 0.5f;

    private const string PrefKeyMaster = "audio.volume.master";
    private const string PrefKeyBgm = "audio.volume.bgm";
    private const string PrefKeyShot = "audio.volume.shot";

    /// <summary>
    /// 是否用 PlayerPrefs 记住玩家调过的音量。<br/>
    /// true  = 关掉游戏再打开还是上次的值（首次运行用默认值）；<br/>
    /// false = 每次启动都回到默认值（总音量 1、背景音乐 0.5、音效 0.5）。
    /// </summary>
    public static bool rememberVolume = true;

    // -1 表示"还没读过"，音量本身合法范围是 0~1
    private static float _masterVolume = -1f;
    private static float _bgmVolume = -1f;
    private static float _shotVolume = -1f;

    /// <summary>总音量（0~1），初始 1</summary>
    public static float MasterVolume
    {
        get { return LoadVolume(ref _masterVolume, PrefKeyMaster, DefaultMasterVolume); }
        set { SaveVolume(ref _masterVolume, PrefKeyMaster, value); ApplyVolumes(); }
    }

    /// <summary>背景音乐音量（0~1），初始 0.5</summary>
    public static float BgmVolume
    {
        get { return LoadVolume(ref _bgmVolume, PrefKeyBgm, DefaultBgmVolume); }
        set { SaveVolume(ref _bgmVolume, PrefKeyBgm, value); ApplyVolumes(); }
    }

    /// <summary>音效音量（0~1），初始 0.5</summary>
    public static float ShotVolume
    {
        get { return LoadVolume(ref _shotVolume, PrefKeyShot, DefaultShotVolume); }
        set { SaveVolume(ref _shotVolume, PrefKeyShot, value); ApplyVolumes(); }
    }

    /// <summary>背景音乐 AudioSource 实际使用的音量 = 总音量 × 背景音乐</summary>
    public static float EffectiveBgmVolume { get { return MasterVolume * BgmVolume; } }

    /// <summary>音效 AudioSource 实际使用的音量 = 总音量 × 音效</summary>
    public static float EffectiveShotVolume { get { return MasterVolume * ShotVolume; } }

    /// <summary>
    /// 把当前音量写进 bgm / shot。改音量时自动调用；
    /// musicmanager 自己 Awake 时也要调一次（实例可能是后创建的）。
    /// </summary>
    public static void ApplyVolumes()
    {
        if (instance == null) return;

        if (instance.bgm != null) instance.bgm.volume = EffectiveBgmVolume;
        if (instance.shot != null) instance.shot.volume = EffectiveShotVolume;
    }

    private static float LoadVolume(ref float cache, string key, float fallback)
    {
        if (cache < 0f)
        {
            float v = rememberVolume ? PlayerPrefs.GetFloat(key, fallback) : fallback;
            cache = Mathf.Clamp01(v);
        }
        return cache;
    }

    private static void SaveVolume(ref float cache, string key, float value)
    {
        cache = Mathf.Clamp01(value);
        if (rememberVolume) PlayerPrefs.SetFloat(key, cache);
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return; // 重复实例本帧就会被销毁，不再做任何初始化
        }

        if (bgm != null)
        {
            // 背景音乐循环：当前音乐播完自动从头再播，不需要额外计时或回调
            bgm.loop = true;
        }
        else
        {
            Debug.LogError("[musicmanager] bgm AudioSource 未赋值");
        }

        // 玩家可能在上一个界面就调过音量（那时本实例还不存在），这里补上
        ApplyVolumes();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PlayBGM(0); // Play the first BGM clip at the start
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void PlayBGM(int index)
    {
        if (bgm == null)
        {
            Debug.LogError("[musicmanager] bgm AudioSource 未赋值，无法播放背景音乐");
            return;
        }
        if (bgmclips == null || index < 0 || index >= bgmclips.Length)
        {
            Debug.LogWarning("Invalid BGM index: " + index);
            return;
        }
        AudioClip newClip = bgmclips[index];
        if (newClip == null)
        {
            Debug.LogWarning("BGM clip 为空, index: " + index);
            return;
        }

        if (newClip != currentbgmclip)
        {
            // 切换到另一首：从 0 开始播放，并保持循环
            bgm.clip = newClip;
            bgm.loop = true;
            bgm.Play();
            currentbgmclip = newClip;
        }
        else if (!bgm.isPlaying)
        {
            // 还是同一首，但已经播完或被外部停掉了：重新播一遍
            bgm.loop = true;
            bgm.Play();
        }
        // 同一首且正在播放：直接忽略，避免重复调用时把音乐从头打断
    }

    public void PlayShot(int index)
    {
        if (shot == null)
        {
            Debug.LogError("[musicmanager] shot AudioSource 未赋值，无法播放音效");
            return;
        }
        if (shotclips == null || index < 0 || index >= shotclips.Length)
        {
            Debug.LogWarning("Invalid Shot index: " + index);
            return;
        }
        AudioClip newClip = shotclips[index];
        if (newClip == null)
        {
            Debug.LogWarning("Shot clip 为空, index: " + index);
            return;
        }
        shot.PlayOneShot(newClip);
    }
}
