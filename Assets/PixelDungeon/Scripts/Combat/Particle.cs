using UnityEngine;

namespace PixelDungeon
{
    /// <summary>Pooled one-shot pixel particle: moves, fades and shrinks, then returns to the pool.
    /// Used for hit sparks, death bursts, muzzle flashes and pickup pops.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Particle : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Vector2 _vel;
        private float _life, _maxLife, _drag, _baseSize;

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        public void Go(Vector2 pos, Vector2 vel, Color color, float life, float size, float drag = 4f)
        {
            transform.position = pos;
            _vel = vel;
            _life = _maxLife = life;
            _drag = drag;
            _baseSize = size;
            _sr.color = color;
            _sr.sortingOrder = 60;
            transform.localScale = Vector3.one * size;
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) { CombatService.I.ReleaseParticle(this); return; }

            float dt = Time.deltaTime;
            transform.position += (Vector3)(_vel * dt);
            _vel = Vector2.MoveTowards(_vel, Vector2.zero, _drag * dt);

            float k = _life / _maxLife;
            var c = _sr.color; c.a = k; _sr.color = c;
            transform.localScale = Vector3.one * (_baseSize * Mathf.Lerp(0.2f, 1f, k));
        }
    }
}
