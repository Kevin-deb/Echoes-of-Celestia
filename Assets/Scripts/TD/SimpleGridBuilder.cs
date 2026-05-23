using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 鼠标左键在地面网格上放置炮塔；占用格用字典记录。
/// </summary>
public sealed class SimpleGridBuilder : MonoBehaviour
{
    [SerializeField] Turret turretPrefab;
    [SerializeField] TDGameManager gameManager;
    [SerializeField] Camera worldCamera;
    [SerializeField] LayerMask groundMask;
    [SerializeField] int turretCost = 30;
    [SerializeField] float cellSize = 1f;
    [SerializeField] float groundRayMaxDistance = 200f;

    readonly Dictionary<Vector2Int, Turret> _cells = new();

    void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
    }

    void Update()
    {
        if (turretPrefab == null || worldCamera == null) return;
        if (!Input.GetMouseButtonDown(0)) return;

        var ray = worldCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, groundRayMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
            return;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        var snapped = Snap(hit.point);
        var cell = WorldToCell(snapped);
        if (_cells.ContainsKey(cell)) return;

        if (GameSession.Instance != null && !GameSession.Instance.TrySpendGold(turretCost))
            return;

        var t = Instantiate(turretPrefab, snapped, Quaternion.identity);
        if (gameManager != null) t.Bind(gameManager);
        _cells[cell] = t;
    }

    Vector3 Snap(Vector3 world)
    {
        var s = cellSize;
        var x = Mathf.Round(world.x / s) * s;
        var z = Mathf.Round(world.z / s) * s;
        return new Vector3(x, world.y, z);
    }

    static Vector2Int WorldToCell(Vector3 world)
    {
        return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
    }
}
