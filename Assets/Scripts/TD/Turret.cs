using UnityEngine;

public sealed class Turret : MonoBehaviour
{
    [SerializeField] TDProjectile projectilePrefab;
    [SerializeField] Transform muzzle;
    [SerializeField] float range = 8f;
    [SerializeField] float fireCooldown = 0.6f;
    [SerializeField] LayerMask enemyLayers = ~0;

    TDGameManager _manager;
    float _nextFire;

    public void Bind(TDGameManager manager)
    {
        _manager = manager;
    }

    void Update()
    {
        if (projectilePrefab == null) return;

        var target = AcquireTarget();
        if (target == null) return;

        var flat = target.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.01f)
            transform.forward = Vector3.Slerp(transform.forward, flat.normalized, 12f * Time.deltaTime);

        if (Time.time < _nextFire) return;
        _nextFire = Time.time + fireCooldown;

        var origin = muzzle != null ? muzzle.position : transform.position + transform.forward * 0.5f;
        var proj = Instantiate(projectilePrefab, origin, Quaternion.identity);
        proj.Launch(target);
    }

    Transform AcquireTarget()
    {
        var hits = Physics.OverlapSphere(transform.position, range, enemyLayers, QueryTriggerInteraction.Collide);
        Transform best = null;
        var bestScore = float.MaxValue;
        var end = _manager != null ? _manager.PathEndPosition : transform.position + transform.forward * 50f;

        foreach (var h in hits)
        {
            var e = h.GetComponentInParent<EnemyWalker>();
            if (e == null || !e.IsAlive) continue;

            var score = (h.transform.position - end).sqrMagnitude;
            if (score < bestScore)
            {
                bestScore = score;
                best = h.transform;
            }
        }

        return best;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
#endif
}
