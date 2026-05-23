using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 选关全屏 Canvas：打开时解锁鼠标；关闭时恢复（若仍在大厅可继续用第三人称锁定）。
/// </summary>
public sealed class MinigameMenuController : MonoBehaviour
{
    [SerializeField] Button closeButton;
    [SerializeField] Button[] levelButtons;
    [SerializeField] string[] levelSceneNames;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseMenu);

        for (var i = 0; i < levelButtons.Length; i++)
        {
            var idx = i;
            if (levelButtons[i] == null) continue;
            levelButtons[i].onClick.AddListener(() => LoadLevelByIndex(idx));
        }
    }

    void OnEnable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshLevelButtons();
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;
        if (Input.GetKeyDown(KeyCode.Escape)) CloseMenu();
    }

    void RefreshLevelButtons()
    {
        var unlocked = GameSession.Instance != null
            ? GameSession.Instance.UnlockedLevelCount
            : int.MaxValue;
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] == null) continue;
            bool ok = i < unlocked && i < levelSceneNames.Length && !string.IsNullOrEmpty(levelSceneNames[i]);
            levelButtons[i].interactable = ok;
        }
    }

    public void LoadLevelByIndex(int index)
    {
        if (index < 0 || index >= levelSceneNames.Length) return;
        if (GameSession.Instance != null && index >= GameSession.Instance.UnlockedLevelCount) return;

        var name = levelSceneNames[index];
        if (string.IsNullOrEmpty(name)) return;

        SceneManager.LoadScene(name, LoadSceneMode.Single);
    }

    public void CloseMenu()
    {
        gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
