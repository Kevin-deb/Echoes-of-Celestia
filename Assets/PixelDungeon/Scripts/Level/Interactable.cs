using System.Linq;
using UnityEngine;

namespace PixelDungeon
{
    /// <summary>Base for things the player activates with F (design doc 3.1).</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public virtual bool CanInteract => true;
        public virtual string Prompt => "Interact";
        public abstract void Interact(PlayerController pc);

        protected static GameDatabase Db => GameManager.I.Db;

        protected SpriteRenderer AddSprite(string spriteKey, int order = 15)
        {
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = Db.GetSprite(spriteKey);
            sr.sortingOrder = order;
            return sr;
        }

        protected void AddTrigger(float radius)
        {
            var col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = radius;
        }
    }

    /// <summary>Treasure chest: optionally key-gated, gives a new weapon or gold (design doc 3.6).</summary>
    public class Chest : Interactable
    {
        private bool _opened;
        private bool _requiresKey;
        private SpriteRenderer _sr;

        public override bool CanInteract => !_opened;
        public override string Prompt => _requiresKey && GameManager.I.Run.Keys < 1 ? "Need a key" : "Open chest";

        public static Chest Spawn(Vector2 pos, string spriteKey, bool requiresKey)
        {
            var go = new GameObject("Chest") { transform = { position = pos } };
            var c = go.AddComponent<Chest>();
            c._requiresKey = requiresKey;
            c._sr = c.AddSprite(spriteKey);
            c.AddTrigger(0.7f);
            return c;
        }

        public override void Interact(PlayerController pc)
        {
            if (_opened) return;
            if (_requiresKey && !GameManager.I.Run.SpendKey()) return;

            _opened = true;
            if (_sr != null) _sr.color = new Color(0.6f, 0.6f, 0.6f);

            // Give a weapon the player doesn't have yet, else gold.
            var owned = pc.Weapons.Weapons.Select(w => w.id).ToHashSet();
            var pool = GameContent.Weapons.Where(w => !owned.Contains(w.id)).ToList();
            if (pool.Count > 0)
                Pickup.Spawn(PickupKind.Weapon, transform.position + Vector3.up * 0.6f, 1, pool[Random.Range(0, pool.Count)]);
            else
                for (int i = 0; i < 8; i++) Pickup.Spawn(PickupKind.CoinGold, transform.position, 5);

            CombatService.I.Pop(transform.position, new Color(1f, 0.9f, 0.4f));
            CombatService.I.PlaySfx(Db.sfxEquip);
        }
    }

    /// <summary>Glowing portal to the next floor; appears once the boss room is cleared.</summary>
    public class Portal : Interactable
    {
        private SpriteRenderer _sr;
        private float _t;

        public override string Prompt => "Descend";

        public static Portal Spawn(Vector2 pos)
        {
            var go = new GameObject("Portal") { transform = { position = pos } };
            var p = go.AddComponent<Portal>();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GameUtil.MakeDot(20, new Color(0.7f, 0.4f, 1f));
            sr.sortingOrder = 12;
            sr.color = new Color(0.7f, 0.45f, 1f, 0.9f);
            p._sr = sr;
            p.AddTrigger(0.9f);
            return p;
        }

        private void Update()
        {
            _t += Time.deltaTime * 3f;
            float s = 1f + Mathf.Sin(_t) * 0.12f;
            transform.localScale = Vector3.one * s;
        }

        public override void Interact(PlayerController pc) => GameManager.I.NextFloor();
    }

    /// <summary>A purchasable item in the shop room (design doc 3.6).</summary>
    public class ShopItem : Interactable
    {
        public enum Kind { Weapon, Health, Energy, ArmorUp }
        private Kind _kind;
        private int _price;
        private WeaponDef _weapon;
        private bool _sold;
        private SpriteRenderer _sr;

        public override bool CanInteract => !_sold;
        public override string Prompt => _sold ? "Sold" :
            $"Buy {Label} ({_price}) {(GameManager.I.Run.Coins >= _price ? "" : "- need gold")}";

        private string Label => _kind switch
        {
            Kind.Weapon => _weapon?.name ?? "Weapon",
            Kind.Health => "Heal",
            Kind.Energy => "Energy",
            _ => "Armor"
        };

        public static ShopItem Spawn(Vector2 pos, Kind kind, int price, WeaponDef weapon = null)
        {
            var go = new GameObject("ShopItem") { transform = { position = pos } };
            var s = go.AddComponent<ShopItem>();
            s._kind = kind; s._price = price; s._weapon = weapon;
            string key = kind switch
            {
                Kind.Weapon => "ChestSilver",
                Kind.Health => "PotionRed",
                Kind.Energy => "PotionBlue",
                _ => "ChestGolden"
            };
            s._sr = s.AddSprite(key, 15);
            s.AddTrigger(0.7f);
            return s;
        }

        public override void Interact(PlayerController pc)
        {
            if (_sold || !GameManager.I.Run.SpendCoins(_price)) return;
            switch (_kind)
            {
                case Kind.Weapon: pc.Weapons.AddWeapon(_weapon, true); break;
                case Kind.Health: pc.Health.Heal(40f); break;
                case Kind.Energy: pc.Energy.Add(60f); break;
                case Kind.ArmorUp: pc.Health.AddArmor(25f); break;
            }
            _sold = true;
            if (_sr != null) _sr.color = new Color(0.5f, 0.5f, 0.5f);
            CombatService.I.Pop(transform.position, new Color(1f, 0.9f, 0.4f));
            CombatService.I.PlaySfx(Db.sfxEquip);
        }
    }

    /// <summary>Blood altar (design doc 7.5): sacrifice max HP for a permanent run damage boost.</summary>
    public class Altar : Interactable
    {
        private bool _used;
        public override bool CanInteract => !_used;
        public override string Prompt => "Sacrifice 20% HP for +25% damage";

        public static Altar Spawn(Vector2 pos)
        {
            var go = new GameObject("Altar") { transform = { position = pos } };
            var a = go.AddComponent<Altar>();
            a.AddSprite("Altar", 10);
            a.AddTrigger(0.8f);
            return a;
        }

        public override void Interact(PlayerController pc)
        {
            if (_used) return;
            _used = true;
            pc.Health.max *= 0.8f;
            pc.Health.current = Mathf.Min(pc.Health.current, pc.Health.max);
            pc.Weapons.DamageMultiplier *= 1.25f;
            CombatService.I.Burst(transform.position, new Color(0.7f, 0.1f, 0.1f), 14);
        }
    }
}
