using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>玩家死亡调度（全局单例，DontDestroyOnLoad，自行创建全屏黑色遮罩）。<br/>
/// 由 <see cref="PlayerDeathZone"/> 触碰到玩家时调用 <see cref="TriggerDeath"/>：<br/>
/// - 场上已有墓碑 → 立刻传送到墓碑并播放显现动画（与墓碑传送后半段完全一致）；<br/>
/// - 否则 → 从上往下拉黑 → 全黑期间还原到初始场景状态（重载当前场景）→ 全黑持续 holdDuration 秒 → 从上往下显现画面。<br/>
/// 遮罩挂在 DontDestroyOnLoad 的根物体上，因此重载场景后仍能保持全黑并继续显现。</summary>
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

    /// <summary>判定死亡。有墓碑则传送重生，否则黑屏并还原场景。</summary>
    public void TriggerDeath(Player player)
    {
        if (player == null) return;

        //场上已有墓碑：不走拉黑重置，直接传送并播放显现后半段
        if (TombstoneService.Instance != null && TombstoneService.Instance.HasActiveTombstone)
        {
            PlayerTombstoneTeleport teleport = player.TombstoneTeleport;
            if (teleport != null) teleport.BeginEmergeOnly();
            return;
        }

        if (running) return;
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