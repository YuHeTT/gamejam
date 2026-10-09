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
        // 已实测确认：本工程美术素材的 y 轴是"向上增大"（与 Canvas 一致），
        // 用 Down 会导致所有点击框整体上下镜像。
        get { return (ArtYAxis)EditorPrefs.GetInt("MenuUIBuilder.ArtYAxis", (int)ArtYAxis.Up); }
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

    // ============================================================================
    //  坐标系统一说明（重要）
    //  美术素材都画在同一张 2500×1500 的画布上；而屏幕可见区是 1920×1080。
    //  两者比例不同（2500/1500=1.667，1920/1080=1.778），所以**不能**用
    //  "美术像素 × 固定系数" 去算屏幕位置——那样在某个轴上必然偏移。
    //
    //  唯一稳的做法：把美术坐标转成 **0~1 的比例**，再用 anchorMin/anchorMax 设到
    //  RectTransform 上。这样元素落在"画面内的相对位置"与美术稿完全一致，
    //  与 Canvas 实际尺寸、分辨率、缩放都无关。
    // ============================================================================

    /// <summary>美术画布 y → 比例（0=底，1=顶）。artY 控制是否需要镜像。</summary>
    private static float ArtYToRatio(float artYValue)
    {
        float r = artYValue / ART_H;
        if (artY == ArtYAxis.Down) r = 1f - r;      // 美术 y 向下增大 → 镜像
        return Mathf.Clamp01(r);
    }

    /// <summary>把美术矩形 (x0,y0,x1,y1) 直接设成 RectTransform 的比例锚点</summary>
    private static void SetAnchorRectFromArt(RectTransform rt,
                                             float x0, float y0, float x1, float y1)
    {
        float ax0 = Mathf.Clamp01(x0 / ART_W);
        float ax1 = Mathf.Clamp01(x1 / ART_W);
        float ay0 = ArtYToRatio(y0);
        float ay1 = ArtYToRatio(y1);

        // 保证 min < max（Y 镜像后可能颠倒）
        rt.anchorMin = new Vector2(Mathf.Min(ax0, ax1), Mathf.Min(ay0, ay1));
        rt.anchorMax = new Vector2(Mathf.Max(ax0, ax1), Mathf.Max(ay0, ay1));
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private const int MaxSpriteSize = 4096;

    private const string ArtMain = "Assets/Art/主界面-1等9项文件";
    private const string ArtMusic = "Assets/Art/音乐设置-1等9项文件";
    private const string ArtSettings = "Assets/Art/设置-1等5项文件";
    private const string ArtChoose = "Assets/Art/选关-1等10项文件";
    private const string ArtArrow = "Assets/Art/arrow";

    private const string SceneMain = "Assets/Scenes/UI.unity";
    private const string SceneChoose = "Assets/Scenes/UI_choose.unity";
    private const string SceneChoose2 = "Assets/Scenes/UI_choose2.unity";
    private const string SceneMusic = "Assets/Scenes/UI_music.unity";
    private const string SceneSettings = "Assets/Scenes/UI_settings.unity";

    /// <summary>
    /// 开始/退出按钮的点击框整体微调量（1920×1080 设计像素）。<br/>
    /// 正值 = 屏幕上往下，负值 = 往上。
    /// </summary>
    private const float MainButtonYOffsetPixels = 20f;

    /// <summary>
    /// 选关素材的"文件名序号 → 图上实际数字"是反的（实测）：
    /// 选关-2=8, -3=7, -4=6, -5=5, -6=4, -7=3, -8=2, -9=1。
    /// 所以数字 n 对应的文件名是 (10 - n)。
    /// </summary>
    private static string ChooseSpriteForNumber(int number)
    {
        return "选关-" + (10 - number);
    }

    /// <summary>
    /// arrow 素材的原始画布是 2048×2048，箭头本身只占中间 582×439（居中）。
    /// 换算：箭头包围盒 (774,814)-(1356,1253)，中心 (1065,1033.5)；
    /// 映射到 2500×1500 的 UI 画布后中心约 (1300, 239)。<br/>
    /// 下面给出两个占位矩形，宽高比严格保持 582:439 = 1.326（避免拉伸），
    /// 尺寸与"返回按钮"（173×148 屏幕像素）相当。<br/>
    /// 右下角箭头：屏幕约 x[1713,1897] y[161,300]；
    /// 左下角箭头：X 左右镜像成 x[65,249]，Y 不变。
    /// </summary>
    private const float ArrowRightX0 = 2266f;
    private const float ArrowRightY0 = 252f;
    private const float ArrowRightX1 = 2453f;
    private const float ArrowRightY1 = 403f;

    // 2500 - 2453 = 47，2500 - 2266 = 234 ⇒ 与右边箭头左右镜像
    private const float ArrowLeftX0 = 47f;
    private const float ArrowLeftY0 = ArrowRightY0;
    private const float ArrowLeftX1 = 234f;
    private const float ArrowLeftY1 = ArrowRightY1;

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
        BuildChooseScene();
        BuildChoose2Scene();
        BuildSubScene(SceneMusic, ArtMusic, "音乐设置-2_new.png", NavTarget.Main);
        BuildSettingsScene();

        AddScenesToBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完成",
            "已生成主界面与各子界面。\n\n" +
            "参考分辨率 1920×1080（16:9），相关场景已加入 Build Settings。\n" +
            "各场景叠加的图层数见 Console。", "好");
    }

    // ------------------------------------------------------------ 选关 / 选关2

    /// <summary>选关界面：背景 + 8 个关卡按钮（→ game1~8）+ 右下角箭头（→ UI_choose2）+ 返回</summary>
    // ============================================================================
    //  两个选关界面共用同一套构建代码。
    //  唯一差别：
    //    ① UI_choose  有 8 个按钮（1~8，→ game1~8），箭头在右下角→UI_choose2
    //    ② UI_choose2 只有上面那排 4 个按钮（1~4，→ other1~4），箭头在左下角→UI_choose
    //  这样两个界面的按钮大小、位置、点击区、背景完全一致，不会出现"改一个废一个"。
    // ============================================================================

    // ============================================================================
    //  箭头（两个选关界面各一个）
    //  位置：以可见区 1920×1080 为基准的屏幕像素（左下为原点），不经过美术画布换算。
    //  尺寸：180×136 px（已确认合适，不要改）。
    //  触发区：在箭头矩形基础上每边外扩，且**挂在 Canvas 上**（不跟随箭头的水平翻转）。
    // ============================================================================
    private const float ArrowW = 180f;
    private const float ArrowH = 136f;
    private const float ArrowPadPt = 0.10f;

    /// <summary>右下角箭头（choose）：贴住可见区右下角</summary>
    private static readonly Vector2 ArrowRightXy = new Vector2(1880f - ArrowW, 40f);

    /// <summary>左下角箭头（choose2）：与右下角箭头左右镜像</summary>
    private static readonly Vector2 ArrowLeftXy = new Vector2(40f, 40f);

    private static void BuildChooseScene()
    {
        BuildChooseVariant(SceneChoose, true, null, "game",
                           "Btn_ArrowRight", ArrowRightXy, false, NavTarget.Choose2);
    }

    private static void BuildChoose2Scene()
    {
        // ① 去掉下半部分 5~8 的"贴图"（只去贴图，点击框与其它一切不动）
        string[] hideNames = { "选关-2", "选关-3", "选关-4", "选关-5" };

        // ② 这里的 1~4 按钮要跳 other1~other4（不是 game1~game4）
        BuildChooseVariant(SceneChoose2, false, hideNames, "other",
                           "Btn_ArrowLeft", ArrowLeftXy, true, NavTarget.Choose);
    }

    /// <summary>
    /// 构建一个选关界面。<br/>
    /// <paramref name="withBottomRow"/> = true 时补上 5~8 那排。<br/>
    /// <paramref name="hideLayerNames"/> = 不需要显示的素材图层名（只影响贴图，不影响点击框）。<br/>
    /// <paramref name="levelScenePrefix"/> = 关卡按钮跳转的场景前缀（"game" 或 "other"）。
    /// </summary>
    private static void BuildChooseVariant(string scenePath, bool withBottomRow,
                                           string[] hideLayerNames,
                                           string levelScenePrefix,
                                           string arrowName, Vector2 arrowXy,
                                           bool arrowMirror, NavTarget arrowTarget)
    {
        Scene scene = OpenOrCreateScene(scenePath);
        GameObject canvas = CreateCanvas();

        // 背景 + 全部按钮素材一次性整幅叠加（与主界面同一套做法）
        Dictionary<string, GameObject> layers = AddLayers(canvas, ArtChoose, hideLayerNames);

        // ---- 上面那排：图上数字 1~4 ----
        // 素材序号与图上数字相反：选关-9=1、-8=2、-7=3、-6=4
        // 位置按美术坐标（与整幅图层共用同一坐标系，所以必然与看到的素材对齐）
        AddLevelHitBox(layers, "选关-9", "选关-9_new", 1, 470, 331, 820, 720, levelScenePrefix);      // 第1列
        AddLevelHitBox(layers, "选关-8", "选关-8_new", 2, 866, 326, 1216, 707, levelScenePrefix);     // 第2列
        AddLevelHitBox(layers, "选关-7", "选关-7_new", 3, 1247, 332, 1666, 702, levelScenePrefix);    // 第3列
        AddLevelHitBox(layers, "选关-6", "选关-6_new", 4, 1648, 339, 2037, 703, levelScenePrefix);    // 第4列

        // ---- 下面那排：图上数字 5~8（仅 UI_choose 需要）----
        // 素材序号与图上数字相反：选关-5=5、-4=6、-3=7、-2=8
        if (withBottomRow)
        {
            AddLevelHitBox(layers, "选关-5", "选关-5_new", 5, 470, 810, 820, 1194, levelScenePrefix);  // 第1列
            AddLevelHitBox(layers, "选关-4", "选关-4_new", 6, 866, 804, 1216, 1181, levelScenePrefix); // 第2列
            AddLevelHitBox(layers, "选关-3", "选关-3_new", 7, 1247, 808, 1666, 1181, levelScenePrefix);// 第3列
            AddLevelHitBox(layers, "选关-2", "选关-2_new", 8, 1648, 804, 2037, 1177, levelScenePrefix);// 第4列
        }

        // ---- 返回主界面 ----
        GameObject backLayer = FindLayer(layers, "选关-10_new");
        if (backLayer != null)
            AddChildHitBox(backLayer, "Btn_Back", 233, 142, 458, 334, NavTarget.Main);

        // ---- 箭头 ----
        AddArrow(canvas, arrowTarget, arrowName, arrowXy, arrowMirror);

        SaveScene(scene, scenePath);
    }

    // ------------------------------------------------------------ 主界面

    // ============================================================================
    //  设置界面（UI_settings）
    //  视觉层：与最初做法完全一致 —— 扫描素材文件夹，每张 PNG 整幅铺满 Canvas。
    //          因为素材都是 2500×1500 且内容已经在画稿上摆好，铺满即正确，
    //          **不要给视觉层设锚点、不要缩放、不要重排**。
    //  点击层：单独挂在 Canvas 根下、用屏幕像素定位（本项目已验证可靠的方式）。
    // ============================================================================
    private static void BuildSettingsScene()
    {
        Scene scene = OpenOrCreateScene(SceneSettings);
        GameObject canvas = CreateCanvas();

        // ---- 视觉：整幅铺满，与最初一致 ----
        foreach (string fileName in CollectLayerFiles(ArtSettings))
        {
            string path = ArtSettings + "/" + fileName;
            AddOverlay(canvas, path, Path.GetFileNameWithoutExtension(fileName));
        }

        // ---- 逻辑对象（三个按钮的点击都绑到它） ----
        GameObject logic = new GameObject("LevelSettingsUI", typeof(LevelSettingsUI));
        logic.transform.SetParent(canvas.transform, false);
        LevelSettingsUI ui = logic.GetComponent<LevelSettingsUI>();

        // ---- 点击框：挂在 Canvas 根下，用屏幕像素 ----
        // 美术画布的 y 轴与屏幕相反 ⇒ y' = 1080 - y（已用返回箭头与三段文字双重验证）。
        // 下面是各段文字在画稿上的**实测**范围（合成整幅图层后逐段量出来的），
        // 不是 PNG 的透明边包围盒 —— 用包围盒会错（设置-2 下方有 127px 透明区）。
        //   退出游戏 美术 x[989,1556]  y[496,633]
        //   退出关卡 美术 x[988,1551]  y[734,882]
        //   重新开始 美术 x[996,1558]  y[965,1119]
        // 点框比文字略放大，手感更好；退出游戏按需求把高度缩到 0.8 倍。

        // 退出游戏：美术 y[496,633] → 翻屏 y[566,697]，外扩到 y[560,703] 后高度 ×0.8 → y[574,689]
        AddSettingsHitBox(canvas, "Btn_QuitGame", 755, 574, 1200, 689, ui, "OnQuitGame");

        // 退出关卡：美术 y[734,882] → 翻屏 y[403,516]，外扩到 y[397,522]
        AddSettingsHitBox(canvas, "Btn_QuitLevel", 754, 397, 1196, 522, ui, "OnQuitLevel");

        // 重新开始：美术 y[965,1119] → 翻屏 y[221,339]，外扩到 y[215,345]
        AddSettingsHitBox(canvas, "Btn_Restart", 760, 215, 1201, 345, ui, "OnRestart");

        // 返回箭头：美术 y[142,334] → 翻屏 y[823,971]（已在画面上验证正确）
        AddSettingsHitBox(canvas, "Btn_Back", 179, 823, 352, 971, ui, "OnBack");

        SaveScene(scene, SceneSettings);

        Debug.Log("[MenuUIBuilder] UI_settings：视觉层整幅铺满（同最初），" +
                  "四个点击框用屏幕像素定位（退出游戏/退出关卡/重新开始/返回）。");
    }

    /// <summary>设置界面上的一个点击框（挂在 Canvas 根下，屏幕像素定位）</summary>
    private static void AddSettingsHitBox(GameObject canvas, string name,
                                          float x0, float y0, float x1, float y1,
                                          LevelSettingsUI ui, string method)
    {
        Button btn = LevelUiUtil.CreateHitBox(canvas.transform, name, x0, y0, x1, y1);

        if (ui == null) return;

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            btn.onClick, GetSettingsMethod(ui, method));
    }

    private static UnityEngine.Events.UnityAction GetSettingsMethod(LevelSettingsUI ui, string method)
    {
        switch (method)
        {
            case "OnQuitGame": return ui.OnQuitGame;
            case "OnQuitLevel": return ui.OnQuitLevel;
            case "OnRestart": return ui.OnRestart;
            default: return ui.OnBack;
        }
    }

    private static void BuildMainMenu()
    {
        Scene scene = OpenOrCreateScene(SceneMain);
        GameObject canvas = CreateCanvas();

        AddOverlay(canvas, ArtMain + "/主界面-1.png", "BG");
        AddOverlay(canvas, ArtMain + "/主界面-9_new.png", "Deco_CatLeft");
        AddOverlay(canvas, ArtMain + "/主界面-8_new.png", "Deco_CatGourd");
        AddOverlay(canvas, ArtMain + "/主界面-7_new.png", "Title");

        // 按钮：整幅视觉图层 + 挂在图层内部的隐形点击框
        // 开始 / 退出：点击框整体下移 MainButtonYOffsetPixels（实测需要微调）
        float dy = MainButtonYOffsetPixels / ART_TO_UI;      // 屏幕像素 → 美术像素

        AddButtonOverlay(canvas, ArtMain + "/主界面-6_new.png", "Btn_Start",
                         1164, 795 + dy, 1389, 898 + dy, NavTarget.Choose);   // 开始 → 选关
        AddButtonOverlay(canvas, ArtMain + "/主界面-5_new.png", "Btn_Quit",
                         1179, 950 + dy, 1390, 1047 + dy, NavTarget.Quit);    // 退出 → 结束运行
        AddButtonOverlay(canvas, ArtMain + "/主界面-2_new.png", "Btn_Music",
                         240, 1210, 381, 1318, NavTarget.Music);              // 音量 → 音乐管理

        // 设置按钮：按需求「主界面的设置按键暂设不可用」——这里不再生成点击框。
        // 齿轮视觉仍然保留（主界面-3_new 那一层照旧叠加），只是点不动。
        AddOverlay(canvas, ArtMain + "/主界面-3_new.png", "Btn_Settings");

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

        // UI_music：三行"条 + 球"要变成音量滑条。
        // 放在这里是为了"重建场景"时不会把滑条弄丢（否则重新生成一次 UI_music 就得再手工补一遍）。
        if (scenePath == SceneMusic)
        {
            int sliders = MusicSliderSetup.Build(canvas);
            Debug.Log("[MenuUIBuilder] " + scenePath + " 已生成 " + sliders + " 条音量滑条。");
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

    /// <summary>
    /// 关卡按钮：在对应素材图层内加点击框，并跳到 <paramref name="levelScenePrefix"/> + 关卡号。<br/>
    /// 例如 levelScenePrefix = "game" 且 level = 3 ⇒ 跳 game3；传 "other" ⇒ 跳 other3。<br/>
    /// 默认 "game"，所以旧调用点无需改动。
    /// </summary>
    private static void AddLevelHitBox(Dictionary<string, GameObject> layerByName,
                                       string nameA, string nameB, int level,
                                       float x0, float y0, float x1, float y1,
                                       string levelScenePrefix = "game")
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

        if (levelScenePrefix == "other")
        {
            // 跳 other{关卡号}
            MenuNavigation nav = hit.AddComponent<MenuNavigation>();
            nav.targetScene = "other" + level;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, nav.Go);
        }
        else
        {
            // 跳 game{关卡号}
            MenuChooseLevel choose = hit.AddComponent<MenuChooseLevel>();
            choose.levelNumber = level;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, choose.Go);
        }

        Debug.Log("[MenuUIBuilder] 关卡按钮 " + level + " → " + levelScenePrefix + level);
    }

    /// <summary>把一个文件夹里的素材按序整幅叠加，返回 图层名 → GameObject 的映射</summary>
    private static Dictionary<string, GameObject> AddLayers(GameObject canvas, string artFolder,
                                                            string[] skipNames = null)
    {
        Dictionary<string, GameObject> map = new Dictionary<string, GameObject>();
        List<string> files = CollectLayerFiles(artFolder);

        foreach (string fileName in files)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);

            // 跳过不需要显示的贴图（只影响视觉层，点击框另行添加）<br/>
            // 注意：实际文件名带 _new 后缀（如 "选关-2_new"），比较时要去掉，
            // 否则跳过列表 "选关-2" 永远匹配不上。
            string baseName = name.EndsWith("_new") ? name.Substring(0, name.Length - 4) : name;
            if (skipNames != null &&
                (System.Array.IndexOf(skipNames, name) >= 0 ||
                 System.Array.IndexOf(skipNames, baseName) >= 0))
                continue;

            Image img = AddOverlay(canvas, artFolder + "/" + fileName, name);
            if (img != null) map[name] = img.gameObject;
        }

        return map;
    }

    /// <summary>
    /// 箭头按钮。参数是<b>屏幕像素坐标</b>（1920×1080 可见区，左下为原点），
    /// 尺寸固定 <see cref="ArrowW"/>×<see cref="ArrowH"/>（严格保持素材宽高比，不变形）。<br/>
    /// 触发区在箭头矩形上每边外扩 <see cref="ArrowPadPt"/>，保证点得到。
    /// </summary>
    private static void AddArrow(GameObject canvas, NavTarget target, string nodeName,
                                 Vector2 xy, bool mirror)
    {
        float x0 = xy.x, y0 = xy.y;
        float x1 = x0 + ArrowW, y1 = y0 + ArrowH;

        GameObject go = new GameObject(nodeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = (RectTransform)go.transform;
        SetAnchorRectScreen(rt, x0, y0, x1, y1);

        Image img = go.GetComponent<Image>();
        img.sprite = LoadSprite(ArtArrow + "/arrow_new.png");
        img.raycastTarget = false;
        img.preserveAspect = false;

        if (img.sprite == null)
        {
            Debug.LogWarning("[MenuUIBuilder] 箭头素材加载失败：" + ArtArrow + "/arrow_new.png");
            Object.DestroyImmediate(go);
            return;
        }

        if (mirror)
        {
            Vector3 s = go.transform.localScale;
            s.x = -Mathf.Abs(s.x);          // 水平翻转：素材朝右 → 朝左
            go.transform.localScale = s;
        }

        // 触发区：直接挂在 Canvas 上，按屏幕像素指定矩形。<br/>
        // 关键：**不能作为箭头节点的子物体** —— 左箭头用 localScale.x = -1 翻转，
        // 子物体会跟着水平镜像，导致点击框被翻到箭头另一侧（表现为"没贴住、又小又偏"）。
        float padX = ArrowW * ArrowPadPt;
        float padY = ArrowH * ArrowPadPt;

        AddHitBoxOnCanvas(canvas, nodeName,
                          x0 - padX, y0 - padY,
                          x1 + padX, y1 + padY, target);

        Debug.Log(string.Format(
            "[MenuUIBuilder] {0}: 箭头 x[{1:F0},{2:F0}] y[{3:F0},{4:F0}] ({5:F0}x{6:F0})；" +
            "触发区 x[{7:F0},{8:F0}] y[{9:F0},{10:F0}] ({11:F0}x{12:F0})  镜像={13}",
            nodeName, x0, x1, y0, y1, x1 - x0, y1 - y0,
            x0 - padX, x1 + padX, y0 - padY, y1 + padY,
            (x1 - x0) + padX * 2, (y1 - y0) + padY * 2, mirror));
    }

    /// <summary>
    /// 在 Canvas 上直接创建一个屏幕像素定位的点击框（不挂在会被翻转/缩放的节点下）。
    /// </summary>
    private static GameObject AddHitBoxOnCanvas(GameObject canvas, string name,
                                                float x0, float y0, float x1, float y1,
                                                NavTarget? target)
    {
        GameObject hit = new GameObject(name + "_Hit",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        hit.transform.SetParent(canvas.transform, false);

        SetAnchorRectScreen((RectTransform)hit.transform, x0, y0, x1, y1);

        Image img = hit.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = true;

        Button btn = hit.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;

        BindClick(hit, btn, target);
        return hit;
    }

    /// <summary>屏幕像素矩形（1920×1080 基准，左下原点）→ 比例锚点</summary>
    private static void SetAnchorRectScreen(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(Mathf.Clamp01(Mathf.Min(x0, x1) / UI_W),
                                   Mathf.Clamp01(Mathf.Min(y0, y1) / UI_H));
        rt.anchorMax = new Vector2(Mathf.Clamp01(Mathf.Max(x0, x1) / UI_W),
                                   Mathf.Clamp01(Mathf.Max(y0, y1) / UI_H));
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>与 <see cref="AddChildHitBox"/> 相同，但走屏幕像素坐标</summary>
    private static GameObject AddChildHitBoxScreen(GameObject parent, string name,
                                             float x0, float y0, float x1, float y1,
                                             NavTarget? target)
    {
        GameObject hit = new GameObject(name + "_Hit",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        hit.transform.SetParent(parent.transform, false);

        SetAnchorRectScreen((RectTransform)hit.transform, x0, y0, x1, y1);

        Image img = hit.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = true;

        Button btn = hit.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;

        BindClick(hit, btn, target);
        return hit;
    }

    /// <summary>统一绑定点击行为</summary>
    private static void BindClick(GameObject go, Button btn, NavTarget? target)
    {
        if (!target.HasValue) return;

        if (target.Value == NavTarget.Quit)
        {
            MenuQuit quit = go.AddComponent<MenuQuit>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, quit.QuitGame);
        }
        else
        {
            MenuNavigation nav = go.AddComponent<MenuNavigation>();
            nav.targetScene = SceneNameOf(target.Value);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, nav.Go);
        }
    }

    /// <summary>
    /// 在某个图层内部创建点击框。参数是<b>美术画布坐标</b>（2500×1500），
    /// 内部直接转成比例锚点，因此与该图层里看到的位置一致。
    /// </summary>
    private static GameObject AddChildHitBox(GameObject parent, string name,
                                             float x0, float y0, float x1, float y1,
                                             NavTarget? target)
    {
        GameObject hit = new GameObject(name + "_Hit",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        hit.transform.SetParent(parent.transform, false);

        SetAnchorRectFromArt((RectTransform)hit.transform, x0, y0, x1, y1);

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

    private enum NavTarget { Main, Choose, Choose2, Music, Settings, Quit }

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
            case NavTarget.Choose2: return "UI_choose2";
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

        // UI 场景 + 所有 gameN / otherN 场景（选关按钮要跳它们，不在 Build Settings 里会加载失败）
        List<string> wanted = new List<string>
        {
            SceneMain, SceneChoose, SceneChoose2, SceneMusic, SceneSettings
        };

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);

            bool isLevelLike = Regex.IsMatch(name, @"^game\d+$") || Regex.IsMatch(name, @"^other\d+$");
            if (isLevelLike && !wanted.Contains(path))
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
