using UnityEngine;

/// <summary>
/// 天平配平后显现胜利点。<br/><br/>
/// 挂在<b>某一个</b>天平（挂 <see cref="BalanceElevator"/> 的那个物体）上，
/// 只在 Inspector 里分别指定"这一个天平"和"这一个胜利点"，
/// 因此不会影响场景里其它的天平 —— 每个天平各挂一个自己的追踪器即可。<br/><br/>
/// 逻辑：当天平连续保持配平 <see cref="revealDelay"/> 秒后，让胜利点显现；
/// 中途配平被打破则计时清零（不是累计，避免"抖一下就算过"）。
/// </summary>
public class BalanceVictoryTrigger : MonoBehaviour
{
    [Header("关联对象")]
    [Tooltip("要监视的天平；留空则自动用本物体上的 BalanceElevator")]
    public BalanceElevator elevator;

    [Tooltip("配平后要显现的物体（other2 里就是被隐藏的胜利点）")]
    public GameObject victoryPoint;

    [Header("判定")]
    [Tooltip("需要连续保持配平的秒数")]
    public float revealDelay = 1.5f;

    [Tooltip("开始时先把胜利点藏起来（与场景里 m_IsActive=false 的设定一致）")]
    public bool hideOnStart = true;

    [Tooltip("在 Console 打印配平进度，便于调试")]
    public bool logProgress = false;

    private float _balancedTimer;

    private void Awake()
    {
        if (elevator == null)
            elevator = GetComponent<BalanceElevator>();

        if (elevator == null)
        {
            Debug.LogError("BalanceVictoryTrigger: 没有指定天平（BalanceElevator），脚本已停用。", this);
            enabled = false;
            return;
        }

        if (victoryPoint == null)
        {
            Debug.LogError("BalanceVictoryTrigger: 没有指定要显现的胜利点，脚本已停用。", this);
            enabled = false;
            return;
        }

        if (hideOnStart) SetVictoryVisible(false);
    }

    private void Update()
    {
        // 用 Time.deltaTime（受 timeScale 影响）：打开设置界面时 timeScale=0，计时应当暂停。
        if (elevator.IsBalanced)
        {
            _balancedTimer += Time.deltaTime;

            if (logProgress && _balancedTimer < revealDelay)
                Debug.Log("[天平胜利点] 配平中 " + _balancedTimer.ToString("F2") + " / " +
                          revealDelay.ToString("F2") + " 秒", this);

            if (_balancedTimer >= revealDelay) SetVictoryVisible(true);
        }
        else
        {
            if (_balancedTimer > 0f)
            {
                _balancedTimer = 0f;
                SetVictoryVisible(false);
            }
        }
    }

    /// <summary>显示/隐藏胜利点（与预制体实例上的 m_IsActive 覆盖同一套机制）</summary>
    public void SetVictoryVisible(bool visible)
    {
        if (victoryPoint == null) return;
        if (victoryPoint.activeSelf == visible) return;

        victoryPoint.SetActive(visible);

        if (visible)
            Debug.Log("[天平胜利点] 天平已保持配平 " + revealDelay.ToString("F2") +
                      " 秒，显现：" + victoryPoint.name, this);
    }

    /// <summary>重置计时并重新隐藏（需要重来一遍时调用）</summary>
    public void ResetTrigger()
    {
        _balancedTimer = 0f;
        SetVictoryVisible(false);
    }
}
