using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks main-story chapter discovery progress for the Space Age chapter.
/// Chapters are defined in canonical reading order and matched to LoreInteractable
/// entries by title. Progress persists via PlayerPrefs.
/// </summary>
public static class MainStoryProgress
{
    public sealed class Chapter
    {
        public string Volume;
        public string Title;
    }

    // Canonical order of the four main-chronicle volumes.
    public static readonly Chapter[] Chapters =
    {
        new Chapter { Volume = "Volume I",   Title = "The Meridian Age" },
        new Chapter { Volume = "Volume II",  Title = "The Signal from Aetherion" },
        new Chapter { Volume = "Volume III", Title = "The Great Fracture" },
        new Chapter { Volume = "Volume IV",  Title = "The Last Silence" },
    };

    const string PrefsPrefix = "ec_mainstory_read_";

    static readonly HashSet<int> _read = new HashSet<int>();
    static bool _loaded;

    /// <summary>Raised whenever a chapter is newly marked as read.</summary>
    public static event Action Changed;

    /// <summary>
    /// Resets main-story progress on every entry to Play mode, so each session
    /// starts with no chapters recovered.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetOnPlay()
    {
        _read.Clear();
        _loaded = true; // skip loading any persisted values this session
        for (int i = 0; i < Chapters.Length; i++)
            PlayerPrefs.DeleteKey(PrefsPrefix + i);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static int TotalChapters => Chapters.Length;

    public static int ReadCount
    {
        get { EnsureLoaded(); return _read.Count; }
    }

    public static bool IsRead(int index)
    {
        EnsureLoaded();
        return _read.Contains(index);
    }

    public static int IndexOfTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return -1;
        for (int i = 0; i < Chapters.Length; i++)
            if (string.Equals(Chapters[i].Title, title, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    /// <summary>Index of the first unread chapter in order, or -1 when all are read.</summary>
    public static int NextUnreadIndex
    {
        get
        {
            EnsureLoaded();
            for (int i = 0; i < Chapters.Length; i++)
                if (!_read.Contains(i)) return i;
            return -1;
        }
    }

    public static void MarkReadByTitle(string title)
    {
        var idx = IndexOfTitle(title);
        if (idx < 0) return;

        EnsureLoaded();
        if (!_read.Add(idx)) return;

        PlayerPrefs.SetInt(PrefsPrefix + idx, 1);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        for (int i = 0; i < Chapters.Length; i++)
            if (PlayerPrefs.GetInt(PrefsPrefix + i, 0) == 1)
                _read.Add(i);
    }
}
