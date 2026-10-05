using UnityEngine;

/// <summary>全局唯一墓碑：放置、替换、查询传送点。场景中放一个并指定 prefab（含 TombstoneMarker）。</summary>
public class TombstoneService : MonoBehaviour
{
    public static TombstoneService Instance { get; private set; }

    [Header("Prefab")]
    [Tooltip("须带 TombstoneMarker；美术由你在编辑器指定")]
    public GameObject tombstonePrefab;

    [Header("Placement")]
    [Tooltip("相对地面命中点的额外偏移（例如让碑底贴地）")]
    public Vector2 placementOffset = Vector2.zero;

    public bool HasActiveTombstone => activeMarker != null;
    public TombstoneMarker ActiveMarker => activeMarker;

    private TombstoneMarker activeMarker;
    private GameObject activeInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("场景中存在多个 TombstoneService，销毁重复项。", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>在玩家脚下地面放置墓碑；若已有则销毁旧的。仅当 grounded 时由道具调用。</summary>
    public bool TryPlaceAtPlayerFeet(Player player)
    {
        if (tombstonePrefab == null)
        {
            Debug.LogWarning("[墓] TombstoneService 未指定 tombstonePrefab。", this);
            return false;
        }

        if (player == null || !player.IsGroundDetected())
            return false;

        if (!player.TryGetGroundHit(out RaycastHit2D hit, ignoreTombstones: true)
            && !player.TryGetGroundHit(out hit))
        {
            hit = default;
        }

        Vector2 pos = hit.collider != null
            ? hit.point + placementOffset
            : (Vector2)player.GroundCheck.position + placementOffset;

        ReplaceTombstone(pos);
        return true;
    }

    public Vector2 GetTeleportFeetPosition()
    {
        return activeMarker != null ? activeMarker.FeetWorldPosition : Vector2.zero;
    }

    private void ReplaceTombstone(Vector2 worldPosition)
    {
        if (activeInstance != null)
            DestroyImmediate(activeInstance);

        activeMarker = null;
        activeInstance = Instantiate(tombstonePrefab, worldPosition, Quaternion.identity);
        activeMarker = activeInstance.GetComponent<TombstoneMarker>();
        if (activeMarker == null)
            activeMarker = activeInstance.AddComponent<TombstoneMarker>();

        int tombLayer = LayerMask.NameToLayer("Tombstone");
        if (tombLayer >= 0)
            SetLayerRecursively(activeInstance, tombLayer);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
