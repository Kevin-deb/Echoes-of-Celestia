using System;
using UnityEngine;
using UnityEngine.UI;

namespace PixelDungeon
{
    /// <summary>Front-end screens: title + hero select (with meta unlocks) and the run result
    /// screen. When hosted inside Echoes of Celestia (a "Hub" scene exists in the build), the
    /// main menu shows a "Return to Hub" button and Esc on the menu also returns to the Hub.</summary>
    public class ScreensUI : MonoBehaviour
    {
        private Canvas _canvas;
        private Font _font;
        private RectTransform _menu, _result;
        private Action<HeroDef> _onStart;

        public void Build(Canvas canvas, Font font)
        {
            _canvas = canvas;
            _font = font;
        }

        public void HideAll()
        {
            if (_menu != null) Destroy(_menu.gameObject);
            if (_result != null) Destroy(_result.gameObject);
            _menu = _result = null;
        }

        // ---------------- Menu ----------------

        public void ShowMenu(Action<HeroDef> onStart)
        {
            _onStart = onStart;
            HideAll();
            _menu = UIKit.Panel(_canvas.transform, Vector2.zero, Vector2.one, new Color(0.06f, 0.06f, 0.1f, 1f));
            _menu.name = "MainMenu";
            BuildMenuContent();
        }

        private void BuildMenuContent()
        {
            foreach (Transform c in _menu) Destroy(c.gameObject);

            var TC = new Vector2(0.5f, 1f);
            var BC = new Vector2(0.5f, 0f);

            // ---- Header block: anchored to the TOP of the screen ----
            var title = UIKit.Label(_menu, "PIXEL DUNGEON", 46, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, _font);
            UIKit.Place((RectTransform)title.transform, TC, new Vector2(0, -26), new Vector2(900, 54));

            var sub = UIKit.Label(_menu, "Choose your hero", 22, Color.white, TextAnchor.MiddleCenter, _font);
            UIKit.Place((RectTransform)sub.transform, TC, new Vector2(0, -88), new Vector2(700, 28));

            var meta = UIKit.Label(_menu, $"Gems: {MetaProgress.Gems}    Best floor: {MetaProgress.BestFloor}",
                17, new Color(0.7f, 0.9f, 1f), TextAnchor.MiddleCenter, _font);
            UIKit.Place((RectTransform)meta.transform, TC, new Vector2(0, -120), new Vector2(700, 24));

            // ---- Exit back to the host game (only when a Hub scene is present in the build) ----
            if (GameManager.CanReturnToHub)
            {
                UIKit.Button(_menu, "← Return to Hub", _font, () => GameManager.I.ReturnToHub(),
                    new Vector2(190, 44), new Vector2(16, -16), new Vector2(0f, 1f));
            }

            // ---- Hero cards: anchored to the BOTTOM, so they can never ride up into the
            // header text no matter how short/wide the Game view is. ----
            var heroes = GameContent.Heroes;
            const float cardW = 150f, cardH = 150f, gap = 10f;
            float pitch = cardW + gap;
            float startX = -(heroes.Length - 1) * 0.5f * pitch;
            for (int i = 0; i < heroes.Length; i++)
            {
                var hero = heroes[i];
                bool unlocked = MetaProgress.IsHeroUnlocked(hero.id);
                string label = unlocked
                    ? $"{hero.name}\n<{hero.passive}>\nHP {hero.maxHp:0}  EN {hero.maxEnergy:0}"
                    : $"{hero.name}\n[LOCKED]\n{hero.unlockCost} gems to unlock";

                var btn = UIKit.Button(_menu, label, _font, () => OnHeroClicked(hero),
                    new Vector2(cardW, cardH), new Vector2(startX + i * pitch, 92), BC);
                var img = btn.GetComponent<Image>();
                img.color = unlocked ? new Color(0.2f, 0.28f, 0.36f, 0.96f) : new Color(0.18f, 0.14f, 0.16f, 0.96f);
                var txt = btn.GetComponentInChildren<Text>();
                txt.fontSize = 14;
            }

            string hintText = GameManager.CanReturnToHub
                ? "Unlock heroes with gems earned from runs.  ·  Esc: return to Hub"
                : "Unlock heroes with gems earned from runs.";
            var hint = UIKit.Label(_menu, hintText, 14, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, _font);
            UIKit.Place((RectTransform)hint.transform, BC, new Vector2(0, 58), new Vector2(760, 24));
        }

        private void OnHeroClicked(HeroDef hero)
        {
            if (MetaProgress.IsHeroUnlocked(hero.id)) _onStart?.Invoke(hero);
            else if (MetaProgress.TryUnlockHero(hero.id, hero.unlockCost)) BuildMenuContent();
        }

        private void Update()
        {
            // On the main menu, Esc backs out to the Hub (mirrors the plane shooter's behavior).
            if (_menu != null && GameManager.CanReturnToHub && Input.GetKeyDown(KeyCode.Escape))
                GameManager.I.ReturnToHub();
        }

        // ---------------- Result ----------------

        public void ShowResult(bool win, int floor, RunState run, Action onContinue)
        {
            HideAll();
            _result = UIKit.Panel(_canvas.transform, Vector2.zero, Vector2.one, new Color(0.05f, 0.05f, 0.08f, 0.96f));

            var title = UIKit.Label(_result, win ? "VICTORY!" : "YOU DIED", 52,
                win ? new Color(1f, 0.85f, 0.3f) : new Color(0.9f, 0.3f, 0.3f), TextAnchor.MiddleCenter, _font);
            UIKit.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(900, 60));

            int gained = win ? run.Gems + 10 : run.Gems;
            var stats = UIKit.Label(_result,
                $"Reached floor {floor}\nGold collected: {run.Coins}\nGems earned: {gained}",
                24, Color.white, TextAnchor.MiddleCenter, _font);
            UIKit.Place((RectTransform)stats.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(700, 140));

            UIKit.Button(_result, "Continue", _font, () => onContinue?.Invoke(),
                new Vector2(240, 60), new Vector2(0, 64), new Vector2(0.5f, 0f));
        }
    }
}
