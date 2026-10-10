using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 门上沿裁剪：门向上开时，把"越过初始门顶"的那部分像素裁掉，落下来时自动露出；
/// 同时把碰撞体中越界的部分一起裁掉，避免"门看不见却仍然挡人"。<br/><br/>
/// 视觉实现用 <b>着色器按世界 Y 裁剪</b>（Custom/DoorClip），而不是 SpriteMask。原因：<br/>
/// 1) 本工程所有精灵都在 Default 排序层且 order 0，SpriteMask 的生效依赖自定义排序区间
///    与绘制顺序，极易整片失效（实测就没生效）；<br/>
/// 2) SpriteMask 裁的是"区域内所有精灵"，会误伤天花板以上的其他物体；着色器裁剪的作用对象
///    精确等于"用了这个材质的渲染器"，只裁门自己；<br/>
/// 3) 不需要 SpriteMask 物体，也不依赖排序层设置。<br/><br/>
/// 与驱动脚本解耦：只观察自身位移，不关心是 botton / doorwithtrigger / ComboDoor 中的哪个在推。<br/><br/>
/// <b>开关门音效也放在这里</b>：本工程每个门都挂了 DoorMask（door_botton / door_floor 预制体，
/// 以及 game5 里的 door_botton 与 smalldoor），所以只要在这一个地方判断"门正在上升还是下降"，
/// 就能覆盖全部三种驱动方式，不必去改 botton / doorwithtrigger / ComboDoor。<br/>
/// 门没有刚体，位置只由驱动脚本改，所以静止时不会因为物理沉降误播音效。
/// </summary>
[DisallowMultipleComponent]
public class DoorMask : MonoBehaviour
{
    [Header("门部位（留空 = 本物体）")]
    [Tooltip("实际会升降的 Transform（其自身及子物体上的 SpriteRenderer 都会被裁剪）")]
    public Transform doorPart;

    [Header("裁剪线")]
    [Tooltip("裁剪分界线（世界 Y）。留空 = 自动取门的“初始门顶”。越过分界线的部分会被裁掉")]
    public float maskLineY = float.NaN;
    [Tooltip("裁剪边缘的软过渡宽度（世界单位）。0.01 左右即可，太小会有锯齿")]
    public float clipFade = 0.01f;
    [Tooltip("用自定义着色器裁剪。关闭则完全不做视觉裁剪（只做碰撞体裁剪）")]
    public bool clipVisual = true;

    [Header("碰撞体裁剪")]
    [Tooltip("把碰撞体中“越过初始门顶”的部分一起裁掉，避免门看不见却仍然挡人")]
    public bool trimCollider = true;
    [Tooltip("裁剪的分界线（世界 Y）。留空 = 自动取门的“初始门顶”")]
    public float trimLineY = float.NaN;
    [Tooltip("剩余高度小于该值时直接关闭碰撞体（米）")]
    public float minRemainHeight = 0.02f;
    [Tooltip("高度变化小于该值时不重建碰撞体，避免物理抖动（米）")]
    public float rebuildDelta = 0.01f;

    [Header("开关门音效")]
    [Tooltip("门上升（开门）时播放哪个音效：Inspector 里 shotclips 的 Element 序号，10 = 门开启音效")]
    public int riseShotIndex = musicmanager.ShotIndexDoorOpen;
    [Tooltip("门下降（关门）时播放哪个音效：Inspector 里 shotclips 的 Element 序号，9 = 门关闭音效")]
    public int fallShotIndex = musicmanager.ShotIndexDoorClose;
    [Tooltip("开关门音效的播放速度倍数：1 = 原速，3 = 3 倍速（音调同时升高、时长变 1/3）。" +
             "Unity 把 pitch 限制在 -3~3，所以 3 就是能调到的最大值")]
    public float moveSoundPitch = 3f;
    [Tooltip("关掉则这个门不发声（动画照常）")]
    public bool playMoveSound = true;

    [Header("调试")]
    [Tooltip("每秒打印一次位移/碰撞体剩余高度/裁剪线，排查用")]
    public bool debugLog = false;

    // 初始状态缓存
    private float _startY;
    private float _clipLine;
    private float _trimLine;
    private BoxCollider2D _box;
    private Vector2 _origSize;
    private Vector2 _origOffset;
    private float _origScaleY = 1f;
    private bool _colliderDisabled;
    private bool _ready;

    // 视觉裁剪
    private Material _clipMat;
    private MaterialPropertyBlock _mpb;
    private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
    private float _lastAppliedHeight = -1f;

    private float _lastLogTime = -10f;

    // 开关门音效：上一帧的门高度 + 当前正在往哪个方向走（0=没动、1=上升、-1=下降）
    private float _lastSoundY;
    private int _moveDir;

    /// <summary>判断"门动了没有"的阈值（米）。与下面碰撞体裁剪用的是同一个量级</summary>
    private const float MoveEpsilon = 0.0001f;

