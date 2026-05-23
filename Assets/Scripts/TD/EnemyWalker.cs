using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class EnemyWalker : MonoBehaviour
{
    [SerializeField] float moveSpeed = 3f;
    [SerializeField] int maxHealth = 30;
    [SerializeField] int rewardGoldOnKill = 5;

    WaypointPath _path;
    int _index;
    int _health;
    TDGameManager _manager;

    public bool IsAlive => _health > 0;

    public void Initialize(WaypointPath path, TDGameManager manager)
    {
        _path = path;
        _manager = manager;
        _health = maxHealth;
        _index = 0;
        if (_path != null && _path.Count > 0)
            transform.position = _path.GetWorldPosition(0);
    }

    void Update()
    {
        if (_path == null || _path.Count < 2) return;

        if (_index >= _path.Count - 1)
        {
            ReachEnd();
            return;
        }

        var next = _path.GetWorldPosition(_index + 1);
        var dir = (next - transform.position);
        var dist = dir.magnitude;
        if (dist < 0.05f)
        {
            _index++;
            return;
        }

        transform.position += dir.normalized * (moveSpeed * Time.deltaTime);
        if (dir.sqrMagnitude > 0.0001f)
            transform.forward = Vector3.Slerp(transform.forward, new Vector3(dir.x, 0f, dir.z).normalized, 10f * Time.deltaTime);
    }

    void ReachEnd()
    {
        _manager?.NotifyEnemyReachedEnd(this);
        Destroy(gameObject);
    }

    public void TakeDamage(int amount)
    {
        _health -= amount;
        if (_health > 0) return;

        _manager?.NotifyEnemyKilled(this, rewardGoldOnKill);
        Destroy(gameObject);
    }
}
