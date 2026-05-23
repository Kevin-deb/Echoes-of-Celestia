using UnityEngine;

public sealed class TDProjectile : MonoBehaviour
{
    [SerializeField] float speed = 18f;
    [SerializeField] int damage = 10;
    [SerializeField] float lifetime = 3f;

    Transform _target;
    float _t;

    public void Launch(Transform target)
    {
        _target = target;
    }

    void Update()
    {
        _t += Time.deltaTime;
        if (_t >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_target == null)
        {
            Destroy(gameObject);
            return;
        }

        var p = transform.position;
        var to = _target.position - p;
        var step = speed * Time.deltaTime;
        if (to.magnitude <= step)
        {
            TryHit(_target);
            Destroy(gameObject);
            return;
        }

        transform.position += to.normalized * step;
    }

    void TryHit(Transform target)
    {
        var e = target != null ? target.GetComponentInParent<EnemyWalker>() : null;
        e?.TakeDamage(damage);
    }
}
