using UnityEngine;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// 敌人死亡时在死亡位置生成粒子爆破特效，
    /// 不依赖任何外部资源，使用内建白色精灵 + ParticleSystem 即时生成。
    /// </summary>
    public sealed class DeathBurstWatcher : MonoBehaviour
    {
        [SerializeField] int particlesPerBurst = 24;
        [SerializeField] float lifetime = 0.7f;
        [SerializeField] float startSpeed = 6f;
        [SerializeField] Color tint = new Color(1f, 0.78f, 0.35f, 1f);

        Material _particleMaterial;

        void OnEnable()
        {
            PlaneGameEvents.EnemyKilled += OnEnemyKilled;
            PlaneGameEvents.PlayerKilled += OnPlayerKilled;
        }

        void OnDisable()
        {
            PlaneGameEvents.EnemyKilled -= OnEnemyKilled;
            PlaneGameEvents.PlayerKilled -= OnPlayerKilled;
        }

        void OnEnemyKilled(Vector3 pos, int score) => SpawnBurst(pos, tint, particlesPerBurst);
        void OnPlayerKilled(Vector3 pos) => SpawnBurst(pos, new Color(1f, 0.35f, 0.35f, 1f), particlesPerBurst * 2);

        Material EnsureMaterial()
        {
            if (_particleMaterial == null)
            {
                _particleMaterial = new Material(Shader.Find("Sprites/Default"));
                _particleMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            return _particleMaterial;
        }

        void SpawnBurst(Vector3 position, Color color, int count)
        {
            var go = new GameObject("DeathBurst");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = EnsureMaterial();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 50;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 1f;

            var main = ps.main;
            main.duration = 0.05f;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = startSpeed;
            main.startSize = 0.18f;
            main.startColor = color;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;
            shape.radiusThickness = 0f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(new Color(1f, 1f, 1f, 1f), 0.15f), new GradientColorKey(color * 0.6f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            ps.Play(true);
        }
    }
}
