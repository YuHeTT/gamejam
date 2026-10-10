using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>玩家死亡调度（全局单例，DontDestroyOnLoad，自行创建全屏黑色遮罩与视频层）。<br/>
/// 由 <see cref="PlayerDeathZone"/> 触碰到玩家时调用 <see cref="TriggerDeath"/>：<br/>
/// - 该判定区勾选了 playVideoOnDeath → 全屏播放视频，至少播放 minVideoTime 秒后可按任意键退出，随后正常重启场景；<br/>
/// - 场上已有墓碑 → 立刻传送到墓碑并播放显现动画（与墓碑传送后半段完全一致）；<br/>
/// - 否则 → 从上往下拉黑 → 全黑期间还原到初始场景状态（重载当前场景）→ 全黑持续 holdDuration 秒 → 从上往下显现画面。<br/>
/// 遮罩/视频层挂在 DontDestroyOnLoad 的根物体上，因此重载场景后仍能继续工作。</summary>
[DisallowMultipleComponent]
public class PlayerDeathManager : MonoBehaviour
{
    private static PlayerDeathManager _instance;

    /// <summary>场景实例；未手动放置时首次使用时自动创建</summary>
    public static PlayerDeathManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<PlayerDeathManager>();
                if (_instance == null)
                    _instance = new GameObject(nameof(PlayerDeathManager)).AddComponent<PlayerDeathManager>();
            }
            return _instance;
        }
    }

    [Header("黑屏擦拭时长（秒）")]
    [Tooltip("从上往下拉黑的时长")]
    public float coverDuration = 0.35f;
    [Tooltip("全黑持续时间（此期间完成场景还原）")]
    public float holdDuration = 0.3f;
    [Tooltip("从上往下显现画面的时长")]
    public float revealDuration = 0.35f;

    [Header("遮罩")]
    [Tooltip("遮罩 Canvas 的渲染层级（越大越靠前）")]
    public int canvasSortingOrder = 32000;

    private Canvas canvas;
    private RectTransform coverRect;
    //遮罩超出屏幕的余量（像素），避免边缘缝隙
    private const float CoverMargin = 4f;

    //全屏视频层（特殊死亡时使用）
    private RawImage videoImage;
    private VideoPlayer videoPlayer;
    private RenderTexture videoRT;

    private bool running;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        ReleaseVideoTexture();
    }

    /// <summary>判定死亡。特殊判定区播视频，有墓碑则传送重生，否则黑屏并还原场景。</summary>
    public void TriggerDeath(Player player, PlayerDeathZone zone = null)
    {
        if (player == null || running) return;

        //特殊判定区：全屏播视频，退出后正常重启场景（优先于墓碑重生）
        if (zone != null && zone.playVideoOnDeath && zone.deathVideo != null)
        {
            musicmanager.PlayRandomDeathSound();
            StartCoroutine(VideoDeathRoutine(zone));
            return;
        }

        //场上已有墓碑：不走拉黑重置，直接传送并播放显现后半段
        if (TombstoneService.Instance != null && TombstoneService.Instance.HasActiveTombstone)
        {
            PlayerTombstoneTeleport teleport = player.TombstoneTeleport;
            if (teleport != null)
            {
                //判定区可能连续触发；传送已经开始时不重复播放死亡音效。
                if (teleport.IsTeleporting)
                    return;

                if (teleport.BeginEmergeOnly())
                    return;
            }
        }

        //悬崖等普通死亡：播放一次后进入黑屏重载流程。
        musicmanager.PlayRandomDeathSound();
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        running = true;

        yield return CoverToBlack();     //从上往下拉黑
        ReloadScene();                    //全黑期间：还原到初始场景状态
        yield return new WaitForSeconds(Mathf.Max(0f, holdDuration));   //全黑持续
        yield return RevealFromBlack();   //从上往下显现画面

        running = false;
    }

    /// <summary>特殊死亡：全屏播放视频。至少播放 zone.minVideoTime 秒后可按任意键退出；视频播完自动退出。退出后正常重启场景。</summary>
    private IEnumerator VideoDeathRoutine(PlayerDeathZone zone)
    {
        running = true;

        //先用黑屏挡住游戏画面（视频层在黑色遮罩之上，因此视频仍会显示在最前）
        SetCoverY(0f);

        //保证视频播放期间背景音乐不中断：VideoPlayer 的 Direct 音频输出会绕开 Unity 音频系统，
        //在部分平台（尤其 Windows）开始播放时会重置音频输出，把正在播的背景音乐一起掐掉。
        //这里记下进入时的播放状态，循环里检测到被停就补播，让音乐整段视频都不断。
        AudioSource bgm = musicmanager.instance != null ? musicmanager.instance.bgm : null;
        bool keepBgmAlive = bgm != null && bgm.isPlaying;

        EnsureVideoTexture();
        videoImage.texture        = videoRT;
        videoPlayer.targetTexture = videoRT;
        videoPlayer.clip          = zone.deathVideo;

        videoImage.gameObject.SetActive(true);
        videoPlayer.Play();

        float elapsed = 0f;
        bool started = false;
        float minTime = Mathf.Max(0f, zone.minVideoTime);
        while (true)
        {
            elapsed += Time.unscaledDeltaTime;
            if (videoPlayer.isPlaying) started = true;

            //背景音乐被视频输出打断则补播（只在真的停了时调一次 Play，不重复打断）
            if (keepBgmAlive && !bgm.isPlaying)
                bgm.Play();

            //视频播放结束 → 退出
            if (started && !videoPlayer.isPlaying) break;
            //已播放满 minTime 秒后，任意键退出
            if (elapsed >= minTime && Input.anyKeyDown) break;
            //兜底：视频始终未开始（格式/平台不支持等），避免卡死
            if (!started && elapsed > 10f)
            {
                Debug.LogWarning("PlayerDeathManager: 死亡视频未能开始播放（可能格式或平台不支持），直接进入复活流程。", this);
                break;
            }

            yield return null;
        }

        videoPlayer.Stop();
        videoImage.gameObject.SetActive(false);
        SetCoverY(Screen.height + CoverMargin);   //收起黑屏

        ReloadScene();     //正常复活：场景重启
        running = false;
    }

    private void EnsureVideoTexture()
    {
        int w = Mathf.Max(2, Screen.width);
        int h = Mathf.Max(2, Screen.height);
        if (videoRT != null && (videoRT.width != w || videoRT.height != h))
        {
            videoRT.Release();
            videoRT = null;
        }
        if (videoRT == null)
            videoRT = new RenderTexture(w, h, 0);
    }

    private void ReleaseVideoTexture()
    {
        if (videoRT == null) return;
        videoRT.Release();
        videoRT = null;
    }

    #region 遮罩
    private void BuildOverlay()
    {
        //Canvas：屏幕空间覆盖，最高层级
        GameObject canvasGO = new GameObject("DeathFadeCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        //黑色矩形：通过上下平移实现"从上往下"的擦拭
        GameObject imageGO = new GameObject("DeathFadeImage");
        imageGO.transform.SetParent(canvasGO.transform, false);

        Image image = imageGO.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        coverRect = image.rectTransform;
        coverRect.anchorMin = new Vector2(0.5f, 0.5f);
        coverRect.anchorMax = new Vector2(0.5f, 0.5f);
        coverRect.pivot = new Vector2(0.5f, 0.5f);

        RefreshCoverSize();
        SetCoverY(Screen.height + CoverMargin);   //初始收在屏幕上方，不可见

        //全屏视频层：默认隐藏，特殊死亡时才显示（在黑色遮罩之后创建，渲染更靠前）
        GameObject videoGO = new GameObject("DeathVideoImage");
        videoGO.transform.SetParent(canvasGO.transform, false);

        videoImage = videoGO.AddComponent<RawImage>();
        videoImage.raycastTarget = false;
        RectTransform videoRect = videoImage.rectTransform;
        videoRect.anchorMin = Vector2.zero;
        videoRect.anchorMax = Vector2.one;
        videoRect.offsetMin = Vector2.zero;
        videoRect.offsetMax = Vector2.zero;

        videoPlayer = videoGO.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake   = false;
        videoPlayer.isLooping     = false;
        videoPlayer.source        = VideoSource.VideoClip;
        videoPlayer.renderMode    = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.skipOnDrop    = true;

        videoGO.SetActive(false);
    }

    private void RefreshCoverSize()
    {
        if (coverRect == null) return;
        coverRect.sizeDelta = new Vector2(Screen.width + CoverMargin, Screen.height + CoverMargin);
    }

    /// <summary>设置遮罩中心相对屏幕中心的纵向偏移（像素）：0=完全覆盖，+h=完全在屏幕上方，-h=完全在屏幕下方</summary>
    private void SetCoverY(float y)
    {
        if (coverRect != null)
            coverRect.anchoredPosition = new Vector2(0f, y);
    }

    /// <summary>从上往下拉黑：遮罩中心从屏幕上方滑到屏幕中央</summary>
    private IEnumerator CoverToBlack()
    {
        RefreshCoverSize();
        float h = Screen.height + CoverMargin;
        float dur = Mathf.Max(0.0001f, coverDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            SetCoverY(Mathf.Lerp(h, 0f, Mathf.Clamp01(t / dur)));
            yield return null;
        }
        SetCoverY(0f);
    }

    /// <summary>从上往下显现：遮罩中心从屏幕中央滑到屏幕下方，画面自顶部逐行露出</summary>
    private IEnumerator RevealFromBlack()
    {
        RefreshCoverSize();
        float h = Screen.height + CoverMargin;
        float dur = Mathf.Max(0.0001f, revealDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            SetCoverY(Mathf.Lerp(0f, -h, Mathf.Clamp01(t / dur)));
            yield return null;
        }
        SetCoverY(h);   //收回到屏幕上方待命
    }
    #endregion

    /// <summary>重载当前场景，还原到最初始的场景状态</summary>
    private void ReloadScene()
    {
        //兜底：带着暂停状态重载场景会让新场景整场卡死，切场景前先把时间缩放还回去
        GamePause.Resume();

        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid())
        {
            Debug.LogError("PlayerDeathManager: 当前场景无效，无法还原到初始场景状态。", this);
            return;
        }

        if (active.buildIndex >= 0)
        {
            SceneManager.LoadScene(active.buildIndex);
        }
        else
        {
            //未加入 Build Settings：编辑器下可按名称加载；打包后请把场景加入 Build Settings
            Debug.LogWarning($"PlayerDeathManager: 场景 '{active.name}' 未加入 Build Settings，正在按名称尝试加载（仅编辑器有效）。", this);
            SceneManager.LoadScene(active.name);
        }
    }
}
