using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// Elemental reactions on a target (design doc 3.4 / 7.1): fire = burn DoT, poison = poison DoT,
    /// ice = slow. Lightning chaining is resolved by the projectile at hit-time, not here.
    /// Exposes SpeedMultiplier so movement controllers can apply slow, and tints the sprite
    /// while a status is active.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class StatusReceiver : MonoBehaviour
    {
        public float SpeedMultiplier { get; private set; } = 1f;

        private Health _hp;
        private float _burn, _burnDps;
        private float _poison, _poisonDps;
        private float _slow;
        private SpriteRenderer _tint;
        private Color _baseColor = Color.white;

        private void Awake() => _hp = GetComponent<Health>();

        public void SetTint(SpriteRenderer sr)
        {
            _tint = sr;
            if (_tint != null) _baseColor = _tint.color;
        }

        public void Apply(Element element, float baseDamage)
        {
            switch (element)
            {
                case Element.Fire: _burn = 3f; _burnDps = Mathf.Max(_burnDps, baseDamage * 0.25f); break;
                case Element.Poison: _poison = 4f; _poisonDps = Mathf.Max(_poisonDps, baseDamage * 0.2f); break;
                case Element.Ice: _slow = 2f; break;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_burn > 0f) { _burn -= dt; _hp.Tick(_burnDps * dt); }
            if (_poison > 0f) { _poison -= dt; _hp.Tick(_poisonDps * dt); }
            SpeedMultiplier = _slow > 0f ? 0.5f : 1f;
            if (_slow > 0f) _slow -= dt;

            if (_tint != null)
            {
                Color target = _baseColor;
                if (_slow > 0f) target = Color.Lerp(_baseColor, new Color(0.5f, 0.8f, 1f), 0.5f);
                else if (_burn > 0f) target = Color.Lerp(_baseColor, new Color(1f, 0.6f, 0.4f), 0.4f);
                else if (_poison > 0f) target = Color.Lerp(_baseColor, new Color(0.6f, 1f, 0.5f), 0.4f);
                _tint.color = target;
            }
        }
    }
}
