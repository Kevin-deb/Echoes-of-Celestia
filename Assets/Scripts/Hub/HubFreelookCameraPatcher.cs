using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 当 Hub 场景加载时，禁掉 Creepy Cat 示例场景自带的相机脚本（MouseLook 等），
/// 让 HubSimpleThirdPerson 接管相机控制。不改动素材包本身。
/// </summary>
public sealed class HubFreelookCameraPatcher : MonoBehaviour
{
    const string HubSceneName = "Hub";
    const string FreelookCameraName = "P_Camera_Freelook";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall(scene);

    static void TryInstall(Scene scene)
    {
        if (!scene.IsValid() || scene.name != HubSceneName) return;
        if (FindFirstObjectByType<HubFreelookCameraPatcher>() != null) return;

        var runner = new GameObject("HubFreelookCameraPatcher");
        runner.AddComponent<HubFreelookCameraPatcher>();
    }

    void Start()
    {
        if (SceneManager.GetActiveScene().name != HubSceneName)
        {
            Destroy(gameObject);
            return;
        }

        var rig = GameObject.Find(FreelookCameraName);
        if (rig == null) return;

        // GetComponentsInChildren<MonoBehaviour> 只会返回 MonoBehaviour 子类，
        // Camera / AudioListener / Light 等内置 Behaviour 不在其中，所以这里只关闭
        // 示例场景挂的自定义脚本（MouseLook、Launcher 等），不会影响相机本身。
        foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;
            behaviour.enabled = false;
        }
    }
}
