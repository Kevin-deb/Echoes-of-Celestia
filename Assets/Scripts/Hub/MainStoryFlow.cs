using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The expanded main-story journey (Genshin-style quest chain). The four chronicle volumes are
/// interleaved with three playable trials that gate progression:
///
///   [Trial I  Sky Assault]  → Volume I → [Trial II  Silence the Sentinels] → Volume II
///   → [Trial III  Pixel Depths] → Volume III → Volume IV
///
/// Trial conditions persist via PlayerPrefs (so a player who already fulfilled one is told the
/// requirement is met), while volume reads keep the existing per-session reset behaviour of
/// MainStoryProgress. All texts are English. This class is pure logic/state — UI lives in
/// MainStoryQuestUI, world gating in LoreInteractable.
/// </summary>
public static class MainStoryFlow
{
    public enum StepKind { Volume, Task }

    public sealed class Step
    {
        public StepKind Kind;
        public string Id;
        public string NodeTop;          // progress-track label, first line
        public string NodeBottom;       // progress-track label, second line (volumes show ??? until read)
        public string ObjectiveTitle;   // HUD objective headline
        public string ObjectiveDetail;  // HUD objective body
        public int VolumeIndex = -1;    // index into MainStoryProgress.Chapters for Volume steps
    }

    public static readonly Step[] Steps =
    {
        new Step { Kind = StepKind.Task, Id = "planes", NodeTop = "Trial I", NodeBottom = "Sky Assault",
                   ObjectiveTitle = "Trial I — Sky Assault",
                   ObjectiveDetail = "Clear Level 1 and Level 2 of the plane shooter at the space station." },
        new Step { Kind = StepKind.Volume, Id = "vol1", NodeTop = "Volume I", VolumeIndex = 0,
                   ObjectiveTitle = "Recover Volume I — The Meridian Age",
                   ObjectiveDetail = "Follow the golden trail and read the chronicle." },
        new Step { Kind = StepKind.Task, Id = "sentinels", NodeTop = "Trial II", NodeBottom = "Silence the Sentinels",
                   ObjectiveTitle = "Trial II — Silence the Sentinels",
                   ObjectiveDetail = "Board a vehicle and destroy the two hostile sentinels with its weapon." },
        new Step { Kind = StepKind.Volume, Id = "vol2", NodeTop = "Volume II", VolumeIndex = 1,
                   ObjectiveTitle = "Recover Volume II — The Signal from Aetherion",
                   ObjectiveDetail = "Follow the golden trail and read the chronicle." },
        new Step { Kind = StepKind.Task, Id = "dungeon", NodeTop = "Trial III", NodeBottom = "Pixel Depths",
                   ObjectiveTitle = "Trial III — Pixel Depths",
                   ObjectiveDetail = "Enter Pixel Dungeon at the com-station and clear Floor 1." },
        new Step { Kind = StepKind.Volume, Id = "vol3", NodeTop = "Volume III", VolumeIndex = 2,
                   ObjectiveTitle = "Recover Volume III — The Great Fracture",
                   ObjectiveDetail = "Follow the golden trail and read the chronicle." },
        new Step { Kind = StepKind.Volume, Id = "vol4", NodeTop = "Volume IV", VolumeIndex = 3,
                   ObjectiveTitle = "Recover Volume IV — The Last Silence",
                   ObjectiveDetail = "Follow the golden trail and read the final chronicle." },
    };

    /// <summary>Raised whenever any condition that feeds the flow may have changed.</summary>
    public static event Action Changed;

    // ── PlayerPrefs keys (story-session scoped: wiped on every Play) ─────────
    const string PlaneClearPrefix = "ec_plane_clear_";    // + scene name → 1 when cleared (set by PlaneRecords)
    const string SentinelsPrefs   = "ec_story_sentinels"; // ';'-joined keys of destroyed sentinels
    const string DungeonPrefs     = "ec_story_dungeon_floor1"; // 1 when PD Floor 1 cleared this session

