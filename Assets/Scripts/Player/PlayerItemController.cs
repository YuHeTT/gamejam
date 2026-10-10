using System;
using UnityEngine;

/// <summary>玩家道具栏：J 拾取/交换，I 使用。挂在 Player 上。<br/>
/// 拾取不再瞬时生效：按 J 后播放一段可调的拾取动画（道具举在头顶、玩家与道具一起纵向弹跳），
/// 动画结束道具才正式生效（触发 OnPickUp）。动画期间可左右移动、禁跳、不能用道具；
/// 按 J 可交换（换成附近道具并从头重播）或丢弃（立刻取消动画）。</summary>
public class PlayerItemController : MonoBehaviour
{
    [Header("Refs")]
    public Player player;                // 留空则自动向上查找
    public Transform pickUpCheck;        // 检测中心，留空用自身

    [Header("PickUpInfo")]
    public float pickUpRadius = 1.2f;
    public LayerMask whatIsItem = ~0;    // 建议设为 Item 层；留 Everything 也能靠类型过滤工作
    public float pickUpCooldown = 0.15f; // 拾取/交换后的再拾取冷却
    private float pickUpCooldownTimer;

    [Header("Keys")]
    public KeyCode pickUpKey = KeyCode.J;
    // 使用键由当前道具决定（Item.useKey），默认 I，可被道具重写（如「蓦」用 L）

    [Header("拾取动画")]
    [Tooltip("拾取动画总时长（秒）。需 ≥ 拉伸+压扁+回弹三段之和，多出的部分自动作为保持段")]
    public float pickUpAnimDuration = 1.2f;
    [Tooltip("第一段：纵向拉长、横向缩窄 的过渡时长（秒）")]
    public float stretchTime = 0.2f;
    [Tooltip("第二段：从拉伸过渡到纵向缩窄、横向拉长 的时长（秒）")]
    public float squashTime = 0.3f;
    [Tooltip("第三段：弹回原本缩放 的时长（秒）")]
    public float returnTime = 0.2f;
    [Tooltip("拉伸态：横向缩放")]
    public float stretchScaleX = 0.8f;
    [Tooltip("拉伸态：纵向缩放")]
    public float stretchScaleY = 1.25f;
    [Tooltip("压扁态：横向缩放")]
    public float squashScaleX = 1.25f;
    [Tooltip("压扁态：纵向缩放")]
    public float squashScaleY = 0.8f;
    [Tooltip("动画期间道具被举在玩家头顶的高度（米）")]
    public float holdHeight = 1.2f;
    [Tooltip("道具举过头顶后的额外位置偏移（本地空间：x 左右、y 上下；会随角色朝向自动镜像）")]
    public Vector2 holdOffset = Vector2.zero;
    [Tooltip("动画期间道具的渲染排序值（需大于玩家视觉的排序值才会显示在上层）")]
    public int previewSortingOrder = 11;
    [Tooltip("Animator 里拾取用的 Bool 参数名")]
    public string pickUpParamName = "PickUp";
    [Tooltip("Animator 里拾取状态的名称（交换道具时用它从头重播）")]
    public string pickUpStateName = "playerPickUp";
    [Tooltip("拾取动画期间的视觉尺寸修正倍率：弥补旧图集里角色画得偏小（实测约需 2 倍）。只作用于玩家视觉，不影响道具")]
    public Vector2 pickUpScaleFix = new Vector2(2f, 2f);
    [Tooltip("拾取动画期间的视觉位置补偿（本地空间：x 随角色朝向自动镜像，y 上下）。缩放后脚底/头顶没对齐时才需要调")]
    public Vector2 pickUpOffset = Vector2.zero;

    /// <summary>当前持有的道具</summary>
    public Item CurrentItem { get; private set; }
    public bool HasItem => CurrentItem != null;

    /// <summary>正在播放拾取动画、尚未正式生效的道具</summary>
    private Item pendingItem;
    private float pickUpAnimTimer;

    /// <summary>实际动画时长：三段形变一定走完，多出的时长作为保持段</summary>
    private float PickUpEffectiveDuration =>
        stretchTime + squashTime + returnTime
        + Mathf.Max(0f, pickUpAnimDuration - (stretchTime + squashTime + returnTime));

    //视觉还原缓存：只缩放玩家的"视觉子物体"，绝不缩放带碰撞体的根物体
    private Transform playerVisual;
    private Vector3 playerVisualBaseScale;
    private Vector3 playerVisualBasePosition;
    private bool playerVisualScaleCached;
    private Vector3 itemBaseScale;
    private int itemBaseSortingOrder;

