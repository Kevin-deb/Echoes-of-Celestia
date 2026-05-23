using UnityEngine;

/// <summary>
/// 若场景中未放置 GameSession，则在首场景加载后自动创建一个，避免选关/经济逻辑空引用。
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
