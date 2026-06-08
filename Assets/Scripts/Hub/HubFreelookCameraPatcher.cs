using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// When the Hub scene loads, disables the example camera scripts bundled with the
/// Creepy Cat asset pack (MouseLook, etc.) so that HubSimpleThirdPerson takes over
/// camera control. The asset pack itself is not modified.
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

        // GetComponentsInChildren<MonoBehaviour> only returns MonoBehaviour subclasses.
        // Built-in Behaviours such as Camera, AudioListener, and Light are excluded,
        // so only the custom scripts added by the example scene (MouseLook, Launcher, etc.)
        // are disabled — the camera component itself is unaffected.
        foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;
            behaviour.enabled = false;
        }
    }
}
