using UnityEngine;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// 程序化背景星空：使用 ParticleSystem 渲染慢速向下滚动的星点，
    /// 放在 Background 排序层之后，作为氛围层不阻挡玩法。
    /// </summary>
    public sealed class Starfield : MonoBehaviour
    {
        [SerializeField] int starCount = 220;
        [SerializeField] float scrollSpeed = 1.6f;
        [SerializeField] float fieldHeight = 24f;
        [SerializeField] float fieldWidth = 32f;

        Material _material;

        void Start()
        {
            EnsureCanvasFollowsCamera();
            BuildStarfield();
        }

        void EnsureCanvasFollowsCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 10f);
        }

        Material EnsureMaterial()
        {
            if (_material == null)
            {
                _material = new Material(Shader.Find("Sprites/Default"));
                _material.hideFlags = HideFlags.HideAndDontSave;
            }
            return _material;
        }

        void BuildStarfield()
        {
            var ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.material = EnsureMaterial();
            renderer.sortingLayerName = "Background";
            renderer.sortingOrder = -100;

            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = fieldHeight / scrollSpeed;
            main.startSpeed = scrollSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 1f));
            main.maxParticles = starCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.startRotation = 0f;

            var emission = ps.emission;
            emission.rateOverTime = starCount / main.startLifetime.constant;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(fieldWidth, 0.1f, 0.1f);
            shape.position = new Vector3(0f, fieldHeight * 0.5f, 0f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = -scrollSpeed;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            ps.Play(true);
        }
    }
}
