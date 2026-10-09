using UnityEngine;

/// <summary>场上存在墓碑时按 W 传送（与是否持有「墓」无关）。</summary>
[DisallowMultipleComponent]
public class PlayerGraveTeleportController : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private PlayerTombstoneTeleport tombstoneTeleport;
    public KeyCode teleportKey = KeyCode.W;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<Player>();
        if (tombstoneTeleport == null)
            tombstoneTeleport = GetComponent<PlayerTombstoneTeleport>();
    }

    private void Update()
    {
        // 暂停（设置面板打开）时不接受传送输入
        if (GamePause.IsPaused)
            return;

        if (player == null || tombstoneTeleport == null)
            return;

        if (tombstoneTeleport.IsTeleporting)
            return;

        if (Input.GetKeyDown(teleportKey))
            tombstoneTeleport.TryBeginTeleport();
    }
}
