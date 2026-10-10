using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 风扇风区的一键搭建工具（<b>已去掉粒子</b>）。<br/>
/// 菜单：Tools → 风扇 → 生成风扇风区预制体<br/>
/// 生成的预制体只含「触发器 + windarea 脚本」这一套逻辑骨架，
/// <b>视觉表现（风动画）请直接给子物体挂 Animator 并指定 wind.controller</b>，
/// 参数 iswind 由 windarea 脚本自动驱动，不需要手写代码。<br/><br/>
/// 实际在用的风区预制体是 <c>Assets/Prefabs/wind.prefab</c>（已按上面方式做好），
/// 本工具用于新建别的风区。
/// </summary>
public static class WindAreaTools
{
    private const string PrefabPath = "Assets/Prefabs/WindArea.prefab";
    private const string WindControllerPath = "Assets/Animation/Controllers/wind.controller";

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
        Debug.Log("已生成风扇风区预制体：" + PrefabPath +
                  "（改它 Collider2D 的 size 即可调整送风范围；" +
                  "视觉请给子物体挂 Animator + wind.controller）");
    }

    [MenuItem("Tools/风扇/给选中物体补一个风动画节点")]
    public static void AddAnimatorToSelection()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("请先在 Hierarchy 里选中目标物体。");
            return;
        }

        GameObject target = Selection.activeGameObject;

        if (target.GetComponentInChildren<Animator>() != null)
        {
            Debug.LogWarning(target.name + " 的子物体里已经有 Animator 了，未重复添加。");
            return;
        }

        // 视觉节点：挂 SpriteRenderer + Animator
        GameObject visual = new GameObject("WindVisual");
        visual.transform.SetParent(target.transform, false);
        visual.transform.localPosition = new Vector3(0f, ZoneSize.y * 0.5f, 0f);

        visual.AddComponent<SpriteRenderer>();

        Animator animator = visual.AddComponent<Animator>();
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WindControllerPath);
        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
        }
        else
        {
            Debug.LogWarning("找不到 " + WindControllerPath + "，请手动指定 Animator 的 Controller。");
        }

        windarea area = target.GetComponent<windarea>();
        if (area != null)
        {
            area.animator = animator;
            EditorUtility.SetDirty(area);
        }

        Undo.RegisterCreatedObjectUndo(visual, "Add Wind Animator");
        Selection.activeGameObject = visual;
        Debug.Log("已给 " + target.name + " 加上风动画节点（参数 iswind 由 windarea 自动驱动）。");
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
        area.requireFloating = true;

        // 说明：这里不再生成粒子。视觉请用菜单
        // Tools → 风扇 → 给选中物体补一个风动画节点，或直接照 wind.prefab 的样子挂 Animator。
        return root;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
