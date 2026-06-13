using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// Central combat helper (design doc 4.3): owns the projectile & particle pools and exposes
    /// firing, melee-arc resolution and the visual feedback bursts. Created once by Bootstrap.
    /// </summary>
    public class CombatService : MonoBehaviour
    {
        public static CombatService I;

        private GameDatabase _db;
        private AudioSource _audio;
        private ObjectPool<Projectile> _proj;
        private ObjectPool<Particle> _part;
        private Sprite _dot, _arrow;

        public void Init(GameDatabase db, AudioSource sfx)
        {
            I = this;
            _db = db;
            _audio = sfx;
            _dot = GameUtil.MakeDot(6, Color.white);
            _arrow = GameUtil.MakeArrow();

            _proj = new ObjectPool<Projectile>(BuildProjectileTemplate(), transform, 48);
            _part = new ObjectPool<Particle>(BuildParticleTemplate(), transform, 80);
        }

        private Projectile BuildProjectileTemplate()
        {
            var go = new GameObject("_projTemplate");
            go.transform.SetParent(transform);
            go.AddComponent<SpriteRenderer>();
            var p = go.AddComponent<Projectile>(); // auto-adds Rigidbody2D + CircleCollider2D
            go.SetActive(false);
            return p;
        }

        private Particle BuildParticleTemplate()
        {
            var go = new GameObject("_partTemplate");
            go.transform.SetParent(transform);
            go.AddComponent<SpriteRenderer>().sprite = _dot;
            var p = go.AddComponent<Particle>();
            go.SetActive(false);
            return p;
        }

        public void ReleaseProjectile(Projectile p) => _proj.Release(p);
        public void ReleaseParticle(Particle p) => _part.Release(p);

        // ---- Firing ----

        /// <summary>Fires a player/enemy ranged weapon: handles multi-shot, spread, element colour & muzzle.</summary>
        public void FireWeapon(Vector2 origin, Vector2 aim, WeaponDef w, Team team, float damageMult, float critBonus)
        {
            Sprite spr = w.projSpriteKey == "__arrow" ? _arrow
                       : w.projSpriteKey != null ? (_db.GetSprite(w.projSpriteKey) ?? _dot)
                       : _dot;
            Color col = w.element == Element.Physical ? new Color(1f, 0.95f, 0.8f) : GameUtil.ElementColor(w.element);
            bool rotate = w.kind == WeaponKind.Bow;
            bool chain = w.element == Element.Lightning;
            int n = Mathf.Max(1, w.projectiles);
            float baseAng = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            for (int i = 0; i < n; i++)
            {
                float ang = baseAng;
                if (n > 1) ang += Mathf.Lerp(-w.spread * 0.5f, w.spread * 0.5f, (float)i / (n - 1));
                else if (w.spread > 0f) ang += Random.Range(-w.spread * 0.5f, w.spread * 0.5f);

                Vector2 dir = new(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                _proj.Get().Launch(origin, dir, spr, col, team, w.damage * damageMult, w.element,
                    w.projSpeed, w.knockback, w.pierce, w.range, w.critChance + critBonus, chain, rotate,
                    w.kind == WeaponKind.Bow ? 1f : 1.1f);
            }
            Muzzle(origin, aim, col);
        }

        public void EnemyShoot(Vector2 origin, Vector2 aim, EnemyDef e)
        {
            Color col = e.element == Element.Physical ? new Color(1f, 0.45f, 0.45f) : GameUtil.ElementColor(e.element);
            _proj.Get().Launch(origin, aim.normalized, _dot, col, Team.Enemy, e.projDamage, e.element,
                e.projSpeed, 2f, 0, 4f, 0f, e.element == Element.Lightning, false, 1.2f);
        }

        /// <summary>Fan-shaped melee hit (design doc 3.3): overlap circle filtered by arc angle.</summary>
        public void MeleeArc(Vector2 origin, Vector2 dir, float range, float arcDeg, float dmg, float knock,
            Element element, Team team, float critChance)
        {
            var hits = Physics2D.OverlapCircleAll(origin + dir.normalized * (range * 0.3f), range);
            float half = arcDeg * 0.5f;
            bool any = false;
            foreach (var h in hits)
            {
                var hp = h.GetComponentInParent<Health>();
                if (hp == null || hp.Dead || hp.team == team) continue;
                Vector2 to = (Vector2)hp.transform.position - origin;
                if (to.sqrMagnitude > 0.0001f && Vector2.Angle(dir, to) > half) continue;

                bool crit = Random.value < critChance;
                float d = crit ? dmg * 1.7f : dmg;
                if (hp.Damage(d, element, origin, knock, team))
                {
                    if (element != Element.Physical) hp.GetComponent<StatusReceiver>()?.Apply(element, d);
                    Spark(hp.transform.position, crit ? Color.white : new Color(1f, 0.9f, 0.6f), crit ? 8 : 5);
                    any = true;
                }
            }
            if (any) { Juice.HitStop(0.045f); Juice.Shake(0.11f); }
        }

        // ---- Particles ----

        public void Spark(Vector2 pos, Color c, int n)
        {
            for (int i = 0; i < n; i++)
                _part.Get().Go(pos, Random.insideUnitCircle * Random.Range(2f, 5f), c,
                    Random.Range(0.15f, 0.3f), Random.Range(0.18f, 0.34f), 7f);
        }

        public void Burst(Vector2 pos, Color c, int n = 14)
        {
            for (int i = 0; i < n; i++)
                _part.Get().Go(pos, Random.insideUnitCircle * Random.Range(2f, 7f), c,
                    Random.Range(0.25f, 0.55f), Random.Range(0.28f, 0.6f), 5f);
        }

        public void Muzzle(Vector2 pos, Vector2 dir, Color c)
        {
            for (int i = 0; i < 3; i++)
                _part.Get().Go(pos + dir.normalized * 0.3f, dir.normalized * Random.Range(3f, 6f) + Random.insideUnitCircle, c,
                    0.12f, Random.Range(0.2f, 0.34f), 10f);
        }

        public void Pop(Vector2 pos, Color c)
        {
            for (int i = 0; i < 5; i++)
                _part.Get().Go(pos, new Vector2(Random.Range(-2f, 2f), Random.Range(2f, 5f)), c, 0.4f, 0.3f, 3f);
        }

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip != null && _audio != null) _audio.PlayOneShot(clip, volume);
        }

        public AudioClip FireSfx => _db != null ? _db.sfxFire : null;
    }
}
