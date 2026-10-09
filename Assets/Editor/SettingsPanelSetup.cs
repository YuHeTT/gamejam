using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 把「UI_settings 场景」改造成 <b>LevelUI 预制体里的一块面板</b>。<br/><br/>
/// 菜单：<b>Tools → UI → 把设置界面做成关卡内面板</b><br/><br/>
/// 为什么不用第二个场景叠加加载：<br/>
/// 两个场景并存会带来 2 个 EventSystem / 2 个 AudioListener / 2 个相机，以及两个 Overlay Canvas
/// 抢排序；而面板直接挂在 LevelUI 那个唯一的 Overlay Canvas 上，什么都不用抢。<br/>
/// 更关键的是：面板模式下关卡场景<b>从头到尾没有被卸载</b>，所以返回时进度天然保留。<br/><br/>
/// 美术改了 <c>设置-*.png</c> 之后重新执行一次本菜单即可；
/// <see cref="LevelUiSetup.BuildPrefab"/> 生成完基础预制体后也会自动调用一次，避免面板被生成流程抹掉。
/// </summary>
public static class SettingsPanelSetup
{
    private const string PrefabPath = "Assets/Prefabs/LevelUI.prefab";
    private const string PanelName = "SettingsPanel";
    private const string ArtFolder = "Assets/Art/设置-1等5项文件/";

    /// <summary>面板里 5 张全屏美术图，顺序 = UI_settings 场景里的层级（从下往上）</summary>
    private struct ArtEntry
    {
        public string objName;
        public string fileName;
        public string guid;   // 路径找不到时的兜底（改名/挪目录都不会失效）
    }

    private static readonly ArtEntry[] Art =
    {
        new ArtEntry { objName = "设置-1",     fileName = "设置-1.png",     guid = "75cfb6df3758a804986f702032efa188" },
        new ArtEntry { objName = "设置-2_new", fileName = "设置-2_new.png", guid = "3978c3bbb7647594e936df72b340158c" },
        new ArtEntry { objName = "设置-3_new", fileName = "设置-3_new.png", guid = "3bf4e0efc7a4d0e4eb71e8770291ab03" },
        new ArtEntry { objName = "设置-4_new", fileName = "设置-4_new.png", guid = "bec6733d80755bf47accf46db9f6c19f" },
        new ArtEntry { objName = "设置-5_new", fileName = "设置-5_new.png", guid = "0fadda3f6d0eb9c449a1bca94092cf84" },
    };

    /// <summary>4 个点击框：坐标与 UI_settings 场景里的锚点一一对应（1920×1080 屏幕像素，左下为原点）</summary>
    private struct Hotspot
    {
        public string objName;   // 最终物体名 = objName + "_Hit"
        public float x0, y0, x1, y1;
        public string method;    // LevelSettingsUI 上的方法名
    }

    private static readonly Hotspot[] Hotspots =
    {
        new Hotspot { objName = "Btn_QuitGame",  x0 = 755f, y0 = 574f, x1 = 1200f, y1 = 689f, method = "OnQuitGame"  },
        new Hotspot { objName = "Btn_QuitLevel", x0 = 754f, y0 = 397f, x1 = 1196f, y1 = 522f, method = "OnQuitLevel" },
        new Hotspot { objName = "Btn_Restart",   x0 = 760f, y0 = 215f, x1 = 1201f, y1 = 345f, method = "OnRestart"   },
        new Hotspot { objName = "Btn_Back",      x0 = 179f, y0 = 823f, x1 = 352f,  y1 = 971f, method = "OnBack"      },
    };

