using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// Environmental hazards (design doc 1.5 / 3.6): toggling spike traps and an oscillating saw blade.
    /// Damages any living thing standing on it, so it can be used against enemies too.
    /// </summary>
    public class Trap : MonoBehaviour
    {
        public enum Kind { Spike, Saw }

        private Kind _kind;
        private SpriteRenderer _sr;
        private Sprite _on, _off;
        private float _timer, _pulse;
        private bool _active;
        private float _damage;
        private Vector2 _origin;
        private float _swing;

        public static Trap SpawnSpike(Vector2 pos)
        {
            var go = new GameObject("SpikeTrap") { transform = { position = pos } };
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 6;
            var t = go.AddComponent<Trap>();
            t._kind = Kind.Spike;
            t._sr = sr;
            t._on = GameManager.I.Db.GetSprite("SpikeOn");
            t._off = GameManager.I.Db.GetSprite("SpikeOff");
            t._damage = 14f;
            t._timer = Random.Range(0f, 1.5f);
            sr.sprite = t._off;
            return t;
        }

        public static Trap SpawnSaw(Vector2 pos)
        {
            var go = new GameObject("SawTrap") { transform = { position = pos } };
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GameManager.I.Db.GetSprite("Saw");
            sr.sortingOrder = 16;
            var t = go.AddComponent<Trap>();
            t._kind = Kind.Saw;
            t._sr = sr;
            t._damage = 10f;
            t._active = true;
            t._origin = pos;
            return t;
        }

        private void Update()
        {
            if (_kind == Kind.Spike)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    _active = !_active;
                    _timer = _active ? 1.0f : 1.6f;
                    _sr.sprite = _active ? _on : _off;
                }
            }
            else // Saw: spin and slide back and forth
            {
                _sr.transform.Rotate(0f, 0f, 720f * Time.deltaTime);
                _swing += Time.deltaTime;
                transform.position = _origin + new Vector2(Mathf.Sin(_swing) * 2.2f, 0f);
            }

            if (!_active) return;
            _pulse -= Time.deltaTime;
            if (_pulse > 0f) return;
            _pulse = 0.4f;

            foreach (var c in Physics2D.OverlapCircleAll(transform.position, _kind == Kind.Saw ? 0.5f : 0.45f))
            {
                var hp = c.GetComponentInParent<Health>();
                if (hp != null && !hp.Dead)
                    hp.Damage(_damage, Element.Physical, transform.position, 4f, Team.Enemy);
            }
        }
    }
}