    private Transform Part => doorPart != null ? doorPart : transform;

    private void Awake()
    {
        _startY = Part.position.y;
        _lastSoundY = _startY;

        // 收集需要裁剪的渲染器（门本体 + 子物体）
        Part.GetComponentsInChildren(true, _renderers);

        Collider2D col = Part.GetComponent<Collider2D>();
        _box = col as BoxCollider2D;
        if (_box != null)
        {
            _origSize = _box.size;
            _origOffset = _box.offset;
            _origScaleY = Mathf.Abs(_box.transform.lossyScale.y);
            if (_origScaleY < 0.0001f) _origScaleY = 1f;
        }

        // 分界线：默认取"初始门顶"——门一动，越过去的像素和碰撞体都被裁
        float defaultLine = col != null ? col.bounds.max.y : _startY;
        _clipLine = float.IsNaN(maskLineY) ? defaultLine : maskLineY;
        _trimLine = float.IsNaN(trimLineY) ? defaultLine : trimLineY;

        if (clipVisual) SetupClippingMaterial();

        _ready = true;

        if (_box == null)
            Debug.LogWarning("DoorMask: " + name + " 上没有 BoxCollider2D，碰撞体裁剪将跳过。", this);
    }

    /// <summary>给门的 SpriteRenderer 换上带 Y 裁剪的材质（用 MPB 传裁剪线，不需要每门一个材质）</summary>
    private void SetupClippingMaterial()
    {
        if (_renderers.Count == 0)
        {
            Debug.LogWarning("DoorMask: " + name + " 的自身和子物体上都没有 SpriteRenderer，无法裁剪。", this);
            return;
        }

        Shader shader = Shader.Find("Custom/DoorClip");
        if (shader == null)
        {
            Debug.LogError("DoorMask: 找不到着色器 Custom/DoorClip。请确认 Assets/Shaders/DoorClip.shader 已导入且编译通过。", this);
            return;
        }

        // 以门原材质为基础 new 一个，保留贴图等设置，只换 shader
        Material baseMat = _renderers[0].sharedMaterial;
        _clipMat = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Sprites/Default"));
        _clipMat.shader = shader;

        _mpb = new MaterialPropertyBlock();
        _mpb.SetFloat("_ClipFade", Mathf.Max(0.0001f, clipFade));

        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].sharedMaterial = _clipMat;
        }

        ApplyClipLine(_clipLine);
    }

    private void ApplyClipLine(float worldY)
    {
        if (_renderers.Count == 0 || _mpb == null) return;

        _mpb.SetFloat("_ClipLineY", worldY);

        // 统一走 RefreshRenderers 施加（材质 + 裁剪线一起补），避免两处逻辑不一致
        RefreshRenderers();
    }

    /// <summary>
    /// 重新收集"门自身 + 子物体"上的 SpriteRenderer，并把裁剪材质与裁剪线补给新出现的渲染器。<br/>
    /// 原实现只在 Awake 收集一次，所以给门<b>加子物体</b>（或换渲染器/换贴图导致渲染器变化）之后，
    /// 新渲染器不会被裁剪：它既不受裁剪线约束（向上超出门顶的部分不消失），
    /// 也会按自己的排序值画在门前面 —— 表现就是"加贴图或子物体后显示异常"。<br/>
    /// GetComponentsInChildren 的 List 重载不产生 GC，可以每帧调用。
    /// </summary>
    private void RefreshRenderers()
    {
        if (_clipMat == null || _mpb == null) return;

        Part.GetComponentsInChildren(true, _renderers);

        for (int i = 0; i < _renderers.Count; i++)
        {
            SpriteRenderer sr = _renderers[i];
            if (sr == null) continue;

            if (sr.sharedMaterial != _clipMat) sr.sharedMaterial = _clipMat;
            sr.SetPropertyBlock(_mpb);
        }
    }

    // LateUpdate：确保在所有门的驱动脚本移动之后再更新，否则会慢一帧
    private void LateUpdate()
    {
        if (!_ready) return;

        // 每帧把"门 + 子物体"上的渲染器补齐裁剪材质与裁剪线：
        // 这样给门加子物体（或改变渲染器/贴图）后，新渲染器会立刻按同一条线裁剪，
        // 不会再出现"加子物体后显示异常"。
        RefreshRenderers();

        float travel = Part.position.y - _startY;

        // 开关门音效：方向一变就播一次（上升=开门、下降=关门）
        UpdateMoveSound();

        // 裁剪分界线固定在"初始门顶"的世界高度：门升上去才会被裁，落下来自动恢复。
        // 只有门真的动了才需要重建碰撞体（用 travel 判断，门静止时完全不碰物理）。
        if (trimCollider && _box != null && Mathf.Abs(travel) > 0.0001f)
            TrimCollider(travel);

        if (debugLog && Time.unscaledTime - _lastLogTime >= 1f)
        {
            _lastLogTime = Time.unscaledTime;
            Debug.Log(string.Format(
                "[DoorMask] {0} 位移={1:F2} 裁剪线Y={2:F2} 门顶Y={3:F2} 碰撞体启用={4} 剩余高={5:F2} 材质={6}",
                name, travel, _clipLine, _clipLine + travel,
                _box != null && _box.enabled,
                _box != null && _box.enabled ? _box.size.y * _origScaleY : 0f,
                _clipMat != null ? _clipMat.shader.name : "未接管"), this);
        }
    }

    /// <summary>
    /// 开关门音效：只看"门正在往上还是往下走"，方向一变就播一次。<br/>
    /// 门停在半路再朝同方向继续时不会重复播（<c>_moveDir</c> 只在真的动了的时候更新，
    /// 停下时保留上次方向）；门静止时完全不播。
    /// </summary>
    private void UpdateMoveSound()
    {
        float y = Part.position.y;
        float dy = y - _lastSoundY;
        _lastSoundY = y;

        // 即使关掉音效也要记录高度，否则中途打开会按累积位移误判一次方向
        if (!playMoveSound) return;

        int dir = dy > MoveEpsilon ? 1 : (dy < -MoveEpsilon ? -1 : 0);
        if (dir == 0) return;        // 这一帧没动，保持上次方向

        if (dir == _moveDir) return; // 还在朝同一个方向走：不重复播
        _moveDir = dir;

        musicmanager.PlayShotSound(dir > 0 ? riseShotIndex : fallShotIndex, moveSoundPitch);
    }

    /// <summary>把碰撞体中"越过分界线"的部分切掉，底边保持不动</summary>
    private void TrimCollider(float travel)
    {
        // 直接由位移算剩余高度，不用 _box.bounds（避免依赖 SyncTransforms，也避免和物理互相干扰）
        float origWorldH = Mathf.Abs(_origSize.y * _origScaleY);
        float remain = origWorldH - travel;

        if (remain <= minRemainHeight)
        {
            if (!_colliderDisabled)
            {
                _box.enabled = false;
                _colliderDisabled = true;
                Physics2D.SyncTransforms();
            }
            return;
        }

        // 高度变化小于阈值就不重建碰撞体：每帧改 size/offset + SyncTransforms
        // 会让正贴着门的玩家刚体反复被重新求解，表现为剧烈抖动
        if (!_colliderDisabled && Mathf.Abs(remain - _lastAppliedHeight) < rebuildDelta)
            return;

        if (_colliderDisabled)
        {
            _box.enabled = true;
            _colliderDisabled = false;
        }

        // 每次都由原始值重算，绝不在当前值上累减。
        //
        // 关键：底边必须保持不动。
        //   原始底边（局部）= _origOffset.y - _origSize.y * 0.5f
        //   新 offset.y     = 原始底边 + 新高度 * 0.5f
        // 旧写法 (remain * 0.5f) / _origScaleY 相当于把底边对到了门的**中心**：
        // 门预制体是 scale.y = 4、collider size.y = 1，remain=原高 4 时旧式算出 offset.y = 0.5，
        // 而正确值是 0 —— 碰撞体整体上移 0.5 局部 = 2 个世界单位，门的下半截不再挡人，玩家直接穿过去。
        // 而且门落回原位时它仍算出 0.5，所以是**永久**错位，不是抖动。
        float newSizeY = remain / _origScaleY;
        float bottomLocal = _origOffset.y - _origSize.y * 0.5f;

        _box.size = new Vector2(_origSize.x, newSizeY);
        _box.offset = new Vector2(_origOffset.x, bottomLocal + newSizeY * 0.5f);
        _lastAppliedHeight = remain;

        Physics2D.SyncTransforms();
    }

    private void OnDestroy()
    {
        // 只销毁自己 new 出来的材质，不碰原始资源
        if (_clipMat != null)
            Destroy(_clipMat);
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D col = Part.GetComponent<Collider2D>();
        if (col == null) return;

        float line = float.IsNaN(maskLineY) ? col.bounds.max.y : maskLineY;
        float halfWidth = col.bounds.extents.x + 0.05f;

        // 红线 = 裁剪分界线，线上方会被裁掉
        Gizmos.color = new Color(1f, 0.25f, 0.25f, 1f);
        Gizmos.DrawLine(new Vector3(col.bounds.center.x - halfWidth, line, 0f),
                        new Vector3(col.bounds.center.x + halfWidth, line, 0f));

        Gizmos.color = new Color(1f, 0.6f, 0.6f, 0.5f);
        Gizmos.DrawWireCube(
            new Vector3(col.bounds.center.x, line + 2f, 0f),
            new Vector3(halfWidth * 2f, 4f, 0.01f));
    }
}
