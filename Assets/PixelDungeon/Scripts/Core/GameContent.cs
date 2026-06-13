using System.Collections.Generic;
using System.Linq;

namespace PixelDungeon
{
    // ---- Data records (tuning lives here so it can change without re-running the editor generator) ----

    public class WeaponDef
    {
        public string id, name, charWeapon;
        public WeaponKind kind;
        public Element element = Element.Physical;
        public float damage = 10, fireRate = 2f, projSpeed = 12f, spread = 0f, knockback = 5f, range = 1.6f;
        public float energyCost = 0f, critChance = 0.05f;
        public int projectiles = 1, pierce = 0;
        public bool thrust = false;           // melee: Jab instead of Slash
        public string projSpriteKey = null;   // null => procedural dot tinted by element
        public float meleeArcDeg = 90f, meleeRange = 1.6f;

        public string AnimTrigger => kind switch
        {
            WeaponKind.Melee => thrust ? "Jab" : "Slash",
            WeaponKind.Bow => "Shot",
            _ => "Cast",
        };
        public bool IsRanged => kind != WeaponKind.Melee;
    }

    public class EnemyDef
    {
        public string id, name, monsterKey;
        public EnemyBehavior behavior = EnemyBehavior.Charger;
        public float maxHp = 20, moveSpeed = 2.5f, contactDamage = 8f;
        public float attackRange = 6f, attackCooldown = 1.6f, projSpeed = 7f, projDamage = 8f;
        public Element element = Element.Physical;
        public int coinDrop = 2;
        public float scale = 1f;
        public string splitInto = null;       // splitter: enemy id spawned on death
        public int splitCount = 2;
        public int tier = 1;

        public EnemyDef Clone() => (EnemyDef)MemberwiseClone();
    }

    public class HeroDef
    {
        public string id, name;
        public string body, head, eyes, ears, hair = "", armor = "", helmet = "", mouth = "", shield = "";
        public string startWeapon;
        public float maxHp = 100, maxEnergy = 100, moveSpeed = 5f;
        public int unlockCost = 0;
        public string passive = "", skill = "";
    }

    /// <summary>All gameplay content, defined in code. See design doc sections 3.2, 3.3, 3.5.</summary>
    public static class GameContent
    {
        public static readonly WeaponDef[] Weapons =
        {
            // --- Melee (no energy; fan-shaped overlap on the Slash/Jab frame) ---
            new() { id="ironsword", name="Iron Sword", charWeapon="IronSword", kind=WeaponKind.Melee, damage=22, fireRate=2.4f, knockback=7, meleeArcDeg=95, meleeRange=1.7f },
            new() { id="katana", name="Katana", charWeapon="Katana", kind=WeaponKind.Melee, damage=15, fireRate=3.8f, knockback=4, critChance=0.12f, meleeArcDeg=75, meleeRange=1.6f },
            new() { id="battleaxe", name="Battle Axe", charWeapon="BatlleAxe", kind=WeaponKind.Melee, damage=42, fireRate=1.2f, knockback=11, meleeArcDeg=120, meleeRange=1.8f },
            new() { id="greathammer", name="Great Hammer", charWeapon="GreatHammer", kind=WeaponKind.Melee, damage=58, fireRate=0.85f, knockback=18, meleeArcDeg=140, meleeRange=1.9f },
            new() { id="spear", name="Gold Spear", charWeapon="GoldSpear", kind=WeaponKind.Melee, damage=26, fireRate=2.1f, knockback=8, thrust=true, meleeArcDeg=45, meleeRange=2.5f },
            new() { id="knife", name="Knife", charWeapon="Knife", kind=WeaponKind.Melee, damage=11, fireRate=5.5f, knockback=2, critChance=0.15f, meleeArcDeg=60, meleeRange=1.3f },

            // --- Bows (physical arrows, blocked by walls) ---
            new() { id="bow", name="Bow", charWeapon="Bow", kind=WeaponKind.Bow, damage=18, fireRate=2.6f, projSpeed=15, knockback=5, range=2.2f, projSpriteKey="__arrow" },
            new() { id="longbow", name="Long Bow", charWeapon="LongBow", kind=WeaponKind.Bow, damage=32, fireRate=1.7f, projSpeed=19, pierce=1, knockback=7, range=2.5f, projSpriteKey="__arrow" },
            new() { id="rapidbow", name="Rapid Bow", charWeapon="Bow", kind=WeaponKind.Bow, damage=9, fireRate=7.5f, projSpeed=17, spread=7, knockback=2, range=2.0f, energyCost=2, projSpriteKey="__arrow" },

            // --- Wands/Staves (cost energy; elemental) ---
            new() { id="firewand", name="Fire Wand", charWeapon="MagicWand", kind=WeaponKind.Wand, element=Element.Fire, damage=14, fireRate=3.2f, projSpeed=13, knockback=4, range=2.0f, energyCost=6 },
            new() { id="icewand", name="Frost Wand", charWeapon="GreenWand", kind=WeaponKind.Wand, element=Element.Ice, damage=12, fireRate=2.8f, projSpeed=12, knockback=3, range=2.0f, energyCost=6 },
            new() { id="stormstaff", name="Storm Staff", charWeapon="ArchStaff", kind=WeaponKind.Staff, element=Element.Lightning, damage=20, fireRate=2.0f, projSpeed=16, pierce=2, knockback=4, range=2.2f, energyCost=10 },
            new() { id="naturewand", name="Nature Wand", charWeapon="NatureWand", kind=WeaponKind.Wand, element=Element.Poison, damage=10, fireRate=3.0f, projSpeed=12, knockback=3, range=2.0f, energyCost=5 },
            new() { id="elderstaff", name="Elder Staff", charWeapon="ElderStaff", kind=WeaponKind.Staff, element=Element.Ice, damage=34, fireRate=1.3f, projSpeed=11, pierce=1, knockback=8, range=2.4f, energyCost=14 },
            // --- Cannon-style: flame spread (design doc: 火焰法杖喷一片) ---
            new() { id="flamestaff", name="Flame Staff", charWeapon="FlameStaff", kind=WeaponKind.Cannon, element=Element.Fire, damage=9, fireRate=2.4f, projSpeed=10, projectiles=5, spread=46, knockback=3, range=1.0f, energyCost=12 },
        };

