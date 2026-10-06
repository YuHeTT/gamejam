using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 平台负载传感器。传感器盒只负责找候选刚体，真正计重还要满足物体底部
/// 在平台支撑面附近且与平台有水平重叠。承重状态使用进入/退出滞后和质量平滑，
/// 避免平台移动时玩家或箱子因短暂间隙被误判为浮空。
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlatformSensor : MonoBehaviour
{
    public bool isActive = true;
    public float detectHeightAboveTop = 2.5f;
    public float detectDepthBelowTop = 0.1f;
    [Tooltip("物体底部高出支撑面多少仍可视为贴着平台（米）")]
    public float stackTolerance = 0.18f;
    [Tooltip("碰撞体轻微穿入平台时允许的最大深度（米）")]
    public float platformEmbedTolerance = 0.08f;
    [Tooltip("候选连续有效多长时间后才吸附为承重（秒）")]
    public float acquireTime = 0.06f;
    [Tooltip("失去支撑后保留承重状态的时间（秒）")]
    public float releaseDelay = 0.18f;
    [Tooltip("质量指数平滑时间（秒）")]
    public float massSmoothingTime = 0.10f;
    [Tooltip("与平台的最小水平重叠宽度（米）")]
    public float minimumHorizontalOverlap = 0.03f;

    private struct BodyInfo
    {
        public Rigidbody2D body;
        public float minY;
        public float maxY;
        public float minX;
        public float maxX;
        public bool accepted;
    }

    private sealed class LoadState
    {
        public Rigidbody2D body;
        public float validTime;
        public float invalidTime;
        public float filteredMass;
        public bool latched;
        public bool seenThisStep;
    }

    private readonly List<Rigidbody2D> _tracked = new List<Rigidbody2D>();
    private readonly Collider2D[] _buffer = new Collider2D[32];
    private readonly Dictionary<Rigidbody2D, LoadState> _states = new Dictionary<Rigidbody2D, LoadState>();
    private readonly List<Rigidbody2D> _stateKeys = new List<Rigidbody2D>();
    private readonly List<Rigidbody2D> _removeKeys = new List<Rigidbody2D>();
    private BodyInfo[] _candidates;
    private Collider2D _area;
    private ContactFilter2D _filter;
    private float _mass;

    public float Mass { get { return _mass; } }
    public int BodyCount { get { return _tracked.Count; } }
    public float PlatformTopY { get; private set; }
    public float MaxAcceptedGap { get; private set; }
    public int FloatingRejectedCount { get; private set; }

    private void Awake()
    {
        _candidates = new BodyInfo[_buffer.Length];
        ResolveArea();
        if (_area == null)
            Debug.LogWarning("PlatformSensor: 没有找到平台碰撞体，无法统计负载。", this);

        _filter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = false,
            useDepth = false,
            useNormalAngle = false
        };
    }

    private Collider2D ResolveArea()
    {
        if (_area != null) return _area;
        Collider2D col = GetComponent<Collider2D>();
        if (col == null || col.isTrigger)
        {
            Collider2D parentCol = transform.parent != null ? transform.parent.GetComponent<Collider2D>() : null;
            if (parentCol != null) col = parentCol;
        }
        if (col == null) col = GetComponentInChildren<Collider2D>();
        _area = col;
        return _area;
    }

    private bool GetDetectBox(out Vector2 center, out Vector2 size)
    {
        center = Vector2.zero;
        size = Vector2.zero;
        if (_area == null) return false;

        Bounds b = _area.bounds;
        float bottom = b.max.y - Mathf.Max(0f, detectDepthBelowTop);
        float top = b.max.y + Mathf.Max(0.01f, detectHeightAboveTop);
        center = new Vector2(b.center.x, (bottom + top) * 0.5f);
        size = new Vector2(b.size.x, Mathf.Max(0.01f, top - bottom));
        return true;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (!isActive || _area == null)
        {
            ResetLoads();
            return;
        }

        if (_candidates == null || _candidates.Length != _buffer.Length)
            _candidates = new BodyInfo[_buffer.Length];

        for (int i = 0; i < _stateKeys.Count; i++)
            _states[_stateKeys[i]].seenThisStep = false;

        int candidateCount = CollectCandidates();
        EvaluateSupport(candidateCount);
        UpdateLoadStates(candidateCount, dt);
        PlatformTopY = _area.bounds.max.y;
    }

    private int CollectCandidates()
    {
        if (!GetDetectBox(out Vector2 center, out Vector2 size)) return 0;

        int count = Physics2D.OverlapBox(center, size, 0f, _filter, _buffer);
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            Collider2D col = _buffer[i];
            if (col == null || col == _area || col.isTrigger) continue;
            if (col.transform.IsChildOf(transform) || transform.IsChildOf(col.transform)) continue;
            if (!MechanismQuery.CanWeighOnBalance(col)) continue;

            Rigidbody2D body = FindBody(col);
            if (body == null || IsPartOfPlatform(body)) continue;

            Bounds b = col.bounds;
            int slot = -1;
            for (int k = 0; k < n; k++)
            {
                if (_candidates[k].body == body)
                {
                    slot = k;
                    break;
                }
            }

            if (slot >= 0)
            {
                BodyInfo merged = _candidates[slot];
                merged.minX = Mathf.Min(merged.minX, b.min.x);
                merged.maxX = Mathf.Max(merged.maxX, b.max.x);
                merged.minY = Mathf.Min(merged.minY, b.min.y);
                merged.maxY = Mathf.Max(merged.maxY, b.max.y);
                _candidates[slot] = merged;
            }
            else if (n < _candidates.Length)
            {
                _candidates[n] = new BodyInfo
                {
                    body = body,
                    minX = b.min.x,
                    maxX = b.max.x,
                    minY = b.min.y,
                    maxY = b.max.y,
                    accepted = false
                };
                n++;
            }
        }
        return n;
    }

    private void EvaluateSupport(int candidateCount)
    {
        for (int i = 0; i < candidateCount; i++)
            _candidates[i].accepted = false;

        Bounds platformBounds = _area.bounds;
        float supportY = platformBounds.max.y;
        float supportMinX = platformBounds.min.x;
        float supportMaxX = platformBounds.max.x;

        bool grew = true;
        while (grew)
        {
            grew = false;
            for (int i = 0; i < candidateCount; i++)
            {
                BodyInfo candidate = _candidates[i];
                if (candidate.accepted) continue;

                // 上下都限制：底部明显低于平台的地面支撑物不会被接受。
                if (candidate.minY < supportY - Mathf.Max(0f, platformEmbedTolerance)) continue;
                if (candidate.minY > supportY + Mathf.Max(0f, stackTolerance)) continue;

                float overlap = Mathf.Min(candidate.maxX, supportMaxX) - Mathf.Max(candidate.minX, supportMinX);
                if (overlap < Mathf.Max(0f, minimumHorizontalOverlap)) continue;

                candidate.accepted = true;
                _candidates[i] = candidate;
                supportY = Mathf.Max(supportY, candidate.maxY);
                supportMinX = Mathf.Min(supportMinX, candidate.minX);
                supportMaxX = Mathf.Max(supportMaxX, candidate.maxX);
                grew = true;
            }
        }

        float maxGap = 0f;
        int rejected = 0;
        for (int i = 0; i < candidateCount; i++)
        {
            if (_candidates[i].accepted)
                maxGap = Mathf.Max(maxGap, _candidates[i].minY - platformBounds.max.y);
            else
                rejected++;
        }
        MaxAcceptedGap = maxGap;
        FloatingRejectedCount = rejected;
    }

    private void UpdateLoadStates(int candidateCount, float dt)
    {
        for (int i = 0; i < candidateCount; i++)
        {
            BodyInfo candidate = _candidates[i];
            if (candidate.body == null) continue;

            LoadState state;
            if (!_states.TryGetValue(candidate.body, out state))
            {
                state = new LoadState { body = candidate.body };
                _states.Add(candidate.body, state);
                _stateKeys.Add(candidate.body);
            }

            state.seenThisStep = true;
            if (candidate.accepted)
            {
                state.validTime += dt;
                state.invalidTime = 0f;
                if (!state.latched && state.validTime >= Mathf.Max(0f, acquireTime))
                    state.latched = true;
            }
            else
            {
                state.validTime = 0f;
                state.invalidTime += dt;
                if (state.latched && state.invalidTime >= Mathf.Max(0f, releaseDelay))
                    state.latched = false;
            }
        }

        _removeKeys.Clear();
        _tracked.Clear();
        float total = 0f;
        float blend = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, massSmoothingTime));

        for (int i = 0; i < _stateKeys.Count; i++)
        {
            Rigidbody2D key = _stateKeys[i];
            LoadState state = _states[key];
            if (!state.seenThisStep)
            {
                state.validTime = 0f;
                state.invalidTime += dt;
                if (state.latched && state.invalidTime >= Mathf.Max(0f, releaseDelay))
                    state.latched = false;
            }

            float targetMass = state.latched && state.body != null ? state.body.mass : 0f;
            state.filteredMass = Mathf.Lerp(state.filteredMass, targetMass, blend);
            total += state.filteredMass;
            if (state.latched && state.body != null)
                _tracked.Add(state.body);

            if (!state.latched && state.filteredMass < 0.001f && state.invalidTime > 1f)
                _removeKeys.Add(key);
        }

        for (int i = 0; i < _removeKeys.Count; i++)
        {
            Rigidbody2D key = _removeKeys[i];
            _states.Remove(key);
            _stateKeys.Remove(key);
        }
        _mass = total;
    }

    private void ResetLoads()
    {
        _mass = 0f;
        _tracked.Clear();
        _states.Clear();
        _stateKeys.Clear();
        _removeKeys.Clear();
        PlatformTopY = 0f;
        MaxAcceptedGap = 0f;
        FloatingRejectedCount = 0;
    }

    private bool IsPartOfPlatform(Rigidbody2D body)
    {
        if (body == null) return true;
        return body.transform == transform || body.transform.IsChildOf(transform) || transform.IsChildOf(body.transform);
    }

    private Rigidbody2D FindBody(Collider2D collision)
    {
        if (collision == null) return null;
        Rigidbody2D body = collision.attachedRigidbody;
        if (body == null) body = collision.GetComponentInParent<Rigidbody2D>();
        return body;
    }

    private void OnDrawGizmos()
    {
        if (!isActive) return;
        ResolveArea();
        if (!GetDetectBox(out Vector2 center, out Vector2 size)) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0.01f));
        if (_area != null)
        {
            Bounds b = _area.bounds;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            Gizmos.DrawLine(new Vector3(b.min.x, b.max.y, 0f), new Vector3(b.max.x, b.max.y, 0f));
        }

        Gizmos.color = _tracked.Count > 0 ? Color.yellow : new Color(1f, 1f, 1f, 0.25f);
        for (int i = 0; i < _tracked.Count; i++)
        {
            if (_tracked[i] != null)
                Gizmos.DrawWireSphere(_tracked[i].worldCenterOfMass, 0.15f);
        }
    }
}
