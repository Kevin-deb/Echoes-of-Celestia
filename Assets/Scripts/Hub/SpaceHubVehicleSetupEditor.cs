#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 每次打开 Assets/Scenes/Space/Hub.unity 时自动检测漫游车和着陆舱是否已挂
/// <see cref="SpaceVehicleSeat"/> 组件；若未安装则自动添加、保存场景并更新
/// Tools/Hub.unity.merged 参考副本。
/// 也可以通过菜单 <b>Echoes / Hub / 重新安装载具交互组件</b> 手动触发。
/// </summary>
[InitializeOnLoad]
static class SpaceHubVehicleSetupEditor
{
    const string HubScenePath  = "Assets/Scenes/Space/Hub.unity";
    const string MergedCopy    = "Tools/Hub.unity.merged";
    const string MenuPath      = "Echoes/Hub/重新安装载具交互组件";
    const string SetupMarker   = "SpaceVehicleSetup_Done_v1";

    // 场景里的载具根对象名 → 配置
    static readonly VehicleConfig[] Vehicles =
    {
        new VehicleConfig
        {
            ObjectName    = "P_Rover_Mark_01",
            DisplayName   = "月球漫游车",
            Mode          = SpaceVehicleSeat.Mode.Ground,
            InvertForward = true,                 // 漫游车模型车头朝 -Z，需要翻转
            TriggerSize   = new Vector3(6f, 3f, 7f),
            TriggerCenter = new Vector3(0f, 1.5f, 0f),
        },
        new VehicleConfig
        {
            ObjectName    = "P_Drop_Ship_Mark_01",
            DisplayName   = "着陆舱",
            Mode          = SpaceVehicleSeat.Mode.Aircraft,
            InvertForward = false,
            TriggerSize   = new Vector3(7f, 4f, 8f),
            TriggerCenter = new Vector3(0f, 2f, 0f),
        },
    };

    struct VehicleConfig
    {
        public string                ObjectName;
        public string                DisplayName;
        public SpaceVehicleSeat.Mode Mode;
        public bool                  InvertForward;
        public Vector3               TriggerSize;
        public Vector3               TriggerCenter;
    }

    static SpaceHubVehicleSetupEditor()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path != HubScenePath) return;
        EditorApplication.delayCall -= RunSetup;
        EditorApplication.delayCall += RunSetup;
    }

    [MenuItem(MenuPath)]
    static void RunSetupManual() => RunSetup();

    static void RunSetup()
    {
        EditorApplication.delayCall -= RunSetup;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // Hub 场景必须已经打开
        Scene hubScene = default;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var s = EditorSceneManager.GetSceneAt(i);
            if (s.path == HubScenePath) { hubScene = s; break; }
        }
        if (!hubScene.IsValid() || !hubScene.isLoaded) return;

        bool changed = false;

        foreach (var cfg in Vehicles)
        {
            var go = FindInScene(hubScene, cfg.ObjectName);
            if (go == null)
            {
                Debug.LogWarning($"[VehicleSetup] 未找到 '{cfg.ObjectName}'，跳过。");
                continue;
            }

            var seat = go.GetComponent<SpaceVehicleSeat>();
            if (seat == null)
            {
                seat = go.AddComponent<SpaceVehicleSeat>();
                changed = true;
            }

            // 每次都覆写配置，确保值最新
            seat.vehicleMode   = cfg.Mode;
            seat.displayName   = cfg.DisplayName;
            seat.invertForward = cfg.InvertForward;

            // 确保有 BoxCollider trigger
            var col = go.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = go.AddComponent<BoxCollider>();
                changed = true;
            }
            col.isTrigger = true;
            col.size      = cfg.TriggerSize;
            col.center    = cfg.TriggerCenter;

            EditorUtility.SetDirty(go);
        }

        if (!changed) return;

        EditorSceneManager.SaveScene(hubScene);
        UpdateMergedCopy();

        Debug.Log("[VehicleSetup] 载具交互组件安装完成，场景已保存，参考副本已更新。");
    }

    static GameObject FindInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindRecursive(root.transform, name);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    static Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var result = FindRecursive(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    static void UpdateMergedCopy()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot)) return;

        var src = Path.Combine(projectRoot, HubScenePath);
        var dst = Path.Combine(projectRoot, MergedCopy);

        try
        {
            if (File.Exists(src))
            {
                var dir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.Copy(src, dst, overwrite: true);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[VehicleSetup] 更新参考副本失败：{e.Message}");
        }
    }
}
#endif
