using System;
using UnityEngine;

/// <summary>
/// 波次、生命、敌人生成与胜负。将敌人预制体放在路点 0 附近生成。
/// </summary>
public sealed class TDGameManager : MonoBehaviour
{
    [Serializable]
    public sealed class Wave
    {
        public int enemyCount = 5;
        public float spawnInterval = 1.2f;
    }

    [SerializeField] WaypointPath path;
    [SerializeField] EnemyWalker enemyPrefab;
    [SerializeField] Wave[] waves = { new Wave { enemyCount = 5, spawnInterval = 1f } };
    [SerializeField] int startingLives = 20;
    [SerializeField] float delayBeforeFirstSpawn = 1f;
    [SerializeField] int levelIndexForProgress = 0;

    int _lives;
    int _waveIndex;
    int _spawnedInWave;
    float _nextSpawnTime;
    bool _started;
    bool _defeat;
    bool _victory;
    int _activeEnemies;

    public Vector3 PathEndPosition => path != null ? path.GetEndTransform().position : transform.position;

    void Start()
    {
        _lives = startingLives;
        _nextSpawnTime = Time.time + delayBeforeFirstSpawn;
        _started = true;
    }

    void Update()
    {
        if (!_started || _defeat || _victory) return;
        TryAdvanceWave();
        TrySpawn();
        TryCheckVictory();
    }

    void TryAdvanceWave()
    {
        if (waves == null || _waveIndex >= waves.Length) return;
        var w = waves[_waveIndex];
        if (_spawnedInWave < w.enemyCount) return;
        if (_activeEnemies > 0) return;

        _waveIndex++;
        _spawnedInWave = 0;
        if (_waveIndex < waves.Length)
            _nextSpawnTime = Time.time + 1f;
    }

    void TrySpawn()
    {
        if (waves == null || _waveIndex >= waves.Length) return;
        var w = waves[_waveIndex];
        if (_spawnedInWave >= w.enemyCount) return;
        if (Time.time < _nextSpawnTime) return;
        if (path == null || enemyPrefab == null) return;

        var e = Instantiate(enemyPrefab, path.GetWorldPosition(0), Quaternion.identity);
        e.Initialize(path, this);
        _activeEnemies++;
        _spawnedInWave++;
        _nextSpawnTime = Time.time + w.spawnInterval;
    }

    void TryCheckVictory()
    {
        if (waves == null || _waveIndex < waves.Length) return;
        if (_activeEnemies > 0) return;
        _victory = true;
        if (GameSession.Instance != null)
            GameSession.Instance.UnlockNextLevelIfNeeded(levelIndexForProgress);
    }

    public void NotifyEnemyKilled(EnemyWalker enemy, int rewardGold)
    {
        _activeEnemies = Mathf.Max(0, _activeEnemies - 1);
        if (GameSession.Instance != null && rewardGold > 0)
            GameSession.Instance.AddGold(rewardGold);
    }

    public void NotifyEnemyReachedEnd(EnemyWalker enemy)
    {
        _activeEnemies = Mathf.Max(0, _activeEnemies - 1);
        _lives--;
        if (_lives > 0) return;
        _defeat = true;
    }
}
