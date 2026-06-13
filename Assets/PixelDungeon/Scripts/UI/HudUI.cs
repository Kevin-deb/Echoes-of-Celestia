using UnityEngine;
using UnityEngine.UI;

namespace PixelDungeon
{
    /// <summary>In-run HUD: health & energy bars (top-left), run resources, floor/boss banners,
    /// interact prompt, controls hint (bottom-left), and a pause menu (Esc) with
    /// Resume / Back-to-Menu so the player can always leave a run.</summary>
    public class HudUI : MonoBehaviour
    {
        private Font _font;
        private GameObject _group, _pausePanel;
        private Image _hpFill, _enFill;
        private Text _hpText, _enText, _infoText, _weaponText, _floorText, _flashText, _bossText, _promptText;

        private GameManager _gm;
        private PlayerController _pc;
        private float _flashTimer, _bossTimer;

        public void Build(Canvas canvas, Font font)
        {
            _font = font;

            // The HUD container MUST stretch to fill the canvas, otherwise every child anchors to a
            // collapsed rect in the middle of the screen.
            var root = UIKit.Make(canvas.transform, "HUD");
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            _group = root.gameObject;

            var TL = new Vector2(0, 1);
            var BL = new Vector2(0, 0);
            var TC = new Vector2(0.5f, 1);
            var BC = new Vector2(0.5f, 0);

            // ---- Top-left: health + energy bars and run resources ----
            var heart = UIKit.Image(root, Color.white, GameUtil.MakeHeart());
            UIKit.Place((RectTransform)heart.transform, TL, new Vector2(16, -16), new Vector2(22, 22));
            _hpFill = UIKit.Bar(root, TL, new Vector2(44, -17), new Vector2(200, 16), new Color(0.85f, 0.2f, 0.27f));
            _hpText = UIKit.Label(root, "", 14, Color.white, TextAnchor.MiddleLeft, font);
            UIKit.Place((RectTransform)_hpText.transform, TL, new Vector2(252, -17), new Vector2(140, 16));

            _enFill = UIKit.Bar(root, TL, new Vector2(44, -40), new Vector2(200, 12), new Color(0.2f, 0.55f, 0.95f));
            _enText = UIKit.Label(root, "", 12, new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleLeft, font);
            UIKit.Place((RectTransform)_enText.transform, TL, new Vector2(252, -40), new Vector2(140, 12));

            _infoText = UIKit.Label(root, "", 16, new Color(1f, 0.92f, 0.5f), TextAnchor.UpperLeft, font);
            UIKit.Place((RectTransform)_infoText.transform, TL, new Vector2(16, -62), new Vector2(420, 22));

            _weaponText = UIKit.Label(root, "", 16, Color.white, TextAnchor.UpperLeft, font);
            UIKit.Place((RectTransform)_weaponText.transform, TL, new Vector2(16, -86), new Vector2(420, 22));

            // ---- Bottom-left: static controls hint ----
            var hint = UIKit.Label(root,
                "WASD move   ·   Mouse aim / fire\nSpace dodge   ·   Q switch weapon\nE skill   ·   F interact   ·   Esc pause",
                13, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerLeft, font);
            UIKit.Place((RectTransform)hint.transform, BL, new Vector2(16, 14), new Vector2(460, 64));

            // ---- Top-center: floor + boss banners ----
            _floorText = UIKit.Label(root, "", 20, Color.white, TextAnchor.UpperCenter, font);
            UIKit.Place((RectTransform)_floorText.transform, TC, new Vector2(0, -16), new Vector2(520, 26));
            _bossText = UIKit.Label(root, "", 24, new Color(1f, 0.35f, 0.35f), TextAnchor.UpperCenter, font);
            UIKit.Place((RectTransform)_bossText.transform, TC, new Vector2(0, -46), new Vector2(600, 28));

            // ---- Center: transient flash ----
            _flashText = UIKit.Label(root, "", 32, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter, font);
            UIKit.Place((RectTransform)_flashText.transform, new Vector2(0.5f, 0.74f), Vector2.zero, new Vector2(800, 56));

            // ---- Bottom-center: interact prompt ----
            _promptText = UIKit.Label(root, "", 18, new Color(0.9f, 1f, 0.9f), TextAnchor.LowerCenter, font);
            UIKit.Place((RectTransform)_promptText.transform, BC, new Vector2(0, 26), new Vector2(620, 24));

            BuildPausePanel(root);
            Hide();
        }

        /// <summary>Pause menu: dim + title + Resume / Back-to-Menu buttons (works at timeScale 0).</summary>
        private void BuildPausePanel(RectTransform root)
        {
            var panel = UIKit.Panel(root, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.72f));
            panel.name = "PausePanel";
            _pausePanel = panel.gameObject;

            var title = UIKit.Label(panel, "PAUSED", 48, Color.white, TextAnchor.MiddleCenter, _font);
            UIKit.Place((RectTransform)title.transform, new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(400, 70));

            UIKit.Button(panel, "Resume", _font, () => GameManager.I.SetPaused(false),
                new Vector2(260, 54), new Vector2(0, 10), new Vector2(0.5f, 0.5f));

