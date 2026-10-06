using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「募」的复制体计数显示：玩家持有「募」时，在玩家头顶显示一排小点。<br/>
/// 点数量 = 剩余可用复制体数（CloneManager.maxClones - 已生成数），并按其数量水平居中。<br/>
/// 剩余为 0 或未持有「募」时完全隐藏（视觉上没有任何显示）。<br/>
/// 点图片由 Inspector 指定；位置可用 offset 微调，间距用 spacing 调整。
/// </summary>
[DisallowMultipleComponent]
public class CloneCounterDisplay : MonoBehaviour
{
    [Header("Refs（留空自动查找）")]
    [Tooltip("玩家，留空自动向上查找")]
    public Player player;
    [Tooltip("道具控制器，留空自动向上查找")]
    public PlayerItemController itemController;
    [Tooltip("显示锚点（玩家视觉），留空自动用 player.anim.transform")]
    public Transform anchor;

    [Header("中心点（世界空间）")]
    [Tooltip("计数点显示的世界空间中心点：手动拖入一个空物体来确定；留空则回退到玩家视觉头顶")]
    public Transform centerPoint;

    [Header("点素材")]
    [Tooltip("小点的图片（资产由你在 Inspector 里拖入）")]
    public Sprite dotSprite;

    [Header("布局")]
    [Tooltip("点与点之间的水平间距（世界单位）")]
    public float spacing = 0.35f;
    [Tooltip("整体位置偏移（世界空间：x 左右、y 上下），用于微调")]
    public Vector2 offset = new Vector2(0f, 0.15f);
    [Tooltip("每个点的大小缩放")]
    public float dotScale = 1f;
    [Tooltip("渲染排序值（需大于玩家视觉的排序值才会显示在玩家之上）")]
    public int sortingOrder = 12;

    //运行时创建的点容器与点
    private Transform container;
    private readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();

    //玩家视觉的 SpriteRenderer：用来取"头顶"位置
    private SpriteRenderer anchorRenderer;
    private bool warnedNoSprite;

    private void Awake()
    {
        ResolveRefs();
    }

    private void OnDisable()
    {
        HideAll();
    }

    private void OnDestroy()
    {
        //容器不挂在玩家下，组件销毁时手动清理，避免残留
        if (container != null) Destroy(container.gameObject);
    }

    private void LateUpdate()
    {
        ResolveRefs();

        int total, remaining;
        GetCounts(out total, out remaining);

        if (total <= 0 || remaining <= 0)
        {
            HideAll();
            return;
        }

        if (dotSprite == null)
        {
            if (!warnedNoSprite)
            {
                warnedNoSprite = true;
                Debug.LogWarning("CloneCounterDisplay: 没有指定点图片（Dot Sprite），头顶计数不会显示。", this);
            }
            HideAll();
            return;
        }

        EnsureDots(total);
        LayoutDots(remaining);
    }

    /// <summary>取"总上限 / 剩余可用数"：只有持有「募」且复制体未用满时才有剩余</summary>
    private void GetCounts(out int total, out int remaining)
    {
        total = 0;
        remaining = 0;

        if (itemController == null || itemController.CurrentItem == null) return;
        if (!(itemController.CurrentItem is SummonItem)) return;   //只对「募」显示

        CloneManager manager = CloneManager.Instance;
        if (manager == null) return;

        total = Mathf.Max(0, manager.maxClones);
        remaining = Mathf.Clamp(total - manager.CloneCount, 0, total);
    }

    /// <summary>点数量变化时重建（maxClones 极少变动，直接重建最省心）</summary>
    private void EnsureDots(int total)
    {
        EnsureContainer();
        if (dots.Count == total) return;

        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] != null) Destroy(dots[i].gameObject);
        }
        dots.Clear();

        for (int i = 0; i < total; i++)
        {
            GameObject go = new GameObject("CloneDot" + i);
            go.transform.SetParent(container, false);
            go.transform.localScale = Vector3.one * dotScale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dotSprite;
            sr.sortingOrder = sortingOrder;
            if (anchorRenderer != null) sr.sortingLayerID = anchorRenderer.sortingLayerID;

            dots.Add(sr);
        }
    }

    /// <summary>只显示前 remaining 个点，并让它们以玩家头顶为中心水平居中</summary>
    private void LayoutDots(int remaining)
    {
        float startX = -(remaining - 1) * 0.5f * spacing;

        for (int i = 0; i < dots.Count; i++)
        {
            SpriteRenderer dot = dots[i];
            if (dot == null) continue;

            bool show = i < remaining;
            dot.gameObject.SetActive(show);
            if (!show) continue;

            dot.transform.localPosition = new Vector3(startX + i * spacing, 0f, 0f);
        }

        //定位中心点：优先用手动拖入的世界中心点，取不到再回退到玩家视觉头顶
        Vector3 basePos;
        if (centerPoint != null)
        {
            basePos = new Vector3(centerPoint.position.x, centerPoint.position.y, transform.position.z);
        }
        else if (anchorRenderer != null)
        {
            Bounds b = anchorRenderer.bounds;
            basePos = new Vector3(b.center.x, b.max.y, transform.position.z);
        }
        else if (anchor != null)
        {
            basePos = new Vector3(anchor.position.x, anchor.position.y, transform.position.z);
        }
        else
        {
            basePos = transform.position;
        }

        container.position = basePos + new Vector3(offset.x, offset.y, 0f);
    }

    private void HideAll()
    {
        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] != null) dots[i].gameObject.SetActive(false);
        }
    }

    private void EnsureContainer()
    {
        if (container != null) return;

        //不挂在玩家下：玩家翻转（绕 Y 旋转 180°）时计数点不会跟着翻转
        GameObject go = new GameObject("CloneCounterDots");
        container = go.transform;
    }

    private void ResolveRefs()
    {
        if (player == null) player = GetComponentInParent<Player>();
        if (itemController == null) itemController = GetComponentInParent<PlayerItemController>();

        if (anchor == null && player != null && player.anim != null)
            anchor = player.anim.transform;

        if (anchorRenderer == null && anchor != null)
            anchorRenderer = anchor.GetComponent<SpriteRenderer>();
    }
}