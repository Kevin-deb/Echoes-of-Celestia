using UnityEngine;

/// <summary>
/// Goes on an entrance collider set to IsTrigger. Prompts the player to press F
/// when they step inside, which opens the level-select UI. Tag the player as
/// "Player" (matching the Starter Assets default).
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
