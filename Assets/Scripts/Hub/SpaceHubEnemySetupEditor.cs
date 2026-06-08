#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs <see cref="PrimaryEnemy"/> on every Oblivion drone in the Hub scene and
/// <see cref="HubCombatTarget"/> on the Player and driveable vehicles.
/// Runs automatically whenever the Hub scene is opened.
/// Menu: <b>Echoes / Hub / Install Enemy and Combat Components</b>
/// </summary>
[InitializeOnLoad]
static class SpaceHubEnemySetupEditor
{
    const string HubScenePath = "Assets/Scenes/Space/Hub.unity";
    const string MergedCopy   = "Tools/Hub.unity.merged";
    const string MenuPath     = "Echoes/Hub/Install Enemy and Combat Components";
    const string DronePrefix  = "P_Oblivion_Drone_01";

    static SpaceHubEnemySetupEditor()
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

        Scene hubScene = default;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var s = EditorSceneManager.GetSceneAt(i);
            if (s.path == HubScenePath) { hubScene = s; break; }
        }
        if (!hubScene.IsValid() || !hubScene.isLoaded) return;

        bool changed = false;

        foreach (var root in hubScene.GetRootGameObjects())
            changed |= SetupDronesRecursive(root.transform);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            changed |= EnsureCombatTarget(player, maxHealth: 100);

        foreach (var seat in Object.FindObjectsOfType<SpaceVehicleSeat>())
            changed |= EnsureCombatTarget(seat.gameObject, maxHealth: 160);

        if (!changed) return;

        EditorSceneManager.SaveScene(hubScene);
        UpdateMergedCopy();
        Debug.Log("[EnemySetup] Enemy and combat components installed, scene saved.");
    }

    static bool SetupDronesRecursive(Transform node)
    {
        bool changed = false;
        if (node.name.StartsWith(DronePrefix))
            changed |= EnsurePrimaryEnemy(node.gameObject);

        for (int i = 0; i < node.childCount; i++)
            changed |= SetupDronesRecursive(node.GetChild(i));

        return changed;
    }

    static bool EnsurePrimaryEnemy(GameObject go)
    {
        bool changed = false;
        if (go.GetComponent<PrimaryEnemy>() == null)
        {
            go.AddComponent<PrimaryEnemy>();
            changed = true;
        }
        EditorUtility.SetDirty(go);
        return changed;
    }

    static bool EnsureCombatTarget(GameObject go, int maxHealth)
    {
        var target = go.GetComponent<HubCombatTarget>();
        if (target != null) return false;

        target = go.AddComponent<HubCombatTarget>();
        var so = new SerializedObject(target);
        so.FindProperty("maxHealth").intValue        = maxHealth;
        so.FindProperty("destroyOnDeath").boolValue  = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(go);
        return true;
    }

    static void UpdateMergedCopy()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot)) return;

        var src = Path.Combine(projectRoot, HubScenePath);
        var dst = Path.Combine(projectRoot, MergedCopy);
        try
        {
            if (!File.Exists(src)) return;
            var dir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(src, dst, overwrite: true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[EnemySetup] Failed to update reference copy: {e.Message}");
        }
    }
}
#endif