    //拾取帧动画（Animator 状态 playerPickUp）
    private Animator anim;
    private int pickUpParamHash;
    private bool pickUpAnimActive;

    /// <summary>槽位变化广播，未来接 UI 直接订阅</summary>
    public event Action<Item> OnItemChanged;

    private void Awake()
    {
        if (player == null)      player      = GetComponentInParent<Player>();
        if (pickUpCheck == null) pickUpCheck = transform;

        pickUpParamHash = Animator.StringToHash(pickUpParamName);
    }

    private void OnDisable()
    {
        //兜底：组件被禁用/销毁时不要留下卡在预览态的道具
        if (pendingItem != null) CancelPendingPickUp();
        else if (player != null) player.isPickingUp = false;
        StopPickUpAnim();
    }

    private void Update()
    {
        // 暂停（设置面板打开）时完全不响应输入：否则按 J 仍能拾取/交换，
        // 按 I/L 仍能使用道具（含「募」的 S 键），暂停界面就形同虚设。
        if (GamePause.IsPaused) return;

        if (player != null && player.IsTeleporting)
        {
            //传送会打断拾取动画，避免道具卡在预览态
            if (pendingItem != null) CancelPendingPickUp();
            return;
        }

        if (pickUpCooldownTimer > 0) pickUpCooldownTimer -= Time.deltaTime;

        //兜底：pending 道具被外部销毁时自动复位动画状态
        if (pendingItem == null && player != null && player.isPickingUp)
        {
            RestoreVisuals();
            player.isPickingUp = false;
            StopPickUpAnim();
        }

        if (Input.GetKeyDown(pickUpKey)) TryPickUp();

        //拾取动画进行中：不能用道具，也不跑 OnCarriedUpdate
        if (pendingItem != null)
        {
            UpdatePickUpAnimation();
            return;
        }

        if (CurrentItem != null)
        {
            if (Input.GetKeyDown(CurrentItem.useKey)) UseCurrentItem();
            //UseCurrentItem 可能因消耗而清空槽位，需再判一次
            if (CurrentItem != null) CurrentItem.OnCarriedUpdate(player);
        }
    }

    /// <summary>拾取/交换/丢弃。返回是否真的发生了交互。<br/>
    /// 空闲时：空手 + 有道具 → 开始拾取动画；持道具 + 有道具 → 交换（旧道具立即掉落）；持道具 + 无道具 → 丢弃。<br/>
    /// 动画中：有其它道具 → 换成它并重播动画；无道具 → 丢弃待拾取的道具并立刻取消动画。</summary>
    public bool TryPickUp()
    {
        if (pickUpCooldownTimer > 0) return false;

        Item target = FindNearestItem();

        // —— 拾取动画进行中 ——
        if (pendingItem != null)
        {
            if (target == null)
                CancelPendingPickUp();        //附近无道具 → 丢弃，立刻取消动画
            else
                BeginPendingPickUp(target);   //附近有道具 → 换成它并从头重播
            return true;
        }

        // —— 空闲态 ——
        //附近没有可交互的道具：若手上持有道具则丢弃，否则无事发生
        if (target == null || target == CurrentItem)
        {
            if (CurrentItem == null) return false;
            DropCurrentItem();
            return true;
        }

        //手上已有道具：交换时旧道具立即掉回世界（不回滚）
        if (CurrentItem != null)
            DropCurrentItem();

        BeginPendingPickUp(target);
        return true;
    }

    /// <summary>丢弃当前持有的道具</summary>
    private void DropCurrentItem()
    {
        if (CurrentItem == null) return;

        Item old = CurrentItem;
        CurrentItem = null;
        old.ExitCarriedState(player, old.GetDropPosition(player.transform.position));
        musicmanager.PlayShotSound(musicmanager.ShotIndexDrop);
        OnItemChanged?.Invoke(null);
        pickUpCooldownTimer = pickUpCooldown;
    }

    /// <summary>使用当前道具的能力</summary>
    public bool UseCurrentItem()
    {
        if (CurrentItem == null) return false;

        bool consumed = CurrentItem.UseAbility(player);   // 能力实现在这里被调用
        if (consumed)
        {
            Destroy(CurrentItem.gameObject);
            CurrentItem = null;
            OnItemChanged?.Invoke(null);
        }
        return true;
    }

    #region 拾取动画
    /// <summary>玩家身上的 Animator（懒解析）</summary>
    private Animator Anim
    {
        get
        {
            if (anim == null && player != null) anim = player.anim;
            return anim;
        }
    }

