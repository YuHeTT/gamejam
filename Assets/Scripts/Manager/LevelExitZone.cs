using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>通关判定区：把本组件挂在一个带长方形 BoxCollider2D（勾选 Is Trigger）的空物体上。<br/>
/// 玩家穿过判定区域即视作通过本关：直接加载 Build Settings 中「当前场景索引 + buildIndexStep」的场景（暂定为 +1）。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class LevelExitZone : MonoBehaviour
{
    [Tooltip("加载的场景索引增量：下一关 = 当前 buildIndex + 该值（暂定 1）")]
    public int buildIndexStep = 1;

    [Tooltip("关闭后本判定区不再触发通关")]
    public bool active = true;

    //防止同一帧/同一关内重复触发
    private bool triggered;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryWin(other);

    private void OnTriggerStay2D(Collider2D other) => TryWin(other);

    private void TryWin(Collider2D other)
    {
        if (!active || triggered || other == null) return;

        //只对玩家生效（复制体是 PlayerClone，不算通关）
        if (other.GetComponentInParent<Player>() == null) return;

        Scene current = SceneManager.GetActiveScene();
        if (!current.IsValid() || current.buildIndex < 0)
        {
            Debug.LogWarning(
                $"LevelExitZone: 当前场景 '{current.name}' 未加入 Build Settings，无法按索引跳转。请把各关卡场景加入 Build Settings。", this);
            return;
        }

        int next = current.buildIndex + buildIndexStep;
        int total = SceneManager.sceneCountInBuildSettings;
        if (next < 0 || next >= total)
        {
            Debug.LogWarning($"LevelExitZone: 下一关索引 {next} 超出范围（Build Settings 共 {total} 个场景）。", this);
            return;
        }

        triggered = true;
        SceneManager.LoadScene(next);
    }
}