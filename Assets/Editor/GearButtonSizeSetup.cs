using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 只调"设置入口齿轮"的大小：<b>LevelUI 预制体</b>里的 <c>Btn_Settings</c>（视觉）与
/// <c>Btn_Settings_Hit</c>（透明点击框）两个矩形。<br/><br/>
/// 菜单：<b>Tools → UI → 齿轮按钮</b><br/>
/// ・缩到 2/3（保持左上角贴边）<br/>
/// ・还原成原大小（160×160）<br/><br/>
/// <b>为什么两个一起改</b>：齿轮是"整幅视觉 + 透明点击框"两个独立矩形拼出来的。
/// 只改视觉不改点击框，会出现两种症状：① 齿轮周围一圈空地仍然能点开设置；
/// ② 光标还没碰到齿轮，齿轮就变暗/放大了（悬停的是那个看不见的大点击框）。<br/><br/>
/// <b>为什么只改预制体就够</b>：12 个关卡场景用的是它的实例，而且已逐个核实
/// <b>没有任何场景覆盖这两个矩形</b>（场景里只覆盖了根 Canvas 的零值锚点和根物体名字）。<br/><br/>
/// <b>为什么不用重算 pivot</b>：齿轮的 pivot 是 (0.5,0.5) = 矩形正中心，是个比例值；
/// 缩小后它仍然是中心，所以"悬停放大"照旧绕齿轮中心进行，不需要重跑按钮反馈工具。<br/><br/>
/// 动手前会把 LevelUI.prefab 备份到工程根目录 <c>_backup_gearsize_&lt;时间戳&gt;</c> 文件夹。
/// </summary>
public static class GearButtonSizeSetup
{
    private const string PrefabPath = "Assets/Prefabs/LevelUI.prefab";
    private const string BackupRoot = "_backup_gearsize_";

    private const string VisualName = "Btn_Settings";
    private const string HitName = "Btn_Settings_Hit";

    // 点击框的锚点就是按这个设计分辨率算出来的
    private const float UI_W = 1920f;
    private const float UI_H = 1080f;

    /// <summary>齿轮左上角（1920×1080 屏幕像素，左下为原点）。缩小和还原都不动它 —— 保持贴边。</summary>
    private static readonly Vector2 TopLeft = new Vector2(24f, 896f);

    /// <summary>原始边长</summary>
    private const float OriginalSize = 160f;

    /// <summary>目标比例（2/3）</summary>
    private const float TargetScale = 2f / 3f;

    private static float TargetSize { get { return OriginalSize * TargetScale; } }

    // ============================================================ 菜单

    [MenuItem("Tools/UI/齿轮按钮/缩到 2/3（保持左上角贴边）", false, 110)]
    public static void ShrinkToTarget()
    {
        Apply(TargetSize, "缩小齿轮");
    }

    [MenuItem("Tools/UI/齿轮按钮/还原成原大小（160×160）", false, 111)]
    public static void RestoreOriginal()
    {
        Apply(OriginalSize, "还原齿轮大小");
    }

    // ============================================================ 主流程

