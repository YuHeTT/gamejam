using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色动画组切换：玩家持有道具时，用该道具提供的动画槽位（静止/移动/跳跃/冲刺）
/// 直接在运行时生成一个 AnimatorOverrideController，套用到角色**最初始的动画机**上。<br/><br/>
/// 也就是说：状态机结构、参数名、状态名、按键逻辑全部沿用原来的 Player_AC，
/// 只把 playerIdle / playerMove / playerJump / playerFall / playerDash
/// 这几段动画换成道具槽位里的片段。<br/><br/>
/// 复制体会跟随玩家一起换装；换控制器会重置 Animator 参数，这里按当前状态把动画布尔重新置位，
/// 避免出现“正在跑却播静止”。
/// </summary>
[DisallowMultipleComponent]
public class PlayerAnimationSet : MonoBehaviour
{
    [Header("Refs（留空自动查找）")]
    [Tooltip("玩家，留空自动向上查找")]
    public Player player;
    [Tooltip("道具控制器，留空自动向上查找")]
    public PlayerItemController itemController;

    [Header("调试")]
    [Tooltip("打印动画组套用/回退的日志，排查用")]
    public bool debugLog = false;

    private Animator anim;
    private RuntimeAnimatorController defaultController;

    // 每个道具只构建一次覆盖控制器，避免反复 new 造成泄漏
    private readonly Dictionary<Item, AnimatorOverrideController> overrideCache
        = new Dictionary<Item, AnimatorOverrideController>();
    private readonly List<KeyValuePair<AnimationClip, AnimationClip>> overrideBuffer
        = new List<KeyValuePair<AnimationClip, AnimationClip>>();

    private void Awake()
    {
        // 不依赖 Player.anim 的解析时序：直接从子物体取 Animator，并缓存序列化进来的默认控制器
        anim = GetComponentInChildren<Animator>();
        if (anim != null)
            defaultController = UnwrapController(anim.runtimeAnimatorController);

        ResolveRefs();
    }

    private void OnEnable()
    {
        ResolveRefs();
        if (itemController == null) return;

        itemController.OnItemChanged += OnItemChanged;
        Apply(itemController.CurrentItem);     // 启用时按当前道具同步一次
    }

    private void OnDisable()
    {
        if (itemController != null)
            itemController.OnItemChanged -= OnItemChanged;
    }

    private void OnDestroy()
    {
        // 运行时 new 出来的覆盖控制器要自己销毁，否则会随场景重载泄漏
        foreach (KeyValuePair<Item, AnimatorOverrideController> kv in overrideCache)
        {
            if (kv.Value != null) Destroy(kv.Value);
        }
        overrideCache.Clear();
    }

    /// <summary>按当前道具应用动画组：没有可用槽位时恢复默认动画</summary>
    public void Apply(Item item)
    {
        if (defaultController == null)
        {
            if (debugLog) Debug.LogWarning("PlayerAnimationSet: 角色 Animator 没有控制器，无法换装。", this);
            return;
        }

        RuntimeAnimatorController target = defaultController;

        if (item != null && HasAnyClip(item.animationClips))
        {
            AnimatorOverrideController built = GetOrBuildOverride(item);
            if (built != null) target = built;
        }

        SetController(target);
    }

    private void OnItemChanged(Item item) => Apply(item);

    private static bool HasAnyClip(ItemAnimationSet set)
    {
        return set != null
            && (set.idle != null || set.move != null || set.jump != null || set.dash != null);
    }

    private AnimatorOverrideController GetOrBuildOverride(Item item)
    {
        if (overrideCache.TryGetValue(item, out AnimatorOverrideController cached) && cached != null)
            return cached;

        AnimatorOverrideController built = BuildOverride(item.animationClips);
        if (built != null) overrideCache[item] = built;
        return built;
    }

