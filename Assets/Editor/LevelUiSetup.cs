using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 关卡 UI 的工具链（方案 A）：<br/>
/// 菜单 <b>Tools → UI → 生成关卡UI预制体</b>        —— 创建 Assets/Prefabs/LevelUI.prefab<br/>
/// 菜单 <b>Tools → UI → 批量注入12个关卡场景</b>  —— 把预制体放进所有 game/other 场景并登记 Build Settings<br/>
/// 之后要改样式，只改这个预制体即可，12 个场景同步生效。
/// </summary>
public static class LevelUiSetup
{
    private const string PrefabPath = "Assets/Prefabs/LevelUI.prefab";
    private const string SceneFolder = "Assets/Scenes";
    private const string GearSpritePath = "Assets/Art/主界面-1等9项文件/主界面-3_c.png";

    // ---------------------------------------------------------------- 预制体

    [MenuItem("Tools/UI/生成关卡UI预制体")]
    public static void BuildPrefab()
    {
        GameObject root = new GameObject("LevelUI",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(LevelUI));

        RectTransform rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(LevelUiUtil.DesignW, LevelUiUtil.DesignH);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        LevelUI levelUi = root.GetComponent<LevelUI>();
        levelUi.settingsScene = "UI_settings";
        levelUi.buttonPosition = new Vector2(24f, 896f);
        levelUi.buttonSize = new Vector2(160f, 160f);
        levelUi.buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GearSpritePath);

        if (levelUi.buttonSprite == null)
            Debug.LogWarning("[LevelUI] 齿轮素材未导入为 Sprite：" + GearSpritePath +
                             "（请确认它的 Texture Type = Sprite）");

        // 齿轮视觉 + 点击框（都挂在 Canvas 下，位置用屏幕像素）
        float x0 = levelUi.buttonPosition.x, y0 = levelUi.buttonPosition.y;
        float x1 = x0 + levelUi.buttonSize.x, y1 = y0 + levelUi.buttonSize.y;

        Image visual = LevelUiUtil.CreateImage(root.transform, "Btn_Settings",
                                               levelUi.buttonSprite, x0, y0, x1, y1);
        if (levelUi.buttonSprite == null)
            visual.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);

        Button btn = LevelUiUtil.CreateHitBox(root.transform, "Btn_Settings", x0, y0, x1, y1);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, levelUi.OpenSettings);
        LevelUiUtil.WireHighlight(btn, visual);   // 齿轮的悬停变暗+放大+点击音效

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[LevelUI] 预制体已生成：" + PrefabPath +
                  "（齿轮 " + levelUi.buttonSize.x + "×" + levelUi.buttonSize.y +
                  " @ 屏幕 (" + x0 + "," + y0 + ")）");

        // 重新生成会把设置面板一起抹掉，所以顺手再建一次，保证"生成预制体"始终得到完整结果
        SettingsPanelSetup.BuildPanel();
    }

    // ---------------------------------------------------------------- 批量注入

    [MenuItem("Tools/UI/批量注入12个关卡场景")]
    public static void InjectIntoLevelScenes()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("缺少预制体",
                "还没生成 LevelUI.prefab。\n请先执行 Tools → UI → 生成关卡UI预制体。", "好");
            return;
        }

        List<string> scenes = FindLevelScenes();
        if (scenes.Count == 0)
        {
            EditorUtility.DisplayDialog("没找到关卡场景",
                "在 " + SceneFolder + " 下没有找到 gameN / otherN 场景。", "好");
            return;
        }

        string current = EditorSceneManager.GetActiveScene().path;
        int done = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string path in scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                // 已经有就不重复加（按名字找根物体）
                if (GameObject.Find("LevelUI") == null)
                {
                    GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    inst.name = "LevelUI";
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                done++;

                Debug.Log("[LevelUI] 已注入：" + path);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();

            if (!string.IsNullOrEmpty(current))
                EditorSceneManager.OpenScene(current, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        AddScenesToBuildSettings(scenes);

        Debug.Log("[LevelUI] 共注入 " + done + " 个关卡场景，并已登记 Build Settings。");
        EditorUtility.DisplayDialog("完成",
            "已把 LevelUI 预制体注入 " + done + " 个关卡场景，并登记 Build Settings。", "好");
    }

    private static List<string> FindLevelScenes()
    {
        List<string> result = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { SceneFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);

            if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^(game|other)\d+$"))
                result.Add(path);
        }

        result.Sort();
        return result;
    }

    private static void AddScenesToBuildSettings(List<string> extra)
    {
        List<EditorBuildSettingsScene> list =
            new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        // UI 场景也要在里面，否则跳转会失败
        List<string> wanted = new List<string>(extra)
        {
            "Assets/Scenes/UI.unity",
            "Assets/Scenes/UI_choose.unity",
            "Assets/Scenes/UI_choose2.unity",
            "Assets/Scenes/UI_settings.unity",
            "Assets/Scenes/UI_music.unity",
        };

        foreach (string path in wanted)
        {
            if (!File.Exists(path)) continue;

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