    /// <summary>让 Animator 进入拾取状态；restart=true 时从头重播（交换道具）</summary>
    private void PlayPickUpAnim(bool restart)
    {
        Animator a = Anim;
        if (a == null) return;

        if (restart && pickUpAnimActive)
            a.Play(pickUpStateName, 0, 0f);      //已在拾取状态 → 回到第 0 帧重播
        else
            a.SetBool(pickUpParamHash, true);    //进入拾取状态

        pickUpAnimActive = true;
    }

    /// <summary>立刻结束拾取状态（丢弃 / 完成 / 被打断）</summary>
    private void StopPickUpAnim()
    {
        if (!pickUpAnimActive) return;
        pickUpAnimActive = false;

        Animator a = Anim;
        if (a != null) a.SetBool(pickUpParamHash, false);
    }

    /// <summary>开始 / 重播拾取动画：目标道具先进入"预览态"（已持有但未生效，图像举在头顶）</summary>
    private void BeginPendingPickUp(Item target)
    {
        if (target == null) return;

        bool restarting = pendingItem != null;   //动画中换道具 → 从头重播

        //先收拾上一次的视觉与旧 pending
        RestoreVisuals();
        if (pendingItem != null && pendingItem != target)
            pendingItem.ReturnToWorld(DropPositionOf(pendingItem));

        pendingItem = target;
        target.BeginPickUpPreview(player);
        CacheVisualBase(target);
        musicmanager.PlayShotSound(musicmanager.ShotIndexPickUp);

        PlayPickUpAnim(restarting);

        pickUpAnimTimer = Mathf.Max(0.0001f, PickUpEffectiveDuration);
        if (player != null)
        {
            player.isPickingUp    = true;
            player.jumpBufferTimer = 0f;
        }
        pickUpCooldownTimer = pickUpCooldown;
    }

    /// <summary>丢弃待拾取的道具，立刻取消动画</summary>
    private void CancelPendingPickUp()
    {
        Item item = pendingItem;
        RestoreVisuals();
        pendingItem = null;

        if (item != null) item.ReturnToWorld(DropPositionOf(item));
        if (player != null) player.isPickingUp = false;
        StopPickUpAnim();
        pickUpCooldownTimer = pickUpCooldown;
    }

    /// <summary>动画结束：道具正式生效并进入槽位</summary>
    private void CompletePendingPickUp()
    {
        Item item = pendingItem;
        RestoreVisuals();
        pendingItem = null;

        if (player != null) player.isPickingUp = false;
        StopPickUpAnim();
        if (item == null) return;

        //道具拒绝被收下（例如"墓"把自己抛回世界）：动画照播，但不进道具栏、也不触发换装
        if (item.TryRejectPickUp(player)) return;

        item.CompletePickUp(player);      //隐藏图像 + 触发 OnPickUp
        CurrentItem = item;
        OnItemChanged?.Invoke(item);
    }

    /// <summary>每帧驱动拾取动画：道具举在头顶 + 玩家与道具一起挤压拉伸</summary>
    private void UpdatePickUpAnimation()
    {
        float duration = Mathf.Max(0.0001f, PickUpEffectiveDuration);
        pickUpAnimTimer -= Time.deltaTime;
        float elapsed = duration - pickUpAnimTimer;

        GetBounceScale(elapsed, out float sx, out float sy);
        ApplyScale(sx, sy);

        //道具跟随玩家视觉，举在正上方（叠加可调偏移，按角色本地朝向旋转，缩放不影响）
        Transform anchor = playerVisual != null ? playerVisual : (player != null ? player.transform : null);
        if (pendingItem != null && anchor != null)
        {
            Vector3 local = new Vector3(holdOffset.x, holdHeight + holdOffset.y, 0f);
            pendingItem.transform.position = anchor.position + anchor.rotation * local;
        }

        if (pickUpAnimTimer <= 0f)
            CompletePendingPickUp();
    }

