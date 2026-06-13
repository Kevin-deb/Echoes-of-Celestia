using UnityEngine;
using PackCreature = Assets.PixelFantasy.Common.Scripts.Creature;
using PackBuilder = Assets.PixelFantasy.PixelHeroes4D.Common.Scripts.CharacterScripts.CharacterBuilder;
using PackState = Assets.PixelFantasy.PixelHeroes4D.Common.Scripts.CharacterScripts.CharacterState;

namespace PixelDungeon
{
    /// <summary>
    /// Adapter over the PixelHeroes4D modular character. Translates "aim at any angle" into the
    /// 4-direction animator (design doc 3.1), drives Idle/Run/attack states, and re-skins the
    /// character when a weapon is equipped (design doc 4.2). Lives on the instantiated Character prefab.
    /// </summary>
    public class CharacterVisual : MonoBehaviour
    {
        private PackCreature _creature;
        private PackBuilder _builder;
        private Animator _anim;
        private SpriteRenderer _body;
        private Facing _facing = Facing.Down;
        private bool _dead;

        public SpriteRenderer Body => _body;
        public Facing CurrentFacing => _facing;

        /// <summary>Grab pack components and strip the asset's example controller scripts that would fight ours.</summary>
        public void Bind()
        {
            _creature = GetComponent<PackCreature>();
            _builder = GetComponent<PackBuilder>();
            _anim = _creature.Animator;
            _body = _creature.Body;

            foreach (var mb in GetComponents<MonoBehaviour>())
            {
                var n = mb.GetType().Name;
                if (n == "CharacterControls" || n == "CharacterController2D" || n == "CharacterAnimation")
                    mb.enabled = false;
            }
        }

        public void Aim(Vector2 dir)
        {
            if (_dead || dir.sqrMagnitude < 0.0004f) return;
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            {
                _facing = Facing.Side;
                _body.flipX = dir.x < 0f;
            }
            else
            {
                _facing = dir.y > 0f ? Facing.Up : Facing.Down;
                _body.flipX = false;
            }
            _anim.SetInteger("Direction", (int)_facing);
        }

        public void SetMoving(bool moving)
        {
            if (_dead) return;
            _anim.SetInteger("State", (int)(moving ? PackState.Run : PackState.Idle));
        }

        public void TriggerAttack(string trigger)
        {
            if (_dead) return;
            _anim.SetTrigger(trigger);
        }

        public void PlayDeath()
        {
            _dead = true;
            _anim.SetInteger("State", (int)PackState.Die);
        }

        /// <summary>Apply a hero's full appearance (race body + hair + armor) in a single rebuild.</summary>
        public void ApplyHero(HeroDef hero)
        {
            _builder.Body = hero.body;
            _builder.Head = hero.head;
            _builder.Eyes = hero.eyes;
            _builder.Ears = hero.ears;
            _builder.Hair = hero.hair ?? "";
            _builder.Mouth = hero.mouth ?? "";
            _builder.Armor = hero.armor ?? "";
            _builder.Helmet = hero.helmet ?? "";
            _builder.Shield = hero.shield ?? "";
            _builder.Rebuild();
        }

        /// <summary>Re-skin: set the Weapon layer (and optional armor/shield) then rebuild the atlas.</summary>
        public void Equip(string charWeapon, string armor = null, string shield = null)
        {
            if (charWeapon != null) _builder.Weapon = charWeapon;
            if (armor != null) _builder.Armor = armor;
            if (shield != null) _builder.Shield = shield;
            _builder.Rebuild();
        }
    }
}