    private static void Apply(float size, string title)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("找不到预制体", "打不开 " + PrefabPath, "好");
            return;
        }

        string rect = string.Format("屏幕像素 x[{0:F0},{1:F0}] y[{2:F0},{3:F0}]（{4:F1}×{4:F1}）",
            TopLeft.x, TopLeft.x + size, TopLeft.y, TopLeft.y + size, size);

        if (!EditorUtility.DisplayDialog(title,
            "只改这两个矩形，其它一律不动：\n" +
            "  Btn_Settings      （齿轮视觉）\n" +
            "  Btn_Settings_Hit  （透明点击框）\n\n" +
            "新大小：" + size.ToString("F1") + "×" + size.ToString("F1") + "\n" +
            "位置：" + rect + "（左上角贴边不动）\n\n" +
            "动手前会先备份 LevelUI.prefab。继续吗？", "执行", "取消"))
            return;

        string backup = BackupPrefab();

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            RectTransform visual = FindRect(root.transform, VisualName);
            RectTransform hit = FindRect(root.transform, HitName);

            if (visual == null || hit == null)
            {
                Debug.LogError("[齿轮大小] 预制体里找不到 " + VisualName + " 或 " + HitName + "，已中止。");
                return;
            }

            RectTransform[] both = { visual, hit };

            // 锚点：左上角不变，右下角按新边长往内收
            Vector2 aMin = new Vector2(TopLeft.x / UI_W, TopLeft.y / UI_H);
            Vector2 aMax = new Vector2((TopLeft.x + size) / UI_W, (TopLeft.y + size) / UI_H);

            foreach (RectTransform rt in both)
            {
                Debug.Log(string.Format(
                    "[齿轮大小] {0}: 锚点 ({1:F6},{2:F6})~({3:F6},{4:F6})  →  ({5:F6},{6:F6})~({7:F6},{8:F6})   pivot={9}",
                    rt.name,
                    rt.anchorMin.x, rt.anchorMin.y, rt.anchorMax.x, rt.anchorMax.y,
                    aMin.x, aMin.y, aMax.x, aMax.y,
                    rt.pivot), rt);

                rt.anchorMin = aMin;
                rt.anchorMax = aMax;

                // 这两个必须保持 0：矩形 = 锚点跨度（本工程所有 UI 图层都是这么摆的）
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
            }

            // 同步 LevelUI 上的字段，否则以后跑"生成关卡UI预制体"又会变回 160
            LevelUI levelUi = root.GetComponent<LevelUI>();
            if (levelUi != null)
            {
                levelUi.buttonPosition = TopLeft;
                levelUi.buttonSize = new Vector2(size, size);
                Debug.Log("[齿轮大小] 已同步 LevelUI.buttonSize = " + levelUi.buttonSize +
                          "，buttonPosition = " + levelUi.buttonPosition);
            }

            // ---- 自检 ----
            if (visual.anchorMin != hit.anchorMin || visual.anchorMax != hit.anchorMax)
                Debug.LogError("[齿轮大小] 视觉与点击框的矩形没改成一致！请立刻执行「还原成原大小」。");

            if (visual.pivot != new Vector2(0.5f, 0.5f) || hit.pivot != new Vector2(0.5f, 0.5f))
                Debug.LogWarning("[齿轮大小] pivot 不是 (0.5,0.5)，悬停放大可能不绕齿轮中心，请检查。");

            UiButtonFx fx = hit.GetComponent<UiButtonFx>();
            if (fx == null)
                Debug.LogWarning("[齿轮大小] 点击框上没有 UiButtonFx，悬停放大/点击音效缺失（可跑一次按钮反馈①）。");
            else if (fx.popTarget != visual)
                Debug.LogWarning("[齿轮大小] UiButtonFx.popTarget 没有指向齿轮视觉，悬停放大会作用在别的物体上。");
            else
                Debug.Log("[齿轮大小] 高亮接线检查通过：变暗指向齿轮视觉、放大 popTarget 也是它。");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[齿轮大小] 已保存 " + PrefabPath + "，备份在 " + backup +
                  "。12 个关卡场景是它的实例，重新打开场景即可看到。" +
                  "点击范围与视觉完全重合，悬停变暗/放大、点击音效均不受影响。");
    }

    // ============================================================ 工具

    private static RectTransform FindRect(Transform root, string name)
    {
        Transform t = root.Find(name);
        if (t != null) return t as RectTransform;

        foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
            if (rt.name == name) return rt;

        return null;
    }

    /// <summary>把 LevelUI.prefab 原样拷到工程根目录（Assets 外，Unity 不会导入）</summary>
    private static string BackupPrefab()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string dir = Path.Combine(projectRoot, BackupRoot + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));

        string dst = Path.Combine(dir, "Prefabs", "LevelUI.prefab");
        Directory.CreateDirectory(Path.GetDirectoryName(dst));
        File.Copy(Path.Combine(projectRoot, PrefabPath), dst, true);

        return dst;
    }
}
