using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// The hero (design doc 3.1/3.2). Top-down WASD movement, mouse aim mapped onto the 4-direction
    /// animator, hold-to-fire, dodge roll with i-frames, weapon switch, a hero-specific E skill and
    /// F to interact. Adds and wires the supporting components (Health/Energy/Weapons/Status).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public HeroDef Hero { get; private set; }
        public Health Health { get; private set; }
        public Energy Energy { get; private set; }
        public WeaponController Weapons { get; private set; }
        public bool InputEnabled = true;

        private CharacterVisual _visual;
        private StatusReceiver _status;
        private Rigidbody2D _rb;
        private Camera _cam;

        private Vector2 _moveInput;
        private Vector2 _aimDir = Vector2.down;
        private float _baseSpeed;

        private bool _dodging;
        private Vector2 _dodgeDir;
        private float _dodgeTimer, _dodgeCooldown, _skillCooldown;

        public Vector2 AimDirection => _aimDir;
        public Vector2 MuzzleOrigin => (Vector2)transform.position + new Vector2(0f, 0.55f);
        public float SkillCooldown01 => Mathf.Clamp01(_skillCooldown / SkillMaxCooldown);
        private const float SkillMaxCooldown = 6f;

        public void Setup(HeroDef hero, Camera cam)
        {
            Hero = hero;
            _cam = cam;
            _visual = GetComponent<CharacterVisual>();
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.simulated = true;                  // prefab ships with simulation off
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.drag = 6f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var box = GetComponent<BoxCollider2D>();
            if (box != null) { box.size = new Vector2(0.7f, 0.55f); box.offset = new Vector2(0f, 0.32f); }

            _baseSpeed = hero.moveSpeed;

            Health = gameObject.AddComponent<Health>();
            Health.Configure(hero.maxHp, hero.id == "knight" ? 40f : 0f, true, Team.Player, _visual.Body);
            Health.Death += OnDeath;

            Energy = gameObject.AddComponent<Energy>();
            Energy.SetMax(hero.maxEnergy);

            _status = gameObject.AddComponent<StatusReceiver>();

            Weapons = gameObject.AddComponent<WeaponController>();
            Weapons.Init(_visual, Energy, Health, hero);
            Weapons.AddWeapon(GameContent.Weapon(hero.startWeapon), true);
        }

        private void Update()
        {
            if (Health == null || Health.Dead) return;
            if (_dodgeCooldown > 0f) _dodgeCooldown -= Time.deltaTime;
            if (_skillCooldown > 0f) _skillCooldown -= Time.deltaTime;

            if (!InputEnabled) { _moveInput = Vector2.zero; _visual.SetMoving(false); return; }

            // --- Aim (mouse) ---
            Vector3 mouse = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 toMouse = (Vector2)mouse - MuzzleOrigin;
            if (toMouse.sqrMagnitude > 0.001f) _aimDir = toMouse.normalized;
            _visual.Aim(_aimDir);

            // --- Move input ---
            _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (_moveInput.sqrMagnitude > 1f) _moveInput.Normalize();
            _visual.SetMoving(_moveInput.sqrMagnitude > 0.01f && !_dodging);

            // --- Dodge (design doc 3.1: i-frames) ---
            if (Input.GetKeyDown(KeyCode.Space) && _dodgeCooldown <= 0f) StartDodge();

            // --- Fire (hold) ---
            if (!_dodging && Input.GetMouseButton(0))
                Weapons.TryFire(_aimDir, MuzzleOrigin);

            // --- Weapon switch ---
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Input.GetKeyDown(KeyCode.Q) || scroll > 0.01f) Weapons.Switch(1);
            else if (scroll < -0.01f) Weapons.Switch(-1);

            // --- Skill ---
            if (Input.GetKeyDown(KeyCode.E) && _skillCooldown <= 0f) UseSkill();

            // --- Interact ---
            if (Input.GetKeyDown(KeyCode.F)) TryInteract();
        }

        private void FixedUpdate()
        {
            if (Health == null || Health.Dead) return;

            if (_dodging)
            {
                _rb.velocity = _dodgeDir * (_baseSpeed * 2.6f);
                _dodgeTimer -= Time.fixedDeltaTime;
                if (_dodgeTimer <= 0f) _dodging = false;
                return;
            }

            if (Health.Controllable)
                _rb.velocity = _moveInput * (_baseSpeed * _status.SpeedMultiplier);
        }

        private void StartDodge()
        {
            _dodgeDir = _moveInput.sqrMagnitude > 0.01f ? _moveInput.normalized : _aimDir;
            _dodging = true;
            _dodgeTimer = 0.22f;
            _dodgeCooldown = 0.6f;
            Health.MakeInvulnerable(0.32f);
            CombatService.I.Pop(transform.position, new Color(0.8f, 0.8f, 0.85f));
        }

        private void UseSkill()
        {
            Vector2 origin = MuzzleOrigin;
            switch (Hero.id)
            {
                case "mage":
                    if (!Energy.Spend(40f)) return;
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i / 14f * Mathf.PI * 2f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        CombatService.I.FireWeapon(origin, d, NovaBolt, Team.Player, 1f, 0f);
                    }
                    CombatService.I.Burst(origin, GameUtil.ElementColor(Element.Fire), 20);
                    break;
                case "ranger":
                    if (!Energy.Spend(20f)) return;
                    for (int i = -1; i <= 1; i++)
                    {
                        float ang = Mathf.Atan2(_aimDir.y, _aimDir.x) * Mathf.Rad2Deg + i * 12f;
                        var d = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                        CombatService.I.FireWeapon(origin, d, GameContent.Weapon("longbow"), Team.Player, 1f, 0.2f);
                    }
                    break;
                case "knight":
                    _dodgeDir = _aimDir; _dodging = true; _dodgeTimer = 0.3f;
                    Health.MakeInvulnerable(0.35f); Health.AddArmor(20f);
                    CombatService.I.MeleeArc(origin, _aimDir, 2.2f, 360f, 30f, 14f, Element.Physical, Team.Player, 0.1f);
                    break;
                case "berserker":
                    CombatService.I.MeleeArc(origin, _aimDir, 2.6f, 360f, 45f, 16f, Element.Physical, Team.Player, 0.15f);
                    CombatService.I.Burst(origin, new Color(1f, 0.8f, 0.3f), 16);
                    Juice.Shake(0.3f);
                    break;
                case "necromancer":
                    if (!Energy.Spend(30f)) return;
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i / 10f * Mathf.PI * 2f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        CombatService.I.FireWeapon(origin, d, PoisonNova, Team.Player, 1f, 0f);
                    }
                    CombatService.I.Burst(origin, GameUtil.ElementColor(Element.Poison), 18);
                    break;
            }
            _skillCooldown = SkillMaxCooldown;
        }

        private static readonly WeaponDef NovaBolt = new() { id = "nova", kind = WeaponKind.Wand, element = Element.Fire, damage = 18, projSpeed = 11, range = 1.2f, knockback = 5 };
        private static readonly WeaponDef PoisonNova = new() { id = "pnova", kind = WeaponKind.Wand, element = Element.Poison, damage = 12, projSpeed = 10, range = 1.4f, knockback = 3 };

        private void TryInteract()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, 1.4f);
            Interactable best = null; float bestD = float.MaxValue;
            foreach (var h in hits)
            {
                var it = h.GetComponentInParent<Interactable>();
                if (it == null || !it.CanInteract) continue;
                float d = Vector2.Distance(transform.position, it.transform.position);
                if (d < bestD) { bestD = d; best = it; }
            }
            best?.Interact(this);
        }

        private void OnDeath()
        {
            _visual.PlayDeath();
            InputEnabled = false;
            _rb.velocity = Vector2.zero;
            CombatService.I.Burst(transform.position, new Color(0.8f, 0.2f, 0.2f), 16);
            GameManager.I.OnPlayerDied();
        }

        public Interactable NearestInteractable()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, 1.4f);
            Interactable best = null; float bestD = float.MaxValue;
            foreach (var h in hits)
            {
                var it = h.GetComponentInParent<Interactable>();
                if (it == null || !it.CanInteract) continue;
                float d = Vector2.Distance(transform.position, it.transform.position);
                if (d < bestD) { bestD = d; best = it; }
            }
            return best;
        }
    }
}
