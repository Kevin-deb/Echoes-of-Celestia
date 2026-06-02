using System.Collections;
using UnityEngine;

/// <summary>
/// Hub 场景主敌人：生命值 + 自动瞄准 + 红色即时射线激光（方案 A）。
/// 设计用于 P_Oblivion_Drone_01；Awake 时自动查找炮塔子节点并禁用素材包自带的 TurretLookAt。
/// </summary>
public sealed class PrimaryEnemy : MonoBehaviour
{
    [Header("生命值")]
    [SerializeField] int maxHealth = 80;

    [Header("攻击")]
    [SerializeField] float detectRange = 70f;
    [SerializeField] float attackRange = 55f;
    [SerializeField] float attackCooldown = 1.4f;
    [SerializeField] int damagePerShot = 12;
    [SerializeField] float aimToleranceDegrees = 7f;

    [Header("炮塔旋转")]
    [SerializeField] float turretTurnSpeed = 90f;
    [SerializeField] float gunTurnSpeed = 90f;

    [Header("激光视觉")]
    [SerializeField] float laserVisibleDuration = 0.12f;
    [SerializeField] float laserWidth = 0.18f;
    [SerializeField] Color laserColor = new Color(1f, 0.15f, 0.1f, 1f);

    [Header("引用（留空则自动查找）")]
    [SerializeField] Transform turretBase;
    [SerializeField] Transform gun;
    [SerializeField] Transform muzzle;

    int _health;
    float _nextShotTime;
    LineRenderer _laserLine;
    AudioSource _shootAudio;
    Coroutine _laserRoutine;

    public bool IsAlive => _health > 0;

    void Awake()
    {
        _health = maxHealth;
        AutoFindTurretParts();
        DisableLegacyTurretScript();
        EnsureLaserLine();
        _shootAudio = GetComponentInChildren<AudioSource>();
    }

    void Update()
    {
        if (!IsAlive) return;

        var target = ResolveAttackTarget();
        if (target == null) return;

        var aimPoint = GetAimPoint(target);
        var dist = Vector3.Distance(transform.position, aimPoint);
        if (dist > detectRange) return;

        AimTurret(aimPoint);

        if (dist > attackRange) return;
        TryShoot(target, aimPoint);
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0) return;

        _health -= amount;
        if (_health > 0) return;

        OnDeath();
    }

    // ── 目标 ──────────────────────────────────────────────────────────────────

    static Transform ResolveAttackTarget()
    {
        var vehicle = SpaceVehicleSeat.ActiveOccupiedTransform;
        if (vehicle != null) return vehicle;

        var player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    static Vector3 GetAimPoint(Transform target)
    {
        var cc = target.GetComponent<CharacterController>();
        if (cc != null)
            return target.TransformPoint(cc.center);

        var col = target.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.center;

        return target.position + Vector3.up;
    }

    // ── 瞄准 ──────────────────────────────────────────────────────────────────

    void AimTurret(Vector3 aimPoint)
    {
        if (turretBase != null)
        {
            var flat = aimPoint - turretBase.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f)
            {
                var desired = Quaternion.LookRotation(flat.normalized, Vector3.up);
                turretBase.rotation = Quaternion.RotateTowards(
                    turretBase.rotation, desired, turretTurnSpeed * Time.deltaTime);
            }
        }

        if (gun == null) return;

        var origin = muzzle != null ? muzzle.position : gun.position;
        var toTarget = aimPoint - origin;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        var look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        gun.rotation = Quaternion.RotateTowards(gun.rotation, look, gunTurnSpeed * Time.deltaTime);
    }

    bool IsAimedAt(Vector3 aimPoint)
    {
        var origin = muzzle != null ? muzzle.position : (gun != null ? gun.position : transform.position);
        var desired = (aimPoint - origin).normalized;
        var forward = muzzle != null ? muzzle.forward : (gun != null ? gun.forward : transform.forward);
        return Vector3.Angle(forward, desired) <= aimToleranceDegrees;
    }

    // ── 射击 ──────────────────────────────────────────────────────────────────

    void TryShoot(Transform target, Vector3 aimPoint)
    {
        if (Time.time < _nextShotTime) return;
        if (!IsAimedAt(aimPoint)) return;

        _nextShotTime = Time.time + attackCooldown;

        var origin = muzzle != null ? muzzle.position : gun.position;
        var dir = (aimPoint - origin).normalized;
        var end = origin + dir * attackRange;

        if (Physics.Raycast(origin, dir, out var hit, attackRange, ~0, QueryTriggerInteraction.Ignore))
            end = hit.point;

        // 玩家在载具内时，装甲阻挡激光，不造成任何伤害（仍显示激光视觉效果）。
        if (!SpaceVehicleSeat.IsOccupied)
        {
            var combat = target.GetComponent<HubCombatTarget>();
            if (combat != null && combat.IsAlive)
                combat.TakeDamage(damagePerShot);
        }

        if (_shootAudio != null)
            _shootAudio.Play();

        if (_laserRoutine != null)
            StopCoroutine(_laserRoutine);
        _laserRoutine = StartCoroutine(FlashLaser(origin, end));
    }

    // ── 激光 LineRenderer ─────────────────────────────────────────────────────

    void EnsureLaserLine()
    {
        var lineGo = new GameObject("EnemyLaserLine");
        lineGo.transform.SetParent(transform, false);

        _laserLine = lineGo.AddComponent<LineRenderer>();
        _laserLine.useWorldSpace = true;
        _laserLine.positionCount = 2;
        _laserLine.startWidth = laserWidth;
        _laserLine.endWidth = laserWidth * 0.35f;
        _laserLine.numCapVertices = 4;
        _laserLine.enabled = false;

        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            var mat = new Material(shader) { color = laserColor };
            _laserLine.material = mat;
            _laserLine.startColor = laserColor;
            _laserLine.endColor = laserColor;
        }
    }

    IEnumerator FlashLaser(Vector3 start, Vector3 end)
    {
        _laserLine.SetPosition(0, start);
        _laserLine.SetPosition(1, end);
        _laserLine.enabled = true;
        yield return new WaitForSeconds(laserVisibleDuration);
        _laserLine.enabled = false;
        _laserRoutine = null;
    }

    // ── 死亡 / 初始化 ───────────────────────────────────────────────────────

    void OnDeath()
    {
        foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == this) continue;
            mb.enabled = false;
        }

        if (_laserLine != null)
            _laserLine.enabled = false;

        Destroy(gameObject, 0.5f);
    }

    void AutoFindTurretParts()
    {
        if (turretBase == null)
            turretBase = FindDeepChild(transform, "P_Turret_Simple_01");
        if (gun == null)
            gun = FindDeepChild(transform, "Turret_Gun_01");
        if (muzzle == null)
            muzzle = FindDeepChild(transform, "Laser_Launch");
    }

    void DisableLegacyTurretScript()
    {
        foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb == this) continue;
            if (mb.GetType().Name == "TurretLookAt")
                mb.enabled = false;
        }
    }

    static Transform FindDeepChild(Transform root, string childName)
    {
        if (root.name == childName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeepChild(root.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }
}
