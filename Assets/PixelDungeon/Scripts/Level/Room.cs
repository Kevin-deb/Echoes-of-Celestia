using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelDungeon
{
    /// <summary>A lockable wall segment placed in a doorway during combat (design doc 3.6).</summary>
    public class Door : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Collider2D _col;

        public static Door Create(Vector2Int cell, Sprite wallSprite, Transform parent)
        {
            var go = new GameObject("Door");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = wallSprite;
            sr.sortingOrder = -8;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.98f, 0.98f);

            go.AddComponent<WallMarker>();

            var d = go.AddComponent<Door>();
            d._sr = sr; d._col = col;
            d.SetLocked(false);
            return d;
        }

        public void SetLocked(bool locked)
        {
            _col.enabled = locked;
            _sr.enabled = locked;
        }
    }

    /// <summary>
    /// A generated room (design doc 3.6). Detects player entry, locks its doors and runs enemy waves
    /// for combat/boss rooms, and is populated with content according to its type.
    /// </summary>
    public class Room : MonoBehaviour
    {
        public RectInt Interior;
        public RoomType Type;
        public int FloorIndex;
        public readonly List<Door> Doors = new();

        private GameDatabase _db;
        private bool _entered, _cleared;
        private int _alive;

        public Vector2 Center => Interior.center;
        public bool Cleared => _cleared;

        public void Setup(RectInt interior, RoomType type, int floorIndex)
        {
            Interior = interior;
            Type = type;
            FloorIndex = floorIndex;
            _db = GameManager.I.Db;

            var col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.offset = Interior.center;
            col.size = new Vector2(Interior.width - 2f, Interior.height - 2f);

            _cleared = type is RoomType.Start or RoomType.Shop or RoomType.Treasure;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_entered) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            _entered = true;
            GameManager.I.OnRoomEntered(this);

            if (Type is RoomType.Combat or RoomType.Boss && !_cleared)
                StartCoroutine(RunCombat());
        }

        // ---------- Combat ----------

        private IEnumerator RunCombat()
        {
            yield return new WaitForSeconds(0.25f);
            Lock();

            if (Type == RoomType.Boss)
            {
                var bosses = GameContent.Bosses();
                var boss = bosses[FloorIndex % bosses.Count];
                GameManager.I.AnnounceBoss(boss.name);
                SpawnEnemy(boss.id, Center + Vector2.up * 1.5f);
                yield return new WaitUntil(() => _alive <= 0);
            }
            else
            {
                int waves = 1 + (FloorIndex >= 1 ? 1 : 0);
                var pool = EnemyPool();
                for (int w = 0; w < waves; w++)
                {
                    int count = Mathf.Clamp(3 + FloorIndex + Random.Range(0, 3), 3, 9);
                    for (int i = 0; i < count; i++)
                        SpawnEnemy(pool[Random.Range(0, pool.Count)].id, RandomInteriorPoint());
                    yield return new WaitForSeconds(0.3f);
                    yield return new WaitUntil(() => _alive <= 0);
                    yield return new WaitForSeconds(0.4f);
                }
            }

            _cleared = true;
            Unlock();

            if (Type == RoomType.Boss)
            {
                Portal.Spawn(Center);
                GameManager.I.OnBossDefeated();
            }
        }

        public Enemy SpawnEnemy(string id, Vector2 pos)
        {
            var baseDef = GameContent.Enemy(id);
            if (baseDef == null) return null;

            var def = baseDef.Clone();
            def.maxHp *= 1f + 0.22f * FloorIndex;
            def.contactDamage *= 1f + 0.15f * FloorIndex;
            def.projDamage *= 1f + 0.15f * FloorIndex;

            var prefab = _db.GetMonster(def.monsterKey);
            GameObject go = prefab != null ? Instantiate(prefab) : new GameObject("Enemy_" + id);
            go.transform.position = pos;

            var e = go.AddComponent<Enemy>();
            e.Init(def, this);
            _alive++;
            return e;
        }

        public void NotifyEnemyDied(Enemy e)
        {
            _alive = Mathf.Max(0, _alive - 1);
        }

        private List<EnemyDef> EnemyPool()
        {
            if (FloorIndex <= 0) return GameContent.EnemiesOfTier(1);
            if (FloorIndex == 1)
            {
                var l = GameContent.EnemiesOfTier(1);
                l.AddRange(GameContent.EnemiesOfTier(2));
                return l;
            }
            return GameContent.EnemiesOfTier(2);
        }

        private Vector2 RandomInteriorPoint(float margin = 1.5f)
        {
            float x = Random.Range(Interior.xMin + margin, Interior.xMax - margin);
            float y = Random.Range(Interior.yMin + margin, Interior.yMax - margin);
            return new Vector2(x, y);
        }

        private void Lock() { foreach (var d in Doors) d.SetLocked(true); }
        private void Unlock() { foreach (var d in Doors) d.SetLocked(false); }

        // ---------- Population ----------

        public void Populate()
        {
            switch (Type)
            {
                case RoomType.Shop: PopulateShop(); break;
                case RoomType.Treasure: PopulateTreasure(); break;
                case RoomType.Start: break;
                default: ScatterProps(Random.Range(2, 5)); break; // Combat / Boss filler
            }
        }

        private void PopulateShop()
        {
            float y = Interior.center.y;
            float left = Interior.center.x - 3f;
            var owned = new HashSet<string>();
            var wpool = GameContent.Weapons.Where(w => w.id != "knife").ToList();
            var weapon = wpool[Random.Range(0, wpool.Count)];

            ShopItem.Spawn(new Vector2(left, y), ShopItem.Kind.Weapon, 30 + FloorIndex * 10, weapon);
            ShopItem.Spawn(new Vector2(left + 3f, y), ShopItem.Kind.Health, 18);
            ShopItem.Spawn(new Vector2(left + 6f, y), ShopItem.Kind.Energy, 15);
            if (Random.value < 0.6f) Altar.Spawn(new Vector2(Interior.center.x, Interior.yMax - 2f));
        }

        private void PopulateTreasure()
        {
            Chest.Spawn(new Vector2(Interior.center.x - 1.5f, Interior.center.y), "ChestWooden", false);
            if (Random.value < 0.6f)
                Chest.Spawn(new Vector2(Interior.center.x + 1.5f, Interior.center.y), "ChestGolden", true);
            if (Random.value < 0.4f)
                MimicChest.Spawn(new Vector2(Interior.center.x, Interior.center.y - 2f), this);
            ScatterProps(2);
        }

        private void ScatterProps(int count)
        {
            string[] kinds = { "Barrel", "BoxSmall", "Vase" };
            for (int i = 0; i < count; i++)
            {
                var p = RandomInteriorPoint(2f);
                if (Vector2.Distance(p, Center) < 1.5f) continue;
                if (Random.value < 0.15f)
                    Destructible.Spawn(p, "Explosives", 10f, true);
                else
                    Destructible.Spawn(p, kinds[Random.Range(0, kinds.Length)], 8f, false);
            }
            if (Type == RoomType.Combat && Random.value < 0.3f)
                Trap.SpawnSpike(RandomInteriorPoint(2.5f));
        }
    }
}
