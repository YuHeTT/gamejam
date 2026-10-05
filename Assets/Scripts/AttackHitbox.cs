using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HitStopSettings
{
    [Min(0f)] public float shakeAmount;
    [Min(0f)] public float shakeTime;
    [Min(0f)] public float freezeTime;
}

public class AttackHitbox : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private bool canBeCountered;

    [Header("Hit Stop")]
    [SerializeField] private HitStopSettings hitEffect;
    [SerializeField] private HitStopSettings counterEffect;
    [SerializeField] private HitStopSettings clashEffect;

    private readonly HashSet<Entity> hitTargets = new();
    private readonly Collider2D[] overlapResults = new Collider2D[16];
    private PolygonCollider2D hitbox;
    private Entity ownerEntity;
    private ContactFilter2D overlapFilter;
    private bool isActive;

    public Entity Owner => ownerEntity;
    public bool CanBeCountered => canBeCountered;

    private void Awake()
    {
        hitbox = GetComponent<PolygonCollider2D>();
        ownerEntity = GetComponentInParent<Entity>();

        if (hitbox == null || ownerEntity == null)
        {
            Debug.LogError(
                $"{name} 需要与 PolygonCollider2D 挂在同一物体上，且父物体需要 Entity。",
                this);
            enabled = false;
            return;
        }

        overlapFilter = new ContactFilter2D();
        overlapFilter.SetLayerMask(
            targetLayer.value | (1 << gameObject.layer));
        overlapFilter.useTriggers = true;

        hitbox.isTrigger = true;
        hitbox.enabled = false;
    }

    //开启攻击判定
    public void EnableHitbox()
    {
        if (hitbox == null)
        {
            return;
        }
        isActive = true;
        hitTargets.Clear();         
        hitbox.enabled = true;

        Physics2D.SyncTransforms();
    }
    //关闭攻击判定
    public void DisableHitbox()
    {
        isActive = false;

        if (hitbox != null)
        {
            hitbox.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (isActive)
        {
            ResolveOverlaps();
        }
    }

    private void ResolveOverlaps()
    {
        if (!isActive || hitbox == null)
        {
            return;
        }

        int overlapCount = hitbox.OverlapCollider(overlapFilter,overlapResults);

        for (int i = 0; i < overlapCount; i++)
        {
            AttackHitbox otherHitbox = overlapResults[i].GetComponent<AttackHitbox>();

            if (TryClash(otherHitbox))
            {
                return;
            }
        }

        for (int i = 0; i < overlapCount && isActive; i++)
        {
            TryHit(overlapResults[i]);
        }
    }
    //判断拼刀
    private bool TryClash(AttackHitbox otherHitbox)
    {
        if (otherHitbox == null ||
            otherHitbox == this ||
            !otherHitbox.isActive ||
            otherHitbox.ownerEntity == ownerEntity)
        {
            return false;
        }

        AttackHitbox clashOwner = null;

        if (ownerEntity.CanClash(otherHitbox.ownerEntity))
        {
            clashOwner = this;
        }
        else if (otherHitbox.ownerEntity.CanClash(ownerEntity))
        {
            clashOwner = otherHitbox;
        }

        if (clashOwner == null)
        {
            return false;
        }

        DisableHitbox();
        otherHitbox.DisableHitbox();
        clashOwner.PlayHitStop(clashOwner.clashEffect);
        clashOwner.ownerEntity.OnClash(clashOwner == this
                ? otherHitbox.ownerEntity
                : ownerEntity);

        return true;
    }
    //判断攻击和反击
    private void TryHit(Collider2D other)
    {
        if (!IsInLayerMask(other.gameObject.layer, targetLayer))
        {
            return;
        }

        Entity target = other.GetComponent<Entity>();

        if (target == null ||target == ownerEntity ||!hitTargets.Add(target))
        {
            return;
        }

        if (canBeCountered && target.CanCounter(this))
        {
            DisableHitbox();
            PlayHitStop(counterEffect);
            target.OnCounterSuccess(ownerEntity);
            return;
        }

        target.Damage(ownerEntity.transform.position);
        PlayHitStop(hitEffect);
    }

    private void PlayHitStop(HitStopSettings settings)
    {
        float shakeTime = settings.freezeTime > 0f
            ? Mathf.Min(settings.shakeTime, settings.freezeTime)
            : settings.shakeTime;

        CameraShaker.Instance?.RequestShake(
            settings.shakeAmount,
            shakeTime);

        TimeManager.FrameFreeze(settings.freezeTime);
    }
    
    private static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
    /// <summary>
    /// 在编辑器中绘制多边形边框（方便调试）
    /// </summary>
    private void OnDrawGizmos()
    {
        if (hitbox == null)
            hitbox = GetComponent<PolygonCollider2D>();

        if (hitbox == null || hitbox.points.Length < 2) return;
        // 激活时绿色，未激活时半透明灰色
        Gizmos.color = isActive ? Color.green : new Color(0.5f, 0.5f, 0.5f, 0.3f);
        // 手动绘制多边形路径
        Vector3[] worldPoints = new Vector3[hitbox.points.Length];
        for (int i = 0; i < hitbox.points.Length; i++)
        {
            worldPoints[i] = transform.TransformPoint(hitbox.points[i]);
        }

        for (int i = 0; i < worldPoints.Length; i++)
        {
            int next = (i + 1) % worldPoints.Length;
            Gizmos.DrawLine(worldPoints[i], worldPoints[next]);
        }
    }
}
