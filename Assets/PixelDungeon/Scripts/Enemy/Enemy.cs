using UnityEngine;
using PackCreature = Assets.PixelFantasy.Common.Scripts.Creature;

namespace PixelDungeon
{
    /// <summary>
    /// Drives an instantiated PixelMonsters prefab as a hostile (design doc 3.5). Strips the asset's
    /// example side-scroller AI, converts the body to top-down physics, and runs a small behaviour
    /// state machine (chase / strafe-and-shoot / bruiser melee / splitter / boss).
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        public EnemyDef Def { get; private set; }
        public Room Room;
        public Health Health { get; private set; }

        private PackCreature _creature;
        private Animator _anim;
        private SpriteRenderer _body;
        private StatusReceiver _status;
        private Rigidbody2D _rb;

        private Vector2 _moveVel;
        private float _attackCD, _contactCD, _strafeSign = 1f, _strafeTimer, _aiThink;
        private bool _dead;

        public void Init(EnemyDef def, Room room)
        {
            Def = def;
            Room = room;

            // Safety net: the database generator already strips the asset's example controllers from
            // our prefab variants at build time. If an un-cleaned prefab slips through, disable now and
            // remove with deferred Destroy — DestroyImmediate is illegal here (Init can run inside a
            // physics callback, e.g. a splitter dying to a projectile spawns its children mid-trigger).
            // Order matters: RequireComponent dependents (MonsterControls) must be destroyed first.
            StripExample("MonsterControls");
            StripExample("MonsterController2D");
            StripExample("MonsterAnimation");
            StripExample("InanimateControls");

            _creature = GetComponent<PackCreature>();
            _anim = _creature != null ? _creature.Animator : GetComponentInChildren<Animator>();
            _body = _creature != null ? _creature.Body : GetComponentInChildren<SpriteRenderer>();

            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.freezeRotation = true;
            _rb.drag = 8f;
            _rb.simulated = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            transform.localScale = Vector3.one * def.scale;

            Health = gameObject.AddComponent<Health>();
            Health.Configure(def.maxHp, 0f, false, Team.Enemy, _body);
            Health.Death += OnDeath;
            Health.Damaged += OnDamaged;

            _status = gameObject.AddComponent<StatusReceiver>();
            _status.SetTint(_body);

            _aiThink = Random.value;
            SetAnimWalking(false);
        }

        private void StripExample(string typeName)
        {
            foreach (var mb in GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == typeName)
                {
                    mb.enabled = false;   // stop Update immediately
                    Destroy(mb);          // deferred removal also silences physics callbacks next frame
                    return;
                }
        }

        private void OnDamaged(float amount, Element element)
        {
            if (_dead) return;
            if (_anim != null) _anim.SetTrigger("Hit");
        }

        private void Update()
        {
            if (_dead) return;
            var player = GameManager.I != null ? GameManager.I.Player : null;
            if (player == null || player.Health.Dead) { _moveVel = Vector2.zero; SetAnimWalking(false); return; }

            if (_attackCD > 0f) _attackCD -= Time.deltaTime;
            if (_contactCD > 0f) _contactCD -= Time.deltaTime;

            Vector2 self = transform.position;
            Vector2 toPlayer = (Vector2)player.transform.position - self;
            float dist = toPlayer.magnitude;
            Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.zero;

            Vector2 desired = Vector2.zero;
            switch (Def.behavior)
            {
                case EnemyBehavior.Charger:
                case EnemyBehavior.Splitter:
                    desired = dir;
                    break;
                case EnemyBehavior.Bruiser:
                    desired = dist > Def.attackRange * 0.8f ? dir : Vector2.zero;
                    if (dist <= Def.attackRange && _attackCD <= 0f) MeleeAttack(dir);
                    break;
                case EnemyBehavior.Strafer:
                    desired = StrafeMove(dir, dist);
                    if (dist <= Def.attackRange && _attackCD <= 0f) RangedAttack(dir);
                    break;
                case EnemyBehavior.Boss:
                    desired = BossMove(dir, dist);
                    if (_attackCD <= 0f) BossAttack(dir, dist);
                    break;
            }

            _moveVel = desired.normalized * (Def.moveSpeed * _status.SpeedMultiplier);
            bool moving = _moveVel.sqrMagnitude > 0.02f;
            SetAnimWalking(moving);
            if (dir.sqrMagnitude > 0.001f && _body != null) _body.flipX = dir.x < 0f;

            // contact damage
            if (dist < 0.9f + Def.scale * 0.2f && _contactCD <= 0f)
            {
                if (player.Health.Damage(Def.contactDamage, Def.element, self, 5f, Team.Enemy))
                {
                    _contactCD = 0.8f;
                    if (Def.element != Element.Physical) player.GetComponent<StatusReceiver>()?.Apply(Def.element, Def.contactDamage);
                }
            }
        }

