using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// 当玩家或敌人受伤时，让其 Sprite 短暂泛白闪烁，提供命中反馈。
    /// 使用 SpriteRenderer.color 修改实例色，恢复原色。
    /// </summary>
    public sealed class HitFlashWatcher : MonoBehaviour
    {
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] float flashDuration = 0.08f;
        [SerializeField] float blendIntensity = 0.85f;

        readonly Dictionary<int, Coroutine> _running = new Dictionary<int, Coroutine>(32);

        void OnEnable()
        {
            PlaneGameEvents.EnemyDamaged += OnDamaged;
            PlaneGameEvents.PlayerDamaged += OnDamaged;
        }

        void OnDisable()
        {
            PlaneGameEvents.EnemyDamaged -= OnDamaged;
            PlaneGameEvents.PlayerDamaged -= OnDamaged;
        }

        void OnDamaged(GameObject target, int _)
        {
            if (target == null) return;
            var sprites = target.GetComponentsInChildren<SpriteRenderer>(true);
            if (sprites.Length == 0) return;

            var key = target.GetInstanceID();
            if (_running.TryGetValue(key, out var existing) && existing != null)
                StopCoroutine(existing);
            _running[key] = StartCoroutine(FlashRoutine(sprites, key));
        }

        IEnumerator FlashRoutine(SpriteRenderer[] sprites, int key)
        {
            var originals = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                originals[i] = sprites[i] != null ? sprites[i].color : Color.white;

            var t = 0f;
            while (t < flashDuration)
            {
                t += Time.unscaledDeltaTime;
                var k = 1f - (t / flashDuration);
                k = Mathf.Clamp01(k) * blendIntensity;
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] == null) continue;
                    sprites[i].color = Color.Lerp(originals[i], flashColor, k);
                }
                yield return null;
            }

            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null) sprites[i].color = originals[i];

            _running.Remove(key);
        }
    }
}
