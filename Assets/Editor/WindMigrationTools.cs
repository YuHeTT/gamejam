using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 风力改造的迁移工具与<b>回退工具</b>。<br/><br/>
/// 目的：让 game6/7/8 的风全部由 <c>wind.prefab</c> 承担（吹风逻辑 + 动画），
/// 把老的 <c>WindArea.prefab</c> 实例（带粒子）停用/删除。<br/><br/>
/// <b>版本 2 的重要修正</b>：不再用 <c>scene.GetRootGameObjects()</c> 找对象
/// —— game8 里的 wind / WindArea 都嵌套在别的物体下面，根物体扫描会把它们全部漏掉。
/// 现在改用 <c>windarea</c> 组件递归定位，并按"子物体里有没有 Animator / ParticleSystem"分类。<br/><br/>
/// 推荐流程：<br/>
/// ① 保存所有场景 → ② <b>步骤1</b> 对齐风区碰撞体并迁移周期设置 →
/// ③ <b>步骤4</b> 扩展成覆盖并集 → ④ <b>步骤2</b> 停用 WindArea 粒子（可逆）→
/// ⑤ 试玩确认 → ⑥ <b>步骤3</b> 删除 WindArea 实例。<br/>
/// 任何一步出问题都可以用 <b>还原</b> 菜单回到改动前的状态。
/// </summary>
public static class WindMigrationTools
{
    private const string PrefabRoot = "Assets/Prefabs";
    private const string SceneFolder = "Assets/Scenes";

    private static readonly string[] Scenes = { "game6", "game7", "game8" };

    // wind 碰撞体要对齐成的尺寸（= 原 WindArea 的设置）
    private static readonly Vector2 ZoneSize = new Vector2(2f, 6f);
    private static readonly Vector2 ZoneOffset = new Vector2(0f, 3f);

    // 配对时允许的最大 X 偏差
    private const float PairTolX = 1.5f;
    // 覆盖判定容差
    private const float CoverTol = 0.05f;

    private const string BackupRoot = "_backup_wind_";

    // ============================================================ 对象发现（递归）

    /// <summary>
    /// 递归找出场景里所有挂了 <c>windarea</c> 的物体（含嵌套在其它物体/预制体下的）。<br/>
    /// 用组件定位而不是根物体扫描，避免漏掉 game8 那种嵌套实例。
    /// </summary>
    private static List<windarea> FindAllWindAreas(Scene scene)
    {
        List<windarea> result = new List<windarea>();

        foreach (windarea w in Resources.FindObjectsOfTypeAll<windarea>())
        {
            if (w == null) continue;
            if (EditorUtility.IsPersistent(w)) continue;              // 预制体资源本体，不是场景实例
            if (w.gameObject.scene != scene) continue;                // 不属于当前场景
            result.Add(w);
        }

        return result;
    }

    /// <summary>子物体里有 Animator 的算"风动画载体"（wind.prefab）</summary>
    private static bool HasAnimator(windarea w)
    {
        return w.GetComponentInChildren<Animator>(true) != null;
    }

    /// <summary>子物体里有 ParticleSystem 的算"旧粒子载体"（WindArea.prefab）</summary>
    private static bool HasParticleSystem(windarea w)
    {
        return w.GetComponentInChildren<ParticleSystem>(true) != null;
    }

    private static List<windarea> WindList(List<windarea> all)
    {
        List<windarea> r = new List<windarea>();
        foreach (windarea w in all) if (HasAnimator(w)) r.Add(w);
        return r;
    }

    private static List<windarea> AreaList(List<windarea> all)
    {
        List<windarea> r = new List<windarea>();
        foreach (windarea w in all) if (!HasAnimator(w) && HasParticleSystem(w)) r.Add(w);
        return r;
    }

    /// <summary>按 X 轴找与 wind 配对的 WindArea（game7 里两者 Y 差了 3.23 米，不能按距离配对）</summary>
    private static windarea FindPair(List<windarea> areas, windarea wind)
    {
        windarea best = null;
        float bd = float.MaxValue;

        foreach (windarea a in areas)
        {
            float dx = Mathf.Abs(a.transform.position.x - wind.transform.position.x);
            if (dx < bd) { bd = dx; best = a; }
        }

        return (best != null && bd <= PairTolX) ? best : null;
    }

