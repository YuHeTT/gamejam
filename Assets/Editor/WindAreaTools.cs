using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 风扇风区的一键搭建工具。<br/>
/// 菜单：Tools → 风扇 → 生成风扇风区预制体<br/>
/// 生成的预制体自带触发器、windarea 脚本和一套调好的上升风粒子；粒子贴图与材质会作为资源存盘，
/// 因此引用不会丢（材质用无光照的 Sprites/Default，不会出现 Standard 着色器"没灯光就全黑"的问题）。
/// </summary>
public static class WindAreaTools
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string PrefabPath = "Assets/Prefabs/WindArea.prefab";
    private const string MaterialFolder = "Assets/Materials";
    private const string MaterialPath = "Assets/Materials/WindParticle.mat";
    private const string TexturePath = "Assets/Materials/WindParticleTex.png";

    // 风区默认尺寸（可按需改）
    private static readonly Vector2 ZoneSize = new Vector2(2f, 6f);

    [MenuItem("Tools/风扇/生成风扇风区预制体")]
    public static void CreateWindAreaPrefab()
    {
        EnsureFolder("Assets", "Prefabs");

        GameObject root = BuildWindAreaObject();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("已生成风扇风区预制体：" + PrefabPath + "（改它 Collider2D 的 size 即可调整送风范围）");
    }

    [MenuItem("Tools/风扇/给选中物体挂上风粒子")]
    public static void AddParticlesToSelection()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("请先在 Hierarchy 里选中目标物体。");
            return;
        }

        GameObject target = Selection.activeGameObject;
        ParticleSystem ps = BuildWindParticles("WindParticles", target.transform);
        ps.transform.localPosition = Vector3.zero;

        windarea area = target.GetComponent<windarea>();
        if (area != null)
        {
            area.windParticles = ps;
            EditorUtility.SetDirty(area);
        }

        Undo.RegisterCreatedObjectUndo(ps.gameObject, "Add Wind Particles");
        Selection.activeGameObject = ps.gameObject;
        Debug.Log("已给 " + target.name + " 添加上升风粒子。");
    }

    private static GameObject BuildWindAreaObject()
    {
        GameObject root = new GameObject("WindArea");

        // 触发器：玩家进入这个范围就会被向上吹
        BoxCollider2D zone = root.AddComponent<BoxCollider2D>();
        zone.isTrigger = true;
        zone.size = ZoneSize;
        zone.offset = new Vector2(0f, ZoneSize.y * 0.5f);   // 以底部为出风口，向上送风

        windarea area = root.AddComponent<windarea>();
        area.windSpeed = 6f;
        area.windForce = 40f;
        area.onlyAffectPlayer = true;
        area.syncParticlesWithTrigger = true;

        // 粒子：发射框与风区同尺寸同位置
        ParticleSystem ps = BuildWindParticles("WindParticles", root.transform);
        ps.transform.localPosition = zone.offset;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(zone.size.x, zone.size.y, 1f);
        shape.position = Vector3.zero;

        area.windParticles = ps;

        // 风扇外观（可选，直接删掉不影响功能）
        GameObject fanSprite = new GameObject("FanSprite");
        fanSprite.transform.SetParent(root.transform);
        fanSprite.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        fanSprite.transform.localScale = new Vector3(2.4f, 0.5f, 1f);
        SpriteRenderer sr = fanSprite.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.35f, 0.35f, 0.4f, 1f);
        sr.sortingOrder = 20;

        return root;
    }

    /// <summary>构建一套"向上吹的风"粒子</summary>
    private static ParticleSystem BuildWindParticles(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        // 创建时自带的默认发射是"喷一下就停"，先清掉
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // ---------- 主模块 ----------
        ParticleSystem.MainModule main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.prewarm = true;                                    // 生成后立刻有粒子，不用干等
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f);   // 位移交给 Velocity over Lifetime
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.75f, 0.92f, 1f, 0.55f),
            new Color(0.95f, 1f, 1f, 0.75f));
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // 留在世界里，不跟着风区乱飘
        main.maxParticles = 800;
        main.playOnAwake = true;
        main.useUnscaledTime = true;                            // 暂停时依然飘动（不需要可改回 false）

        // ---------- 发射 ----------
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(45f);

        // ---------- 形状（外部可覆盖成风区尺寸）----------
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1f, 1f, 1f);

        // ---------- 生命周期内上升 ----------
        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;        // Local：风区旋转后风向跟着转
        // x/y/z 三条曲线必须用同一个 mode，否则 Unity 会报
        // "Particle Velocity curves must all be in the same mode"
        // （注意：这些模块没有暴露 xMode/yMode/zMode 属性，只能靠赋 MinMaxCurve 时带上的 mode 保证一致）
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(1f, 4.5f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // ---------- 竖直方向的持续推力，做出"越往上越急"的风 ----------
        ParticleSystem.ForceOverLifetimeModule force = ps.forceOverLifetime;
        force.enabled = true;
        force.space = ParticleSystemSimulationSpace.Local;
        // forceOverLifetime 同样要求三个轴的 mode 一致
        force.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        force.y = new ParticleSystem.MinMaxCurve(60f, 60f);
        force.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // ---------- 淡入淡出 ----------
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.8f, 0.95f, 1f), 0f),
                new GradientColorKey(new Color(1f, 1f, 1f), 0.5f),
                new GradientColorKey(new Color(0.8f, 0.95f, 1f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ---------- 尺寸随时间微微变大 ----------
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.1f)));

        // ---------- 渲染：无光照 2D 材质，并压在场景精灵上层 ----------
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 30;                             // 高于背景和平台，避免被盖住
        renderer.material = CreateWindMaterialAsset();

        return ps;
    }

    /// <summary>
    /// 创建（或复用）风粒子材质资源：径向渐变贴图 + Sprites/Default（无光照，2D 安全）。<br/>
    /// 贴图与材质都存成磁盘资源，避免存进预制体后引用丢失而变成粉色/默认粒子。
    /// </summary>
    private static Material CreateWindMaterialAsset()
    {
        EnsureFolder("Assets", "Materials");

        Texture2D tex = GetOrCreateTextureAsset();

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");

        if (mat == null)
        {
            mat = new Material(shader);
            mat.name = "WindParticle";
            mat.mainTexture = tex;
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
            mat.mainTexture = tex;
            EditorUtility.SetDirty(mat);
        }

        return mat;
    }

    /// <summary>取径向渐变贴图：能拿到内置柔和圆点就用它，否则代码生成并保存为 PNG 资源</summary>
    private static Texture2D GetOrCreateTextureAsset()
    {
        // 1) 优先使用内置的柔和圆点贴图（已是工程内资源，引用不会丢）
        Texture2D builtin = EditorGUIUtility.Load("ParticleSystem/Default-Particle.psd") as Texture2D;
        if (builtin != null && EditorUtility.IsPersistent(builtin))
            return builtin;

        // 2) 退路：代码生成一张径向渐变贴图并存盘
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (existing != null) return existing;

        const int S = 128;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.name = "WindParticleTex";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[S * S];
        Vector2 center = new Vector2(S * 0.5f, S * 0.5f);
        float radius = S * 0.5f;

        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;                                  // 平方衰减，边缘更柔和
                pixels[y * S + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        File.WriteAllBytes(TexturePath, png);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);

        Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        return imported;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
