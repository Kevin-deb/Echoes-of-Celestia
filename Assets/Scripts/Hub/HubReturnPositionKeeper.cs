using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Remembers where the player stood in the Hub when entering a mini-game (plane shooter or
/// Pixel Dungeon) and puts them back at that exact spot when they return — instead of the
/// default Hub spawn point. Pure runtime injection: no scene or existing script is modified.
/// </summary>
public static class HubReturnPositionKeeper
{
    const string HubSceneName = "Hub";
    static readonly string[] MinigameScenes = { "Level1", "Level2", "PixelDungeon" };

    static Vector3 _savedPos;
    static Quaternion _savedRot;
    static bool _hasSaved;
    static string _lastSceneName = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        HandleScene(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HandleScene(scene);

    static void HandleScene(Scene scene)
    {
        if (!scene.IsValid()) return;

        if (scene.name == HubSceneName)
        {
            bool restore = _hasSaved && IsMinigame(_lastSceneName);
            var go = new GameObject("HubReturnPositionKeeper");
            var worker = go.AddComponent<HubReturnPositionWorker>();
            worker.Init(restore, _savedPos, _savedRot);
            if (restore) _hasSaved = false;   // consume; tracker re-arms while in the Hub
        }

        _lastSceneName = scene.name;
    }

    static bool IsMinigame(string sceneName)
    {
        foreach (var s in MinigameScenes)
            if (s == sceneName) return true;
        return false;
    }

    /// <summary>Continuously records the player's Hub position (called by the worker).</summary>
    internal static void SavePose(Vector3 pos, Quaternion rot)
    {
        _savedPos = pos;
        _savedRot = rot;
        _hasSaved = true;
    }

    /// <summary>
    /// Hub-scene worker: first restores the saved pose (when returning from a mini-game),
    /// then keeps recording the player's pose every frame so the NEXT entry is captured too.
    /// </summary>
    sealed class HubReturnPositionWorker : MonoBehaviour
    {
        bool _restore;
        Vector3 _restorePos;
        Quaternion _restoreRot;
        Transform _player;

        public void Init(bool restore, Vector3 pos, Quaternion rot)
        {
            _restore = restore;
            _restorePos = pos;
            _restoreRot = rot;
        }

        IEnumerator Start()
        {
            // Wait for the Hub hierarchy (player, controllers) to finish initialising.
            yield return null;
            yield return null;

            for (int attempt = 0; attempt < 120 && _player == null; attempt++)
            {
                var pg = GameObject.FindGameObjectWithTag("Player");
                if (pg != null) _player = pg.transform;
                else yield return null;
            }
            if (_player == null) yield break;

            if (_restore)
            {
                var cc = _player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                _player.SetPositionAndRotation(_restorePos, _restoreRot);
                if (cc != null) cc.enabled = true;
                Physics.SyncTransforms();
            }
        }

        void LateUpdate()
        {
            if (_player == null) return;
            // Record only while the player walks on foot (mini-games are entered on foot).
            SavePose(_player.position, _player.rotation);
        }
    }
}
