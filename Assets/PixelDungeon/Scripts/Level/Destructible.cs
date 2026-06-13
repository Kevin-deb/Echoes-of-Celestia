using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// Breakable room filler (design doc 1.4 / 3.7): barrels, boxes, vases — and explosive barrels.
    /// Has HP (team Enemy) so the player's hits break it; drops loot. Solid so it also blocks movement.
    /// </summary>
    public class Destructible : MonoBehaviour
    {
        private Health _hp;
        private SpriteRenderer _sr;
        private bool _explosive;

        public static Destructible Spawn(Vector2 pos, string spriteKey, float maxHp, bool explosive)
        {
            var go = new GameObject("Destructible") { transform = { position = pos } };
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GameManager.I.Db.GetSprite(spriteKey);
            sr.sortingOrder = 14;
            if (explosive) sr.color = new Color(1f, 0.55f, 0.45f);  // hint that it's volatile

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.7f, 0.6f);
            box.offset = new Vector2(0f, 0.3f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var h = go.AddComponent<Health>();
            h.Configure(maxHp, 0f, false, Team.Enemy, sr);

            var d = go.AddComponent<Destructible>();
            d._hp = h; d._sr = sr; d._explosive = explosive;
            h.Death += d.OnBreak;
            return d;
        }

        private void OnBreak()
        {
            Vector2 pos = transform.position;
            if (_explosive)
            {
                CombatService.I.Burst(pos, new Color(1f, 0.6f, 0.2f), 26);
                Juice.Shake(0.4f);
                Juice.HitStop(0.05f);
                foreach (var c in Physics2D.OverlapCircleAll(pos, 2.2f))
                {
                    var hp = c.GetComponentInParent<Health>();
                    if (hp != null && !hp.Dead && hp != _hp)
                        hp.Damage(45f, Element.Fire, pos, 12f, Team.Enemy);  // hurts everyone — strategic
                }
            }
            else
            {
                CombatService.I.Burst(pos, new Color(0.7f, 0.55f, 0.35f), 10);
            }
            Loot.DropFromDestructible(pos);
            Destroy(gameObject);
        }
    }
}