        public static readonly EnemyDef[] Enemies =
        {
            // T1 fodder
            new() { id="rat", name="Rat", monsterKey="Rat", behavior=EnemyBehavior.Charger, maxHp=14, moveSpeed=3.2f, contactDamage=6, coinDrop=1, tier=1, scale=0.9f },
            new() { id="bat", name="Bat", monsterKey="Bat", behavior=EnemyBehavior.Charger, maxHp=10, moveSpeed=3.8f, contactDamage=6, coinDrop=1, tier=1, scale=0.9f },
            new() { id="spider", name="Spider", monsterKey="Spider", behavior=EnemyBehavior.Charger, maxHp=16, moveSpeed=3.0f, contactDamage=7, coinDrop=1, tier=1 },
            new() { id="wolf", name="Wolf", monsterKey="Wolf", behavior=EnemyBehavior.Charger, maxHp=26, moveSpeed=3.6f, contactDamage=10, coinDrop=2, tier=1 },
            new() { id="boar", name="Boar", monsterKey="Boar", behavior=EnemyBehavior.Bruiser, maxHp=40, moveSpeed=2.6f, contactDamage=12, attackRange=2f, coinDrop=3, tier=1, scale=1.1f },
            new() { id="bee", name="Bee", monsterKey="Bee", behavior=EnemyBehavior.Strafer, maxHp=9, moveSpeed=3.2f, contactDamage=5, attackRange=6, attackCooldown=1.8f, projSpeed=6, projDamage=5, coinDrop=1, tier=1, scale=0.8f },
            new() { id="scorpion", name="Scorpion", monsterKey="Scorpion", behavior=EnemyBehavior.Charger, maxHp=22, moveSpeed=2.8f, contactDamage=9, coinDrop=2, tier=1 },

            // T2 elites
            new() { id="mushroom", name="Mushroom", monsterKey="Mushroom", behavior=EnemyBehavior.Strafer, maxHp=34, moveSpeed=2.0f, contactDamage=8, attackRange=7, attackCooldown=2.0f, projSpeed=6, projDamage=9, element=Element.Poison, coinDrop=4, tier=2 },
            new() { id="snake", name="Snake", monsterKey="Snake", behavior=EnemyBehavior.Strafer, maxHp=30, moveSpeed=2.6f, contactDamage=9, attackRange=6, attackCooldown=1.6f, projSpeed=8, projDamage=8, element=Element.Poison, coinDrop=4, tier=2 },
            new() { id="helldog", name="Hell Dog", monsterKey="HellDog", behavior=EnemyBehavior.Bruiser, maxHp=55, moveSpeed=3.4f, contactDamage=14, attackRange=2.4f, coinDrop=5, tier=2, scale=1.05f },
            new() { id="spirit", name="Spirit", monsterKey="Spirit", behavior=EnemyBehavior.Strafer, maxHp=28, moveSpeed=2.8f, contactDamage=8, attackRange=8, attackCooldown=1.5f, projSpeed=7, projDamage=10, element=Element.Lightning, coinDrop=5, tier=2 },
            new() { id="salamandra", name="Salamandra", monsterKey="Salamandra", behavior=EnemyBehavior.Strafer, maxHp=46, moveSpeed=2.4f, contactDamage=11, attackRange=6.5f, attackCooldown=1.8f, projSpeed=7, projDamage=12, element=Element.Fire, coinDrop=6, tier=2, scale=1.05f },
            new() { id="jelly", name="Jelly", monsterKey="Jelly", behavior=EnemyBehavior.Splitter, maxHp=40, moveSpeed=1.8f, contactDamage=9, coinDrop=4, tier=2, splitInto="jellysmall", splitCount=3, scale=1.1f },
            new() { id="jellysmall", name="Jelly", monsterKey="Jelly", behavior=EnemyBehavior.Charger, maxHp=10, moveSpeed=2.6f, contactDamage=5, coinDrop=1, tier=2, scale=0.6f },
            new() { id="flyingeye", name="Flying Eye", monsterKey="FlyingEye", behavior=EnemyBehavior.Strafer, maxHp=36, moveSpeed=3.0f, contactDamage=9, attackRange=8, attackCooldown=1.2f, projSpeed=8, projDamage=9, element=Element.Lightning, coinDrop=6, tier=2 },
            // Ambusher (disguised as a chest, tier 0 so it never appears in normal waves)
            new() { id="mimic", name="Mimic", monsterKey="Mimic", behavior=EnemyBehavior.Charger, maxHp=70, moveSpeed=3.2f, contactDamage=14, coinDrop=10, tier=0, scale=1f },

            // T3 bosses
            new() { id="beholder", name="Beholder", monsterKey="Beholder", behavior=EnemyBehavior.Boss, maxHp=420, moveSpeed=2.2f, contactDamage=16, attackRange=9, attackCooldown=1.1f, projSpeed=8, projDamage=12, element=Element.Lightning, coinDrop=40, tier=3, scale=1.5f },
            new() { id="demon", name="Demon", monsterKey="Demon", behavior=EnemyBehavior.Boss, maxHp=520, moveSpeed=2.6f, contactDamage=20, attackRange=8, attackCooldown=1.3f, projSpeed=9, projDamage=14, element=Element.Fire, coinDrop=50, tier=3, scale=1.6f },
            new() { id="earthgolem", name="Earth Golem", monsterKey="EarthGolem", behavior=EnemyBehavior.Boss, maxHp=620, moveSpeed=1.8f, contactDamage=24, attackRange=3, attackCooldown=1.8f, coinDrop=55, tier=3, scale=1.7f },
            new() { id="flamegolem", name="Flame Golem", monsterKey="FlameGolem", behavior=EnemyBehavior.Boss, maxHp=560, moveSpeed=2.0f, contactDamage=22, attackRange=7, attackCooldown=1.4f, projSpeed=8, projDamage=13, element=Element.Fire, coinDrop=55, tier=3, scale=1.7f },
            new() { id="icegolem", name="Ice Golem", monsterKey="IceGolem", behavior=EnemyBehavior.Boss, maxHp=600, moveSpeed=1.9f, contactDamage=22, attackRange=7, attackCooldown=1.5f, projSpeed=8, projDamage=13, element=Element.Ice, coinDrop=55, tier=3, scale=1.7f },
        };

