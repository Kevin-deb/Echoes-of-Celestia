using System.Collections.Generic;
using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// Pooled projectile (design doc 4.3). Kinematic + trigger collider so it reports hits against
    /// walls, enemies and the player. Handles pierce, element application, lightning chaining,
    /// crits and the on-hit feel (spark, hit-stop, shake).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Projectile : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Rigidbody2D _rb;

        private Team _team;
        private float _damage, _knockback, _speed, _life, _critChance;
        private int _pierce;
        private Element _element;
        private bool _rotate, _chain;
        private Vector2 _dir;
        private readonly HashSet<Health> _hit = new();

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;
            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.18f;
        }

        public void Launch(Vector2 pos, Vector2 dir, Sprite sprite, Color color, Team team,
            float damage, Element element, float speed, float knockback, int pierce, float life,
            float critChance, bool chain, bool rotate, float scale)
        {
            transform.position = pos;
            _dir = dir.normalized;
            _team = team;
            _damage = damage;
            _element = element;
            _speed = speed;
            _knockback = knockback;
            _pierce = pierce;
            _life = life;
            _critChance = critChance;
            _chain = chain;
            _rotate = rotate;
            _hit.Clear();

            _sr.sprite = sprite;
            _sr.color = color;
            _sr.sortingOrder = 50;
            transform.localScale = Vector3.one * scale;
            transform.right = rotate ? (Vector3)_dir : Vector3.right;
        }

        private void FixedUpdate()
        {
            _rb.MovePosition(_rb.position + _dir * (_speed * Time.fixedDeltaTime));
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) Despawn();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<WallMarker>() != null)
            {
                CombatService.I.Spark(transform.position, _sr.color, 4);
                Despawn();
                return;
            }

            var hp = other.GetComponentInParent<Health>();
            if (hp == null || hp.Dead || hp.team == _team || _hit.Contains(hp)) return;
            _hit.Add(hp);

            bool crit = Random.value < _critChance;
            float dmg = crit ? _damage * 1.7f : _damage;

            if (hp.Damage(dmg, _element, transform.position, _knockback, _team))
            {
                ApplyOnHit(hp, dmg, crit);
            }

            if (_chain) ChainTo(hp);

            _pierce--;
            if (_pierce < 0) Despawn();
        }

        private void ApplyOnHit(Health hp, float dmg, bool crit)
        {
            var status = hp.GetComponent<StatusReceiver>();
            if (status != null && _element != Element.Physical) status.Apply(_element, dmg);

            CombatService.I.Spark(transform.position, crit ? Color.white : _sr.color, crit ? 8 : 5);
            Juice.HitStop(crit ? 0.06f : 0.035f);
            Juice.Shake(crit ? 0.18f : 0.08f);
        }

        private void ChainTo(Health from)
        {
            const float radius = 3.5f;
            var hits = Physics2D.OverlapCircleAll(from.transform.position, radius);
            Health best = null; float bestDist = float.MaxValue;
            foreach (var h in hits)
            {
                var oh = h.GetComponentInParent<Health>();
                if (oh == null || oh.Dead || oh.team == _team || _hit.Contains(oh)) continue;
                float d = Vector2.Distance(from.transform.position, oh.transform.position);
                if (d < bestDist) { bestDist = d; best = oh; }
            }
            if (best == null) return;
            _hit.Add(best);
            best.Damage(_damage * 0.6f, Element.Lightning, from.transform.position, _knockback * 0.5f, _team);
            best.GetComponent<StatusReceiver>()?.Apply(Element.Lightning, _damage);
            CombatService.I.Spark(best.transform.position, GameUtil.ElementColor(Element.Lightning), 6);
        }

        private void Despawn() => CombatService.I.ReleaseProjectile(this);
    }
}