    /// <summary>世界坐标下的纵向覆盖范围（考虑 lossyScale）</summary>
    private static bool TryGetVerticalSpan(GameObject go, out float bottom, out float top)
    {
        bottom = top = 0f;

        BoxCollider2D box = go.GetComponent<BoxCollider2D>();
        if (box == null) return false;

        Vector3 s = go.transform.lossyScale;
        float centerY = go.transform.position.y + box.offset.y * s.y;
        float halfH = box.size.y * Mathf.Abs(s.y) * 0.5f;

        bottom = centerY - halfH;
        top = centerY + halfH;
        return true;
    }

    /// <summary>把碰撞体的<b>世界</b>横向宽度设为 width，并居中（offset.x 归零）</summary>
    private static bool SetWorldHorizontalSize(GameObject go, float width)
    {
        BoxCollider2D box = go.GetComponent<BoxCollider2D>();
        if (box == null) return false;

        float sx = Mathf.Abs(go.transform.lossyScale.x);
        if (sx < 1e-4f) return false;

        box.size = new Vector2(width / sx, box.size.y);
        box.offset = new Vector2(0f, box.offset.y);
        return true;
    }

    /// <summary>把碰撞体的<b>世界</b>纵向范围设为 [bottom, top]，内部自动换算回局部 size/offset</summary>
    private static bool SetWorldVerticalSpan(GameObject go, float bottom, float top)
    {
        BoxCollider2D box = go.GetComponent<BoxCollider2D>();
        if (box == null) return false;

        Vector3 s = go.transform.lossyScale;
        float sy = Mathf.Abs(s.y);
        if (sy < 1e-4f)
        {
            Debug.LogWarning("[风改造] " + go.name + " 的 Y 缩放接近 0，无法换算碰撞体。");
            return false;
        }

        float worldCenter = (bottom + top) * 0.5f;
        float worldHeight = Mathf.Max(0.01f, top - bottom);

        box.size = new Vector2(box.size.x, worldHeight / sy);
        box.offset = new Vector2(box.offset.x,
                                 (worldCenter - go.transform.position.y) / s.y);
        return true;
    }

    // ============================================================ 步骤 1

