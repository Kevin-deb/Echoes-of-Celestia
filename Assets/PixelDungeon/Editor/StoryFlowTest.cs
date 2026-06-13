#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelDungeon.EditorTools
{
    /// <summary>
    /// Headless logic test for the expanded Main Story flow (no play mode needed).
    /// Snapshots the relevant PlayerPrefs, walks the whole 7-step journey by simulating each
    /// condition, asserts the step index after every transition, then restores the prefs.
    /// Run with: -executeMethod PixelDungeon.EditorTools.StoryFlowTest.Run
    /// </summary>
    public static class StoryFlowTest
    {
        static readonly string[] IntKeys =
        {
            "ec_mainstory_read_0", "ec_mainstory_read_1", "ec_mainstory_read_2", "ec_mainstory_read_3",
            "ec_plane_clear_Level1", "ec_plane_clear_Level2", "ec_story_dungeon_floor1",
        };
        const string StrKey = "ec_story_sentinels";

        static int _fails;

        public static void Run()
        {
            // ---- snapshot & clear ----
            var intSnap = new Dictionary<string, int?>();
            foreach (var k in IntKeys)
            {
                intSnap[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : (int?)null;
                PlayerPrefs.DeleteKey(k);
            }
            string strSnap = PlayerPrefs.HasKey(StrKey) ? PlayerPrefs.GetString(StrKey) : null;
            PlayerPrefs.DeleteKey(StrKey);

            try
            {
                Walk();
            }
            finally
            {
                // ---- restore ----
                foreach (var kv in intSnap)
                {
                    if (kv.Value.HasValue) PlayerPrefs.SetInt(kv.Key, kv.Value.Value);
                    else PlayerPrefs.DeleteKey(kv.Key);
                }
                if (strSnap != null) PlayerPrefs.SetString(StrKey, strSnap);
                else PlayerPrefs.DeleteKey(StrKey);
                PlayerPrefs.Save();
            }

            Debug.Log($"=== STORY FLOW TEST RESULT: {(_fails == 0 ? "PASS" : "FAIL")} — fails={_fails} ===");
            EditorApplication.Exit(_fails == 0 ? 0 : 7);
        }

        static void Walk()
        {
            Check(MainStoryFlow.CurrentStepIndex == 0, "journey starts at Trial I (Sky Assault)");
            Check(!MainStoryFlow.IsStepComplete(0), "Trial I incomplete at start");

            MainStoryFlow.NotifyPlaneLevelCleared("Level1");
            Check(MainStoryFlow.CurrentStepIndex == 0, "one plane level is not enough");
            Check(MainStoryFlow.ProgressSuffix(0).Contains("1/2"), "plane progress shows 1/2");

            MainStoryFlow.NotifyPlaneLevelCleared("Level2");
            Check(MainStoryFlow.CurrentStepIndex == 1, "both plane levels cleared -> Volume I step");

            var gate = MainStoryFlow.GetChapterGate("The Meridian Age", out _);
            Check(gate == MainStoryFlow.ChapterGate.NeedStoryMode,
                $"unread Volume I requires story mode (got {gate})");

            MainStoryProgress.MarkReadByTitle("The Meridian Age");
            Check(MainStoryFlow.CurrentStepIndex == 2, "Volume I read -> Trial II (Sentinels)");
            Check(MainStoryFlow.GetChapterGate("The Meridian Age", out _) == MainStoryFlow.ChapterGate.AlreadyRead,
                "read chapters become freely re-readable");

            MainStoryFlow.NotifySentinelDown("sentinelA");
            Check(MainStoryFlow.CurrentStepIndex == 2, "one sentinel is not enough");
            MainStoryFlow.NotifySentinelDown("sentinelA");
            Check(MainStoryFlow.SentinelsDownCount == 1, "duplicate sentinel key ignored");
            MainStoryFlow.NotifySentinelDown("sentinelB");
            Check(MainStoryFlow.CurrentStepIndex == 3, "both sentinels down -> Volume II step");

            MainStoryProgress.MarkReadByTitle("The Signal from Aetherion");
            Check(MainStoryFlow.CurrentStepIndex == 4, "Volume II read -> Trial III (Pixel Depths)");
            Check(!MainStoryFlow.DungeonFloor1Cleared, "dungeon trial incomplete before floor clear");

            MainStoryFlow.NotifyDungeonFloor1Cleared();   // = Pixel Dungeon reports Floor 1 cleared
            Check(MainStoryFlow.DungeonFloor1Cleared, "floor-1 notification counts as cleared");
            Check(MainStoryFlow.CurrentStepIndex == 5, "dungeon trial met -> Volume III step");

            MainStoryProgress.MarkReadByTitle("The Great Fracture");
            Check(MainStoryFlow.CurrentStepIndex == 6, "Volume III read -> Volume IV step");
            MainStoryProgress.MarkReadByTitle("The Last Silence");
            Check(MainStoryFlow.CurrentStepIndex == 7 && MainStoryFlow.JourneyComplete, "journey complete");
        }

        static void Check(bool condition, string message)
        {
            if (condition) Debug.Log("[StoryFlowTest] ok: " + message);
            else { _fails++; Debug.LogError("[StoryFlowTest] FAIL: " + message); }
        }
    }
}
#endif
