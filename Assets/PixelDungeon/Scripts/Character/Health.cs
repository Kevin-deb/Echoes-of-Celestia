using System;
using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// HP + optional armor layer (design doc 3.4). Shared by player, enemies and destructibles.
    /// Handles invulnerability windows, knockback and a white hit-flash.
    /// </summary>
    [RequireComponent(typeof(Transform))]
    public class Health : MonoBehaviour
    {
        public float max = 100f;
        public float current;
        public float armor = 0f;
        public bool isPlayer = false;
        public Team team = Team.Enemy;
        public float postHitInvuln = 0f;   // brief mercy invuln after taking a hit (player)

        [NonSerialized] public bool Dead;
        /// <summary>Brief window after a knockback hit where the owner's movement controller yields
        /// to physics so the knockback impulse is actually visible.</summary>
        [NonSerialized] public float Stun;
        public bool Controllable => Stun <= 0f;

        public event Action<float, Element> Damaged;   // (amount, element) — fired on real hits
        public event Action Death;
        public event Action Changed;

        private float _invulnTimer;
        private SpriteRenderer _flashTarget;
        private Material _baseMat, _flashMat;
        private float _flashTimer;

        public float Fraction => max <= 0 ? 0 : Mathf.Clamp01(current / max);
        public bool Invulnerable => _invulnTimer > 0f;

        private void Awake()
        {
            if (current <= 0f) current = max;
        }

        public void Configure(float maxHp, float startArmor, bool player, Team t, SpriteRenderer flashTarget)
        {
            max = maxHp;
            current = maxHp;
            armor = startArmor;
            isPlayer = player;
            team = t;
            _flashTarget = flashTarget;
            postHitInvuln = player ? 0.6f : 0f;
            if (_flashTarget != null) _baseMat = _flashTarget.sharedMaterial;
            Changed?.Invoke();
        }

        public void MakeInvulnerable(float seconds) => _invulnTimer = Mathf.Max(_invulnTimer, seconds);

        /// <summary>Full hit: armor soak, hp loss, knockback, flash, events. Returns true if it landed.</summary>
        public bool Damage(float amount, Element element, Vector2 from, float knockback, Team source)
        {
            if (Dead || amount <= 0f) return false;
            if (Invulnerable) return false;

            if (armor > 0f)
            {
                float soak = Mathf.Min(armor, amount);
                armor -= soak;
                amount -= soak;
            }

            current -= amount;
            Damaged?.Invoke(amount, element);
            Changed?.Invoke();
            Flash();

            var rb = GetComponent<Rigidbody2D>();
            if (rb != null && knockback > 0f)
            {
                Vector2 dir = ((Vector2)transform.position - from);
                if (dir.sqrMagnitude > 0.0001f)
                {
                    rb.AddForce(dir.normalized * knockback, ForceMode2D.Impulse);
                    Stun = Mathf.Max(Stun, isPlayer ? 0.08f : 0.14f);
                }
            }

            if (postHitInvuln > 0f) MakeInvulnerable(postHitInvuln);

            if (current <= 0f) Die();
            return true;
        }

        /// <summary>Silent damage for damage-over-time (no flash/knockback/invuln gate).</summary>
        public void Tick(float amount)
        {
            if (Dead || amount <= 0f) return;
            current -= amount;
            Changed?.Invoke();
            if (current <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (Dead) return;
            current = Mathf.Min(max, current + amount);
            Changed?.Invoke();
        }

        public void AddArmor(float a) { armor += a; Changed?.Invoke(); }

        private void Die()
        {
            if (Dead) return;
            Dead = true;
            Death?.Invoke();
        }

        private void Flash()
        {
            if (_flashTarget == null) return;
            if (_flashMat == null) _flashMat = new Material(Shader.Find("GUI/Text Shader"));
            _flashTarget.material = _flashMat;
            _flashTimer = 0.08f;
        }

        private void Update()
        {
            if (_invulnTimer > 0f) _invulnTimer -= Time.deltaTime;
            if (Stun > 0f) Stun -= Time.deltaTime;
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && _flashTarget != null && _baseMat != null)
                    _flashTarget.material = _baseMat;
            }
        }
    }
}
