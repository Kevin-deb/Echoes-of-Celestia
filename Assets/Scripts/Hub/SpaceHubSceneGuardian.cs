#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Guards <c>Assets/Scenes/Space/Hub.unity</c>. Runs on every Editor startup or script
/// reload and checks whether the scene file has been silently reverted to an older
/// version by an external tool (cloud sync, file history, undo utility, etc.).
/// If the file is too small or missing the expected moon-terrain marker, it is
/// automatically restored from the stable reference copy at <c>Tools/Hub.unity.merged</c>
/// and reloaded in the Editor.
///
/// Note: if you intentionally edit the Hub scene manually, copy the saved result over
/// <c>Tools/Hub.unity.merged</c> afterwards; otherwise the next Editor startup will
/// restore the older version from that reference.
/// </summary>
[InitializeOnLoad]
static class SpaceHubSceneGuardian
{
    const string ScenePath     = "Assets/Scenes/Space/Hub.unity";
    const string ReferencePath = "Tools/Hub.unity.merged";
    const string ExpectedMarker = "m_Name: Moon_Closed_A";
    const double MinSizeRatio   = 0.5;

    static SpaceHubSceneGuardian()
    {
        EditorApplication.delayCall -= EnsureMergedHub;
        EditorApplication.delayCall += EnsureMergedHub;
    }

    static void EnsureMergedHub()
    {
        EditorApplication.delayCall -= EnsureMergedHub;

        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot)) return;

        var sceneFile     = Path.GetFullPath(Path.Combine(projectRoot, ScenePath));
        var referenceFile = Path.GetFullPath(Path.Combine(projectRoot, ReferencePath));

        if (!File.Exists(referenceFile))
        {
            Debug.LogWarning(
                $"[SpaceHubSceneGuardian] Reference copy not found at {ReferencePath} — cannot auto-restore. " +
                "Re-run Tools/merge_space_hub.ps1 and copy the result to Hub.unity.merged.");
            return;
        }

        if (!File.Exists(sceneFile))
        {
            RestoreFromReference(referenceFile, sceneFile, "scene file missing");
            return;
        }

        var referenceLength = new FileInfo(referenceFile).Length;
        var sceneLength     = new FileInfo(sceneFile).Length;
        var tooSmall        = referenceLength > 0 && sceneLength < referenceLength * MinSizeRatio;
        var hasMoon         = ContainsLine(sceneFile, ExpectedMarker);

        if (!tooSmall && hasMoon) return;

        RestoreFromReference(
            referenceFile,
            sceneFile,
            tooSmall ? $"scene size {sceneLength} is much smaller than reference {referenceLength}" : "moon terrain marker missing");
    }

    static bool ContainsLine(string path, string needle)
    {
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (line.IndexOf(needle, System.StringComparison.Ordinal) >= 0)
                    return true;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    static void RestoreFromReference(string source, string destination, string reason)
    {
        try
        {
            var destDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
            File.Copy(source, destination, overwrite: true);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
            ReopenSceneIfActive();
            Debug.LogWarning(
                $"[SpaceHubSceneGuardian] {ScenePath} was corrupted ({reason}) " +
                $"and has been restored from {ReferencePath}.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SpaceHubSceneGuardian] Auto-restore failed: {e.Message}");
        }
    }

    static void ReopenSceneIfActive()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (scene.path != ScenePath) continue;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return;
        }
    }
}
#endif
