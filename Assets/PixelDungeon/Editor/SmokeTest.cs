#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelDungeon.EditorTools
{
    /// <summary>
    /// Headless verification of the embedded mini-game INSIDE Echoes of Celestia:
    /// menu (with Return-to-Hub) → run → pause menu → abort to menu → ReturnToHub →
    /// Hub loads, portal installs on the com-station, and globals (2D gravity) are restored.
    /// Run in batch with: -executeMethod PixelDungeon.EditorTools.SmokeTest.Run  (no -quit).
    /// </summary>
    public static class SmokeTest
    {
        private const string Flag = "pd_eoc_smoke";

        /// <summary>Finds the PixelDungeon scene wherever it lives (it may be moved/organized).</summary>
        public static string FindScenePath()
        {
            foreach (var guid in AssetDatabase.FindAssets("PixelDungeon t:Scene"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/PixelDungeon.unity")) return path;
            }
            return null;
        }

        [MenuItem("Tools/Pixel Dungeon/Smoke Test (Play)")]
        public static void Run()
        {
            var scenePath = FindScenePath();
            if (scenePath == null) { Debug.LogError("[PixelDungeon] PixelDungeon.unity not found."); EditorApplication.Exit(7); return; }

            // Plant "fulfilled" trial flags so the driver can verify the per-Play story reset wipes them.
            PlayerPrefs.SetInt("ec_plane_clear_Level1", 1);
            PlayerPrefs.SetInt("ec_story_dungeon_floor1", 1);
            PlayerPrefs.SetString("ec_story_sentinels", "testA;testB");
            PlayerPrefs.Save();

            EditorSceneManager.OpenScene(scenePath);
            EditorPrefs.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnPlay()
        {
            if (!EditorPrefs.GetBool(Flag, false)) return;
            EditorPrefs.DeleteKey(Flag);
            var go = new GameObject("SmokeDriver");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<SmokeDriver>();
        }
    }

    public class SmokeDriver : MonoBehaviour
    {
        private int _errors;                 // errors that fail the test
        private int _externalErrors;         // host-scene errors not from our code (reported, not failing)
        private bool _externalPhase;         // true while in Hub / plane scenes (third-party code running)
        private readonly StringBuilder _log = new();

        private void Awake() => Application.logMessageReceived += OnLog;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            bool ours = condition.Contains("PixelDungeon") || stackTrace.Contains("PixelDungeon")
                     || condition.Contains("MainStory") || stackTrace.Contains("MainStory")
                     || condition.Contains("PlaneRecords") || stackTrace.Contains("PlaneRecords")
                     || stackTrace.Contains("HubReturnPosition");
            if (_externalPhase && !ours)
            {
                _externalErrors++;
                if (_log.Length < 6000) _log.AppendLine($"[external {type}] {condition}");
                return;
            }
            _errors++;
            if (_log.Length < 6000) _log.AppendLine($"[{type}] {condition}");
        }

        private IEnumerator Start()
        {
            yield return Wait(10);

            // The per-Play story reset must have wiped the trial flags planted before play.
            bool resetOk = MainStoryFlow.PlaneClearCount == 0
                        && MainStoryFlow.SentinelsDownCount == 0
                        && !MainStoryFlow.DungeonFloor1Cleared
                        && MainStoryFlow.CurrentStepIndex == 0;
            _log.AppendLine($"ResetOnPlay: ok={resetOk} planes={MainStoryFlow.PlaneClearCount} " +
                            $"sentinels={MainStoryFlow.SentinelsDownCount} dungeon={MainStoryFlow.DungeonFloor1Cleared} step={MainStoryFlow.CurrentStepIndex}");
            if (!resetOk) _errors++;

            int guard = 0;
            while (GameManager.I == null && guard++ < 600) yield return null;
            if (GameManager.I == null) { Finish("GameManager never initialized"); yield break; }
            var gm = GameManager.I;

            // --- Menu: Return-to-Hub button present? ---
            yield return Wait(5);
            var menu = GameObject.Find("MainMenu");
            bool hubButton = menu != null && menu.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                .Any(t => t.text.Contains("Return to Hub"));
            _log.AppendLine($"Menu: found={menu != null} hubButton={hubButton} canReturn={GameManager.CanReturnToHub}");

            // --- Run ---
            gm.StartRun(GameContent.Hero("mage"));
            yield return Wait(40);
            if (gm.Player == null || gm.Player.Health == null) { Finish("Player not created"); yield break; }

            // --- Pause menu ---
            gm.SetPaused(true);
            yield return null;
            var pausePanel = GameObject.Find("PausePanel");
            _log.AppendLine($"Pause: timeScale={Time.timeScale} panel={(pausePanel != null && pausePanel.activeInHierarchy)}");
            gm.SetPaused(false);
            yield return null;
            _log.AppendLine($"Resume: timeScale={Time.timeScale}");

            // --- Splitter-killed-by-projectile regression (spawn happens inside OnTriggerEnter2D) ---
            var startRoom = FindObjectsOfType<Room>().FirstOrDefault(r => r.Type == RoomType.Start);
            if (startRoom != null)
            {
                var jelly = startRoom.SpawnEnemy("jelly", gm.Player.MuzzleOrigin + Vector2.right * 2f);
                yield return Wait(3);
                if (jelly != null)
                {
                    jelly.Health.current = 1f;
                    for (int i = 0; i < 12 && jelly != null && !jelly.Health.Dead; i++)
                    {
                        gm.Player.Weapons.TryFire(Vector2.right, gm.Player.MuzzleOrigin);
                        yield return Wait(4);
                    }
                    yield return Wait(20);
                    _log.AppendLine($"Splitter: dead={(jelly == null || jelly.Health.Dead)} children={FindObjectsOfType<Enemy>().Count(e => e.Def.id == "jellysmall")}");
                }
            }

            // --- Tour all rooms (waves + boss) ---
            var rooms = FindObjectsOfType<Room>();
            _log.AppendLine($"Rooms: {rooms.Length}");
            foreach (var room in rooms)
            {
                if (gm.Player == null || gm.Player.Health.Dead) break;
                gm.Player.transform.position = room.Center;
                Physics2D.SyncTransforms();
                yield return Wait(30);
                for (int i = 0; i < 6 && gm.Player != null && !gm.Player.Health.Dead; i++)
                {
                    gm.Player.Weapons.TryFire(Vector2.right, gm.Player.MuzzleOrigin);
                    yield return Wait(2);
                }
            }

            // --- Floor-1-cleared milestone dialog (Main Story trial) ---
            if (gm.IsPlaying && gm.Player != null && !gm.Player.Health.Dead)
            {
                int prevBest = PixelDungeon.MetaProgress.BestFloor;
                gm.NextFloor();
                yield return Wait(10);
                var dlg = GameObject.Find("FloorClearedPanel");
                bool paused0 = Mathf.Approximately(Time.timeScale, 0f);
                _log.AppendLine($"FloorCleared: dialog={(dlg != null)} timeScale0={paused0} bestFloor={PixelDungeon.MetaProgress.BestFloor} (was {prevBest})");
                if (dlg == null || !paused0 || PixelDungeon.MetaProgress.BestFloor < 2) _errors++;
                if (dlg != null)
                {
                    // First button = "Keep Playing"
                    var btn = dlg.GetComponentInChildren<UnityEngine.UI.Button>(true);
                    btn?.onClick.Invoke();
                    yield return Wait(3);
                    _log.AppendLine($"FloorCleared resume: timeScale={Time.timeScale}");
                    if (!Mathf.Approximately(Time.timeScale, 1f)) _errors++;
                }
            }

            // --- Mid-run abort back to the hero-select menu ---
            if (gm.IsPlaying)
            {
                gm.SetPaused(true);
                yield return Wait(2);
                gm.AbortRun();
                yield return Wait(5);
                var menuAgain = GameObject.Find("MainMenu");
                _log.AppendLine($"AbortRun: menuVisible={menuAgain != null} timeScale={Time.timeScale}");
                if (menuAgain == null) _errors++;
            }
            else
            {
                _log.AppendLine("AbortRun skipped (run already over)");
            }

            // --- Return to Hub ---
            if (!GameManager.CanReturnToHub)
            {
                Finish("Hub scene not loadable");
                yield break;
            }
            _externalPhase = true;
            gm.ReturnToHub();
            yield return Wait(60);

            var active = SceneManager.GetActiveScene().name;
            _log.AppendLine($"Hub: activeScene={active} gravity={Physics2D.gravity} cursorVisible={Cursor.visible}");
            if (active != "Hub") _errors++;
            if (Physics2D.gravity == Vector2.zero) { _errors++; _log.AppendLine("Gravity NOT restored!"); }

            // Portal installer needs a few frames.
            yield return Wait(120);
            var portal = GameObject.Find("PixelDungeonPortal");
            var station = GameObject.Find("P_Base_ComStation_A");
            _log.AppendLine($"Portal: station={(station != null)} portal={(portal != null)}");
            if (portal == null) _errors++;

            // --- Return-position keeper: stand near the station, enter PD, come back ---
            yield return ReturnPositionRoundTrip(station);

            // --- Main Story HUD: start story mode (reflection) and verify objective + guide ---
            yield return StoryHudChecks();

            // --- Plane records: real Level1 scene, trigger a victory, verify it is recorded ---
            yield return PlaneRecordsCheck();

            Finish(_errors == 0 ? "OK" : "errors logged");
        }

        private IEnumerator ReturnPositionRoundTrip(GameObject station)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo == null || station == null)
            {
                _log.AppendLine("ReturnPos: SKIPPED (player or station missing)");
                _errors++;
                yield break;
            }

            // Teleport the player next to the com-station (simulates walking there).
            var p = playerGo.transform;
            var target = station.transform.position + new Vector3(4f, 0f, 4f);
            target.y = p.position.y;
            var cc = playerGo.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            p.position = target;
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
            yield return Wait(8);   // let the keeper record the pose

            SceneManager.LoadScene("PixelDungeon", LoadSceneMode.Single);
            yield return Wait(50);  // PD boots

            int guard = 0;
            while (GameManager.I == null && guard++ < 300) yield return null;
            if (GameManager.I == null) { _errors++; _log.AppendLine("ReturnPos: PD did not boot"); yield break; }
            GameManager.I.ReturnToHub();
            yield return Wait(150); // Hub reloads + keeper restores

            playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo == null) { _errors++; _log.AppendLine("ReturnPos: player missing after return"); yield break; }
            var dxz = Vector2.Distance(
                new Vector2(playerGo.transform.position.x, playerGo.transform.position.z),
                new Vector2(target.x, target.z));
            _log.AppendLine($"ReturnPos: dist={dxz:0.00}m (target {target}, actual {playerGo.transform.position})");
            if (dxz > 3f) _errors++;

            // Portal must also re-install on this second Hub load.
            yield return Wait(60);
            if (GameObject.Find("PixelDungeonPortal") == null) { _errors++; _log.AppendLine("ReturnPos: portal missing on 2nd Hub load"); }
        }

        private IEnumerator StoryHudChecks()
        {
            var ui = FindObjectOfType<MainStoryQuestUI>();
            if (ui == null) { _errors++; _log.AppendLine("StoryHud: MainStoryQuestUI missing"); yield break; }

            var t = typeof(MainStoryQuestUI);
            var start = t.GetMethod("StartStoryMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            start?.Invoke(ui, null);
            yield return Wait(5);

            bool active = MainStoryQuestUI.StoryModeActive;
            var objRoot = t.GetField("_objectiveRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ui) as GameObject;
            var guide = t.GetField("_guide", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ui) as MainStoryPathGuide;
            yield return Wait(40);  // a poll tick passes (0.5s)

            string objText = "";
            if (objRoot != null)
            {
                var txt = objRoot.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (txt != null) objText = txt.text;
            }
            _log.AppendLine($"StoryHud: modeActive={active} objectiveShown={(objRoot != null && objRoot.activeSelf)} " +
                            $"objective=\"{objText}\" guideTarget={(guide != null && guide.Target != null ? guide.Target.name : "null")} " +
                            $"step={MainStoryFlow.CurrentStepIndex}/{MainStoryFlow.TotalSteps}");
            if (!active || objRoot == null || !objRoot.activeSelf) _errors++;
            if (guide == null || (guide.Target == null && !MainStoryFlow.JourneyComplete)) _errors++;

            var quit = t.GetMethod("QuitStoryMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            quit?.Invoke(ui, null);
        }

        private IEnumerator PlaneRecordsCheck()
        {
            // Snapshot the plane prefs this check will touch, then restore afterwards.
            string[] intKeys = { "ec_plane_clear_Level1", "ec_plane_wins_Level1", "ec_plane_best_Level1" };
            var snap = new System.Collections.Generic.Dictionary<string, int?>();
            foreach (var k in intKeys) snap[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : (int?)null;
            string histSnap = PlayerPrefs.HasKey("ec_plane_history") ? PlayerPrefs.GetString("ec_plane_history") : null;
            foreach (var k in intKeys) PlayerPrefs.DeleteKey(k);

            SceneManager.LoadScene("Level1", LoadSceneMode.Single);
            yield return Wait(90);

            var gmType = System.Type.GetType("GameManager, Assembly-CSharp");
            var inst = gmType?.GetField("instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null) as MonoBehaviour;
            if (inst == null)
            {
                _log.AppendLine("PlaneRecords: plane GameManager not found — SKIP");
                _errors++;
            }
            else
            {
                gmType.GetMethod("LevelCleared")?.Invoke(inst, null);   // simulate a win
                yield return Wait(60);
                bool cleared = MainStoryFlow.IsPlaneLevelCleared("Level1");
                var hist = EchoesOfCelestia.Plane2D.PlaneRecords.History();
                _log.AppendLine($"PlaneRecords: cleared={cleared} historyCount={hist.Length} latest=\"{(hist.Length > 0 ? hist[0] : "")}\"");
                if (!cleared || hist.Length == 0) _errors++;
            }

            // Restore prefs.
            foreach (var kv in snap)
            {
                if (kv.Value.HasValue) PlayerPrefs.SetInt(kv.Key, kv.Value.Value);
                else PlayerPrefs.DeleteKey(kv.Key);
            }
            if (histSnap != null) PlayerPrefs.SetString("ec_plane_history", histSnap);
            else PlayerPrefs.DeleteKey("ec_plane_history");
            PlayerPrefs.Save();
        }

        private static IEnumerator Wait(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        private void Finish(string status)
        {
            Application.logMessageReceived -= OnLog;
            Debug.Log($"=== SMOKE TEST RESULT: {(status == "OK" ? "PASS" : "FAIL")} ({status}) — errors={_errors} externalErrors={_externalErrors} ===\n{_log}");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(_errors == 0 && status == "OK" ? 0 : 7);
        }
    }
}
#endif
