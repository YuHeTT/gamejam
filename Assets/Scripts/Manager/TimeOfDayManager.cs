using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>全局昼夜状态（默认白天）。<br/>
/// 拾取「暮」→ 黑夜；丢弃「暮」→ 白天。<br/>
/// 玩家 / 场景 / 其他实体各自的昼夜差异效果，统一在 <see cref="ApplyTimeOfDay"/> 中接入。</summary>
public static class TimeOfDayManager
{
    /// <summary>true = 黑夜，false = 白天。游戏默认白天。</summary>
    public static bool IsNight { get; private set; }

    /// <summary>昼夜切换广播，参数 true 表示进入黑夜。玩家/场景/实体可在 OnEnable 订阅、OnDisable 退订。</summary>
    public static event Action<bool> OnTimeChanged;

    /// <summary>切换昼夜。同一状态重复调用不会重复广播。</summary>
    public static void SetNight(bool night)
    {
        if (IsNight == night)
        {
            Debug.Log($"[昼夜] 未切换：当前已经是{(night ? "黑夜" : "白天")}");
            return;
        }

        IsNight = night;
        Debug.Log($"[昼夜] 切换成功：{(night ? "白天 → 黑夜" : "黑夜 → 白天")}");
        ApplyTimeOfDay(night);
    }

    /// <summary>应用昼夜表现。玩家 / 场景 / 实体的具体差异效果在此填写。</summary>
    private static void ApplyTimeOfDay(bool night)
    {
        // TODO: 在此接入白天 / 黑夜各自的实际效果（玩家、场景、实体）。

        OnTimeChanged?.Invoke(night);
    }

    /// <summary>关闭"域重载"时静态状态不会自动清空，这里手动复位，避免残留黑夜状态或失效订阅。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsNight = false;
        OnTimeChanged = null;

        //重新加载关卡（死亡重开、重进关卡）不会重置静态字段，需要在每次场景加载后把昼夜复位成白天，
        //否则上一局的"黑夜"会被带到新一局。这里挂一次场景加载回调（静态事件跨场景存活，不会重复累积）。
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    /// <summary>场景加载完成：把昼夜复位成默认的白天（本来就是白天时无操作）</summary>
    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetNight(false);
    }
}