            UIKit.Button(panel, "Back to Menu", _font, () => GameManager.I.AbortRun(),
                new Vector2(260, 54), new Vector2(0, -60), new Vector2(0.5f, 0.5f));

            var hint = UIKit.Label(panel, "Esc to resume · Back to Menu ends this run (gems are kept)",
                14, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter, _font);
            UIKit.Place((RectTransform)hint.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -118), new Vector2(700, 24));

            _pausePanel.SetActive(false);
        }

        public void Bind(GameManager gm, PlayerController pc)
        {
            _gm = gm;
            _pc = pc;
            _group.SetActive(true);
            _pausePanel.SetActive(false);

            pc.Health.Changed += RefreshHealth;
            pc.Energy.Changed += RefreshEnergy;
            pc.Weapons.WeaponChanged += RefreshWeapon;
            gm.Run.Changed += RefreshInfo;

            RefreshHealth(); RefreshEnergy(); RefreshWeapon(); RefreshInfo();
        }

        public void Hide() { if (_group != null) _group.SetActive(false); }

        public void SetFloor(int floor, string theme)
        {
            _floorText.text = $"Floor {floor} — {theme}";
        }

        public void Flash(string msg)
        {
            _flashText.text = msg;
            _flashTimer = 2f;
        }

        public void ShowBoss(string name)
        {
            _bossText.text = $"⚔ {name} ⚔";
            _bossTimer = 3f;
        }

        public void SetPaused(bool paused)
        {
            if (_pausePanel != null) _pausePanel.SetActive(paused);
        }

        private GameObject _floorClearPanel;

        /// <summary>Milestone dialog shown after clearing Floor 1: keep playing or return to the Hub.
        /// The game is frozen (timeScale 0) while it is open; buttons work on unscaled time.</summary>
        public void ShowFloorCleared(bool canReturnHub, System.Action onContinue, System.Action onReturnHub)
        {
            if (_floorClearPanel != null) Destroy(_floorClearPanel);

            var panel = UIKit.Panel((RectTransform)_group.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.78f));
            panel.name = "FloorClearedPanel";
            _floorClearPanel = panel.gameObject;

            var title = UIKit.Label(panel, "FLOOR 1 CLEARED!", 44, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter, _font);
            UIKit.Place((RectTransform)title.transform, new Vector2(0.5f, 0.70f), Vector2.zero, new Vector2(800, 60));

            var msg = UIKit.Label(panel,
                "Main Story trial fulfilled — the first depth is conquered.\nKeep delving, or return to the Hub to continue the story.",
                20, Color.white, TextAnchor.MiddleCenter, _font);
            UIKit.Place((RectTransform)msg.transform, new Vector2(0.5f, 0.57f), Vector2.zero, new Vector2(820, 60));

            UIKit.Button(panel, "Keep Playing", _font,
                () => { Destroy(_floorClearPanel); _floorClearPanel = null; onContinue?.Invoke(); },
                new Vector2(260, 54), new Vector2(canReturnHub ? -150 : 0, -40), new Vector2(0.5f, 0.5f));

            if (canReturnHub)
            {
                UIKit.Button(panel, "Return to Hub", _font,
                    () => { Destroy(_floorClearPanel); _floorClearPanel = null; onReturnHub?.Invoke(); },
                    new Vector2(260, 54), new Vector2(150, -40), new Vector2(0.5f, 0.5f));
            }
        }

        private void RefreshHealth()
        {
            if (_pc == null) return;
            _hpFill.fillAmount = _pc.Health.Fraction;
            _hpText.text = $"{Mathf.CeilToInt(_pc.Health.current)}/{Mathf.CeilToInt(_pc.Health.max)}" +
                           (_pc.Health.armor > 0 ? $"  +{Mathf.CeilToInt(_pc.Health.armor)}" : "");
        }

        private void RefreshEnergy()
        {
            if (_pc == null) return;
            _enFill.fillAmount = _pc.Energy.Fraction;
            _enText.text = $"{Mathf.CeilToInt(_pc.Energy.current)}/{Mathf.CeilToInt(_pc.Energy.max)}";
        }

        private void RefreshWeapon()
        {
            if (_pc == null || _pc.Weapons.Current == null) return;
            _weaponText.text = $"[{_pc.Weapons.Index + 1}/{_pc.Weapons.Weapons.Count}] {_pc.Weapons.Current.name}";
        }

        private void RefreshInfo()
        {
            if (_gm == null || _gm.Run == null) return;
            _infoText.text = $"Gold {_gm.Run.Coins}   Keys {_gm.Run.Keys}   Gems {_gm.Run.Gems}";
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.unscaledDeltaTime;
                var c = _flashText.color; c.a = Mathf.Clamp01(_flashTimer); _flashText.color = c;
                if (_flashTimer <= 0f) _flashText.text = "";
            }
            if (_bossTimer > 0f)
            {
                _bossTimer -= Time.unscaledDeltaTime;
                if (_bossTimer <= 0f) _bossText.text = "";
            }

            if (_pc != null && !_pc.Health.Dead)
            {
                var it = _pc.NearestInteractable();
                _promptText.text = it != null ? $"[F] {it.Prompt}" : "";
            }
            else _promptText.text = "";
        }
    }
}
