using System;
using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// Mana/energy pool (design doc 3.3): ranged & magic weapons spend it, it regenerates after a
    /// short delay. This is the "風筝 vs 搏命" resource tension between ranged and melee play.
    /// </summary>
    public class Energy : MonoBehaviour
    {
        public float max = 100f;
        public float current;
        public float regenPerSecond = 14f;
        public float regenDelay = 0.6f;

        public event Action Changed;

        private float _delay;

        public float Fraction => max <= 0 ? 0 : Mathf.Clamp01(current / max);

        private void Awake()
        {
            if (current <= 0f) current = max;
        }

        public void SetMax(float m)
        {
            max = m;
            current = m;
            Changed?.Invoke();
        }

        public bool Spend(float amount)
        {
            if (amount <= 0f) return true;
            if (current < amount) return false;
            current -= amount;
            _delay = regenDelay;
            Changed?.Invoke();
            return true;
        }

        public void Add(float amount)
        {
            current = Mathf.Min(max, current + amount);
            Changed?.Invoke();
        }

        private void Update()
        {
            if (_delay > 0f) { _delay -= Time.deltaTime; return; }
            if (current < max)
            {
                current = Mathf.Min(max, current + regenPerSecond * Time.deltaTime);
                Changed?.Invoke();
            }
        }
    }
}
