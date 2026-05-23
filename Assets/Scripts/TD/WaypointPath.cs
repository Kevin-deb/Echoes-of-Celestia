using UnityEngine;

/// <summary>
/// 将子物体顺序作为路点，或使用显式数组。
/// </summary>
public sealed class WaypointPath : MonoBehaviour
{
    [SerializeField] Transform[] waypoints;

    public int Count => waypoints != null ? waypoints.Length : 0;

    public Vector3 GetWorldPosition(int index)
    {
        if (waypoints == null || index < 0 || index >= waypoints.Length)
            return transform.position;
        return waypoints[index].position;
    }

    public Transform GetEndTransform()
    {
        if (waypoints == null || waypoints.Length == 0) return transform;
        return waypoints[waypoints.Length - 1];
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (waypoints == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawSphere(waypoints[i].position, 0.25f);
            if (i > 0 && waypoints[i - 1] != null)
                Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
        }
    }
#endif
}
