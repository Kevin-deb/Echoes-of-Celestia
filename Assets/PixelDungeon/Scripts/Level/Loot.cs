using UnityEngine;

namespace PixelDungeon
{
    /// <summary>Rolls and scatters drops when enemies die or destructibles break (design doc 3.7).</summary>
    public static class Loot
    {
        public static void DropFromEnemy(EnemyDef def, Vector2 pos)
        {
            int coins = def.coinDrop;
            for (int i = 0; i < coins; i++)
            {
                var kind = Random.value < 0.25f ? PickupKind.CoinGold : PickupKind.CoinSilver;
                Pickup.Spawn(kind, pos + Random.insideUnitCircle * 0.4f, kind == PickupKind.CoinGold ? 5 : 1);
            }
            if (Random.value < 0.10f) Pickup.Spawn(PickupKind.Health, pos + Random.insideUnitCircle * 0.3f, 12);
            if (Random.value < 0.14f) Pickup.Spawn(PickupKind.Energy, pos + Random.insideUnitCircle * 0.3f, 25);
            if (def.tier >= 2 && Random.value < 0.18f) Pickup.Spawn(PickupKind.Gem, pos + Random.insideUnitCircle * 0.3f, 1);
            if (Random.value < 0.05f) Pickup.Spawn(PickupKind.Key, pos + Random.insideUnitCircle * 0.3f, 1);
        }

        public static void DropFromDestructible(Vector2 pos)
        {
            float r = Random.value;
            if (r < 0.45f) Pickup.Spawn(Random.value < 0.2f ? PickupKind.CoinGold : PickupKind.CoinSilver, pos, 1);
            else if (r < 0.55f) Pickup.Spawn(PickupKind.Health, pos, 8);
            else if (r < 0.62f) Pickup.Spawn(PickupKind.Energy, pos, 18);
        }

        public static void DropBossReward(Vector2 pos)
        {
            for (int i = 0; i < 3; i++) Pickup.Spawn(PickupKind.Gem, pos + Random.insideUnitCircle * 0.8f, 1);
            for (int i = 0; i < 6; i++) Pickup.Spawn(PickupKind.CoinGold, pos + Random.insideUnitCircle * 1.2f, 5);
            Pickup.Spawn(PickupKind.Health, pos + Vector2.up * 0.5f, 40);
        }
    }
}
