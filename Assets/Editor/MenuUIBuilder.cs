using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 一键搭建主界面与三个子界面。菜单：Tools → UI → 生成主界面与子界面<br/><br/>
/// <b>核心做法：整幅全屏叠加（overlay），不做坐标换算。</b><br/>
/// 所有美术素材都是同一张 2500×1500 画布，元素画在画布绝对位置上。
/// 把每张图当作"整幅全屏图层"拉伸到 Canvas 上，元素相对背景的位置就是美术稿的位置——
/// 不依赖贴图导入尺寸：Unity 无论把图压成 2048 还是 4096，所有图层被缩放的比例完全一致，
/// 不会产生错位。<br/><br/>
/// 子界面采用<b>自动扫描</b>：把该文件夹里所有素材（背景 + 全部元素）按序号叠加上去。
/// 同名素材若存在 <c>_new</c> 版本（已抠白底）则优先使用 <c>_new</c>。<br/><br/>
/// 点击由独立的<b>隐形点击框</b>承担，坐标走 1920×1080 设计分辨率，与贴图尺寸无关。
/// </summary>
public static class MenuUIBuilder
{
    // 设计分辨率（16:9）
    private const float UI_W = 1920f;
    private const float UI_H = 1080f;

    // 美术画布尺寸
    private const float ART_W = 2500f;
    private const float ART_H = 1500f;
    private const float ART_TO_UI = UI_W / ART_W;      // 0.768

    /// <summary>美术坐标的 y 方向</summary>
    public enum ArtYAxis
    {
        /// <summary>点击框挂在图层内部，不需要换算</summary>
        Auto = 0,

        /// <summary>美术 y 向下增大（顶部为 0）—— PNG 像素行的自然方向。
        /// 若"点击框偏到反方向"，改成这个</summary>
        Down = 1,

        /// <summary>美术 y 向上增大（左下为原点），与 Canvas 一致</summary>
        Up = 2,
    }

    /// <summary>
    /// 美术 y 轴方向。<br/>
    /// 若点击框整体上下错位（画面上元素在上面、点击区却在下面，或反之），
    /// 用菜单 Tools → UI → 切换点击框的 Y 方向 一键盘切换，再重新生成一次即可。
    /// </summary>
    private static ArtYAxis artY
    {
        get { return (ArtYAxis)EditorPrefs.GetInt("MenuUIBuilder.ArtYAxis", (int)ArtYAxis.Down); }
        set { EditorPrefs.SetInt("MenuUIBuilder.ArtYAxis", (int)value); }
    }

    [MenuItem("Tools/UI/切换点击框的 Y 方向（上下错位时用）")]
    private static void ToggleArtY()
    {
        artY = artY == ArtYAxis.Down ? ArtYAxis.Up : ArtYAxis.Down;
        Debug.Log("[MenuUIBuilder] 点击框 Y 方向已切换为：" + artY +
                  "（Down = 美术 y 向下增大；Up = 美术 y 向上增大）。" +
                  "请重新执行 Tools → UI → 生成主界面与子界面 后查看效果。");
    }

    /// <summary>把美术的 (x, y) 换算成图层/Canvas 的 anchoredPosition（左下原点，y 向上）</summary>
    private static Vector2 ArtToAnchored(float x, float y)
    {
        float vx = x * ART_TO_UI;
        float vy;

        switch (artY)
        {
            case ArtYAxis.Down:
                // 美术 y 向下 → 镜像到 y 向上
                vy = (ART_H - y) * ART_TO_UI;
                break;
            case ArtYAxis.Up:
                vy = y * ART_TO_UI;
                break;
            default:
                vy = y * ART_TO_UI;
                break;
        }

        return new Vector2(vx, vy);
    }

    /// <summary>美术矩形 (x0,y0,x1,y1) → 点击框的中心与小大（图层坐标，左下原点）</summary>
    private static void ArtRectToAnchored(float x0, float y0, float x1, float y1,
                                          out Vector2 center, out Vector2 size)
    {
        Vector2 a = ArtToAnchored(x0, y0);
        Vector2 b = ArtToAnchored(x1, y1);

        center = (a + b) * 0.5f;
        size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
    }

