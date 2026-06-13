using System.Collections.Generic;
using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// Persistent meta-progression (design doc 3.7 "double economy"): gems and unlocks
    /// survive across runs via PlayerPrefs. Coins/keys are per-run and live in RunState.
    /// </summary>
    public static class MetaProgress
    {
        private const string GemsKey = "pd_gems";
        private const string UnlockedHeroesKey = "pd_heroes";
        private const string BestFloorKey = "pd_bestfloor";

        public static int Gems
        {
            get => PlayerPrefs.GetInt(GemsKey, 0);
            set { PlayerPrefs.SetInt(GemsKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static int BestFloor
        {
            get => PlayerPrefs.GetInt(BestFloorKey, 0);
            set { if (value > BestFloor) { PlayerPrefs.SetInt(BestFloorKey, value); PlayerPrefs.Save(); } }
        }

        public static void AddGems(int n) => Gems += n;

        public static bool IsHeroUnlocked(string heroId)
        {
            // First two heroes are free; the rest must be unlocked with gems.
            if (heroId == "mage" || heroId == "ranger") return true;
            var set = GetUnlockedSet();
            return set.Contains(heroId);
        }

        public static bool TryUnlockHero(string heroId, int cost)
        {
            if (IsHeroUnlocked(heroId)) return true;
            if (Gems < cost) return false;
            Gems -= cost;
            var set = GetUnlockedSet();
            set.Add(heroId);
            PlayerPrefs.SetString(UnlockedHeroesKey, string.Join(",", set));
            PlayerPrefs.Save();
            return true;
        }

        private static HashSet<string> GetUnlockedSet()
        {
            var raw = PlayerPrefs.GetString(UnlockedHeroesKey, "");
            var set = new HashSet<string>();
            foreach (var s in raw.Split(',')) if (!string.IsNullOrEmpty(s)) set.Add(s);
            return set;
        }
    }
}