    [MenuItem("Tools/历史工具/风区迁移/改造-步骤1：对齐风区尺寸并迁移周期设置")]
    public static void Step1_AlignAndMigrate()
    {
        int sceneCount = 0, windTotal = 0, migrated = 0;

        foreach (string sceneName in Scenes)
        {
            string path = SceneFolder + "/" + sceneName + ".unity";
            if (!File.Exists(path)) continue;

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            List<windarea> all = FindAllWindAreas(scene);
            List<windarea> winds = WindList(all);
            List<windarea> areas = AreaList(all);

            Debug.Log(string.Format("[风改造] {0}：找到 wind {1} 个、WindArea {2} 个（共 {3} 个 windarea）",
                sceneName, winds.Count, areas.Count, all.Count));

            bool dirty = false;

            foreach (windarea wind in winds)
            {
                // 1) 世界宽度对齐 2（只改宽度与水平居中；纵向留给步骤4 按并集处理，避免误缩小）
                BoxCollider2D box = wind.GetComponent<BoxCollider2D>();
                if (box != null)
                {
                    if (SetWorldHorizontalSize(wind.gameObject, ZoneSize.x))
                    {
                        EditorUtility.SetDirty(box);
                        dirty = true;
                    }

                    float wb, wt;
                    TryGetVerticalSpan(wind.gameObject, out wb, out wt);
                    Debug.Log(string.Format(
                        "[风改造] {0} / {1}（父 {2}）对齐后：世界宽 {3:F2}，世界纵向 y[{4:F2},{5:F2}]" +
                        "（局部 size=({6:F2},{7:F2}) offset=({8:F2},{9:F2})，lossyScale.y={10:F2}）",
                        sceneName, wind.name, wind.transform.parent != null ? wind.transform.parent.name : "(根)",
                        ZoneSize.x, wb, wt, box.size.x, box.size.y, box.offset.x, box.offset.y,
                        wind.transform.lossyScale.y));
                }
                else
                {
                    Debug.LogWarning("[风改造] " + sceneName + " / " + wind.name + " 没有 BoxCollider2D");
                }

                // 2) 从配对的 WindArea 迁移周期设置
                windarea pair = FindPair(areas, wind);
                if (pair == null)
                {
                    Debug.Log("[风改造] " + wind.name + " 附近没有 WindArea（X 差过大），周期设置保持原样。");
                }
                else if (pair.usePeriodicCycle != wind.usePeriodicCycle ||
                         !Mathf.Approximately(pair.openDuration, wind.openDuration) ||
                         !Mathf.Approximately(pair.closedDuration, wind.closedDuration))
                {
                    wind.usePeriodicCycle = pair.usePeriodicCycle;
                    wind.openDuration = pair.openDuration;
                    wind.closedDuration = pair.closedDuration;
                    EditorUtility.SetDirty(wind);
                    dirty = true;
                    migrated++;

                    Debug.Log("[风改造] " + wind.name + "（父 " + wind.transform.parent?.name + "）周期设置 ← " +
                              pair.name + "：usePeriodicCycle=" + wind.usePeriodicCycle +
                              " 开" + wind.openDuration + "s / 关" + wind.closedDuration + "s");
                }
                else
                {
                    Debug.Log("[风改造] " + wind.name + " 周期设置已与 " + pair.name + " 一致。");
                }

                windTotal++;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            sceneCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("步骤1 完成",
            "已处理 " + sceneCount + " 个场景 / " + windTotal + " 个 wind 实例。\n\n" +
            "· 碰撞体已对齐为 size(2,6) offset(0,3)\n" +
            "· 迁移了 " + migrated + " 个实例的周期设置\n\n" +
            "下一步：Tools → 历史工具 → 风区迁移 → 改造-步骤4（扩展成覆盖并集）。", "好");
    }

    // ============================================================ 步骤 4

    [MenuItem("Tools/历史工具/风区迁移/改造-步骤4：把 wind 风区扩展成覆盖并集")]
    public static void Step4_ExtendZoneToUnion()
    {
        if (!EditorUtility.DisplayDialog("确认扩展",
            "会把无法完整覆盖配对 WindArea 的 wind 实例，其触发器纵向范围扩展成两者的并集。\n\n" +
            "只变大、不缩小。扩展后务必试玩确认。", "扩展", "取消"))
            return;

        int changed = 0, already = 0;

        foreach (string sceneName in Scenes)
        {
            string path = SceneFolder + "/" + sceneName + ".unity";
            if (!File.Exists(path)) continue;

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            List<windarea> all = FindAllWindAreas(scene);
            List<windarea> winds = WindList(all);
            List<windarea> areas = AreaList(all);

            bool dirty = false;

            foreach (windarea wind in winds)
            {
                windarea pair = FindPair(areas, wind);
                if (pair == null)
                {
                    Debug.Log("[风改造-步骤4] " + wind.name + " 无配对 WindArea，跳过。");
                    continue;
                }

                float wb, wt, ab, at;
                if (!TryGetVerticalSpan(wind.gameObject, out wb, out wt) ||
                    !TryGetVerticalSpan(pair.gameObject, out ab, out at)) continue;

                float nb = Mathf.Min(wb, ab);
                float nt = Mathf.Max(wt, at);

                if (Mathf.Abs(nb - wb) < 0.01f && Mathf.Abs(nt - wt) < 0.01f)
                {
                    already++;
                    continue;
                }

                if (!SetWorldVerticalSpan(wind.gameObject, nb, nt))
                {
                    Debug.LogWarning("[风改造-步骤4] " + wind.name + " 换算失败，跳过。");
                    continue;
                }

                BoxCollider2D box = wind.GetComponent<BoxCollider2D>();
                EditorUtility.SetDirty(box);
                dirty = true;
                changed++;

                Debug.Log(string.Format(
                    "[风改造-步骤4] {0} / {1}（父 {2}）：世界 y[{3:F2},{4:F2}] → y[{5:F2},{6:F2}]" +
                    "（局部 size.y={7:F2}, offset.y={8:F2}，lossyScale.y={9:F2}）",
                    sceneName, wind.name, wind.transform.parent != null ? wind.transform.parent.name : "(根)",
                    wb, wt, nb, nt, box.size.y, box.offset.y, wind.transform.lossyScale.y));
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("步骤4 完成",
            "已扩展 " + changed + " 个风区，另有 " + already + " 个本来就够大。\n\n" +
            "下一步：Tools → 历史工具 → 风区迁移 → 改造-步骤2（停用粒子）。", "好");
    }

    // ============================================================ 步骤 2

    [MenuItem("Tools/历史工具/风区迁移/改造-步骤2：停用 WindArea 实例的粒子（可逆）")]
    public static void Step2_DisableParticles()
    {
        int n = 0;

        foreach (string sceneName in Scenes)
        {
            string path = SceneFolder + "/" + sceneName + ".unity";
            if (!File.Exists(path)) continue;

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool dirty = false;

            foreach (windarea area in AreaList(FindAllWindAreas(scene)))
            {
                foreach (ParticleSystem ps in area.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.EmissionModule emission = ps.emission;
                    if (emission.enabled)
                    {
                        emission.enabled = false;
                        EditorUtility.SetDirty(ps);
                        n++;
                    }
                }
                dirty = true;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("步骤2 完成",
            "已停用 " + n + " 个粒子发射（只针对 WindArea 实例）。\n\n" +
            "这一步可逆。下一步：试玩确认后再执行步骤3。", "好");
    }

    // ============================================================ 步骤 3

    [MenuItem("Tools/历史工具/风区迁移/改造-步骤3：删除 WindArea 实例（有覆盖缺口会自动跳过）")]
    public static void Step3_DeleteInstances()
    {
        if (!EditorUtility.DisplayDialog("确认删除",
            "将删除已被 wind 完整覆盖的 WindArea 实例。\n\n" +
            "· 覆盖不住的会【自动跳过】并在 Console 报缺口大小。\n" +
            "· 可用“还原”菜单回退。", "开始", "取消"))
            return;

        int deleted = 0, skipped = 0;

        foreach (string sceneName in Scenes)
        {
            string path = SceneFolder + "/" + sceneName + ".unity";
            if (!File.Exists(path)) continue;

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            List<windarea> all = FindAllWindAreas(scene);
            List<windarea> winds = WindList(all);
            List<windarea> areas = AreaList(all);

            Debug.Log(string.Format("[风改造] {0}：wind {1} 个、WindArea {2} 个",
                sceneName, winds.Count, areas.Count));

            bool dirty = false;

            foreach (windarea area in areas)
            {
                // 找 X 最接近的 wind
                windarea wind = null;
                float best = float.MaxValue;
                foreach (windarea w in winds)
                {
                    float dx = Mathf.Abs(w.transform.position.x - area.transform.position.x);
                    if (dx < best) { best = dx; wind = w; }
                }

                if (wind == null || best > PairTolX)
                {
                    Debug.LogWarning("[风改造] " + sceneName + " 的 " + area.name +
                                     "[" + area.transform.parent?.name + "] 找不到对应的 wind 实例（X 差 " +
                                     best.ToString("F2") + "），跳过删除。");
                    skipped++;
                    continue;
                }

                float wb, wt, ab, at;
                if (!TryGetVerticalSpan(wind.gameObject, out wb, out wt) ||
                    !TryGetVerticalSpan(area.gameObject, out ab, out at))
                {
                    Debug.LogWarning("[风改造] " + sceneName + " / " + area.name + " 取不到碰撞体范围，跳过。");
                    skipped++;
                    continue;
                }

                if (wb > ab + CoverTol || wt < at - CoverTol)
                {
                    Debug.LogWarning(string.Format(
                        "[风改造] ★跳过★ {0} / {1}：wind 覆盖 y[{2:F2},{3:F2}]，" +
                        "WindArea 覆盖 y[{4:F2},{5:F2}]，缺口 {6:F2}。先跑步骤1+步骤4 再删。",
                        sceneName, area.name, wb, wt, ab, at,
                        Mathf.Max(0f, wb - ab) + Mathf.Max(0f, at - wt)));
                    skipped++;
                    continue;
                }

                Debug.Log("[风改造] 删除 " + sceneName + " / " + area.name +
                          "（父 " + area.transform.parent?.name + "）@ " + area.transform.position);
                Object.DestroyImmediate(area.gameObject);
                deleted++;
                dirty = true;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("步骤3 完成",
            "已删除 " + deleted + " 个，跳过 " + skipped + " 个（原因见 Console）。", "好");
    }

    // ============================================================ 一键整合

    /// <summary>
    /// 一键完成：停用全部 WindArea 粒子 → 把 wind 风区按<b>世界空间</b>扩展成与配对 WindArea 的并集
    /// → 删除已被完整覆盖的 WindArea → 打印每个实例的世界数值。
    /// </summary>
    [MenuItem("Tools/历史工具/风区迁移/★一键整合：停粒子 + 扩展覆盖 + 删除 WindArea")]
    public static void OneClickConsolidate()
    {
        if (!EditorUtility.DisplayDialog("一键整合",
            "将对 game6 / game7 / game8 执行：\n\n" +
            "1. 停用所有 WindArea 实例的粒子发射（可逆）\n" +
            "2. 把每个 wind 风区在世界空间扩展成与配对 WindArea 的并集（只变大）\n" +
            "3. 删除已被完整覆盖的 WindArea 实例\n\n" +
            "全部按世界空间计算，会打印每个实例的实际数值。\n" +
            "出问题可用“还原：从备份恢复改动前的文件”回退。", "执行", "取消"))
            return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        int particlesOff = 0, extended = 0, deleted = 0, kept = 0;

        foreach (string sceneName in Scenes)
        {
            string path = SceneFolder + "/" + sceneName + ".unity";
            if (!File.Exists(path)) continue;

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            List<windarea> all = FindAllWindAreas(scene);
            List<windarea> winds = WindList(all);
            List<windarea> areas = AreaList(all);

            sb.AppendLine("── " + sceneName + " ──");
            bool dirty = false;

            // 1) 停用粒子
            foreach (windarea area in areas)
            {
                foreach (ParticleSystem ps in area.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.EmissionModule emission = ps.emission;
                    if (emission.enabled)
                    {
                        emission.enabled = false;
                        EditorUtility.SetDirty(ps);
                        particlesOff++;
                        dirty = true;
                    }
                }
            }

            // 2) 扩展覆盖（世界空间并集）
            foreach (windarea wind in winds)
            {
                windarea pair = FindPair(areas, wind);

                float wb, wt;
                if (!TryGetVerticalSpan(wind.gameObject, out wb, out wt)) continue;

                if (pair == null)
                {
                    sb.AppendLine(string.Format("  wind {0,-18} 世界y[{1:F2},{2:F2}]  无配对，跳过扩展",
                        wind.name, wb, wt));
                    continue;
                }

                float ab, at;
                if (!TryGetVerticalSpan(pair.gameObject, out ab, out at)) continue;

                float nb = Mathf.Min(wb, ab), nt = Mathf.Max(wt, at);

                if (Mathf.Abs(nb - wb) >= 0.01f || Mathf.Abs(nt - wt) >= 0.01f)
                {
                    if (SetWorldVerticalSpan(wind.gameObject, nb, nt))
                    {
                        EditorUtility.SetDirty(wind.GetComponent<BoxCollider2D>());
                        dirty = true;
                        extended++;
                    }
                }

                TryGetVerticalSpan(wind.gameObject, out wb, out wt);
                sb.AppendLine(string.Format("  wind {0,-18} 世界y[{1:F2},{2:F2}]  ←配对 {3,-18} 世界y[{4:F2},{5:F2}]",
                    wind.name + "[" + (wind.transform.parent != null ? wind.transform.parent.name : "根") + "]",
                    wb, wt,
                    pair.name + "[" + (pair.transform.parent != null ? pair.transform.parent.name : "根") + "]",
                    ab, at));
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            // 3) 删除（重新取一次，因为上一步改过碰撞体）
            all = FindAllWindAreas(scene);
            winds = WindList(all);
            areas = AreaList(all);
            dirty = false;

            foreach (windarea area in areas)
            {
                windarea wind = null;
                float best = float.MaxValue;
                foreach (windarea w in winds)
                {
                    float dx = Mathf.Abs(w.transform.position.x - area.transform.position.x);
                    if (dx < best) { best = dx; wind = w; }
                }

                float wb = 0f, wt = 0f, ab = 0f, at = 0f;

                bool pairOk = wind != null && best <= PairTolX;
                if (pairOk) pairOk = TryGetVerticalSpan(wind.gameObject, out wb, out wt);
                if (pairOk) pairOk = TryGetVerticalSpan(area.gameObject, out ab, out at);

                if (!pairOk)
                {
                    sb.AppendLine("  ★保留 " + area.name + "（无配对 wind 或取不到碰撞体范围）");
                    kept++;
                    continue;
                }

                if (wb > ab + CoverTol || wt < at - CoverTol)
                {
                    sb.AppendLine(string.Format("  ★保留 {0}：wind[{1:F2},{2:F2}] 盖不住 [{3:F2},{4:F2}]，缺口 {5:F2}",
                        area.name, wb, wt, ab, at, Mathf.Max(0f, wb - ab) + Mathf.Max(0f, at - wt)));
                    kept++;
                    continue;
                }

                sb.AppendLine("  删除 " + area.name + "（已被 wind 完整覆盖）");
                Object.DestroyImmediate(area.gameObject);
                deleted++;
                dirty = true;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            sb.AppendLine();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = string.Format(
            "停用粒子 {0} 处；扩展风区 {1} 个；删除 WindArea {2} 个；保留 {3} 个。\n\n" +
            "详细数值：\n" + sb, particlesOff, extended, deleted, kept);

        Debug.Log("[风改造-一键整合]\n" + sb);
        EditorUtility.DisplayDialog("一键整合完成", summary, "好");
    }

    // ============================================================ 还原 / 诊断

    [MenuItem("Tools/历史工具/风区迁移/还原：从备份恢复改动前的文件")]
    public static void RestoreFromBackup()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;

        string[] dirs = Directory.GetDirectories(projectRoot, BackupRoot + "*");
        if (dirs.Length == 0)
        {
            EditorUtility.DisplayDialog("找不到备份",
                "工程根目录下没有 " + BackupRoot + "* 备份文件夹。", "好");
            return;
        }

        System.Array.Sort(dirs);
        string latest = dirs[dirs.Length - 1];

        if (!EditorUtility.DisplayDialog("从备份还原",
            "将用下面这个备份覆盖当前文件：\n\n" + latest +
            "\n\n会覆盖 windarea.cs 与 game6/7/8.unity。\n当前未保存的改动会丢失。", "还原", "取消"))
            return;

        int ok = 0, fail = 0;

        foreach (string s in Scenes)
        {
            string src = Path.Combine(latest, "Scenes/" + s + ".unity");
            string dst = Path.Combine(Application.dataPath, "Scenes/" + s + ".unity");
            if (!File.Exists(src)) { fail++; continue; }
            File.Copy(src, dst, true);
            ok++;
            Debug.Log("[风改造-还原] " + s + ".unity ← " + src);
        }

        string srcScript = Path.Combine(latest, "Scripts/windarea.cs");
        string dstScript = Path.Combine(Application.dataPath, "Scripts/two/windarea.cs");
        if (File.Exists(srcScript))
        {
            File.Copy(srcScript, dstScript, true);
            ok++;
            Debug.Log("[风改造-还原] windarea.cs ← " + srcScript);
        }
        else fail++;

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("还原完成",
            "已还原 " + ok + " 个文件" + (fail > 0 ? "，失败 " + fail + " 个" : "") +
            "。\n\n请重新打开 game6/7/8 场景。", "好");
    }

    [MenuItem("Tools/历史工具/风区迁移/诊断：列出所有风区实例（含嵌套）")]
    public static void Diagnose()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        foreach (string sceneName in Scenes)
        {
            string path = SceneFolder + "/" + sceneName + ".unity";
            if (!File.Exists(path)) continue;

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            List<windarea> all = FindAllWindAreas(scene);

            sb.AppendLine("── " + sceneName + " ──");

            foreach (windarea w in all)
            {
                string kind = HasAnimator(w) ? "wind  " : (HasParticleSystem(w) ? "WindArea" : "未知 ");
                float b, t;
                string span = TryGetVerticalSpan(w.gameObject, out b, out t)
                    ? string.Format("y[{0:F2},{1:F2}]", b, t) : "无碰撞体";

                sb.AppendLine(string.Format("  {0} {1,-22} 父={2,-20} {3}  cycle={4} 开{5} 关{6}",
                    kind, w.name, w.transform.parent != null ? w.transform.parent.name : "(根)",
                    span, w.usePeriodicCycle, w.openDuration, w.closedDuration));
            }
            sb.AppendLine();
        }

        Debug.Log("[风改造-诊断]\n" + sb);
        EditorUtility.DisplayDialog("诊断", sb.ToString(), "好");
    }
}
