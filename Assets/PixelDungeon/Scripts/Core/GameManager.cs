using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelDungeon
{
    /// <summary>Per-run economy (design doc 3.7): coins & keys are spent in-run; gems carry to meta.</summary>
    public class RunState
    {
        public int Coins, Keys, Gems;
        public event System.Action Changed;

        public void AddCoins(int n) { Coins += n; Changed?.Invoke(); }
        public bool SpendCoins(int n) { if (Coins < n) return false; Coins -= n; Changed?.Invoke(); return true; }
        public void AddKeys(int n) { Keys += n; Changed?.Invoke(); }
        public bool SpendKey() { if (Keys < 1) return false; Keys--; Changed?.Invoke(); return true; }
        public void AddGems(int n) { Gems += n; Changed?.Invoke(); }
    }

    /// <summary>
    /// Top-level run flow: menu -> build floor -> clear rooms -> beat boss -> descend -> ... ->
    /// victory/death -> meta unlock -> menu. Embedded-host extras: Esc opens a pause menu whose
    /// "Back to Menu" aborts the run, and the main menu offers "Return to Hub" when a Hub scene
    /// exists in the build (i.e. when running inside Echoes of Celestia).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        /// <summary>Scene名称:与 Echoes of Celestia 的 SceneNames.Hub 保持一致。</summary>
        public const string HubSceneName = "Hub";

        public GameDatabase Db { get; private set; }
        public PlayerController Player { get; private set; }
        public RunState Run { get; private set; }
        public int Floor { get; private set; }
        public int MaxFloors = 3;

        private Camera _cam;
        private CameraFollow _follow;
        private HudUI _hud;
        private ScreensUI _screens;
        private AudioSource _music;

        private HeroDef _hero;
        private Dungeon _dungeon;
        private Room _currentRoom;
        private bool _paused, _playing, _modal;

        public bool IsPlaying => _playing;
        public static bool CanReturnToHub => Application.CanStreamedLevelBeLoaded(HubSceneName);

        public void Init(GameDatabase db, Camera cam, CameraFollow follow, HudUI hud, ScreensUI screens, AudioSource music)
        {
            I = this;
            Db = db;
            _cam = cam;
            _follow = follow;
            _hud = hud;
            _screens = screens;
            _music = music;
            ShowMenu();
        }

        private void ShowMenu()
        {
            _playing = false;
            Time.timeScale = 1f;
            _hud.Hide();
            _screens.ShowMenu(StartRun);
        }

        public void StartRun(HeroDef hero)
        {
            _hero = hero;
            Floor = 0;
            Run = new RunState();
            Time.timeScale = 1f;
            _paused = false;
            Juice.ExternalPause = false;
            _playing = true;

            _screens.HideAll();
            BuildFloor();

            _hud.Bind(this, Player);
            if (_music != null && Db.bgm != null) { _music.clip = Db.bgm; _music.loop = true; _music.volume = 0.4f; _music.Play(); }
        }

        private void BuildFloor()
        {
            if (_dungeon != null && _dungeon.Root != null) Destroy(_dungeon.Root.gameObject);

            _dungeon = new DungeonGenerator().Generate(Floor, Db);

            if (Player == null) CreatePlayer();
            Player.transform.position = _dungeon.StartPosition;
            var rb = Player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = Vector2.zero;
            Player.InputEnabled = true;

            _follow.Snap(_dungeon.StartPosition);
            _hud.SetFloor(Floor + 1, Db.GetTheme(Floor)?.name ?? "");
        }

        private void CreatePlayer()
        {
            var go = Instantiate(Db.playerPrefab);
            go.name = "Player";
            var vis = go.AddComponent<CharacterVisual>();
            vis.Bind();
            vis.ApplyHero(_hero);
            var pc = go.AddComponent<PlayerController>();
            pc.Setup(_hero, _cam);
            Player = pc;
            _follow.Target = pc.transform;
        }

        public void NextFloor()
        {
            Floor++;
            // The mini-game's own persistent record (shown in its menu).
            MetaProgress.BestFloor = Floor + 1;
            // The host's Main Story "Pixel Depths" trial — session-scoped, reset on every Play.
            if (Floor >= 1) MainStoryFlow.NotifyDungeonFloor1Cleared();
            if (Floor >= MaxFloors) { Victory(); return; }
            Player.Health.Heal(Player.Health.max * 0.3f);
            BuildFloor();
            _hud.Flash($"Floor {Floor + 1}");

            if (Floor == 1) ShowFloorClearedDialog();
        }

        /// <summary>Modal shown right after clearing Floor 1 (a Main Story trial milestone):
        /// keep playing, or return to the Hub.</summary>
        private void ShowFloorClearedDialog()
        {
            _modal = true;
            Juice.ExternalPause = true;
            Time.timeScale = 0f;
            _hud.ShowFloorCleared(CanReturnToHub,
                onContinue: () =>
                {
                    _modal = false;
                    Juice.ExternalPause = false;
                    Time.timeScale = 1f;
                },
                onReturnHub: () =>
                {
                    _modal = false;
                    ReturnToHub();
                });
        }

        public void OnBossDefeated() => _hud.Flash("Boss defeated! Portal opened.");
        public void AnnounceBoss(string n) => _hud.ShowBoss(n);
        public void OnRoomEntered(Room r) => _currentRoom = r;
        public void SpawnLooseEnemy(string id, Vector2 pos) => _currentRoom?.SpawnEnemy(id, pos);

        public void OnPlayerDied()
        {
            _playing = false;
            MetaProgress.AddGems(Run.Gems);
            MetaProgress.BestFloor = Floor + 1;
            if (_music != null) _music.Stop();
            _screens.ShowResult(false, Floor + 1, Run, Restart);
        }

        private void Victory()
        {
            _playing = false;
            MetaProgress.AddGems(Run.Gems + 10);
            MetaProgress.BestFloor = MaxFloors;
            if (_music != null) _music.Stop();
            _screens.ShowResult(true, MaxFloors, Run, Restart);
        }

        private void Restart()
        {
            if (Player != null) { Destroy(Player.gameObject); Player = null; }
            if (_dungeon != null && _dungeon.Root != null) Destroy(_dungeon.Root.gameObject);
            _dungeon = null;
            ShowMenu();
        }

        // ---------------- Pause / abort / hub (embedded-host controls) ----------------

        public void SetPaused(bool paused)
        {
            if (!_playing || _modal || _paused == paused) return;
            _paused = paused;
            Juice.ExternalPause = paused;
            Time.timeScale = paused ? 0f : 1f;
            _hud.SetPaused(paused);
        }

        /// <summary>Pause-menu "Back to Menu": ends the current run (banking earned gems, like death)
        /// and returns to the hero-select menu.</summary>
        public void AbortRun()
        {
            if (!_playing) return;
            SetPaused(false);
            _playing = false;
            MetaProgress.AddGems(Run.Gems);
            MetaProgress.BestFloor = Floor + 1;
            if (_music != null) _music.Stop();
            Restart();
        }

        /// <summary>Main-menu "Return to Hub": leaves the mini-game scene entirely.</summary>
        public void ReturnToHub()
        {
            if (!CanReturnToHub) return;
            Time.timeScale = 1f;
            Juice.ExternalPause = false;
            SceneManager.LoadScene(HubSceneName, LoadSceneMode.Single);
        }

        private void Update()
        {
            if (_playing && !_modal && Input.GetKeyDown(KeyCode.Escape))
                SetPaused(!_paused);
        }
    }
}
