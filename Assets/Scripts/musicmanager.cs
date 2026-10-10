using UnityEngine;

public class musicmanager : MonoBehaviour
{

    public static musicmanager instance;
    public AudioSource bgm;
    public AudioSource shot;

    public AudioClip[] bgmclips;
    public AudioClip[] shotclips;

    private AudioClip currentbgmclip;
    private AudioClip[] deathClips;

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

        // 变速音效用的独立音源（用到才创建，可能还不存在）
        if (instance._pitchShot != null) instance._pitchShot.volume = EffectiveShotVolume;
        if (instance._walkShot != null) instance._walkShot.volume = EffectiveShotVolume;
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

    // ========================================================================
    //  常用音效下标
    //  下标 = Inspector 里 shotclips 显示的 "Element N" 的 N（数组从 Element 0 开始）。
    // ========================================================================

    /// <summary>开门（门上升）音效 = shotclips 的 Element 10「门开启音效」</summary>
    public const int ShotIndexDoorOpen = 10;

    /// <summary>关门（门下降）音效 = shotclips 的 Element 9「门关闭音效」</summary>
    public const int ShotIndexDoorClose = 9;

    /// <summary>拾取道具音效 = shotclips 的 Element 3「拾取音效」</summary>
    public const int ShotIndexPickUp = 3;

    /// <summary>放下道具音效 = shotclips 的 Element 4「放下音效」</summary>
    public const int ShotIndexDrop = 4;

    /// <summary>玩家走路音效 = shotclips 的 Element 6「修改后的走路音效」</summary>
    public const int ShotIndexWalk = 6;

    /// <summary>玩家跳跃音效 = shotclips 的 Element 7「跳跃音效」</summary>
    public const int ShotIndexJump = 7;

    /// <summary>放置墓碑音效 = shotclips 的 Element 12「放置墓碑」</summary>
    public const int ShotIndexPlaceTombstone = 12;

    /// <summary>冲刺音效 = shotclips 的 Element 13「冲刺音效 1」</summary>
    public const int ShotIndexDash = 13;

    /// <summary>UI 按钮点击音效 = shotclips 的 Element 5「点击音效」</summary>
    public const int ShotIndexUiClick = 5;

    /// <summary>踩压力板 / 踩按钮音效 = shotclips 的 Element 8「踩压力板音效」</summary>
    public const int ShotIndexPressurePlate = 8;

    /// <summary>通关音效 = shotclips 的 Element 0「你过关音效」</summary>
    public const int ShotIndexLevelClear = 0;

    /// <summary>
    /// 播放音效。<br/>
    /// <paramref name="index"/> = Inspector 里 shotclips 的 Element 序号（从 0 开始）；<br/>
    /// <paramref name="pitch"/> = 播放速度倍数：1 = 原速，2 = <b>2 倍速</b>（音调同时升高）。<br/>
    /// musicmanager 不在场时安全忽略 —— 门、机关这些地方不必每次都写一遍 null 判断。
    /// </summary>
    public static void PlayShotSound(int index, float pitch = 1f)
    {
        if (instance == null) return;
        instance.PlayShotInternal(index, pitch);
    }

    /// <summary>播放随机死亡音效（悬崖死亡、特殊死亡或墓碑重生）。片段放在 Resources/DeathSounds 中。</summary>
    public static void PlayRandomDeathSound()
    {
        if (instance == null || instance.shot == null) return;

        if (instance.deathClips == null)
            instance.deathClips = Resources.LoadAll<AudioClip>("DeathSounds");

        if (instance.deathClips == null || instance.deathClips.Length == 0)
        {
            Debug.LogWarning("[musicmanager] Resources/DeathSounds 中没有找到死亡音效片段");
            return;
        }

        AudioClip clip = instance.deathClips[Random.Range(0, instance.deathClips.Length)];
        if (clip != null)
            instance.shot.PlayOneShot(clip);
    }

    // 变速音效（开关门就是变速播放的）必须走<b>独立的 AudioSource</b>：
    // AudioSource.pitch 是整个音源共用的，直接在 shot 上改会把拾取 / 跳跃 / 走路等
    // 所有音效一起变速，而且改完立刻还原也来不及 —— 一次性音效播放期间一直在读这个值。
    // 这个音源在第一次用到变速音效时才创建，输出设置跟随 shot，听感保持一致。
    private AudioSource _pitchShot;
    private AudioSource _walkShot;

