using System.Collections;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    private Coroutine freezeRoutine;
    private float freezeUntil;
    private float restoreTimeScale = 1f;

    public static bool IsFrameFrozen => Instance != null && Instance.freezeRoutine != null;

    private void Awake() => Instance = this;

    /// <summary>
    /// 帧冻结 —— 传入冻结时长（秒）
    /// </summary>
    public static void FrameFreeze(float duration)
    {
        if (Instance == null || duration <= 0f)
        {
            return;
        }

        Instance.freezeUntil = Mathf.Max(Instance.freezeUntil,Time.realtimeSinceStartup + duration);
        if (Instance.freezeRoutine == null)
        {
            Instance.freezeRoutine =Instance.StartCoroutine(Instance.Freeze());
        }
    }

    private IEnumerator Freeze()
    {
        restoreTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        while (Time.realtimeSinceStartup < freezeUntil)
        {
            yield return null;
        }

        Time.timeScale = restoreTimeScale;
        freezeUntil = 0f;
        freezeRoutine = null;
    }
}