        private Vector2 StrafeMove(Vector2 dir, float dist)
        {
            float preferred = Def.attackRange * 0.55f;
            _strafeTimer -= Time.deltaTime;
            if (_strafeTimer <= 0f) { _strafeSign = Random.value < 0.5f ? -1f : 1f; _strafeTimer = Random.Range(1f, 2.2f); }
            Vector2 perp = new Vector2(-dir.y, dir.x) * _strafeSign;
            if (dist > preferred + 1f) return dir * 0.8f + perp * 0.3f;
            if (dist < preferred - 1f) return -dir * 0.9f + perp * 0.3f;
            return perp;
        }

        private Vector2 BossMove(Vector2 dir, float dist)
        {
            float preferred = 5f;
            if (dist > preferred + 1.5f) return dir;
            if (dist < preferred - 1.5f) return -dir * 0.6f;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            return perp * 0.5f;
        }

        private void MeleeAttack(Vector2 dir)
        {
            _attackCD = Def.attackCooldown;
            if (_anim != null) _anim.SetTrigger("Attack");
            CombatService.I.MeleeArc((Vector2)transform.position + dir * 0.4f, dir, Def.attackRange + 0.4f, 110f,
                Def.contactDamage * 1.4f, 6f, Def.element, Team.Enemy, 0f);
        }

        private void RangedAttack(Vector2 dir)
        {
            _attackCD = Def.attackCooldown;
            if (_anim != null) _anim.SetTrigger("Attack");
            CombatService.I.EnemyShoot((Vector2)transform.position + new Vector2(0f, 0.5f), dir, Def);
        }

        private int _bossPhase;
        private void BossAttack(Vector2 dir, float dist)
        {
            _attackCD = Def.attackCooldown;
            if (_anim != null) _anim.SetTrigger("Attack");
            Vector2 origin = (Vector2)transform.position + new Vector2(0f, 0.6f);
            _bossPhase++;
            if (_bossPhase % 3 == 0)
            {
                // radial volley
                int n = 12;
                for (int i = 0; i < n; i++)
                {
                    float a = i / (float)n * Mathf.PI * 2f;
                    CombatService.I.EnemyShoot(origin, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), Def);
                }
                Juice.Shake(0.25f);
            }
            else
            {
                // 3-shot spread at player
                for (int i = -1; i <= 1; i++)
                {
                    float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + i * 14f;
                    CombatService.I.EnemyShoot(origin, new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)), Def);
                }
            }
        }

        private void SetAnimWalking(bool walking)
        {
            if (_anim == null) return;
            _anim.SetBool("Walk", walking);
            _anim.SetBool("Idle", !walking);
        }

        private void FixedUpdate()
        {
            if (_dead || _rb == null) return;
            if (Health != null && Health.Controllable) _rb.velocity = _moveVel;
        }

        private void OnDeath()
        {
            if (_dead) return;
            _dead = true;
            _moveVel = Vector2.zero;
            if (_rb != null) { _rb.velocity = Vector2.zero; _rb.simulated = false; }
            foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
            if (_anim != null) { _anim.SetBool("Walk", false); _anim.SetBool("Die", true); }

            Vector2 pos = transform.position;
            CombatService.I.Burst(pos, Def.element == Element.Physical ? new Color(0.8f, 0.3f, 0.3f) : GameUtil.ElementColor(Def.element), Def.behavior == EnemyBehavior.Boss ? 30 : 16);
            Juice.Shake(Def.behavior == EnemyBehavior.Boss ? 0.5f : 0.18f);

            Loot.DropFromEnemy(Def, pos);
            if (Def.behavior == EnemyBehavior.Boss) Loot.DropBossReward(pos);

            if (!string.IsNullOrEmpty(Def.splitInto) && Room != null)
            {
                for (int i = 0; i < Def.splitCount; i++)
                    Room.SpawnEnemy(Def.splitInto, pos + Random.insideUnitCircle * 0.8f);
            }

            Room?.NotifyEnemyDied(this);
            Destroy(gameObject, 1.3f);
        }
    }
}
