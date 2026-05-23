using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 挂在「门」的触发器上：玩家进入后显示提示，按 F 进入目标场景。
/// 支持按 GameSession 的已解锁关卡数进行关卡门禁。
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class HubDoorInteractable : MonoBehaviour
{
    [SerializeField] int levelIndex = 0;
    [SerializeField] string levelDisplayName = "Level";
    [SerializeField] string targetSceneName = "";
    [SerializeField] GameObject promptRoot;
    [SerializeField] Text promptText;

    bool _playerInside;

    void Reset()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = true;
        RefreshPrompt();
        if (promptRoot != null) promptRoot.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = false;
        if (promptRoot != null) promptRoot.SetActive(false);
    }

    void Update()
    {
        if (!_playerInside) return;
        if (!Input.GetKeyDown(KeyCode.F)) return;
        if (!IsUnlocked()) return;
        if (string.IsNullOrEmpty(targetSceneName)) return;
        SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
    }

    void LateUpdate()
    {
        if (!_playerInside) return;
        RefreshPrompt();
    }

    bool IsUnlocked()
    {
        if (GameSession.Instance == null) return true;
        return levelIndex < GameSession.Instance.UnlockedLevelCount;
    }

    void RefreshPrompt()
    {
        if (promptText == null) return;
        if (IsUnlocked())
        {
            promptText.text = $"Press F to enter {levelDisplayName}";
        }
        else
        {
            var need = levelIndex + 1;
            promptText.text = $"{levelDisplayName} is locked (unlock through Level {need})";
        }
    }
}
