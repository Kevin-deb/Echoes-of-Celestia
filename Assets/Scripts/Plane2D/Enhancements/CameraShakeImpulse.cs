using UnityEngine;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// 基于 trauma 模型的相机震屏：trauma^2 控制振幅，使用 Perlin 噪声生成方向，
    /// 自动衰减，避免堆栈过强。
    /// </summary>
    public sealed class CameraShakeImpulse : MonoBehaviour
    {
        [SerializeField] float maxOffset = 0.55f;
        [SerializeField] float maxRotZ = 4f;
        [SerializeField] float decay = 1.6f;
        [SerializeField] float frequency = 22f;

        Vector3 _origin;
        Quaternion _originRot;
        float _trauma;
        float _seedX;
        float _seedY;
        float _seedR;

        void Awake()
        {
            _origin = transform.localPosition;
            _originRot = transform.localRotation;
            _seedX = Random.value * 100f;
            _seedY = Random.value * 100f;
            _seedR = Random.value * 100f;
        }

        void OnEnable()
        {
            PlaneGameEvents.EnemyDamaged += OnEnemyDamaged;
            PlaneGameEvents.EnemyKilled += OnEnemyKilled;
            PlaneGameEvents.PlayerDamaged += OnPlayerDamaged;
            PlaneGameEvents.PlayerKilled += OnPlayerKilled;
        }

        void OnDisable()
        {
            PlaneGameEvents.EnemyDamaged -= OnEnemyDamaged;
            PlaneGameEvents.EnemyKilled -= OnEnemyKilled;
            PlaneGameEvents.PlayerDamaged -= OnPlayerDamaged;
            PlaneGameEvents.PlayerKilled -= OnPlayerKilled;
        }

        void OnEnemyDamaged(GameObject _, int __) => Add(0.08f);
        void OnEnemyKilled(Vector3 _, int __) => Add(0.22f);
        void OnPlayerDamaged(GameObject _, int __) => Add(0.42f);
        void OnPlayerKilled(Vector3 _) => Add(0.85f);

        public void Add(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        void LateUpdate()
        {
            if (_trauma <= 0.0001f)
            {
                transform.localPosition = _origin;
                transform.localRotation = _originRot;
                return;
            }

            var shake = _trauma * _trauma;
            var t = Time.unscaledTime * frequency;
            var nx = Mathf.PerlinNoise(_seedX, t) * 2f - 1f;
            var ny = Mathf.PerlinNoise(_seedY, t) * 2f - 1f;
            var nr = Mathf.PerlinNoise(_seedR, t) * 2f - 1f;

            transform.localPosition = _origin + new Vector3(nx, ny, 0f) * shake * maxOffset;
            transform.localRotation = _originRot * Quaternion.Euler(0f, 0f, nr * shake * maxRotZ);

            _trauma = Mathf.Max(0f, _trauma - decay * Time.unscaledDeltaTime);
        }
    }
}