    private const int MaxSpriteSize = 4096;

    private const string ArtMain = "Assets/Art/主界面-1等9项文件";
    private const string ArtMusic = "Assets/Art/音乐设置-1等9项文件";
    private const string ArtSettings = "Assets/Art/设置-1等5项文件";
    private const string ArtChoose = "Assets/Art/选关-1等10项文件";

    private const string SceneMain = "Assets/Scenes/UI.unity";
    private const string SceneChoose = "Assets/Scenes/UI_choose.unity";
    private const string SceneMusic = "Assets/Scenes/UI_music.unity";
    private const string SceneSettings = "Assets/Scenes/UI_settings.unity";

    [MenuItem("Tools/UI/生成主界面与子界面")]
    public static void BuildAll()
    {
        if (!EditorUtility.DisplayDialog("生成 UI",
            "将重建四个场景的 UI：\n" +
            "  UI（主界面）/ UI_choose / UI_music / UI_settings\n\n" +
            "做法：所有素材按【整幅全屏图层】叠加，位置由美术画稿决定，\n" +
            "因此不会因贴图导入尺寸而产生错位。\n" +
            "子界面会自动扫描该文件夹里的全部素材并叠加。\n\n" +
            "每个场景生成 UICamera + Canvas(1920×1080) + EventSystem。\n" +
            "同时把素材导入尺寸提到 4096、并把四个场景加入 Build Settings。继续吗？",
            "生成", "取消"))
            return;

        FixArtImportSettings();
        AssetDatabase.Refresh();

        BuildMainMenu();
        BuildSubScene(SceneChoose, ArtChoose, "选关-10_new.png", NavTarget.Main, true);
        BuildSubScene(SceneMusic, ArtMusic, "音乐设置-2_new.png", NavTarget.Main);
        BuildSubScene(SceneSettings, ArtSettings, "设置-5_new.png", NavTarget.Main);

        AddScenesToBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完成",
            "已生成主界面与三个子界面。\n\n" +
            "参考分辨率 1920×1080（16:9），四个场景已加入 Build Settings。\n" +
            "各场景叠加的图层数见 Console。", "好");
    }

    // ------------------------------------------------------------ 主界面

    private static void BuildMainMenu()
    {
        Scene scene = OpenOrCreateScene(SceneMain);
        GameObject canvas = CreateCanvas();

        AddOverlay(canvas, ArtMain + "/主界面-1.png", "BG");
        AddOverlay(canvas, ArtMain + "/主界面-9_new.png", "Deco_CatLeft");
        AddOverlay(canvas, ArtMain + "/主界面-8_new.png", "Deco_CatGourd");
        AddOverlay(canvas, ArtMain + "/主界面-7_new.png", "Title");

        // 按钮：整幅视觉图层 + 隐形点击框（点击框用美术画布坐标）
        AddButtonOverlay(canvas, ArtMain + "/主界面-6_new.png", "Btn_Start",
                         1164, 795, 1389, 898, NavTarget.Choose);      // 开始 → 选关
        AddButtonOverlay(canvas, ArtMain + "/主界面-5_new.png", "Btn_Quit",
                         1179, 950, 1390, 1047, NavTarget.Quit);       // 退出 → 结束运行
        AddButtonOverlay(canvas, ArtMain + "/主界面-2_new.png", "Btn_Music",
                         240, 1210, 381, 1318, NavTarget.Music);       // 音量 → 音乐管理
        AddButtonOverlay(canvas, ArtMain + "/主界面-3_new.png", "Btn_Settings",
                         448, 1197, 567, 1320, NavTarget.Settings);    // 设置 → 设置界面

        SaveScene(scene, SceneMain);
    }

    // ------------------------------------------------------------ 子界面

    /// <summary>
    /// 子界面：自动扫描 <paramref name="artFolder"/> 里的全部素材并整幅叠加。<br/>
    /// 点击框<b>挂到对应素材图层的子树里</b>（而不是 Canvas 根下），
    /// 这样它与该图层共享同一个坐标原点与缩放，天然对齐视觉位置。
    /// </summary>
    private static void BuildSubScene(string scenePath, string artFolder,
                                      string backSpriteName, NavTarget backTarget,
                                      bool withLevelButtons = false)
    {
        Scene scene = OpenOrCreateScene(scenePath);
        GameObject canvas = CreateCanvas();

        List<string> files = CollectLayerFiles(artFolder);
        Dictionary<string, GameObject> layerByName = new Dictionary<string, GameObject>();

        foreach (string fileName in files)
        {
            string path = artFolder + "/" + fileName;
            string layerName = Path.GetFileNameWithoutExtension(fileName);

            Image img = AddOverlay(canvas, path, layerName);
            if (img != null) layerByName[layerName] = img.gameObject;
        }

        Debug.Log("[MenuUIBuilder] " + scenePath + " 叠加了 " + layerByName.Count + " 个图层。");

        // 返回按钮：点击框挂到返回素材那一层里（素材位置固定在左上角 233,142 - 458,334）
        GameObject backLayer = FindLayer(layerByName, backSpriteName);
        if (backLayer != null)
            AddChildHitBox(backLayer, "Btn_Back", 233, 142, 458, 334, backTarget);
        else
            Debug.LogWarning("[MenuUIBuilder] " + scenePath + " 找不到返回按钮素材层 '" + backSpriteName + "'");

        // 选关：8 个关卡按钮，每个点击框挂到它自己的素材层里
        if (withLevelButtons)
        {
            AddLevelHitBox(layerByName, "选关-2", "选关-2_new", 1, 477, 810, 832, 1194);
            AddLevelHitBox(layerByName, "选关-3", "选关-3_new", 2, 867, 804, 1289, 1177);
            AddLevelHitBox(layerByName, "选关-4", "选关-4_new", 3, 1284, 808, 1651, 1181);
            AddLevelHitBox(layerByName, "选关-5", "选关-5_new", 4, 1670, 813, 2063, 1184);
            AddLevelHitBox(layerByName, "选关-6", "选关-6_new", 5, 470, 331, 820, 720);
            AddLevelHitBox(layerByName, "选关-7", "选关-7_new", 6, 866, 326, 1216, 707);
            AddLevelHitBox(layerByName, "选关-8", "选关-8_new", 7, 1247, 332, 1666, 702);
            AddLevelHitBox(layerByName, "选关-9", "选关-9_new", 8, 1648, 339, 2037, 703);
        }

        SaveScene(scene, scenePath);
    }

    /// <summary>按图层名找 GameObject；名字不完全一致时按末尾序号兜底匹配</summary>
    private static GameObject FindLayer(Dictionary<string, GameObject> map, string wanted)
    {
        if (map.ContainsKey(wanted)) return map[wanted];

        // 去掉扩展名
        string key = Path.GetFileNameWithoutExtension(wanted);
        if (map.ContainsKey(key)) return map[key];

        // 按末尾序号匹配（例如 "选关-2" 与 "选关-2_new"）
        Match m = Regex.Match(key, @"(\d+)(_new)?$");
        if (!m.Success) return null;
        int idx = int.Parse(m.Groups[1].Value);

        foreach (KeyValuePair<string, GameObject> kv in map)
        {
            Match k = Regex.Match(kv.Key, @"(\d+)(_new)?$");
            if (k.Success && int.Parse(k.Groups[1].Value) == idx) return kv.Value;
        }

        return null;
    }

    private static void AddLevelHitBox(Dictionary<string, GameObject> layerByName,
                                       string nameA, string nameB, int level,
                                       float x0, float y0, float x1, float y1)
    {
        GameObject layer = FindLayer(layerByName, nameA);
        if (layer == null) layer = FindLayer(layerByName, nameB);
        if (layer == null)
        {
            Debug.LogWarning("[MenuUIBuilder] 找不到关卡 " + level + " 的素材层（" + nameA + "），跳过点击框。");
            return;
        }

        GameObject hit = AddChildHitBox(layer, "Btn_Level" + level, x0, y0, x1, y1, null);
        Button btn = hit.GetComponent<Button>();

        MenuChooseLevel choose = hit.AddComponent<MenuChooseLevel>();
        choose.levelNumber = level;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, choose.Go);
    }

    /// <summary>
    /// 在某个整幅图层内部创建点击框。<br/>
    /// 父图层是 Stretch 的 RectTransform，所以点击框用它自己的 (x0,y0,x1,y1) 直接定位于同一坐标系，
    /// 不需要任何跨坐标系的换算。
    /// </summary>
    private static GameObject AddChildHitBox(GameObject parent, string name,
                                             float x0, float y0, float x1, float y1,
                                             NavTarget? target)
    {
        GameObject hit = new GameObject(name + "_Hit",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        hit.transform.SetParent(parent.transform, false);

        RectTransform rt = (RectTransform)hit.transform;
        rt.anchorMin = Vector2.zero;              // 图层坐标系原点（左下）
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 center, size;
        ArtRectToAnchored(x0, y0, x1, y1, out center, out size);

        rt.sizeDelta = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        rt.anchoredPosition = center;

        Image img = hit.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = true;

        Button btn = hit.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;

        if (target.HasValue)
        {
            if (target.Value == NavTarget.Quit)
            {
                MenuQuit quit = hit.AddComponent<MenuQuit>();
                UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, quit.QuitGame);
            }
            else
            {
                MenuNavigation nav = hit.AddComponent<MenuNavigation>();
                nav.targetScene = SceneNameOf(target.Value);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, nav.Go);
            }
        }

        return hit;
    }

    /// <summary>
    /// 收集一个素材文件夹里所有要叠加的图：<br/>
    /// ・只取 .png（排除 .meta）<br/>
    /// ・同一序号若同时存在 原名.png 与 原名_new.png，优先 _new（已抠白底）<br/>
    /// ・按文件名末尾的序号排序，保证叠加顺序 = 素材序号顺序
    /// </summary>
    private static List<string> CollectLayerFiles(string artFolder)
    {
        List<string> result = new List<string>();
        if (!Directory.Exists(artFolder))
        {
            Debug.LogWarning("[MenuUIBuilder] 素材文件夹不存在：" + artFolder);
            return result;
        }

        Dictionary<int, string> chosen = new Dictionary<int, string>();
        Regex numTail = new Regex(@"(\d+)(_new)?$");

        string[] entries = Directory.GetFiles(artFolder, "*.png");
        foreach (string full in entries)
        {
            string fileName = Path.GetFileName(full);
            if (fileName.EndsWith(".meta")) continue;

            Match m = numTail.Match(Path.GetFileNameWithoutExtension(fileName));
            if (!m.Success) continue;

            int index = int.Parse(m.Groups[1].Value);
            bool isNew = m.Groups[2].Success;

            if (!chosen.ContainsKey(index))
            {
                chosen[index] = fileName;
            }
            else
            {
                // 已有同序号的：_new 优先
                bool existingIsNew = chosen[index].Contains("_new");
                if (isNew && !existingIsNew) chosen[index] = fileName;
            }
        }

        List<int> keys = new List<int>(chosen.Keys);
        keys.Sort();
        foreach (int k in keys) result.Add(chosen[k]);

        return result;
    }

    // ------------------------------------------------------------ 构建工具

    private enum NavTarget { Main, Choose, Music, Settings, Quit }

    private static Scene OpenOrCreateScene(string path)
    {
        if (File.Exists(path))
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static void SaveScene(Scene scene, string path)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log("[MenuUIBuilder] 已生成 " + path);
    }

    private static GameObject CreateCanvas()
    {
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.GetComponent<Canvas>() != null) Object.DestroyImmediate(go);
            else if (go.GetComponent<Camera>() != null) Object.DestroyImmediate(go);
        }

        EnsureCamera();
        EnsureEventSystem();

        GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UI_W, UI_H);   // 1920×1080
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvasGo;
    }

    /// <summary>
    /// 纯 UI 场景其实不需要相机（Screen Space Overlay 由 Unity 直接画到屏幕，
    /// EventSystem 也不依赖相机）。加一个只为：<br/>
    /// 1) 避免 Game 视图出现 "No cameras rendering" 提示；<br/>
    /// 2) 为将来加背景特效 / 后处理 / AudioListener 留口子。<br/>
    /// Culling Mask = Nothing，不渲染任何场景物体。
    /// </summary>
    private static void EnsureCamera()
    {
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.GetComponent<Camera>() != null) return;
        }

        GameObject camGo = new GameObject("UICamera", typeof(Camera), typeof(AudioListener));
        Camera cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.118f, 0.118f, 0.118f, 1f);   // #1E1E1E
        cam.cullingMask = 0;              // Nothing
        cam.orthographic = true;
        cam.orthographicSize = UI_H * 0.5f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
        cam.depth = -100f;
        camGo.transform.position = new Vector3(0f, 0f, -10f);
    }

    private static void EnsureEventSystem()
    {
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) return;
        }

        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 把一个素材作为【整幅全屏图层】铺到 Canvas 上。<br/>
    /// 位置由素材自身的透明区域决定，不做任何坐标换算——这是全屏叠加法的关键。
    /// </summary>
    private static Image AddOverlay(GameObject canvas, string spritePath, string name)
    {
        Sprite sprite = LoadSprite(spritePath);
        if (sprite == null) return null;

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)go.transform);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = false;        // 与背景铺在同一块矩形上
        img.raycastTarget = false;         // 视觉层一律不接收点击
        return img;
    }

    /// <summary>
    /// 按钮 = 整幅全屏视觉图层 + 挂在<b>该图层子树内</b>的隐形点击框。<br/>
    /// 挂在图层内部是关键：点击框与图层共享同一个坐标原点与缩放，
    /// 于是"用美术坐标算出的位置"与"画面上看到的位置"必然一致，不会再错位。
    /// </summary>
    private static void AddButtonOverlay(GameObject canvas, string spritePath, string name,
                                         float x0, float y0, float x1, float y1, NavTarget target)
    {
        Image visual = AddOverlay(canvas, spritePath, name);
        if (visual == null) return;

        AddChildHitBox(visual.gameObject, name, x0, y0, x1, y1, target);
    }

    private static string SceneNameOf(NavTarget target)
    {
        switch (target)
        {
            case NavTarget.Choose: return "UI_choose";
            case NavTarget.Music: return "UI_music";
            case NavTarget.Settings: return "UI_settings";
            default: return "UI";
        }
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning("[MenuUIBuilder] 找不到精灵（请确认该图按 Sprite 导入）：" + path);
        return sprite;
    }

    // ------------------------------------------------------------ 素材导入设置

    private static void FixArtImportSettings()
    {
        string[] folders = { ArtMain, ArtMusic, ArtSettings, ArtChoose };

        foreach (string folder in folders)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool changed = false;

                if (importer.maxTextureSize < MaxSpriteSize)
                {
                    importer.maxTextureSize = MaxSpriteSize;
                    changed = true;
                }

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }

                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    changed = true;
                }

                if (changed) importer.SaveAndReimport();
            }
        }
    }

    // ------------------------------------------------------------ 构建列表

    private static void AddScenesToBuildSettings()
    {
        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        // UI 场景 + 所有 gameN 场景（选关按钮要跳它们，不在 Build Settings 里会加载失败）
        List<string> wanted = new List<string> { SceneMain, SceneChoose, SceneMusic, SceneSettings };

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^game\d+$") && !wanted.Contains(path))
                wanted.Add(path);
        }

        foreach (string path in wanted)
        {
            bool exists = false;
            foreach (EditorBuildSettingsScene s in list)
            {
                if (s.path == path) { exists = true; break; }
            }
            if (!exists) list.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = list.ToArray();
    }
}
