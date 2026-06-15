using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// The 3D Hub locks and hides the cursor. The plane scenes need it back the
    /// way the original 2D project expects — visible but confined to the window.
    /// Otherwise the player plane aims from a locked cursor position, so a click
    /// makes the cursor disappear and the heading goes haywire.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class PlaneSceneCursorGuard : MonoBehaviour
    {
        static readonly string[] PlaneSceneNames = { "MainMenu", "Level1", "Level2" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureForScene(scene);

        static void EnsureForScene(Scene scene)
        {
            if (!IsPlaneScene(scene.name)) return;
            if (FindObjectOfType<PlaneSceneCursorGuard>() != null) return;

            var guard = new GameObject("PlaneSceneCursorGuard");
            guard.AddComponent<PlaneSceneCursorGuard>();
        }

        static bool IsPlaneScene(string sceneName)
        {
            foreach (var planeSceneName in PlaneSceneNames)
            {
                if (sceneName == planeSceneName)
                    return true;
            }

            return false;
        }

        void Awake()
        {
            ApplyCursorState();
        }

        void Update()
        {
            ApplyCursorState();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                ApplyCursorState();
        }

        static void ApplyCursorState()
        {
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }
    }
}
