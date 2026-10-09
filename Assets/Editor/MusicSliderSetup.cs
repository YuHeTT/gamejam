using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 把 UI_music 里的三行"手绘长条 + 小球"改造成真正的 <see cref="Slider"/>，
/// 并接到 <see cref="musicmanager"/> 的音量上。<br/><br/>
/// 菜单：<b>Tools → UI → 给 UI_music 加音量滑条</b><br/><br/>
/// <b>做法</b>：条的美术图层原样保留（它就是滑条的视觉轨道），只把"球"那一层删掉，
/// 改由滑条的 Handle 用 <see cref="RawImage"/> + <c>uvRect</c> 从原 PNG 里取那一小块来画——
/// 这样不需要额外生成任何素材，球还是美术画的那颗球。<br/><br/>
/// <b>为什么坐标都用比例锚点</b>：美术画布是 2500×1500，屏幕可见区是 1920×1080，两者比例不同，
/// 所以条/球的位置一律用"美术像素 ÷ 画布尺寸"的比例锚点，与分辨率、缩放都无关
/// （与 MenuUIBuilder 同一套思路）。<br/><br/>
/// 改了这三行的美术位置后，重新执行一次本菜单即可。
/// </summary>
public static class MusicSliderSetup
{
    private const string ScenePath = "Assets/Scenes/UI_music.unity";
    private const string ArtFolder = "Assets/Art/音乐设置-1等9项文件/";

    private const float ART_W = 2500f;      // 美术画布
    private const float ART_H = 1500f;
    private const float UI_W = 1920f;       // 设计分辨率
    private const float UI_H = 1080f;

    /// <summary>球心离条两端留出的额外余量（美术像素），免得球压在手绘描边上</summary>
    private const float BallPadArt = 4f;

    /// <summary>一行音量：条的图层、球的图层、以及两者在美术画布上的像素范围（闭区间，y 从顶部算）</summary>
    private struct Row
    {
        public string sliderName;
        public string barLayer;
        public string ballLayer;
        public string ballPng;
        public float bx0, bx1, by0, by1;    // 条
        public float sx0, sx1, sy0, sy1;    // 球
        public MusicVolumeSlider.Channel channel;
        public float initialValue;
    }

    // 像素范围是用脚本量各 PNG 的 alpha 包围盒得到的（脚本见提交说明），不是猜的
    private static readonly Row[] Rows =
    {
        new Row {
            sliderName = "Slider_Master", barLayer = "音乐设置-8_new", ballLayer = "音乐设置-5_new",
            ballPng = "音乐设置-5_new.png",
            bx0 = 1120, bx1 = 1923, by0 = 465, by1 = 571,
            sx0 = 1269, sx1 = 1355, sy0 = 475, sy1 = 572,
            channel = MusicVolumeSlider.Channel.Master, initialValue = musicmanager.DefaultMasterVolume,
        },
        new Row {
            sliderName = "Slider_Bgm", barLayer = "音乐设置-6_new", ballLayer = "音乐设置-7_new",
            ballPng = "音乐设置-7_new.png",
            bx0 = 1122, bx1 = 1921, by0 = 675, by1 = 780,
            sx0 = 1355, sx1 = 1453, sy0 = 674, sy1 = 767,
            channel = MusicVolumeSlider.Channel.Bgm, initialValue = musicmanager.DefaultBgmVolume,
        },
        new Row {
            sliderName = "Slider_Shot", barLayer = "音乐设置-4_new", ballLayer = "音乐设置-9_new",
            ballPng = "音乐设置-9_new.png",
            bx0 = 1130, bx1 = 1921, by0 = 918, by1 = 1022,
            sx0 = 1491, sx1 = 1582, sy0 = 919, sy1 = 1012,
            channel = MusicVolumeSlider.Channel.Shot, initialValue = musicmanager.DefaultShotVolume,
        },
    };

