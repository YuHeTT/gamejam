using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 给 UI 按钮加上「悬停/按下」反馈（变暗变透明 + 放大 + 点击音效）。<br/><br/>
/// 菜单：<b>Tools → UI → 按钮反馈</b><br/>
/// ① 应用（会先自动备份 5 个 UI 场景 + LevelUI 预制体）<br/>
/// ② 还原：把按钮数据改回应用前的样子（纯数据反向操作，不依赖备份）<br/>
/// ③ 从备份文件还原资源（② 不够用时用这个，连美术/结构一起回退）<br/><br/>
/// <b>怎么认出"这个按钮对应哪张美术图层"</b>：不靠对界面排布的假设，而是把候选图层的 PNG
/// <b>解码出来算 alpha 包围盒</b>（"这张图上实际画了东西的范围"），
/// 再取那个正好罩住按钮矩形、面积最小的图层。逐条结果都打进 Console，可人工复核；<br/>
/// 找不到就跳过并告警，绝不瞎接。<br/><br/>
/// 坐标一律换算到 <b>Canvas 局部空间</b>比较，避免受 CanvasScaler 缩放 / 分辨率影响。
/// </summary>
public static class UiButtonFxSetup
{
    private const string BackupRoot = "_backup_uibtnfx_";
    private const string LevelUiPrefab = "Assets/Prefabs/LevelUI.prefab";

    private static readonly string[] UiScenes =
    {
        "Assets/Scenes/UI.unity",
        "Assets/Scenes/UI_choose.unity",
        "Assets/Scenes/UI_choose2.unity",
        "Assets/Scenes/UI_music.unity",
        "Assets/Scenes/UI_settings.unity",
    };

    // ============================================================ 菜单

