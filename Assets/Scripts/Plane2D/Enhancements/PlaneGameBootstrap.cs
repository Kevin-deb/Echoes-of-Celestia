using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// Single entry point for the plane-shooter enhancements: when Level1_Scene
    /// loads it injects every Enhancement component automatically, without editing
    /// any of the original 2D project's scripts, scenes or prefabs.
    /// </summary>
    public static class PlaneGameBootstrap
    {
        const string PlaneSceneName = "Level1_Scene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RegisterHooks()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInject(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInject(scene);

        static void TryInject(Scene scene)
        {
            if (!scene.IsValid()) return;
            if (scene.name != PlaneSceneName) return;
            if (Object.FindObjectOfType<PlaneGameEnhancementsRoot>() != null) return;

            PlaneGameEvents.ResetAllSubscribers();
            UnlockCursorForPlayfield();
            ApplyPlaneScenePatchesBeforeGameManagerStart();

            var root = new GameObject("PlaneGameEnhancements");
            root.AddComponent<PlaneGameEnhancementsRoot>();

            root.AddComponent<PlaneSceneInputController>();
            root.AddComponent<EntityWatcher>();
            root.AddComponent<HitFlashWatcher>();
            root.AddComponent<DeathBurstWatcher>();
            root.AddComponent<ScorePopupWatcher>();
            root.AddComponent<WaveDifficultyScaler>();
            root.AddComponent<PauseMenuExtension>();
            root.AddComponent<PlaneSceneCameraFollow>();

            var starfieldGo = new GameObject("Starfield");
            starfieldGo.transform.SetParent(root.transform, false);
            starfieldGo.AddComponent<Starfield>();

            AttachCameraShake();
        }

        static void UnlockCursorForPlayfield()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Has to run before GameManager.Start — sceneLoaded fires earlier than
        /// any in-scene Start. Skip it and FigureOutHowManyEnemiesExist spits out
        /// its yellow warning.
        /// </summary>
        static void ApplyPlaneScenePatchesBeforeGameManagerStart()
        {
            foreach (var gm in Object.FindObjectsOfType<GameManager>())
            {
                if (gm == null) continue;
                gm.gameIsWinnable = false;
                gm.printDebugOfWinnableStatus = false;
            }

            foreach (var sp in Object.FindObjectsOfType<EnemySpawner>())
            {
                if (sp == null) continue;
                sp.spawnInfinite = true;
            }
        }

        static void AttachCameraShake()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (cam.GetComponent<CameraShakeImpulse>() == null)
                cam.gameObject.AddComponent<CameraShakeImpulse>();
        }
    }

    /// <summary>Just a marker that says the enhancements are already injected.</summary>
    public sealed class PlaneGameEnhancementsRoot : MonoBehaviour { }
}
