using UnityEngine;

/// <summary>玩家死亡判定区：把本组件挂在一个带长方形 BoxCollider2D（勾选 Is Trigger）的空物体上。<br/>
/// 玩家碰到判定框即判定死亡：<br/>
/// - 场上已有墓碑 → 立刻传送到墓碑并播放显现动画（与墓碑传送后半段一致）；<br/>
/// - 否则 → 屏幕从上往下拉黑 → 全黑期间还原到初始场景状态 → 从上往下显现画面。<br/>
/// 判定框本身不参与物理，只做重叠检测（玩家带 Dynamic 刚体，触发回调可用）。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PlayerDeathZone : MonoBehaviour
{
    [Tooltip("勾选后自动把本碰撞体设为 Trigger")]
    public bool forceTrigger = true;

    [Tooltip("关闭后本判定区不再造成死亡")]
    public bool active = true;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        if (forceTrigger)
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => TryKill(other);

    private void OnTriggerStay2D(Collider2D other) => TryKill(other);

    private void TryKill(Collider2D other)
    {
        if (!active || other == null) return;

        //只对玩家生效（复制体是 PlayerClone，不触发死亡）
        Player player = other.GetComponentInParent<Player>();
        if (player == null) return;

        PlayerDeathManager.Instance.TriggerDeath(player);
    }
}