    /// <summary>按三个阶段求当前缩放系数：拉伸 → 压扁 → 弹回，之后保持 1,1</summary>
    private void GetBounceScale(float elapsed, out float sx, out float sy)
    {
        float t1 = Mathf.Max(0f, stretchTime);
        float t2 = t1 + Mathf.Max(0f, squashTime);
        float t3 = t2 + Mathf.Max(0f, returnTime);

        if (elapsed < t1)
        {
            float k = t1 > 0f ? elapsed / t1 : 1f;
            sx = Mathf.Lerp(1f, stretchScaleX, k);
            sy = Mathf.Lerp(1f, stretchScaleY, k);
        }
        else if (elapsed < t2)
        {
            float k = (t2 - t1) > 0f ? (elapsed - t1) / (t2 - t1) : 1f;
            sx = Mathf.Lerp(stretchScaleX, squashScaleX, k);
            sy = Mathf.Lerp(stretchScaleY, squashScaleY, k);
        }
        else if (elapsed < t3)
        {
            float k = (t3 - t2) > 0f ? (elapsed - t2) / (t3 - t2) : 1f;
            sx = Mathf.Lerp(squashScaleX, 1f, k);
            sy = Mathf.Lerp(squashScaleY, 1f, k);
        }
        else
        {
            sx = 1f;
            sy = 1f;
        }
    }

    /// <summary>把缩放写进玩家视觉子物体与道具（都不含玩家碰撞体）</summary>
    private void ApplyScale(float sx, float sy)
    {
        var factor = new Vector3(sx, sy, 1f);

        if (playerVisual != null)
        {
            //在 Q 弹三段缩放之上再叠加尺寸修正：旧图集的拾取帧画得比新图集小一圈，
            //这里用 pickUpScaleFix 补齐；只作用于玩家视觉，道具保持原样。
            var fix = new Vector3(pickUpScaleFix.x, pickUpScaleFix.y, 1f);
            playerVisual.localScale = Vector3.Scale(Vector3.Scale(playerVisualBaseScale, fix), factor);

            //位置补偿：偏移写在根物体的本地空间里，而翻转是根物体绕 Y 转 180°，
            //所以 x 会随朝向自动镜像，这里不需要再乘方向。
            playerVisual.localPosition = playerVisualBasePosition
                + new Vector3(pickUpOffset.x, pickUpOffset.y, 0f);
        }

        if (pendingItem != null)
            pendingItem.transform.localScale = Vector3.Scale(itemBaseScale, factor);
    }

    /// <summary>缓存玩家视觉与道具的原始缩放、道具的渲染排序</summary>
    private void CacheVisualBase(Item target)
    {
        if (!playerVisualScaleCached && player != null && player.anim != null)
        {
            playerVisual           = player.anim.transform;   //玩家视觉是根物体下的子物体
            playerVisualBaseScale  = playerVisual.localScale;
            playerVisualBasePosition = playerVisual.localPosition;
            playerVisualScaleCached = true;
        }

        if (target == null) return;

        itemBaseScale = target.transform.localScale;

        SpriteRenderer sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            itemBaseSortingOrder = sr.sortingOrder;
            sr.sortingOrder      = previewSortingOrder;   //保证举在头顶时显示在玩家之上
        }
    }

    /// <summary>还原玩家视觉与道具的缩放、道具的渲染排序（pendingItem 需仍然有效）</summary>
    private void RestoreVisuals()
    {
        if (playerVisual != null)
        {
            playerVisual.localScale    = playerVisualBaseScale;
            playerVisual.localPosition = playerVisualBasePosition;
        }

        if (pendingItem == null) return;

        pendingItem.transform.localScale = itemBaseScale;

        SpriteRenderer sr = pendingItem.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = itemBaseSortingOrder;
    }

    /// <summary>道具放回世界时的落点（以玩家位置为基准）</summary>
    private Vector2 DropPositionOf(Item item)
    {
        Vector2 anchor = player != null ? (Vector2)player.transform.position : (Vector2)item.transform.position;
        return item.GetDropPosition(anchor);
    }
    #endregion

    /// <summary>找最近的可拾取道具</summary>
    private Item FindNearestItem()
    {
        Vector2 center = pickUpCheck.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, pickUpRadius, whatIsItem);

        Item nearest  = null;
        float bestSqr = float.MaxValue;
        foreach (Collider2D hit in hits)
        {
            Item item = hit.GetComponentInParent<Item>();
            if (item == null || item.IsCarried) continue;   // 类型过滤同时排除玩家自身碰撞体

            float sqr = ((Vector2)item.transform.position - center).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; nearest = item; }
        }
        return nearest;
    }

    private void OnDrawGizmosSelected()   // 与 Entity 的 Gizmo 风格一致，便于调半径
    {
        Gizmos.color = Color.yellow;
        Vector3 c = pickUpCheck != null ? pickUpCheck.position : transform.position;
        Gizmos.DrawWireSphere(c, pickUpRadius);
    }
}
