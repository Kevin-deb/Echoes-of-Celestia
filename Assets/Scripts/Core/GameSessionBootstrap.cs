using UnityEngine;

/// <summary>
/// If a scene has no GameSession placed in it, create one right after the first
/// scene loads so the level-select and economy code never hit a null reference.
/// </summary>
static class GameSessionBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureGameSession()
    {
        if (GameSession.Instance != null) return;
        if (UnityEngine.Object.FindFirstObjectByType<GameSession>() != null) return;

        var go = new GameObject("GameSession");
        go.AddComponent<GameSession>();
    }
}