    [MenuItem("Tools/UI/按钮反馈/① 应用：悬停变暗+放大+点击音效（先自动备份）", false, 100)]
    public static void ApplyFromMenu()
    {
        if (!EditorUtility.DisplayDialog("应用按钮反馈",
            "将给 5 个 UI 场景 + LevelUI 预制体里的所有按钮接上：\n" +
            "  ・悬停/按下：变暗 + 轻微变透明（亮度 ×0.88、alpha ×0.9）\n" +
            "  ・悬停放大 1.03、按下 0.97\n" +
            "  ・点击播放 shotclips 的 Element 5「点击音效」\n\n" +
            "动手前会先把这些文件备份到工程根目录的 " + BackupRoot + "<时间戳> 文件夹。\n" +
            "继续吗？", "应用", "取消"))
            return;

        string backup = BackupFiles();
        int wired = ApplyAll();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完成",
            "已处理 " + wired + " 个按钮。\n\n备份位置：\n" + backup +
            "\n\n想撤销：先用「② 还原」，不够再用「③ 从备份文件还原资源」。\n" +
            "详细逐条结果见 Console。", "好");
    }

    [MenuItem("Tools/UI/按钮反馈/② 还原：移除按钮反馈（不依赖备份）", false, 101)]
    public static void RevertFromMenu()
    {
        if (!EditorUtility.DisplayDialog("移除按钮反馈",
            "把 5 个 UI 场景 + LevelUI 预制体里的按钮改回应用前的样子：\n" +
            "  ・targetGraphic 指回自己的透明点击框，transition 回 None\n" +
            "  ・颜色、navigation 复位，美术图层 pivot 回 (0.5, 0.5)\n" +
            "  ・删掉 UiButtonFx 组件\n\n" +
            "（纯数据反向操作，即使没有备份也能回到原状）", "还原", "取消"))
            return;

        int n = RevertAll();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完成", "已还原 " + n + " 个按钮。", "好");
    }

    [MenuItem("Tools/UI/按钮反馈/③ 从备份文件还原资源（最后手段）", false, 102)]
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

        if (!EditorUtility.DisplayDialog("从备份还原资源",
            "将用下面这个备份覆盖当前场景/预制体文件：\n\n" + latest +
            "\n\n当前未保存的改动会丢失。", "还原", "取消"))
            return;

        int ok = 0, fail = 0;

        foreach (string path in AllTargetFiles())
        {
            string src = Path.Combine(latest, BackupRelativePath(path));
            if (!File.Exists(src)) { fail++; continue; }
            File.Copy(src, Path.Combine(projectRoot, path), true);
            ok++;
            Debug.Log("[按钮反馈-还原] " + path + " ← " + src);
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("还原完成",
            "已还原 " + ok + " 个文件" + (fail > 0 ? "，缺失 " + fail + " 个" : "") +
            "。\n\n请重新打开相关场景查看。", "好");
    }

    // ============================================================ 备份

    private static IEnumerable<string> AllTargetFiles()
    {
        foreach (string s in UiScenes) yield return s;
        yield return LevelUiPrefab;
    }

    /// <summary>Assets/Scenes/UI.unity → Scenes/UI.unity</summary>
    private static string BackupRelativePath(string assetPath)
    {
        return assetPath.StartsWith("Assets/") ? assetPath.Substring("Assets/".Length) : assetPath;
    }

    /// <summary>把要改的文件原样拷到工程根目录的备份文件夹（放 Assets 外，Unity 不会导入）</summary>
    private static string BackupFiles()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string dir = Path.Combine(projectRoot, BackupRoot + stamp);

        foreach (string path in AllTargetFiles())
        {
            string src = Path.Combine(projectRoot, path);
            if (!File.Exists(src)) continue;

            string dst = Path.Combine(dir, BackupRelativePath(path));
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst, true);
        }

        Debug.Log("[按钮反馈] 已备份到 " + dir);
        return dir;
    }

    // ============================================================ 应用 / 还原

    public static int ApplyAll()
    {
        int total = 0;
        string current = EditorSceneManager.GetActiveScene().path;

        try
        {
            foreach (string path in UiScenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = WireAll(scene);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, path);

                total += n;
                Debug.Log("[按钮反馈] " + path + "：接线 " + n + " 个按钮");
            }

            total += WirePrefab();
        }
        finally
        {
            if (!string.IsNullOrEmpty(current))
                EditorSceneManager.OpenScene(current, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
        }

        return total;
    }

    public static int RevertAll()
    {
        int total = 0;
        string current = EditorSceneManager.GetActiveScene().path;

        try
        {
            foreach (string path in UiScenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = UnwireAll(scene);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, path);

                total += n;
                Debug.Log("[按钮反馈] " + path + "：还原 " + n + " 个按钮");
            }

            total += UnwirePrefab();
        }
        finally
        {
            if (!string.IsNullOrEmpty(current))
                EditorSceneManager.OpenScene(current, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
        }

        return total;
    }

    private static List<Button> FindButtons(List<Transform> roots)
    {
        List<Button> list = new List<Button>();
        foreach (Transform t in roots)
        {
            if (t == null) continue;
            foreach (Button b in t.GetComponentsInChildren<Button>(true))
                list.Add(b);
        }
        return list;
    }

    private static List<Transform> SceneRoots(Scene scene)
    {
        List<Transform> roots = new List<Transform>();
        foreach (GameObject go in scene.GetRootGameObjects()) roots.Add(go.transform);
        return roots;
    }

    private static int WireAll(Scene scene)
    {
        List<Transform> roots = SceneRoots(scene);
        int n = 0;
        foreach (Button btn in FindButtons(roots))
        {
            if (Wire(btn, roots)) n++;
        }
        return n;
    }

    private static int UnwireAll(Scene scene)
    {
        List<Transform> roots = SceneRoots(scene);
        int n = 0;
        foreach (Button btn in FindButtons(roots))
        {
            Unwire(btn);
            n++;
        }
        return n;
    }

    private static int WirePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(LevelUiPrefab);
        if (root == null)
        {
            Debug.LogError("[按钮反馈] 打不开 " + LevelUiPrefab);
            return 0;
        }

        int n = 0;
        try
        {
            List<Transform> roots = new List<Transform> { root.transform };
            foreach (Button btn in FindButtons(roots))
            {
                if (Wire(btn, roots)) n++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, LevelUiPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log("[按钮反馈] " + LevelUiPrefab + "：接线 " + n + " 个按钮");
        return n;
    }

    private static int UnwirePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(LevelUiPrefab);
        if (root == null) return 0;

        int n = 0;
        try
        {
            foreach (Button btn in FindButtons(new List<Transform> { root.transform }))
            {
                Unwire(btn);
                n++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, LevelUiPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log("[按钮反馈] " + LevelUiPrefab + "：还原 " + n + " 个按钮");
        return n;
    }

    // ------------------------------------------------------------ 单个按钮

    /// <summary>
    /// 给"刚生成好的"点击框接线：搜索范围自动取当前场景（或预制体内容的最外层根）。
    /// 各生成器（MenuUIBuilder / SettingsPanelSetup）用这个，就不必各自维护一份"哪个按钮对应哪张图"的表格。
    /// </summary>
    public static bool WireByPixel(Button btn)
    {
        if (btn == null) return false;

        Scene scene = btn.gameObject.scene;
        List<Transform> roots;

        if (scene.IsValid() && scene.isLoaded)
        {
            roots = SceneRoots(scene);
        }
        else
        {
            // 预制体内容（PrefabUtility.LoadPrefabContents）不属于任何已加载场景
            Transform root = btn.transform;
            while (root.parent != null) root = root.parent;
            roots = new List<Transform> { root };
        }

        return Wire(btn, roots);
    }

    /// <summary>给一个按钮接线，返回是否成功找到视觉图层</summary>
    public static bool Wire(Button btn, List<Transform> searchRoots)
    {        if (btn == null) return false;

        Image visual = ResolveVisual(btn, searchRoots);
        if (visual == null)
        {
            Debug.LogWarning("[按钮反馈] 找不到「" + btn.name + "」对应的美术图层，已跳过。", btn);
            return false;
        }

        LevelUiUtil.WireHighlight(btn, visual);

        Vector2 pivot = ((RectTransform)visual.transform).pivot;
        Debug.Log(string.Format("[按钮反馈] {0,-20} ← 图层「{1}」  pivot=({2:F3},{3:F3})",
            btn.name, visual.name, pivot.x, pivot.y), btn);

        return true;
    }

    /// <summary>把一个按钮改回应用前的状态</summary>
    public static void Unwire(Button btn)
    {
        if (btn == null) return;

        Image own = btn.GetComponent<Image>();

        // 接线时把 targetGraphic 指向了美术图层；先记下它，等下要把 pivot 复位
        // （targetGraphic 是 Graphic，取它的 transform 才是 RectTransform）
        RectTransform visual = btn.targetGraphic != null
            ? btn.targetGraphic.transform as RectTransform
            : null;
        if (own != null && visual == own.rectTransform) visual = null;

        // 1) 缩放组件（先归位再删）
        UiButtonFx fx = btn.GetComponent<UiButtonFx>();
        if (fx != null)
        {
            fx.ResetScale();
            Object.DestroyImmediate(fx);
        }

        // 2) targetGraphic / transition / 颜色 回默认
        if (own != null) btn.targetGraphic = own;
        btn.transition = Selectable.Transition.None;
        btn.colors = ColorBlock.defaultColorBlock;

        // 3) navigation 回默认
        Navigation nav = btn.navigation;
        nav.mode = Navigation.Mode.Automatic;
        btn.navigation = nav;

        // 4) pivot 复位。全部美术图层原本就是 (0.5, 0.5)（已核对 5 个场景 + 预制体的序列化数据）。
        //    注意只改 pivot，不动 localScale —— 左箭头靠 scale.x = -1 镜像，动它会把箭头翻回去。
        if (visual != null) visual.pivot = new Vector2(0.5f, 0.5f);
    }

    // ============================================================ 视觉图层识别

    private static readonly Dictionary<string, Rect> BboxCache = new Dictionary<string, Rect>();

    private static Image ResolveVisual(Button btn, List<Transform> searchRoots)
    {
        RectTransform btnRt = btn.transform as RectTransform;
        if (btnRt == null) return null;

        RectTransform reference = FindReferenceRect(btnRt);

        // 按钮自身在"参考矩形（Canvas）比例坐标"里的矩形
        Rect btnNorm;
        if (!TryGetNormalizedRect(btnRt, reference, out btnNorm)) return null;

        Vector2 center = btnNorm.center;

        Image best = null;
        float bestArea = float.MaxValue;
        HashSet<Image> seen = new HashSet<Image>();

        foreach (Transform root in searchRoots)
        {
            if (root == null) continue;

            foreach (Image img in root.GetComponentsInChildren<Image>(true))
            {
                if (img == null || !seen.Add(img)) continue;

                if (img.sprite == null) continue;                 // 纯色块 / 透明点击框
                if (img.transform == btn.transform) continue;     // 自己那个透明点击框

                Rect box;
                if (!TryGetContentNormalizedRect(img, reference, out box)) continue;

                // 背景层（内容占了画布一半以上）不参与，否则它会罩住所有按钮
                if (box.width * box.height > 0.5f) continue;

                if (!box.Contains(center)) continue;

                float area = box.width * box.height;
                if (area < bestArea)
                {
                    bestArea = area;
                    best = img;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// 参考矩形：优先取所在 Canvas 的根 RectTransform，没有就取最外层的 RectTransform。<br/>
    /// 只拿它当"比例坐标系的原点"，不使用它的 position / scale / rect —— 那些在预制体资源里是无效值。
    /// </summary>
    private static RectTransform FindReferenceRect(RectTransform rt)
    {
        Canvas canvas = rt.GetComponentInParent<Canvas>(true);
        if (canvas != null)
        {
            RectTransform c = canvas.transform as RectTransform;
            if (c != null) return c;
        }

        RectTransform top = rt;
        while (top.parent != null)
        {
            RectTransform p = top.parent as RectTransform;
            if (p == null) break;
            top = p;
        }
        return top;
    }

    /// <summary>
    /// 把一个 RectTransform 的矩形换算成"相对参考矩形的比例矩形"（0~1）。<br/><br/>
    /// <b>为什么不用 TransformPoint</b>：预制体里的根 Canvas 的 RectTransform 序列化成了
    /// <c>m_LocalScale = (0,0,0)</c>（运行时才由 Canvas 驱动），一旦走世界坐标就会除出 NaN，
    /// 于是预制体里的按钮一个都认不出来。改成沿父链把 <c>anchorMin/anchorMax</c> 逐级复合，
    /// 完全不需要 position / scale / rect，场景和预制体资源里结果一致，也不受 CanvasScaler 影响。<br/><br/>
    /// 只支持"锚点定位 + offset 为 0"的写法（本工程所有这些图层都是这么摆的）；遇到 offset 不为 0 返回 false。
    /// </summary>
    private static bool TryGetNormalizedRect(RectTransform rt, RectTransform reference, out Rect norm)
    {
        norm = new Rect(0f, 0f, 1f, 1f);
        if (rt == null || reference == null) return false;
        if (rt == reference) return true;

        // 从 rt 往上收集到 reference（不含）为止
        List<RectTransform> chain = new List<RectTransform>();
        RectTransform cur = rt;
        while (cur != null && cur != reference)
        {
            chain.Add(cur);
            cur = cur.parent as RectTransform;
        }
        if (cur != reference) return false;      // 不在同一棵树下

        // 由外向内复合：child = parent.min + (anchorMin..anchorMax) × parent.size
        Rect acc = new Rect(0f, 0f, 1f, 1f);
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            RectTransform t = chain[i];

            if (t.offsetMin != Vector2.zero || t.offsetMax != Vector2.zero) return false;

            Vector2 amin = t.anchorMin;
            Vector2 amax = t.anchorMax;

            acc = new Rect(
                acc.x + amin.x * acc.width,
                acc.y + amin.y * acc.height,
                (amax.x - amin.x) * acc.width,
                (amax.y - amin.y) * acc.height);
        }

        norm = acc;
        return true;
    }

    /// <summary>图层"实际画了东西"的范围（相对参考矩形的比例坐标）</summary>
    private static bool TryGetContentNormalizedRect(Image img, RectTransform reference, out Rect box)
    {
        box = new Rect();

        Rect layer;
        if (!TryGetNormalizedRect(img.rectTransform, reference, out layer)) return false;

        Rect bbox;
        if (!TryGetSpriteContentBbox(img.sprite, out bbox)) return false;

        // 贴图内的归一化包围盒 → 图层矩形内的比例位置
        box = new Rect(
            layer.x + bbox.xMin * layer.width,
            layer.y + bbox.yMin * layer.height,
            bbox.width * layer.width,
            bbox.height * layer.height);

        return true;
    }

    /// <summary>
    /// 解码 PNG，算 alpha &gt; 0 的包围盒（在 sprite 矩形内的归一化坐标，y 从底部算）。<br/>
    /// 这就是"这张图上实际画了东西的地方" —— 美术是一张整幅画布只在某个位置画了东西，
    /// 所以它精确等于按钮所在的位置，不依赖任何人工记录的坐标表。
    /// </summary>
    private static bool TryGetSpriteContentBbox(Sprite sprite, out Rect bbox)
    {
        bbox = new Rect();

        string path = AssetDatabase.GetAssetPath(sprite);
        if (string.IsNullOrEmpty(path)) return false;

        if (BboxCache.TryGetValue(path, out bbox)) return true;
        if (!File.Exists(path)) return false;

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!tex.LoadImage(File.ReadAllBytes(path))) return false;

            Color32[] px = tex.GetPixels32();
            int w = tex.width, h = tex.height;

            int x0 = w, x1 = -1, y0 = h, y1 = -1;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (px[row + x].a == 0) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }

            if (x1 < 0) return false;   // 整张全透明

            Rect sr = sprite.rect;
            if (sr.width <= 0f || sr.height <= 0f) return false;

            // Texture2D 像素的 y 本来就是从底部算，与 Unity UI 的归一化方向一致
            bbox = new Rect(
                (x0 - sr.xMin) / sr.width,
                (y0 - sr.yMin) / sr.height,
                (x1 - x0 + 1) / sr.width,
                (y1 - y0 + 1) / sr.height);

            BboxCache[path] = bbox;
            return true;
        }
        finally
        {
            Object.DestroyImmediate(tex);
        }
    }
}
