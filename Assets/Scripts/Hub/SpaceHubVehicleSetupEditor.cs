#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Checks on every Hub scene open whether the rover and drop-ship have a
/// <see cref="SpaceVehicleSeat"/> component attached. If not, it adds the component,
/// saves the scene, and updates <c>Tools/Hub.unity.merged</c>.
/// Can also be triggered manually via <b>Echoes / Hub / Reinstall Vehicle Components</b>.
/// </summary>
[InitializeOnLoad]
static class SpaceHubVehicleSetupEditor
{
    const string HubScenePath = "Assets/Scenes/Space/Hub.unity";
    const string MergedCopy   = "Tools/Hub.unity.merged";
    const string MenuPath     = "Echoes/Hub/Reinstall Vehicle Components";
    const string SetupMarker  = "SpaceVehicleSetup_Done_v1";

    // Vehicle root object names → configuration
    static readonly VehicleConfig[] Vehicles =
    {
        new VehicleConfig
        {
            ObjectName    = "P_Rover_Mark_01",
            DisplayName   = "Lunar Rover",
            Mode          = SpaceVehicleSeat.Mode.Ground,
            InvertForward = true,  // The rover model faces -Z, so forward must be flipped.
            TriggerSize   = new Vector3(6f, 3f, 7f),
            TriggerCenter = new Vector3(0f, 1.5f, 0f),
        },
        new VehicleConfig
        {
            ObjectName    = "P_Drop_Ship_Mark_01",
            DisplayName   = "Landing Craft",
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

        // Hub scene must already be open in the Editor.
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
                Debug.LogWarning($"[VehicleSetup] Object '{cfg.ObjectName}' not found — skipping.");
                continue;
            }

            var seat = go.GetComponent<SpaceVehicleSeat>();
            if (seat == null)
            {
                seat    = go.AddComponent<SpaceVehicleSeat>();
                changed = true;
            }

            // Always overwrite config values to ensure they stay up to date.
            seat.vehicleMode   = cfg.Mode;
            seat.displayName   = cfg.DisplayName;
            seat.invertForward = cfg.InvertForward;

            // Ensure a BoxCollider trigger exists for proximity detection.
            var col = go.GetComponent<BoxCollider>();
            if (col == null)
            {
                col     = go.AddComponent<BoxCollider>();
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
        Debug.Log("[VehicleSetup] Vehicle components installed, scene saved, reference copy updated.");
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
            Debug.LogWarning($"[VehicleSetup] Failed to update reference copy: {e.Message}");
        }
    }
}
#endif