    [MenuItem("Tools/UI/给 UI_music 加音量滑条")]
    public static void BuildFromMenu()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject canvas = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.GetComponent<Canvas>() != null) { canvas = go; break; }
        }

        if (canvas == null)
        {
            Debug.LogError("[MusicSliderSetup] " + ScenePath + " 里找不到 Canvas。");
            return;
        }

        int built = Build(canvas);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log("[MusicSliderSetup] UI_music 已生成 " + built + "/" + Rows.Length +
                  " 条音量滑条（总音量 / 背景音乐 / 音效），初始值 " +
                  musicmanager.DefaultMasterVolume + " / " + musicmanager.DefaultBgmVolume +
                  " / " + musicmanager.DefaultShotVolume + "。");
    }

    /// <summary>
    /// 在 Canvas 上生成三条音量滑条。可反复执行（会先删掉上一次生成的）。
    /// </summary>
    /// <returns>成功生成的条数</returns>
    public static int Build(GameObject canvas)
    {
        if (canvas == null) return 0;

        int built = 0;

        foreach (Row r in Rows)
        {
            // 反复执行：清掉旧滑条
            Transform old = canvas.transform.Find(r.sliderName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // 球那层整幅图层删掉：视觉改由滑条手柄承担，否则会有两颗球
            Transform ballLayer = canvas.transform.Find(r.ballLayer);
            if (ballLayer != null) Object.DestroyImmediate(ballLayer.gameObject);

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + r.ballPng);
            if (tex == null)
            {
                Debug.LogError("[MusicSliderSetup] 找不到球素材：" + ArtFolder + r.ballPng +
                               "（请确认该图按 Sprite/Texture 导入）");
                continue;
            }

            CreateSlider(canvas, r, tex);
            built++;
        }

        return built;
    }

    private static void CreateSlider(GameObject canvas, Row r, Texture2D ballTex)
    {
        float barW = r.bx1 - r.bx0 + 1;
        float barH = r.by1 - r.by0 + 1;
        float ballW = r.sx1 - r.sx0 + 1;
        float ballH = r.sy1 - r.sy0 + 1;

        // ---------------------------------------------------------- 滑条根（= 美术里的那条）
        GameObject sliderGo = new GameObject(r.sliderName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
        sliderGo.transform.SetParent(canvas.transform, false);   // 放在最后 → 盖在所有图层之上

        RectTransform rootRt = (RectTransform)sliderGo.transform;
        rootRt.anchorMin = new Vector2(r.bx0 / ART_W, 1f - (r.by1 + 1f) / ART_H);
        rootRt.anchorMax = new Vector2((r.bx1 + 1f) / ART_W, 1f - r.by0 / ART_H);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // 完全透明但吃射线：整条都能点、能拖
        Image bg = sliderGo.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0f);
        bg.raycastTarget = true;

        // ---------------------------------------------------------- 手柄滑动区
        // 水平内缩半个球，保证球在 0 / 1 两端也待在条里面；
        // 竖直取球的高度带（都写成占滑条高度的比例 → 换分辨率不会跑偏）。
        float inset = Mathf.Clamp((ballW * 0.5f + BallPadArt) / barW, 0f, 0.49f);

        float barCenterArt = (r.by0 + r.by1 + 1f) * 0.5f;
        float ballCenterArt = (r.sy0 + r.sy1 + 1f) * 0.5f;
        float relCenterY = (barCenterArt - ballCenterArt) / barH;    // 美术 y 向下 ⇒ 球更靠上时为正
        float relH = ballH / barH;

        float areaMinY = Mathf.Clamp01(0.5f + relCenterY - relH * 0.5f);
        float areaMaxY = Mathf.Clamp01(0.5f + relCenterY + relH * 0.5f);

        GameObject area = new GameObject("Handle Slide Area", typeof(RectTransform));
        area.transform.SetParent(sliderGo.transform, false);

        RectTransform areaRt = (RectTransform)area.transform;
        areaRt.anchorMin = new Vector2(inset, areaMinY);
        areaRt.anchorMax = new Vector2(1f - inset, areaMaxY);
        areaRt.pivot = new Vector2(0.5f, 0.5f);
        areaRt.offsetMin = Vector2.zero;
        areaRt.offsetMax = Vector2.zero;

        // ---------------------------------------------------------- 手柄 = 那颗球
        GameObject handle = new GameObject("Handle",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        handle.transform.SetParent(area.transform, false);

        RectTransform handleRt = (RectTransform)handle.transform;
        handleRt.anchorMin = new Vector2(0f, 0f);
        handleRt.anchorMax = new Vector2(0f, 1f);      // 运行时会由 Slider 驱动成 (值,0)-(值,1)
        handleRt.pivot = new Vector2(0.5f, 0.5f);
        handleRt.anchoredPosition = Vector2.zero;
        // 高度交给滑动区（比例），宽度按 1920 基准给固定值 —— 与 Unity 自带 Slider 手柄同样的做法
        handleRt.sizeDelta = new Vector2(ballW / ART_W * UI_W, 0f);

        // 球不用单独切图：直接用原 PNG 的那一小块（UV 是 0~1 比例，与贴图是否被压缩无关）
        float u0 = r.sx0 / ART_W;
        float u1 = (r.sx1 + 1f) / ART_W;
        float v0 = 1f - (r.sy1 + 1f) / ART_H;
        float v1 = 1f - r.sy0 / ART_H;

        float halfTexelU = ballTex.width > 0 ? 0.5f / ballTex.width : 0f;
        float halfTexelV = ballTex.height > 0 ? 0.5f / ballTex.height : 0f;

        RawImage ball = handle.GetComponent<RawImage>();
        ball.texture = ballTex;
        ball.raycastTarget = false;      // 拖拽由滑条根接管即可
        ball.uvRect = new Rect(
            u0 + halfTexelU,
            v0 + halfTexelV,
            Mathf.Max(0f, (u1 - u0) - halfTexelU * 2f),
            Mathf.Max(0f, (v1 - v0) - halfTexelV * 2f));

        // ---------------------------------------------------------- Slider
        Slider slider = sliderGo.GetComponent<Slider>();
        slider.fillRect = null;          // 美术里的条没有"已填充"概念，只有球指示位置
        slider.handleRect = handleRt;
        slider.targetGraphic = bg;
        slider.transition = Selectable.Transition.None;   // 不要 Unity 的高亮/按下染色，保持美术原色
        slider.direction = Slider.Direction.LeftToRight;
        slider.wholeNumbers = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = r.initialValue;   // 编辑期就把球摆到正确位置

        // ---------------------------------------------------------- 逻辑
        MusicVolumeSlider logic = sliderGo.AddComponent<MusicVolumeSlider>();
        logic.channel = r.channel;
    }
}
