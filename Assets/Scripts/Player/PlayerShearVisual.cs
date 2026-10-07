using System.Collections.Generic;
using UnityEngine;

/// <summary>传送期间对子物体 SpriteRenderer 做错切 / 压扁 / 从头显现（Built-in Custom/PlayerShearSprite）。</summary>
public class PlayerShearVisual : MonoBehaviour
{
    static readonly int ShearId = Shader.PropertyToID("_Shear");
    static readonly int FlattenId = Shader.PropertyToID("_Flatten");
    static readonly int RevealId = Shader.PropertyToID("_Reveal");
    static readonly int ShearMaxOffsetId = Shader.PropertyToID("_ShearMaxOffset");
    static readonly int PivotBottomYId = Shader.PropertyToID("_PivotBottomY");
    static readonly int SpriteBottomYId = Shader.PropertyToID("_SpriteBottomY");
    static readonly int SpriteTopYId = Shader.PropertyToID("_SpriteTopY");

    [SerializeField] private Player player;

    [Header("Collapse")]
    [Tooltip("错切强度：顶部沿角色本地水平方向的偏移系数。1 约为 45° 倾倒。")]
    public float shearStrength = 1.2f;
    [Tooltip("勾选：朝面朝方向的反方向倒下。取消：朝面朝方向倒下。")]
    public bool fallOppositeFacing = true;

    private Shader shearShader;
    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private readonly Dictionary<SpriteRenderer, Material> originalMaterials = new Dictionary<SpriteRenderer, Material>();
    private bool effectActive;

    private void Awake()
    {
        if (player == null)
            player = GetComponentInParent<Player>();

        //优先从 Resources 加载：打包后 Shader.Find 可能因着色器未被子物体/材质引用而被剥离，返回 null 导致效果失效。
        //着色器已放在 Assets/Resources/ 下，保证一定被打进构建。
        shearShader = Resources.Load<Shader>("PlayerShearSprite");
        if (shearShader == null)
            shearShader = Shader.Find("Custom/PlayerShearSprite");
        if (shearShader == null)
            Debug.LogError("找不到 Shader Custom/PlayerShearSprite（请确认 Assets/Resources/PlayerShearSprite.shader 存在）。", this);

        GetComponentsInChildren(true, renderers);
    }

    public void BeginEffectMode()
    {
        if (shearShader == null || effectActive)
            return;

        effectActive = true;
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr == null) continue;

            if (!originalMaterials.ContainsKey(sr))
                originalMaterials[sr] = sr.sharedMaterial;

            sr.material = new Material(shearShader);
            ApplyToRenderer(sr, 0f, 0f, 1f);
        }
    }

    public void EndEffectMode()
    {
        if (!effectActive)
            return;

        effectActive = false;
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr == null) continue;

            if (originalMaterials.TryGetValue(sr, out Material orig))
                sr.sharedMaterial = orig;
        }
    }

    /// <summary>倒下进度 0→1：错切倾倒，同时压扁成贴地的一条线。</summary>
    public void SetCollapse(float t)
    {
        t = Mathf.Clamp01(t);
        ApplyAll(t, t, 1f);
    }

    /// <summary>显现进度 0→1：0 完全不可见，增大时从头顶往脚底揭开。</summary>
    public void SetReveal(float t)
    {
        ApplyAll(0f, 0f, Mathf.Clamp01(t));
    }

    public void SetHidden()
    {
        ApplyAll(0f, 0f, 0f);
    }

    private void ApplyAll(float shear, float flatten, float reveal)
    {
        if (!effectActive)
            return;

        foreach (SpriteRenderer sr in renderers)
        {
            if (sr != null)
                ApplyToRenderer(sr, shear, flatten, reveal);
        }
    }

    private void ApplyToRenderer(SpriteRenderer sr, float shear, float flatten, float reveal)
    {
        Material m = sr.material;
        if (m == null || sr.sprite == null)
            return;

        Bounds b = sr.sprite.bounds;
        // Flip 使用 Y 轴 180°，着色器在物体本地 X 上错切。
        // 本地 -X 始终是「面朝的反方向」（贴图默认朝右）。
        float maxOffset = (fallOppositeFacing ? -1f : 1f) * shearStrength;

        m.SetFloat(ShearId, shear);
        m.SetFloat(FlattenId, flatten);
        m.SetFloat(RevealId, reveal);
        m.SetFloat(ShearMaxOffsetId, maxOffset);
        m.SetFloat(PivotBottomYId, b.min.y);
        m.SetFloat(SpriteBottomYId, b.min.y);
        m.SetFloat(SpriteTopYId, b.max.y);
    }
}