    /// <summary>走路音效使用独立音源，停止走路时不会影响其它音效。</summary>
    private AudioSource WalkShotSource
    {
        get
        {
            if (_walkShot != null) return _walkShot;

            GameObject go = new GameObject("shot_walk");
            go.transform.SetParent(transform, false);

            _walkShot = go.AddComponent<AudioSource>();
            _walkShot.playOnAwake = false;
            _walkShot.loop = false;
            _walkShot.volume = EffectiveShotVolume;
            _walkShot.spatialBlend = 0f;

            if (shot != null)
            {
                _walkShot.outputAudioMixerGroup = shot.outputAudioMixerGroup;
                _walkShot.ignoreListenerPause = shot.ignoreListenerPause;
                _walkShot.priority = shot.priority;
            }

            return _walkShot;
        }
    }

    /// <summary>播放当前配置的走路音效。</summary>
    public static void PlayWalkSound()
    {
        if (instance == null) return;
        instance.PlayWalkInternal();
    }

    /// <summary>停止走路音效，不影响其它音效。</summary>
    public static void StopWalkSound()
    {
        if (instance != null && instance._walkShot != null)
            instance._walkShot.Stop();
    }

    private void PlayWalkInternal()
    {
        if (shotclips == null || ShotIndexWalk < 0 || ShotIndexWalk >= shotclips.Length)
        {
            Debug.LogWarning("Invalid walk Shot index: " + ShotIndexWalk);
            return;
        }

        AudioClip clip = shotclips[ShotIndexWalk];
        if (clip == null)
        {
            Debug.LogWarning("Walk shot clip 为空, index: " + ShotIndexWalk);
            return;
        }

        AudioSource src = WalkShotSource;
        //新走路文件可能包含一段连续脚步；播放期间不要每个计时周期重启它。
        //离开移动状态时由 StopWalkSound() 负责立即停止。
        if (src.isPlaying)
            return;
        src.PlayOneShot(clip);
    }

    private AudioSource PitchShotSource
    {
        get
        {
            if (_pitchShot != null) return _pitchShot;

            GameObject go = new GameObject("shot_pitch");
            go.transform.SetParent(transform, false);

            _pitchShot = go.AddComponent<AudioSource>();
            _pitchShot.playOnAwake = false;
            _pitchShot.loop = false;
            _pitchShot.pitch = 1f;
            _pitchShot.volume = EffectiveShotVolume;   // 跟随「总音量 × 音效」

            if (shot != null)
            {
                _pitchShot.outputAudioMixerGroup = shot.outputAudioMixerGroup;
                _pitchShot.spatialBlend = shot.spatialBlend;
                _pitchShot.ignoreListenerPause = shot.ignoreListenerPause;
                _pitchShot.priority = shot.priority;
            }

            return _pitchShot;
        }
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

        // 音效通过跨场景持久存在的全局音源播放，不应受其固定世界坐标影响音量。
        if (shot != null)
            shot.spatialBlend = 0f;

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

    /// <summary>原速播放音效（等价于 pitch = 1）</summary>
    public void PlayShot(int index)
    {
        PlayShotInternal(index, 1f);
    }

    /// <summary>
    /// 播放音效。<paramref name="pitch"/> != 1 时走独立的变速音源，
    /// 不影响 shot 上其它音效的速度。
    /// </summary>
    public void PlayShotInternal(int index, float pitch)
    {
        if (shot == null)
        {
            Debug.LogError("[musicmanager] shot AudioSource 未赋值，无法播放音效");
            return;
        }

        shot.spatialBlend = 0f;
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

        // 原速：和以前完全一样，用 shot 本身
        if (Mathf.Approximately(pitch, 1f))
        {
            shot.PlayOneShot(newClip);
            return;
        }

        // 变速：用独立音源，只改它的 pitch
        AudioSource src = PitchShotSource;
        src.pitch = pitch;
        src.PlayOneShot(newClip);
    }
}
