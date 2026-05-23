using UnityEngine;

/// <summary>
/// 跨场景数据：金币、解锁关卡等。使用 DontDestroyOnLoad + PlayerPrefs 简易持久化。
/// </summary>
public sealed class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    [SerializeField] int startingGold = 100;
    [SerializeField] int startingUnlockedLevels = 1;

    public int Gold { get; private set; }
    public int UnlockedLevelCount { get; private set; }

    const string PrefsGold = "ec_gold";
    const string PrefsUnlocked = "ec_unlocked_levels";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadFromPrefs();
    }

    void LoadFromPrefs()
    {
        if (PlayerPrefs.HasKey(PrefsGold))
            Gold = PlayerPrefs.GetInt(PrefsGold);
        else
            Gold = startingGold;

        if (PlayerPrefs.HasKey(PrefsUnlocked))
            UnlockedLevelCount = Mathf.Max(1, PlayerPrefs.GetInt(PrefsUnlocked));
        else
            UnlockedLevelCount = Mathf.Max(1, startingUnlockedLevels);
    }

    public void SaveToPrefs()
    {
        PlayerPrefs.SetInt(PrefsGold, Gold);
        PlayerPrefs.SetInt(PrefsUnlocked, UnlockedLevelCount);
        PlayerPrefs.Save();
    }

    public void AddGold(int amount)
    {
        Gold = Mathf.Max(0, Gold + amount);
        SaveToPrefs();
    }

    public bool TrySpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        SaveToPrefs();
        return true;
    }

    public void UnlockNextLevelIfNeeded(int completedLevelIndex)
    {
        var needed = completedLevelIndex + 2;
        if (needed <= UnlockedLevelCount) return;

        UnlockedLevelCount = needed;
        SaveToPrefs();
    }
}