    [MenuItem("Tools/UI/把设置界面做成关卡内面板")]
    public static void BuildPanel()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("缺少预制体",
                "找不到 " + PrefabPath + "。\n请先执行 Tools → UI → 生成关卡UI预制体。", "好");
            return;
        }

        LevelUI assetUi = prefab.GetComponent<LevelUI>();
        if (assetUi == null)
        {
            EditorUtility.DisplayDialog("预制体结构不对",
                PrefabPath + " 上没有 LevelUI 组件。", "好");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        int artCount = 0;

        try
        {
            // 注意：必须从「刚加载出来的 root」上取组件，不能从预制体资源对象上取。
            // 从资源上取到的是另一个临时对象，往它身上赋值不会被 SaveAsPrefabAsset 写进文件，
            // 结果就是 settingsPanel 落盘成 {fileID: 0}（曾经踩过这个坑）。
            LevelUI levelUi = root.GetComponent<LevelUI>();
            if (levelUi == null)
            {
                Debug.LogError("[SettingsPanel] " + PrefabPath + " 上没有 LevelUI 组件，已中止。");
                return;
            }

            // 0. 先清掉旧面板，保证可以反复执行
            Transform old = root.transform.Find(PanelName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            // 1. 面板根：铺满屏幕，放在最后 → 盖住齿轮（同一个 Canvas 里，靠后的深度更大）
            GameObject panel = new GameObject(PanelName, typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            StretchFullScreen((RectTransform)panel.transform);

            // 2. 全屏"吃射线"块：挡住关卡里的一切点击（齿轮、关卡自身的 UI）
            //    放在面板第一个位置，所以它压不住后面 4 个热区
            Image block = LevelUiUtil.CreateImage(panel.transform, "Btn_Block", null, 0f, 0f,
                                                  LevelUiUtil.DesignW, LevelUiUtil.DesignH);
            block.raycastTarget = true;
            block.color = new Color(1f, 1f, 1f, 0f);

            // 3. 5 张全屏美术图
            foreach (ArtEntry entry in Art)
            {
                Sprite sprite = LoadSprite(entry);
                if (sprite == null) continue;

                Image img = LevelUiUtil.CreateImage(panel.transform, entry.objName, sprite,
                                                    0f, 0f, LevelUiUtil.DesignW, LevelUiUtil.DesignH);
                img.raycastTarget = false;
                artCount++;
            }

            // 4. 交互逻辑挂在面板根上（LevelSettingsUI 会向上找到 LevelUI 走"面板模式"）
            LevelSettingsUI ui = panel.AddComponent<LevelSettingsUI>();

            // 5. 4 个热区 + 接线
            foreach (Hotspot h in Hotspots)
            {
                Button btn = LevelUiUtil.CreateHitBox(panel.transform, h.objName, h.x0, h.y0, h.x1, h.y1);

                switch (h.method)
                {
                    case "OnQuitGame":  UnityEventTools.AddPersistentListener(btn.onClick, ui.OnQuitGame);  break;
                    case "OnQuitLevel": UnityEventTools.AddPersistentListener(btn.onClick, ui.OnQuitLevel); break;
                    case "OnRestart":   UnityEventTools.AddPersistentListener(btn.onClick, ui.OnRestart);   break;
                    case "OnBack":      UnityEventTools.AddPersistentListener(btn.onClick, ui.OnBack);      break;
                }
            }

            // 6. 关卡 UI 指向面板，并让面板默认关闭
            levelUi.settingsPanel = panel;
            panel.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 7. 回读校验：引用必须真的落盘，否则运行时会静默退回"切场景"老逻辑（返回=重开关卡）
        GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        LevelUI savedUi = saved != null ? saved.GetComponent<LevelUI>() : null;
        bool referenceOk = savedUi != null && savedUi.settingsPanel != null;

        if (referenceOk)
        {
            Debug.Log("[SettingsPanel] 校验通过：LevelUI.settingsPanel = " +
                      savedUi.settingsPanel.name + "（已落盘）");
        }
        else
        {
            Debug.LogError("[SettingsPanel] 面板引用没有写进预制体：settingsPanel 仍为空！" +
                           "运行时会退回老的“跳设置场景”逻辑（返回会重开关卡）。");
        }

        Debug.Log("[SettingsPanel] 面板已写入 " + PrefabPath +
                  "：美术 " + artCount + "/" + Art.Length + " 张，热区 " + Hotspots.Length +
                  " 个，位于齿轮之后（层级最上层）。12 个关卡场景里的 LevelUI 实例会自动同步。");

        if (artCount < Art.Length)
        {
            Debug.LogWarning("[SettingsPanel] 有美术图没找到，请检查 " + ArtFolder + " 下的文件名，" +
                             "或重新执行一次本菜单。");
        }
    }

    private static Sprite LoadSprite(ArtEntry entry)
    {
        string path = ArtFolder + entry.fileName;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite == null)
        {
            // 路径找不到就用 GUID 兜底（改名/挪目录后仍然有效）
            string byGuid = AssetDatabase.GUIDToAssetPath(entry.guid);
            if (!string.IsNullOrEmpty(byGuid))
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(byGuid);
        }

        if (sprite == null)
            Debug.LogError("[SettingsPanel] 找不到设置界面美术：" + path +
                           "（请确认它已导入为 Sprite）");

        return sprite;
    }

    private static void StretchFullScreen(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.anchoredPosition = Vector2.zero;
    }
}
