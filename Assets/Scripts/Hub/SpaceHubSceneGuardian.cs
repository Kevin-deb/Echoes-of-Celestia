#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 守护 <c>Assets/Scenes/Space/Hub.unity</c>。每次 Editor 启动或脚本重载后检查场景文件，
/// 如果它被外部工具（云同步、文件历史、还原工具等）退回到不含月面内容的旧版本，
/// 自动从 <c>Tools/Hub.unity.merged</c> 这个稳定副本恢复，并在 Console 给出警告。
///
/// 注意：如果你确实想手工编辑 Hub 场景，在保存之后请同步把新的内容拷贝覆盖
/// <c>Tools/Hub.unity.merged</c>，否则下次编辑器启动可能又会被这个守护恢复回旧版。
/// </summary>
[InitializeOnLoad]
static class SpaceHubSceneGuardian
{
    const string ScenePath = "Assets/Scenes/Space/Hub.unity";
    const string ReferencePath = "Tools/Hub.unity.merged";
    const string ExpectedMarker = "m_Name: Moon_Closed_A";
    const double MinSizeRatio = 0.5;

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

        var sceneFile = Path.GetFullPath(Path.Combine(projectRoot, ScenePath));
        var referenceFile = Path.GetFullPath(Path.Combine(projectRoot, ReferencePath));

        if (!File.Exists(referenceFile))
        {
            Debug.LogWarning(
                $"[SpaceHubSceneGuardian] 找不到稳定副本 {ReferencePath}，无法自动恢复。" +
                "请确保该文件存在（可重新执行 Tools/merge_space_hub.ps1 并将结果复制为 Hub.unity.merged）。");
            return;
        }

        if (!File.Exists(sceneFile))
        {
            RestoreFromReference(referenceFile, sceneFile, "场景文件不存在");
            return;
        }

        var referenceLength = new FileInfo(referenceFile).Length;
        var sceneLength = new FileInfo(sceneFile).Length;
        var tooSmall = referenceLength > 0 && sceneLength < referenceLength * MinSizeRatio;
        var hasMoon = ContainsLine(sceneFile, ExpectedMarker);

        if (!tooSmall && hasMoon) return;

        RestoreFromReference(
            referenceFile,
            sceneFile,
            tooSmall ? $"场景大小 {sceneLength} 远小于参考 {referenceLength}" : "缺少月面地形标记");
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
                $"[SpaceHubSceneGuardian] {ScenePath} 检测异常（{reason}），" +
                $"已自动从 {ReferencePath} 恢复。");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SpaceHubSceneGuardian] 自动恢复失败：{e.Message}");
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
