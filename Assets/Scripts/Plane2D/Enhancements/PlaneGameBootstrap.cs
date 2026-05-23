using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// 飞机大战增强系统的统一入口：
    /// 当 Level1_Scene 加载时自动注入所有 Enhancement 组件。
    /// 不修改 2D 原项目的任何脚本/场景/预制体。
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
        /// 必须在 GameManager.Start 之前执行（sceneLoaded 早于场景中脚本的 Start）。
        /// 否则会触发 FigureOutHowManyEnemiesExist 的黄字警告。
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

    /// <summary>仅作为「已注入」标记。</summary>
    public sealed class PlaneGameEnhancementsRoot : MonoBehaviour { }
}
