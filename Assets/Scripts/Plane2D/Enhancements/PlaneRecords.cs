using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoesOfCelestia.Plane2D
{
    /// <summary>
    /// Battle-record store for the plane shooter. Persists, per level: cleared flag, win count,
    /// best score — plus a rolling history of the latest runs. Wins also feed the Main Story
    /// "Sky Assault" trial via MainStoryFlow. Pure additive module (runtime injection).
    /// </summary>
    public static class PlaneRecords
    {
        const string BestPrefix  = "ec_plane_best_";
        const string WinsPrefix  = "ec_plane_wins_";
        const string HistoryKey  = "ec_plane_history";
        const int    MaxHistory  = 10;

        public static int BestScore(string scene) => PlayerPrefs.GetInt(BestPrefix + scene, 0);
        public static int WinCount(string scene) => PlayerPrefs.GetInt(WinsPrefix + scene, 0);

        /// <summary>Lifetime record (battle history is persistent). The Main Story trial flag is
        /// session-scoped and reset on every Play — use MainStoryFlow for story state.</summary>
        public static bool IsCleared(string scene) => WinCount(scene) > 0;

        /// <summary>Latest-first history lines: "scene|WIN/LOSS|score|yyyy-MM-dd HH:mm".</summary>
        public static string[] History()
        {
            var raw = PlayerPrefs.GetString(HistoryKey, "");
            return string.IsNullOrEmpty(raw)
                ? Array.Empty<string>()
                : raw.Split('\n').Where(l => !string.IsNullOrEmpty(l)).ToArray();
        }

        public static void Record(string scene, bool win, int score)
        {
            if (win)
            {
                PlayerPrefs.SetInt(WinsPrefix + scene, WinCount(scene) + 1);
                if (score > BestScore(scene)) PlayerPrefs.SetInt(BestPrefix + scene, score);
            }

            var entry = $"{scene}|{(win ? "WIN" : "LOSS")}|{score}|{DateTime.Now:yyyy-MM-dd HH:mm}";
            var list = new List<string> { entry };
            list.AddRange(History());
            if (list.Count > MaxHistory) list.RemoveRange(MaxHistory, list.Count - MaxHistory);
            PlayerPrefs.SetString(HistoryKey, string.Join("\n", list));
            PlayerPrefs.Save();

            if (win) MainStoryFlow.NotifyPlaneLevelCleared(scene);   // also saves prefs + raises Changed
        }
    }

    /// <summary>
    /// Injected into the plane-shooter level scenes. Detects the run outcome once per visit
    /// (victory page shown → WIN; gameIsOver → LOSS), records it, and offers a Battle Records
    /// panel on the H key. Does not modify any original plane-game script.
    /// </summary>
    public sealed class PlaneOutcomeWatcher : MonoBehaviour
    {
        static readonly string[] LevelScenes = { "Level1", "Level2" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInject(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInject(scene);

        static void TryInject(Scene scene)
        {
            if (!scene.IsValid() || Array.IndexOf(LevelScenes, scene.name) < 0) return;
            if (FindObjectOfType<PlaneOutcomeWatcher>() != null) return;
            new GameObject("PlaneOutcomeWatcher").AddComponent<PlaneOutcomeWatcher>();
        }

        bool _recorded;
        string _sceneName;
        GameObject _panel;
        Text _panelText;

        void Start()
        {
            _sceneName = SceneManager.GetActiveScene().name;
            BuildHint();
            BuildPanel();
        }

        void Update()
        {
            if (!_recorded) DetectOutcome();

            if (Input.GetKeyDown(KeyCode.H) && _panel != null)
            {
                bool show = !_panel.activeSelf;
                if (show) RefreshPanel();
                _panel.SetActive(show);
            }
        }

        void DetectOutcome()
        {
            var gm = GameManager.instance;
            if (gm == null) return;

            if (gm.gameIsOver)
            {
                _recorded = true;
                PlaneRecords.Record(_sceneName, false, GameManager.score);
                return;
            }

            // Victory: LevelCleared() disables pausing and shows the victory page while
            // gameIsOver stays false — that combination is unique to a win.
            var ui = UIManager.instance;
            if (ui == null || ui.allowPause || ui.pages == null) return;
            int v = gm.gameVictoryPageIndex;
            if (v < 0 || v >= ui.pages.Count || ui.pages[v] == null) return;
            if (!ui.pages[v].gameObject.activeInHierarchy) return;

            _recorded = true;
            PlaneRecords.Record(_sceneName, true, GameManager.score);
        }

        // ── UI ────────────────────────────────────────────────────────────────

        Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static Text MakeText(Transform parent, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            return t;
        }

        void BuildHint()
        {
            var canvas = MakeCanvas("PlaneRecordsHint", 32759);
            var bg = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bg.transform.SetParent(canvas.transform, false);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -86f);
            rt.sizeDelta = new Vector2(260f, 40f);
            bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var t = MakeText(bg.transform, 20, new Color(1f, 0.84f, 0.32f), TextAnchor.MiddleCenter);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            t.text = "H: Battle Records";
        }

        void BuildPanel()
        {
            var canvas = MakeCanvas("PlaneRecordsPanel", 32762);
            _panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panel.transform.SetParent(canvas.transform, false);
            var rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(640f, 560f);
            _panel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.09f, 0.96f);

            var title = MakeText(_panel.transform, 30, new Color(1f, 0.84f, 0.32f), TextAnchor.UpperCenter);
            var tRt = title.rectTransform;
            tRt.anchorMin = new Vector2(0f, 1f); tRt.anchorMax = new Vector2(1f, 1f);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = new Vector2(0f, -20f);
            tRt.sizeDelta = new Vector2(0f, 40f);
            title.text = "BATTLE RECORDS";
            title.fontStyle = FontStyle.Bold;

            _panelText = MakeText(_panel.transform, 20, new Color(0.9f, 0.92f, 0.95f), TextAnchor.UpperLeft);
            var pRt = _panelText.rectTransform;
            pRt.anchorMin = new Vector2(0f, 0f); pRt.anchorMax = new Vector2(1f, 1f);
            pRt.offsetMin = new Vector2(36f, 24f);
            pRt.offsetMax = new Vector2(-36f, -72f);
            _panelText.supportRichText = true;

            _panel.SetActive(false);
        }

        void RefreshPanel()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var lvl in LevelScenes)
            {
                string status = PlaneRecords.IsCleared(lvl) ? "<color=#7CFC9A>CLEARED</color>" : "<color=#888888>not cleared</color>";
                sb.AppendLine($"<b>{PrettyLevel(lvl)}</b>   {status}   Wins: {PlaneRecords.WinCount(lvl)}   Best: {PlaneRecords.BestScore(lvl)}");
            }
            sb.AppendLine();
            sb.AppendLine("<b>Recent Runs</b>");
            var hist = PlaneRecords.History();
            if (hist.Length == 0) sb.AppendLine("<color=#888888>No runs recorded yet.</color>");
            foreach (var line in hist)
            {
                var p = line.Split('|');
                if (p.Length < 4) continue;
                string col = p[1] == "WIN" ? "#7CFC9A" : "#FF7B6B";
                sb.AppendLine($"{p[3]}   {PrettyLevel(p[0]),-9}   <color={col}>{p[1]}</color>   score {p[2]}");
            }
            _panelText.text = sb.ToString();
        }

        static string PrettyLevel(string scene) => scene == "Level1" ? "Level 1" : scene == "Level2" ? "Level 2" : scene;
    }
}
