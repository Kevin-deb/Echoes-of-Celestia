using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// 3D Hub 会锁定并隐藏鼠标；进入飞机大战时需要恢复为 2D 原项目的可见受限光标。
    /// 否则玩家飞机会用被锁定的鼠标坐标计算朝向，导致点击后光标消失且朝向异常。
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