    /// <summary>
    /// Mirrors MainStoryProgress.ResetOnPlay: every entry into Play mode starts the main story
    /// from scratch — trial fulfilment included. Persistent game records (Pixel Dungeon best
    /// floor / gems, plane battle history) are NOT touched; only the story-trial flags are.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetOnPlay()
    {
        PlayerPrefs.DeleteKey(PlaneClearPrefix + "Level1");
        PlayerPrefs.DeleteKey(PlaneClearPrefix + "Level2");
        PlayerPrefs.DeleteKey(SentinelsPrefs);
        PlayerPrefs.DeleteKey(DungeonPrefs);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // ── Step completion ───────────────────────────────────────────────────────

    public static bool IsStepComplete(int index)
    {
        if (index < 0 || index >= Steps.Length) return false;
        var s = Steps[index];
        if (s.Kind == StepKind.Volume) return MainStoryProgress.IsRead(s.VolumeIndex);
        switch (s.Id)
        {
            case "planes":    return PlaneClearCount >= 2;
            case "sentinels": return SentinelsDownCount >= 2;
            case "dungeon":   return DungeonFloor1Cleared;
            default:          return false;
        }
    }

    /// <summary>First incomplete step, or Steps.Length when the journey is finished.</summary>
    public static int CurrentStepIndex
    {
        get
        {
            for (int i = 0; i < Steps.Length; i++)
                if (!IsStepComplete(i)) return i;
            return Steps.Length;
        }
    }

    public static int TotalSteps => Steps.Length;
    public static bool JourneyComplete => CurrentStepIndex >= Steps.Length;

    public static Step Current
    {
        get
        {
            var i = CurrentStepIndex;
            return i < Steps.Length ? Steps[i] : null;
        }
    }

    /// <summary>Live progress suffix for task steps, e.g. " (1/2)". Empty for volumes.</summary>
    public static string ProgressSuffix(int index)
    {
        if (index < 0 || index >= Steps.Length) return "";
        var s = Steps[index];
        if (s.Kind != StepKind.Task) return "";
        switch (s.Id)
        {
            case "planes":    return $"  ({PlaneClearCount}/2)";
            case "sentinels": return $"  ({SentinelsDownCount}/2)";
            case "dungeon":   return DungeonFloor1Cleared ? "  (done)" : "";
            default:          return "";
        }
    }

    // ── Trial: plane shooter ─────────────────────────────────────────────────

    public static bool IsPlaneLevelCleared(string sceneName) =>
        PlayerPrefs.GetInt(PlaneClearPrefix + sceneName, 0) == 1;

    public static int PlaneClearCount =>
        (IsPlaneLevelCleared("Level1") ? 1 : 0) + (IsPlaneLevelCleared("Level2") ? 1 : 0);

    /// <summary>Called by PlaneRecords when a level is won.</summary>
    public static void NotifyPlaneLevelCleared(string sceneName)
    {
        PlayerPrefs.SetInt(PlaneClearPrefix + sceneName, 1);
        PlayerPrefs.Save();
        RaiseChanged();
    }

    // ── Trial: Hub sentinels ─────────────────────────────────────────────────

    public static int SentinelsDownCount
    {
        get
        {
            var raw = PlayerPrefs.GetString(SentinelsPrefs, "");
            return string.IsNullOrEmpty(raw) ? 0 : raw.Split(';').Count(k => !string.IsNullOrEmpty(k));
        }
    }

    /// <summary>Called by PrimaryEnemy.Scrap with a stable per-enemy key.</summary>
    public static void NotifySentinelDown(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var raw = PlayerPrefs.GetString(SentinelsPrefs, "");
        var set = new HashSet<string>(raw.Split(';').Where(k => !string.IsNullOrEmpty(k)));
        if (!set.Add(key)) { RaiseChanged(); return; }
        PlayerPrefs.SetString(SentinelsPrefs, string.Join(";", set));
        PlayerPrefs.Save();
        RaiseChanged();
    }

    // ── Trial: Pixel Dungeon ─────────────────────────────────────────────────

    /// <summary>Floor 1 cleared during THIS story session (the mini-game's own persistent
    /// best-floor record is intentionally not consulted, so each Play starts fresh).</summary>
    public static bool DungeonFloor1Cleared => PlayerPrefs.GetInt(DungeonPrefs, 0) == 1;

    /// <summary>Called by the Pixel Dungeon GameManager when Floor 1 is cleared.</summary>
    public static void NotifyDungeonFloor1Cleared()
    {
        PlayerPrefs.SetInt(DungeonPrefs, 1);
        PlayerPrefs.Save();
        RaiseChanged();
    }

    // ── Chapter gating (used by LoreInteractable) ────────────────────────────

    public enum ChapterGate { NotMainStory, AlreadyRead, ReadableNow, NeedStoryMode, LockedByOrder }

    public static ChapterGate GetChapterGate(string title, out string lockReason)
    {
        lockReason = "";
        var volIdx = MainStoryProgress.IndexOfTitle(title);
        if (volIdx < 0) return ChapterGate.NotMainStory;
        if (MainStoryProgress.IsRead(volIdx)) return ChapterGate.AlreadyRead;

        if (!MainStoryQuestUI.StoryModeActive) return ChapterGate.NeedStoryMode;

        int stepIdx = StepIndexOfVolume(volIdx);
        int current = CurrentStepIndex;
        if (stepIdx == current) return ChapterGate.ReadableNow;

        var cur = Current;
        lockReason = cur != null ? cur.ObjectiveTitle + ProgressSuffix(current) : "";
        return ChapterGate.LockedByOrder;
    }

    public static int StepIndexOfVolume(int volumeIndex)
    {
        for (int i = 0; i < Steps.Length; i++)
            if (Steps[i].Kind == StepKind.Volume && Steps[i].VolumeIndex == volumeIndex) return i;
        return -1;
    }

    public static void RaiseChanged() => Changed?.Invoke();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ForwardProgressChanges()
    {
        MainStoryProgress.Changed -= RaiseChanged;
        MainStoryProgress.Changed += RaiseChanged;
    }
}
