namespace PixelDungeon
{
    /// <summary>Damage element. Drives on-hit status reactions.</summary>
    public enum Element { Physical, Fire, Ice, Lightning, Poison }

    /// <summary>Weapon family. Decides which character attack animation plays and the firing style.</summary>
    public enum WeaponKind { Melee, Bow, Wand, Staff, Cannon }

    /// <summary>Who fired a projectile / who an attack can hurt.</summary>
    public enum Team { Player, Enemy }

    /// <summary>High-level enemy AI archetype (see design doc 3.5).</summary>
    public enum EnemyBehavior { Charger, Strafer, Bruiser, Splitter, Mimic, Boss }

    /// <summary>Role of a generated room in the floor.</summary>
    public enum RoomType { Start, Combat, Shop, Treasure, Boss }

    /// <summary>What a dropped/placed pickup gives the player.</summary>
    public enum PickupKind { CoinSilver, CoinGold, Gem, Health, Energy, Key, Weapon }

    /// <summary>
    /// Quantized facing used by the 4-direction PixelHeroes4D animator.
    /// Values match the animator's "Direction" int parameter (0=front/down, 1=side, 2=back/up).
    /// </summary>
    public enum Facing { Down = 0, Side = 1, Up = 2 }
}
