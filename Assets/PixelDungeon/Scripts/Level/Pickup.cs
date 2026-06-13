using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// A floating collectible (design doc 3.7). Created at runtime; bobs, magnets toward a nearby
    /// player and applies its effect on touch. Weapon pickups carry a WeaponDef.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        private static GameDatabase _db;
        private static Sprite _heart;

        public static void SetDatabase(GameDatabase db) => _db = db;

        private PickupKind _kind;
        private int _value;
        private WeaponDef _weapon;
        private SpriteRenderer _sr;
        private float _bob;
        private float _spawnGuard = 0.35f;   // don't magnet/collect instantly on spawn

        public static Pickup Spawn(PickupKind kind, Vector2 pos, int value = 1, WeaponDef weapon = null)
        {
            var go = new GameObject($"Pickup_{kind}");
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 25;
            sr.sprite = IconFor(kind, weapon);

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.55f;

            var p = go.AddComponent<Pickup>();
            p._kind = kind; p._value = value; p._weapon = weapon; p._sr = sr;
            p._bob = Random.value * Mathf.PI * 2f;

            // a little spawn pop in the toss direction
            CombatService.I?.Pop(pos, kind == PickupKind.CoinGold ? new Color(1f, 0.85f, 0.2f) : Color.white);
            return p;
        }

        private static Sprite IconFor(PickupKind kind, WeaponDef weapon)
        {
            switch (kind)
            {
                case PickupKind.CoinGold: return _db.GetSprite("CoinGold");
                case PickupKind.CoinSilver: return _db.GetSprite("CoinSilver");
                case PickupKind.Gem: return _db.GetSprite("GemBlue");
                case PickupKind.Energy: return _db.GetSprite("PotionBlue");
                case PickupKind.Key: return _db.GetSprite("KeyIron");
                case PickupKind.Health: return _heart ??= GameUtil.MakeHeart();
                case PickupKind.Weapon: return _db.GetSprite(weapon != null ? "ChestWooden" : "GemPurple");
            }
            return _heart ??= GameUtil.MakeHeart();
        }

        private void Update()
        {
            if (_spawnGuard > 0f) _spawnGuard -= Time.deltaTime;
            _bob += Time.deltaTime * 4f;
            _sr.transform.localPosition = new Vector3(0f, Mathf.Sin(_bob) * 0.06f, 0f);

            var player = GameManager.I != null ? GameManager.I.Player : null;
            if (player == null || _spawnGuard > 0f) return;

            float d = Vector2.Distance(transform.position, player.transform.position);
            if (d < 2.4f)
                transform.position = Vector2.MoveTowards(transform.position, player.transform.position, (3f + (2.4f - d) * 6f) * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_spawnGuard > 0f) return;
            var pc = other.GetComponentInParent<PlayerController>();
            if (pc == null) return;
            Collect(pc);
        }

        private void Collect(PlayerController pc)
        {
            switch (_kind)
            {
                case PickupKind.CoinGold: GameManager.I.Run.AddCoins(_value); break;
                case PickupKind.CoinSilver: GameManager.I.Run.AddCoins(_value); break;
                case PickupKind.Gem: GameManager.I.Run.AddGems(_value); break;
                case PickupKind.Health: pc.Health.Heal(_value); break;
                case PickupKind.Energy: pc.Energy.Add(_value); break;
                case PickupKind.Key: GameManager.I.Run.AddKeys(_value); break;
                case PickupKind.Weapon: pc.Weapons.AddWeapon(_weapon, true); CombatService.I.PlaySfx(_db.sfxEquip); break;
            }
            CombatService.I.Pop(transform.position, _sr.color);
            Destroy(gameObject);
        }
    }
}
