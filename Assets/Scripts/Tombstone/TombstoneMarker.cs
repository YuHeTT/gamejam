using UnityEngine;

/// <summary>场景中的墓碑标记：仅作传送锚点与放置物，不参与与玩家/道具等的交互逻辑。<br/>
/// 请在 Physics 2D 碰撞矩阵中让 Tombstone 层只与 Ground 碰撞（若使用 Collider）。</summary>
[DisallowMultipleComponent]
public class TombstoneMarker : MonoBehaviour
{
    [Tooltip("传送后玩家脚底应对齐的世界点；留空则用本物体 Transform 位置")]
    [SerializeField] private Transform feetAnchor;

    /// <summary>传送目标：玩家 groundCheck 应落在此世界坐标</summary>
    public Vector2 FeetWorldPosition =>
        feetAnchor != null ? (Vector2)feetAnchor.position : (Vector2)transform.position;

    private void Reset()
    {
        feetAnchor = transform;
    }
}
