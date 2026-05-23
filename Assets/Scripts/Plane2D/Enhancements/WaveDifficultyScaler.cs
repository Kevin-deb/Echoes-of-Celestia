using UnityEngine;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// 随时间推进逐渐降低 EnemySpawner.spawnDelay，提高敌人移动速度，
    /// 模拟「波次进阶」的难度曲线。不直接修改原 EnemySpawner 字段定义，
    /// 仅运行时调整其公共属性，保持原玩法基础上自然加压。
    /// </summary>
    public sealed class WaveDifficultyScaler : MonoBehaviour
    {
        [SerializeField] float initialIntervalScale = 1.0f;
        [SerializeField] float minIntervalScale = 0.35f;
        [SerializeField] float intervalDecayPerSecond = 0.012f;
        [SerializeField] float enemySpeedMultiplierMax = 1.6f;
        [SerializeField] float enemySpeedRampPerSecond = 0.02f;

        float _initSpawnDelay = -1f;
        float _initEnemySpeedAvg = -1f;
        float _accumulated;

        void Start()
        {
            CaptureInitialValues();
        }

        void CaptureInitialValues()
        {
            var spawners = FindObjectsOfType<EnemySpawner>();
            if (spawners.Length > 0)
            {
                float sum = 0f;
                int count = 0;
                for (int i = 0; i < spawners.Length; i++)
                {
                    sum += spawners[i].spawnDelay;
                    count++;
                }
                _initSpawnDelay = count > 0 ? sum / count : 2f;
            }
        }

        void Update()
        {
            _accumulated += Time.deltaTime;

            var intervalScale = Mathf.Max(
                minIntervalScale,
                initialIntervalScale - intervalDecayPerSecond * _accumulated);

            var enemySpeedMul = Mathf.Min(
                enemySpeedMultiplierMax,
                1f + enemySpeedRampPerSecond * _accumulated);

            ApplyToSpawners(intervalScale);
            ApplyToLiveEnemies(enemySpeedMul);
        }

        void ApplyToSpawners(float intervalScale)
        {
            if (_initSpawnDelay <= 0f) return;
            var spawners = FindObjectsOfType<EnemySpawner>();
            for (int i = 0; i < spawners.Length; i++)
            {
                spawners[i].spawnDelay = _initSpawnDelay * intervalScale;
            }
        }

        void ApplyToLiveEnemies(float speedMul)
        {
            var enemies = FindObjectsOfType<Enemy>();
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] == null) continue;
                if (_initEnemySpeedAvg <= 0f && i == 0) _initEnemySpeedAvg = enemies[i].moveSpeed;
                enemies[i].moveSpeed = Mathf.Max(enemies[i].moveSpeed, enemies[i].moveSpeed * 0.999f);
                if (_initEnemySpeedAvg > 0f)
                    enemies[i].moveSpeed = _initEnemySpeedAvg * speedMul;
            }
        }
    }
}
