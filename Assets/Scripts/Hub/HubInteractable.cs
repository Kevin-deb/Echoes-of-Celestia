using UnityEngine;

/// <summary>
/// 挂在带 IsTrigger 的入口碰撞体上。玩家进入后提示按 F，打开选关 UI。
/// 请将玩家角色 Tag 设为 Player（与 Starter Assets 默认一致）。
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class HubInteractable : MonoBehaviour
{
    [SerializeField] GameObject promptRoot;
    [SerializeField] GameObject minigameMenuRoot;

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
        if (promptRoot != null) promptRoot.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = false;
        if (promptRoot != null) promptRoot.SetActive(false);
        if (minigameMenuRoot != null) minigameMenuRoot.SetActive(false);
    }

    void Update()
    {
        if (!_playerInside || minigameMenuRoot == null) return;
        if (!Input.GetKeyDown(KeyCode.F)) return;
        minigameMenuRoot.SetActive(true);
        if (promptRoot != null) promptRoot.SetActive(false);
    }

    void LateUpdate()
    {
        if (!_playerInside || minigameMenuRoot == null || promptRoot == null) return;
        if (!minigameMenuRoot.activeSelf) promptRoot.SetActive(true);
    }
}
