using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// Scene entry point for the embedded Pixel Dungeon mini-game. Loads the generated GameDatabase
    /// and constructs all runtime systems (camera, juice, combat pools, audio, UI, game manager)
    /// from code, so the playable scene only needs this one component.
    ///
    /// Because this runs inside the Echoes of Celestia host project, every global it touches
    /// (2D gravity, target frame rate, time scale, cursor) is saved on entry and restored in
    /// OnDestroy — i.e. whenever the player leaves back to the Hub — so the host game and the
    /// other mini-games keep working untouched.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        private Vector2 _prevGravity2D;
        private int _prevTargetFrameRate;
        private bool _started;

        private void Start()
        {
            var db = Resources.Load<GameDatabase>("GameDatabase");
            if (db == null)
            {
                Debug.LogError("[PixelDungeon] GameDatabase not found in Resources. " +
                               "Run the menu command: Tools ▸ Pixel Dungeon ▸ Build Game Assets.");
                return;
            }

            // --- Take over global state (restored in OnDestroy) ---
            _prevGravity2D = Physics2D.gravity;
            _prevTargetFrameRate = Application.targetFrameRate;
            _started = true;

            Physics2D.gravity = Vector2.zero;             // top-down: no falling
            Application.targetFrameRate = 60;
            Pickup.SetDatabase(db);

            // Camera
            var cam = Camera.main;
            if (cam == null)
            {
                var cg = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cg.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 6.2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            if (Mathf.Abs(cam.transform.position.z) < 1f) cam.transform.position = new Vector3(0f, 0f, -10f);
            var follow = cam.GetComponent<CameraFollow>() ?? cam.gameObject.AddComponent<CameraFollow>();

            // URP needs a global 2D light; under the host's Built-in pipeline this no-ops.
            GameUtil.TryAddGlobalLight2D();

            // Feel
            var juice = new GameObject("Juice").AddComponent<Juice>();
            juice.Init(cam);
            Juice.ExternalPause = false;

            // Audio
            var sfx = new GameObject("SFX").AddComponent<AudioSource>();
            var music = new GameObject("Music").AddComponent<AudioSource>();

            // Combat (pools)
            var combat = new GameObject("Combat").AddComponent<CombatService>();
            combat.Init(db, sfx);

            // UI
            var canvas = UIKit.CreateCanvas();
            var hud = canvas.gameObject.AddComponent<HudUI>();
            hud.Build(canvas, db.font);
            var screens = canvas.gameObject.AddComponent<ScreensUI>();
            screens.Build(canvas, db.font);

            // Game flow
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            gm.Init(db, cam, follow, hud, screens, music);
        }

        private void Update()
        {
            // The 3D Hub locks/hides the cursor; this 2D game needs it visible the whole time
            // (same approach as the plane shooter's PlaneSceneCursorGuard).
            if (!_started) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            // Scene unload (back to Hub): hand every global back exactly as we found it.
            if (!_started) return;
            Physics2D.gravity = _prevGravity2D;
            Application.targetFrameRate = _prevTargetFrameRate;
            Time.timeScale = 1f;
            Juice.ExternalPause = false;
        }
    }
}