    /// <summary>
    /// 用角色最初始的动画机建一个覆盖控制器，只把 静止/移动/跳跃 对应的原始片段换成道具的动画。
    /// 未填的槽位保持原片段不动。
    /// </summary>
    private AnimatorOverrideController BuildOverride(ItemAnimationSet set)
    {
        AnimatorOverrideController ovc = new AnimatorOverrideController(defaultController);

        overrideBuffer.Clear();
        ovc.GetOverrides(overrideBuffer);

        // 部分情况下 GetOverrides 会是空的，改用控制器上的全部原始片段
        if (overrideBuffer.Count == 0 && defaultController != null)
        {
            AnimationClip[] originals = defaultController.animationClips;
            for (int i = 0; i < originals.Length; i++)
            {
                AnimationClip original = originals[i];
                if (original != null)
                    overrideBuffer.Add(new KeyValuePair<AnimationClip, AnimationClip>(original, original));
            }
        }

        bool anyApplied = false;

        for (int i = 0; i < overrideBuffer.Count; i++)
        {
            AnimationClip original = overrideBuffer[i].Key;
            if (original == null) continue;

            AnimationClip replacement = MatchClip(original.name, set);
            if (replacement == null) continue;   // 该槽位没填 → 保持原动画

            overrideBuffer[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
            ovc[original] = replacement;
            anyApplied = true;
        }

        if (!anyApplied)
        {
            Debug.LogWarning("PlayerAnimationSet: 道具动画没有匹配到角色动画机里的任何原始片段。" +
                             "请确认道具的动画槽位已填写（原始片段名：" + ListOriginalNames() + "）。", this);
            return null;
        }

        ovc.ApplyOverrides(overrideBuffer);

        if (debugLog) Debug.Log("PlayerAnimationSet: 已按道具动画生成覆盖控制器。", this);
        return ovc;
    }

    /// <summary>
    /// 按原始片段名匹配槽位。跳跃状态在动画机里是 yVelocity 混合树，
    /// 其子片段 playerJump / playerFall 都归到 jump 槽位（上升与下落共用同一段）。
    /// </summary>
    private static AnimationClip MatchClip(string clipName, ItemAnimationSet set)
    {
        if (string.IsNullOrEmpty(clipName)) return null;

        if (Contains(clipName, "Jump") || Contains(clipName, "Fall")) return set.jump;
        if (Contains(clipName, "Dash")) return set.dash;
        if (Contains(clipName, "Idle")) return set.idle;
        if (Contains(clipName, "Move")) return set.move;
        return null;
    }

    private static bool Contains(string src, string token)
        => src.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;

    private string ListOriginalNames()
    {
        List<string> names = new List<string>();
        foreach (KeyValuePair<AnimationClip, AnimationClip> pair in overrideBuffer)
        {
            if (pair.Key != null) names.Add(pair.Key.name);
        }
        return string.Join(", ", names);
    }

    private void SetController(RuntimeAnimatorController controller)
    {
        Animator a = Anim;
        if (a == null || controller == null) return;
        if (a.runtimeAnimatorController == controller) return;   // 没变化就不动，避免无谓的状态重置

        a.runtimeAnimatorController = controller;

        // 换控制器会重置 Animator 参数并回到默认状态：按当前状态重新置位并立刻切到对应状态
        if (player != null && player.stateMachine != null && player.stateMachine.currentState != null)
        {
            string boolName = player.stateMachine.currentState.AnimBoolName;
            if (!string.IsNullOrEmpty(boolName))
            {
                a.SetBool("Idle", false);
                a.SetBool("Move", false);
                a.SetBool("Jump", false);
                a.SetBool("Dash", false);
                a.SetBool("PickUp", false);
                a.SetBool(boolName, true);
                a.Play(StateNameForBool(boolName), 0, 0f);
            }
        }
        if (player != null && player.rb != null)
            a.SetFloat("yVelocity", player.rb.velocity.y);

        // 复制体跟随玩家换装（之后新生成的复制体会自动继承玩家当前动画组）
        CloneManager manager = CloneManager.Existing;
        if (manager != null)
            manager.ApplyAnimatorController(controller);
    }

    private Animator Anim
    {
        get
        {
            if (anim == null)
                anim = (player != null && player.anim != null)
                    ? player.anim
                    : GetComponentInChildren<Animator>();
            return anim;
        }
    }

    private void ResolveRefs()
    {
        if (player == null) player = GetComponentInParent<Player>();
        if (itemController == null) itemController = GetComponentInParent<PlayerItemController>();
    }

    private static RuntimeAnimatorController UnwrapController(RuntimeAnimatorController controller)
    {
        while (controller is AnimatorOverrideController nested && nested.runtimeAnimatorController != null)
            controller = nested.runtimeAnimatorController;
        return controller;
    }

    private static string StateNameForBool(string boolName)
    {
        switch (boolName)
        {
            case "Move": return "playerMove";
            case "Jump": return "Jump/Fall";
            case "Dash": return "playerDash";
            case "PickUp": return "playerPickUp";
            default: return "playerIdle";
        }
    }
}