        public static readonly HeroDef[] Heroes =
        {
            new() { id="mage", name="Mage", body="Elf1", head="Elf1", eyes="Elf1", ears="Elf1", hair="Hair2", armor="Wizard", startWeapon="firewand",
                    maxHp=80, maxEnergy=130, moveSpeed=5.0f, unlockCost=0, passive="+30% max energy", skill="Nova blast" },
            new() { id="ranger", name="Ranger", body="DarkElf", head="DarkElf1", eyes="DarkElf1", ears="DarkElf1", hair="Hair5", armor="Archer", startWeapon="rapidbow",
                    maxHp=90, maxEnergy=90, moveSpeed=5.4f, unlockCost=0, passive="+15% crit", skill="Triple shot" },
            new() { id="knight", name="Knight", body="Human1", head="Human1", eyes="Human1", ears="Human1", hair="Hair1", armor="Knight", shield="Shield2", startWeapon="ironsword",
                    maxHp=135, maxEnergy=60, moveSpeed=4.6f, unlockCost=12, passive="Starts with armor", skill="Shield dash" },
            new() { id="berserker", name="Berserker", body="Orc1", head="Orc1", eyes="Orc1", ears="Orc1", mouth="Orc1", armor="Viking", startWeapon="battleaxe",
                    maxHp=120, maxEnergy=50, moveSpeed=5.0f, unlockCost=15, passive="Low HP = attack speed", skill="Whirlwind" },
            new() { id="necromancer", name="Necromancer", body="Zombie1", head="Zombie1", eyes="Zombie1", ears="Zombie1", armor="Necromant", hair="", startWeapon="naturewand",
                    maxHp=85, maxEnergy=110, moveSpeed=4.8f, unlockCost=20, passive="Poison mastery", skill="Summon" },
        };

        public static WeaponDef Weapon(string id) => Weapons.FirstOrDefault(w => w.id == id) ?? Weapons[0];
        public static EnemyDef Enemy(string id) => Enemies.FirstOrDefault(e => e.id == id);
        public static HeroDef Hero(string id) => Heroes.FirstOrDefault(h => h.id == id) ?? Heroes[0];

        public static List<EnemyDef> EnemiesOfTier(int tier) => Enemies.Where(e => e.tier == tier && e.behavior != EnemyBehavior.Boss && e.id != "jellysmall").ToList();
        public static List<EnemyDef> Bosses() => Enemies.Where(e => e.behavior == EnemyBehavior.Boss).ToList();
    }
}
