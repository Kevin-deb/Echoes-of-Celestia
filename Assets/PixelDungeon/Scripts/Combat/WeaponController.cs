using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// The player's weapon bag (design doc 3.3). Holds carried weapons, handles firing cadence,
    /// energy cost, melee vs ranged dispatch, hero passives and re-skinning the character on switch.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        public readonly List<WeaponDef> Weapons = new();
        public int Index;
        public float DamageMultiplier = 1f;   // run-wide modifier (altars, buffs)

        private CharacterVisual _visual;
        private Energy _energy;
        private Health _health;
        private HeroDef _hero;
        private float _cooldown;

        public event Action WeaponChanged;

        public WeaponDef Current => Weapons.Count > 0 ? Weapons[Index] : null;

        public void Init(CharacterVisual visual, Energy energy, Health health, HeroDef hero)
        {
            _visual = visual;
            _energy = energy;
            _health = health;
            _hero = hero;
        }

        public void AddWeapon(WeaponDef w, bool equip = false)
        {
            if (w == null) return;
            int existing = Weapons.FindIndex(x => x.id == w.id);
            if (existing >= 0) { if (equip) { Index = existing; Equip(); } return; }
            Weapons.Add(w);
            if (equip || Weapons.Count == 1) { Index = Weapons.Count - 1; Equip(); }
            else WeaponChanged?.Invoke();
        }

        public void Switch(int dir)
        {
            if (Weapons.Count <= 1) return;
            Index = (Index + dir + Weapons.Count) % Weapons.Count;
            Equip();
        }

        private void Equip()
        {
            _cooldown = 0f;
            if (Current != null) _visual.Equip(Current.charWeapon);
            WeaponChanged?.Invoke();
        }

        public bool CanFire => Current != null && _cooldown <= 0f;

        public bool TryFire(Vector2 aimDir, Vector2 muzzleOrigin)
        {
            if (!CanFire || aimDir.sqrMagnitude < 0.0004f) return false;
            var w = Current;

            float critBonus = _hero.id == "ranger" ? 0.15f : 0f;

            if (w.IsRanged)
            {
                if (!_energy.Spend(w.energyCost)) return false;
                CombatService.I.FireWeapon(muzzleOrigin, aimDir, w, Team.Player, DamageMultiplier, critBonus);
                CombatService.I.PlaySfx(CombatService.I.FireSfx, 0.5f);
            }
            else
            {
                CombatService.I.MeleeArc(muzzleOrigin, aimDir, w.meleeRange, w.meleeArcDeg,
                    w.damage * DamageMultiplier, w.knockback, w.element, Team.Player, w.critChance + critBonus);
            }

            _visual.TriggerAttack(w.AnimTrigger);
            _cooldown = 1f / Mathf.Max(0.05f, w.fireRate * AttackSpeedMultiplier());
            return true;
        }

        private float AttackSpeedMultiplier()
        {
            // Berserker passive: attack speed scales up as HP drops (design doc 3.2).
            if (_hero.id == "berserker" && _health != null && _health.Fraction < 0.4f) return 1.8f;
            return 1f;
        }

        private void Update()
        {
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;
        }
    }
}
