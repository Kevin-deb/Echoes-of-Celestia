using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Main-story quest HUD for the Hub scene (Genshin-style):
/// - A gold diamond + "Main Story" icon in the top-left corner; while story mode is active a
///   gold objective block underneath tells the player exactly what to do next (all English).
/// - Click the icon (or press J) to open the journey window: a 7-step progress track that
///   interleaves the four chronicle volumes with the three playable trials
///   (Sky Assault → Vol I → Silence the Sentinels → Vol II → Pixel Depths → Vol III → Vol IV).
///   Recovered volumes can be re-read by clicking their gold nodes.
/// - Gold ground-dot guidance points at the current objective: a lore object, the space-station
///   portal, the com-station, or the nearest hostile sentinel.
/// - Story mode persists across mini-game round-trips within the session, and transient gold
///   banners announce trial completion (or that a requirement was already fulfilled).
///
/// The UI is created from code at runtime and only exists in the Hub scene.
/// </summary>
public sealed class MainStoryQuestUI : MonoBehaviour
{
    const string HubSceneName = "Hub";

    static readonly Color Gold     = new Color(1f, 0.84f, 0.32f, 1f);
    static readonly Color GoldDim  = new Color(0.45f, 0.40f, 0.24f, 1f);
    static readonly Color Panel    = new Color(0.05f, 0.06f, 0.09f, 0.98f);
    static readonly Color TrackBg  = new Color(0.22f, 0.22f, 0.24f, 1f);
    static readonly Color NodeOff  = new Color(0.3f, 0.3f, 0.32f, 1f);
    static readonly Color Current  = new Color(1f, 0.96f, 0.75f, 1f);

    // Session-persistent so entering a mini-game and returning keeps story mode on.
    // Explicitly cleared on every Play so each test run starts the story from scratch
    // (robust even if domain reload gets disabled in Enter Play Mode options).
    static bool s_storyMode;
    static int  s_lastAnnouncedStep = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetSessionState()
    {
        s_storyMode = false;
        s_lastAnnouncedStep = -1;
    }

    public static bool StoryModeActive => s_storyMode;

    static MainStoryQuestUI _instance;

    // Icon + objective HUD
    GameObject _iconRoot;
    GameObject _objectiveRoot;
    Text       _objectiveTitle;
    Text       _objectiveDetail;

    // Toast banner
    GameObject _toastRoot;
    Text       _toastText;
    float      _toastTimer;

    // Window
    GameObject _windowRoot;
    Text       _summaryText;
    Image      _trackFill;
    Image[]    _nodeDots;
    Text[]     _nodeLabels;
    Button[]   _nodeButtons;
    Text       _modeStatusText;
    bool       _windowOpen;

    // Guidance
    MainStoryPathGuide _guide;
    Transform _planePortal, _comStation;
    float _pollTimer;

    static Sprite s_circleSprite;

    // ── Auto creation (Hub scene only) ────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

    static void TryCreate(Scene scene)
    {
        if (!scene.IsValid() || scene.name != HubSceneName) return;
        if (_instance != null) return;

        var go = new GameObject("MainStoryQuestUI");
        _instance = go.AddComponent<MainStoryQuestUI>();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        // NOTE: story mode intentionally NOT reset here — it survives mini-game round-trips.

        BuildUI();

        var guideGo = new GameObject("MainStoryPathGuide");
        guideGo.transform.SetParent(transform, false);
        _guide = guideGo.AddComponent<MainStoryPathGuide>();
        _guide.Target = null;
    }

    void Start()
    {
        // Sync the announcement cursor; if conditions advanced while we were away
        // (e.g. plane levels cleared), announce it once story HUD is alive.
        if (s_lastAnnouncedStep < 0) s_lastAnnouncedStep = MainStoryFlow.CurrentStepIndex;
        AnnounceAdvance();
        RefreshAll();
    }

    void OnEnable()  { MainStoryFlow.Changed += OnFlowChanged; }
    void OnDisable() { MainStoryFlow.Changed -= OnFlowChanged; }

    void Update()
    {
        HandleCursorReveal();

        if (Input.GetKeyDown(KeyCode.J))
            ToggleWindow();

        if (_windowOpen && Input.GetKeyDown(KeyCode.Escape))
            CloseWindow();

        // Keep the icon hidden while reading lore or while the window is open.
        var iconVisible = !_windowOpen && !LoreReadingUI.IsAnyOpen;
        if (_iconRoot != null && _iconRoot.activeSelf != iconVisible)
            _iconRoot.SetActive(iconVisible);

        var objVisible = iconVisible && s_storyMode;
        if (_objectiveRoot != null && _objectiveRoot.activeSelf != objVisible)
            _objectiveRoot.SetActive(objVisible);

        // Toast fade-out.
        if (_toastTimer > 0f)
        {
            _toastTimer -= Time.deltaTime;
            if (_toastRoot != null)
            {
                var cg = _toastRoot.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = Mathf.Clamp01(_toastTimer / 1.2f);
                if (_toastTimer <= 0f) _toastRoot.SetActive(false);
            }
        }

        // Periodic poll: live trial counters & nearest-sentinel guidance.
        _pollTimer -= Time.deltaTime;
        if (_pollTimer <= 0f)
        {
            _pollTimer = 0.5f;
            if (s_storyMode)
            {
                RefreshObjective();
                RefreshGuideTarget();
            }
        }
    }

    bool _altCursorActive;

    /// <summary>
    /// Holding Alt temporarily frees the cursor so the player can click HUD elements
    /// (such as the Main Story icon). Releasing Alt restores gameplay cursor lock.
    /// </summary>
    void HandleCursorReveal()
    {
        if (_windowOpen || LoreReadingUI.IsAnyOpen)
        {
            _altCursorActive = false;
            return;
        }

        bool altDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        if (altDown && !_altCursorActive)
        {
            _altCursorActive  = true;
            Cursor.lockState  = CursorLockMode.None;
            Cursor.visible    = true;
        }
        else if (!altDown && _altCursorActive)
        {
            _altCursorActive  = false;
            Cursor.lockState  = CursorLockMode.Locked;
            Cursor.visible    = false;
        }
    }

    void OnFlowChanged()
    {
        AnnounceAdvance();
        RefreshAll();
    }

    void RefreshAll()
    {
        RefreshWindow();
        RefreshObjective();
        RefreshGuideTarget();
    }

    // ── Step advancement announcements ───────────────────────────────────────

    void AnnounceAdvance()
    {
        int cur = MainStoryFlow.CurrentStepIndex;
        if (cur <= s_lastAnnouncedStep) { s_lastAnnouncedStep = Mathf.Max(s_lastAnnouncedStep, cur); return; }

        if (s_storyMode)
        {
            var lines = new System.Collections.Generic.List<string>();
            for (int k = s_lastAnnouncedStep; k < cur && k < MainStoryFlow.TotalSteps; k++)
            {
                var step = MainStoryFlow.Steps[k];
                if (step.Kind == MainStoryFlow.StepKind.Task)
                {
                    lines.Add(k == s_lastAnnouncedStep
                        ? $"Trial complete — {step.NodeBottom}!"
                        : $"Requirement already met — {step.NodeBottom} was fulfilled earlier.");
                }
                else
                {
                    lines.Add($"{step.NodeTop} recovered.");
                }
            }
            if (cur >= MainStoryFlow.TotalSteps)
                lines.Add("All chapters recovered — the echoes are complete.");
            else
                lines.Add($"Next: {MainStoryFlow.Steps[cur].ObjectiveTitle}");

            ShowToast(string.Join("\n", lines.GetRange(Mathf.Max(0, lines.Count - 3), Mathf.Min(3, lines.Count))));
        }

        s_lastAnnouncedStep = cur;
    }

    void ShowToast(string msg)
    {
        if (_toastRoot == null || _toastText == null) return;
        _toastText.text = msg;
        _toastRoot.SetActive(true);
        var cg = _toastRoot.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;
        _toastTimer = 5f;
    }

    // ── Window open / close ───────────────────────────────────────────────────

    void ToggleWindow()
    {
        if (_windowOpen) CloseWindow();
        else             OpenWindow();
    }

    void OpenWindow()
    {
        _windowOpen = true;
        _windowRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        RefreshWindow();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void CloseWindow()
    {
        _windowOpen = false;
        _windowRoot.SetActive(false);

        if (!LoreReadingUI.IsAnyOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    // ── Story mode ────────────────────────────────────────────────────────────

    void StartStoryMode()
    {
        s_storyMode = true;
        s_lastAnnouncedStep = MainStoryFlow.CurrentStepIndex;
        RefreshAll();
        CloseWindow();

        var cur = MainStoryFlow.Current;
        ShowToast(cur != null
            ? $"Objective: {cur.ObjectiveTitle}{MainStoryFlow.ProgressSuffix(MainStoryFlow.CurrentStepIndex)}"
            : "All chapters recovered — the echoes are complete.");
    }

    void QuitStoryMode()
    {
        s_storyMode = false;
        if (_guide != null) _guide.Target = null;
        RefreshAll();
    }

    // ── Guidance target ───────────────────────────────────────────────────────

    void RefreshGuideTarget()
    {
        if (_guide == null) return;

        if (!s_storyMode || MainStoryFlow.JourneyComplete)
        {
            _guide.Target = null;
            return;
        }

        var step = MainStoryFlow.Current;
        switch (step.Id)
        {
            case "planes":
                _guide.Target = FindCached(ref _planePortal, "Door_PlaneShooter");
                break;
            case "dungeon":
                _guide.Target = FindCached(ref _comStation, "P_Base_ComStation_A");
                break;
            case "sentinels":
                _guide.Target = NearestAliveSentinel();
                break;
            default:
                _guide.Target = step.VolumeIndex >= 0
                    ? FindLoreObject(MainStoryProgress.Chapters[step.VolumeIndex].Title)
                    : null;
                break;
        }
    }

    static Transform FindCached(ref Transform cache, string objectName)
    {
        if (cache == null)
        {
            var go = GameObject.Find(objectName);
            if (go != null) cache = go.transform;
        }
        return cache;
    }

    Transform NearestAliveSentinel()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        var from = player != null ? player.transform.position : Vector3.zero;
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (var e in PrimaryEnemy.All)
        {
            if (e == null || !e.IsAlive) continue;
            float d = (e.transform.position - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = e.transform; }
        }
        return best;
    }

    static Transform FindLoreObject(string title)
    {
        foreach (var lore in LoreInteractable.All)
        {
            if (lore == null) continue;
            if (string.Equals(lore.EntryTitle, title, System.StringComparison.OrdinalIgnoreCase))
                return lore.transform;
        }
        return null;
    }

    // ── UI construction ───────────────────────────────────────────────────────

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        gameObject.AddComponent<GraphicRaycaster>();

        BuildIcon();
        BuildObjectiveHud();
        BuildToast();
        BuildWindow();
    }

    void BuildIcon()
    {
        _iconRoot = MakeGo("QuestIcon", gameObject);
        var rt = _iconRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40f, -36f);
        rt.sizeDelta = new Vector2(230f, 52f);

        var btnImg = _iconRoot.AddComponent<Image>();
        btnImg.color = new Color(0f, 0f, 0f, 0.28f);
        var btn = _iconRoot.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(ToggleWindow);

        var diamond = MakeGo("Diamond", _iconRoot);
        var dImg = diamond.AddComponent<Image>();
        dImg.color = Gold;
        var dRt = diamond.GetComponent<RectTransform>();
        dRt.anchorMin = dRt.anchorMax = new Vector2(0f, 0.5f);
        dRt.pivot     = new Vector2(0.5f, 0.5f);
        dRt.sizeDelta = new Vector2(26f, 26f);
        dRt.anchoredPosition = new Vector2(28f, 0f);
        dRt.localRotation = Quaternion.Euler(0f, 0f, 45f);

        var label = MakeText("Label", _iconRoot, 22, Gold);
        var lRt = label.GetComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(1f, 1f);
        lRt.offsetMin = new Vector2(54f, 0f);
        lRt.offsetMax = new Vector2(-8f, 0f);
        var lTxt = label.GetComponent<Text>();
        lTxt.text      = "Main Story  [ J ]";
        lTxt.alignment = TextAnchor.MiddleLeft;
        lTxt.fontStyle = FontStyle.Bold;
    }

    /// <summary>Gold objective block under the Main Story icon (visible in story mode).</summary>
    void BuildObjectiveHud()
    {
        _objectiveRoot = MakeGo("Objective", gameObject);
        var rt = _objectiveRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40f, -94f);
        rt.sizeDelta = new Vector2(520f, 92f);

        var bg = _objectiveRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.30f);

        // Thin gold accent on the left edge (Genshin-style quest card).
        var accent = MakeGo("Accent", _objectiveRoot);
        var aImg = accent.AddComponent<Image>();
        aImg.color = Gold;
        var aRt = accent.GetComponent<RectTransform>();
        aRt.anchorMin = new Vector2(0f, 0f);
        aRt.anchorMax = new Vector2(0f, 1f);
        aRt.pivot     = new Vector2(0f, 0.5f);
        aRt.sizeDelta = new Vector2(4f, 0f);
        aRt.anchoredPosition = Vector2.zero;

        var title = MakeText("ObjTitle", _objectiveRoot, 21, Gold);
        var tRt = title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot     = new Vector2(0.5f, 1f);
        tRt.sizeDelta = new Vector2(-28f, 30f);
        tRt.anchoredPosition = new Vector2(8f, -8f);
        _objectiveTitle = title.GetComponent<Text>();
        _objectiveTitle.alignment = TextAnchor.MiddleLeft;
        _objectiveTitle.fontStyle = FontStyle.Bold;

        var detail = MakeText("ObjDetail", _objectiveRoot, 17, new Color(1f, 0.92f, 0.65f, 0.95f));
        var dRt = detail.GetComponent<RectTransform>();
        dRt.anchorMin = new Vector2(0f, 0f);
        dRt.anchorMax = new Vector2(1f, 1f);
        dRt.offsetMin = new Vector2(22f, 8f);
        dRt.offsetMax = new Vector2(-8f, -38f);
        _objectiveDetail = detail.GetComponent<Text>();
        _objectiveDetail.alignment = TextAnchor.UpperLeft;

        _objectiveRoot.SetActive(false);
    }

    void BuildToast()
    {
        _toastRoot = MakeGo("Toast", gameObject);
        var rt = _toastRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -130f);
        rt.sizeDelta = new Vector2(860f, 96f);
        _toastRoot.AddComponent<CanvasGroup>();

        var bg = _toastRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        var txt = MakeText("ToastText", _toastRoot, 24, Gold);
        var tRt = txt.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(16f, 8f);
        tRt.offsetMax = new Vector2(-16f, -8f);
        _toastText = txt.GetComponent<Text>();
        _toastText.alignment = TextAnchor.MiddleCenter;
        _toastText.fontStyle = FontStyle.Bold;

        _toastRoot.SetActive(false);
    }

    void BuildWindow()
    {
        _windowRoot = MakeGo("QuestWindow", gameObject);
        var winRt = _windowRoot.GetComponent<RectTransform>();
        winRt.anchorMin = winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot     = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1180f, 580f);
        winRt.anchoredPosition = Vector2.zero;

        var bg = _windowRoot.AddComponent<Image>();
        bg.color = Panel;
        AddBorder(_windowRoot, Gold, 2f);

        var title = MakeText("Title", _windowRoot, 30, Gold);
        var tRt = title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot     = new Vector2(0.5f, 1f);
        tRt.sizeDelta = new Vector2(-80f, 48f);
        tRt.anchoredPosition = new Vector2(0f, -26f);
        var tTxt = title.GetComponent<Text>();
        tTxt.text      = "Main Story  —  The Shattered Meridian";
        tTxt.alignment = TextAnchor.MiddleLeft;
        tTxt.fontStyle = FontStyle.Bold;

        var summary = MakeText("Summary", _windowRoot, 20, new Color(0.85f, 0.85f, 0.85f));
        var sRt = summary.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 1f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot     = new Vector2(0.5f, 1f);
        sRt.sizeDelta = new Vector2(-80f, 30f);
        sRt.anchoredPosition = new Vector2(0f, -74f);
        _summaryText = summary.GetComponent<Text>();
        _summaryText.alignment = TextAnchor.MiddleLeft;
        _summaryText.supportRichText = true;

        BuildProgressTrack();

        var status = MakeText("ModeStatus", _windowRoot, 18, new Color(0.7f, 0.7f, 0.7f));
        var stRt = status.GetComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0.5f, 0f);
        stRt.anchorMax = new Vector2(0.5f, 0f);
        stRt.pivot     = new Vector2(0.5f, 0f);
        stRt.sizeDelta = new Vector2(1000f, 28f);
        stRt.anchoredPosition = new Vector2(0f, 92f);
        _modeStatusText = status.GetComponent<Text>();
        _modeStatusText.alignment = TextAnchor.MiddleCenter;
        _modeStatusText.fontStyle = FontStyle.Italic;

        var startGo = MakeButton("StartBtn", _windowRoot, "Start",
            new Color(0.16f, 0.13f, 0.05f), Gold);
        var startRt = startGo.GetComponent<RectTransform>();
        startRt.anchorMin = startRt.anchorMax = new Vector2(0.5f, 0f);
        startRt.pivot     = new Vector2(1f, 0f);
        startRt.sizeDelta = new Vector2(190f, 50f);
        startRt.anchoredPosition = new Vector2(-20f, 28f);
        startGo.GetComponent<Button>().onClick.AddListener(StartStoryMode);
        AddBorder(startGo, Gold, 1.5f);

        var quitGo = MakeButton("QuitBtn", _windowRoot, "Quit",
            new Color(0.12f, 0.12f, 0.14f), new Color(0.85f, 0.85f, 0.85f));
        var quitRt = quitGo.GetComponent<RectTransform>();
        quitRt.anchorMin = quitRt.anchorMax = new Vector2(0.5f, 0f);
        quitRt.pivot     = new Vector2(0f, 0f);
        quitRt.sizeDelta = new Vector2(190f, 50f);
        quitRt.anchoredPosition = new Vector2(20f, 28f);
        quitGo.GetComponent<Button>().onClick.AddListener(QuitStoryMode);

        var closeGo = MakeButton("CloseBtn", _windowRoot, "✕",
            new Color(0.26f, 0.10f, 0.10f), new Color(1f, 0.7f, 0.7f));
        var cRt = closeGo.GetComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot     = new Vector2(1f, 1f);
        cRt.sizeDelta = new Vector2(44f, 44f);
        cRt.anchoredPosition = new Vector2(-12f, -12f);
        closeGo.GetComponent<Button>().onClick.AddListener(CloseWindow);

        _windowRoot.SetActive(false);
    }

    void BuildProgressTrack()
    {
        int n = MainStoryFlow.TotalSteps;

        var track = MakeGo("Track", _windowRoot);
        var trackRt = track.GetComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0f, 0.5f);
        trackRt.anchorMax = new Vector2(1f, 0.5f);
        trackRt.pivot     = new Vector2(0.5f, 0.5f);
        trackRt.offsetMin = new Vector2(100f, -20f);
        trackRt.offsetMax = new Vector2(-100f, 40f);

        var line = MakeGo("Line", track);
        var lineImg = line.AddComponent<Image>();
        lineImg.color = TrackBg;
        var lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 0.5f);
        lineRt.anchorMax = new Vector2(1f, 0.5f);
        lineRt.pivot     = new Vector2(0.5f, 0.5f);
        lineRt.sizeDelta = new Vector2(0f, 6f);
        lineRt.anchoredPosition = Vector2.zero;

        var fill = MakeGo("Fill", track);
        _trackFill = fill.AddComponent<Image>();
        _trackFill.color = Gold;
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0.5f);
        fillRt.anchorMax = new Vector2(0f, 0.5f);
        fillRt.pivot     = new Vector2(0f, 0.5f);
        fillRt.sizeDelta = new Vector2(0f, 6f);
        fillRt.anchoredPosition = Vector2.zero;

        _nodeDots    = new Image[n];
        _nodeLabels  = new Text[n];
        _nodeButtons = new Button[n];

        var circle = GetCircleSprite();

        for (int i = 0; i < n; i++)
        {
            float t = n > 1 ? i / (float)(n - 1) : 0.5f;
            var step = MainStoryFlow.Steps[i];
            bool isTask = step.Kind == MainStoryFlow.StepKind.Task;

            var node = MakeGo($"Node{i}", track);
            var nodeImg = node.AddComponent<Image>();
            var nodeRt = node.GetComponent<RectTransform>();
            nodeRt.anchorMin = new Vector2(t, 0.5f);
            nodeRt.anchorMax = new Vector2(t, 0.5f);
            nodeRt.pivot     = new Vector2(0.5f, 0.5f);
            nodeRt.anchoredPosition = Vector2.zero;

            if (isTask)
            {
                // Trials are gold diamonds (rotated squares).
                nodeRt.sizeDelta = new Vector2(26f, 26f);
                nodeRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            else
            {
                nodeImg.sprite = circle;
                nodeImg.type   = Image.Type.Simple;
                nodeRt.sizeDelta = new Vector2(34f, 34f);
            }
            _nodeDots[i] = nodeImg;

            // Recovered volumes become clickable to re-read.
            if (!isTask)
            {
                var btn = node.AddComponent<Button>();
                btn.targetGraphic = nodeImg;
                int volIdx = step.VolumeIndex;
                btn.onClick.AddListener(() => OnVolumeNodeClicked(volIdx));
                _nodeButtons[i] = btn;
            }

            var label = MakeText($"NodeLabel{i}", track, 15, GoldDim);
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(t, 0.5f);
            labelRt.anchorMax = new Vector2(t, 0.5f);
            labelRt.pivot     = new Vector2(0.5f, 1f);
            labelRt.sizeDelta = new Vector2(160f, 80f);
            labelRt.anchoredPosition = new Vector2(0f, -28f);
            var labelTxt = label.GetComponent<Text>();
            labelTxt.alignment = TextAnchor.UpperCenter;
            labelTxt.fontStyle = FontStyle.Bold;
            _nodeLabels[i] = labelTxt;
        }
    }

    void OnVolumeNodeClicked(int volumeIndex)
    {
        if (!MainStoryProgress.IsRead(volumeIndex)) return;
        var title = MainStoryProgress.Chapters[volumeIndex].Title;
        foreach (var lore in LoreInteractable.All)
        {
            if (lore == null) continue;
            if (string.Equals(lore.EntryTitle, title, System.StringComparison.OrdinalIgnoreCase))
            {
                CloseWindow();
                lore.OpenReader();
                return;
            }
        }
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    void RefreshObjective()
    {
        if (_objectiveTitle == null) return;

        if (MainStoryFlow.JourneyComplete)
        {
            _objectiveTitle.text  = "◆ Journey Complete";
            _objectiveDetail.text = "All chapters recovered — the echoes are complete.";
            return;
        }

        int cur = MainStoryFlow.CurrentStepIndex;
        var step = MainStoryFlow.Steps[cur];
        _objectiveTitle.text  = $"◆ {step.ObjectiveTitle}{MainStoryFlow.ProgressSuffix(cur)}";
        _objectiveDetail.text = step.ObjectiveDetail;
    }

    void RefreshWindow()
    {
        if (_summaryText == null) return;

        int total   = MainStoryFlow.TotalSteps;
        int current = MainStoryFlow.CurrentStepIndex;
        int readCnt = MainStoryProgress.ReadCount;

        _summaryText.text =
            $"Journey Progress:  <color=#FFD24C>{current}</color> / {total} steps      " +
            $"Chapters Recovered:  <color=#FFD24C>{readCnt}</color> / {MainStoryProgress.TotalChapters}";

        for (int i = 0; i < total; i++)
        {
            var step = MainStoryFlow.Steps[i];
            bool done = MainStoryFlow.IsStepComplete(i);
            bool isCurrent = i == current;

            if (_nodeDots[i] != null)
            {
                _nodeDots[i].color = done ? Gold : isCurrent ? Current : NodeOff;
                _nodeDots[i].transform.localScale = isCurrent ? Vector3.one * 1.25f : Vector3.one;
            }

            if (_nodeLabels[i] != null)
            {
                string bottom;
                if (step.Kind == MainStoryFlow.StepKind.Volume)
                {
                    var ch = MainStoryProgress.Chapters[step.VolumeIndex];
                    bottom = done ? ch.Title + "\n<size=12>(click node to re-read)</size>" : "??? ";
                }
                else
                {
                    bottom = step.NodeBottom + MainStoryFlow.ProgressSuffix(i);
                }
                _nodeLabels[i].text  = $"{step.NodeTop}\n{bottom}";
                _nodeLabels[i].color = done ? Gold : isCurrent ? Current : GoldDim;
            }

            if (_nodeButtons[i] != null)
                _nodeButtons[i].interactable = done;
        }

        if (_trackFill != null && total > 0)
        {
            float frac = Mathf.Clamp01(current / (float)total);
            var parentRt = _trackFill.transform.parent as RectTransform;
            float trackWidth = parentRt != null ? parentRt.rect.width : 0f;
            _trackFill.rectTransform.sizeDelta = new Vector2(trackWidth * frac, 6f);
        }

        if (_modeStatusText != null)
        {
            if (MainStoryFlow.JourneyComplete)
                _modeStatusText.text = "All chapters recovered. The echoes are complete.";
            else if (s_storyMode)
                _modeStatusText.text = "Story mode active — follow the golden trail. Trials: clear the plane shooter, scrap the sentinels, conquer the dungeon.";
            else
                _modeStatusText.text = "Press Start to begin the journey — the golden trail will lead the way.";
        }
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    static GameObject MakeGo(string name, GameObject parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static GameObject MakeText(string name, GameObject parent, int fontSize, Color color)
    {
        var go = MakeGo(name, parent);
        go.AddComponent<CanvasRenderer>();
        var txt = go.AddComponent<Text>();
        txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize      = fontSize;
        txt.color         = color;
        txt.supportRichText = true;
        return go;
    }

    static GameObject MakeButton(string name, GameObject parent, string label, Color bg, Color textColor)
    {
        var go = MakeGo(name, parent);
        go.AddComponent<CanvasRenderer>();
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        var cb  = btn.colors;
        cb.normalColor      = bg;
        cb.highlightedColor = bg + new Color(0.12f, 0.12f, 0.12f, 0f);
        cb.pressedColor     = bg - new Color(0.06f, 0.06f, 0.06f, 0f);
        btn.colors = cb;

        var labelGo = MakeText("Label", go, 22, textColor);
        var lRt = labelGo.GetComponent<RectTransform>();
        lRt.anchorMin = Vector2.zero;
        lRt.anchorMax = Vector2.one;
        lRt.offsetMin = lRt.offsetMax = Vector2.zero;
        var lTxt = labelGo.GetComponent<Text>();
        lTxt.text      = label;
        lTxt.alignment = TextAnchor.MiddleCenter;
        lTxt.fontStyle = FontStyle.Bold;
        return go;
    }

    static void AddBorder(GameObject panel, Color color, float w)
    {
        CreateEdge("B_Top",    panel, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, w), color);
        CreateEdge("B_Bottom", panel, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, w), color);
        CreateEdge("B_Left",   panel, new Vector2(0, 0), new Vector2(0, 1), new Vector2(w, 0), color);
        CreateEdge("B_Right",  panel, new Vector2(1, 0), new Vector2(1, 1), new Vector2(w, 0), color);
    }

    static void CreateEdge(string name, GameObject parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color color)
    {
        var go  = MakeGo(name, parent);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = size;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Sprite GetCircleSprite()
    {
        if (s_circleSprite != null) return s_circleSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
        };
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - c) / c;
            float dy = (y + 0.5f - c) / c;
            float r  = Mathf.Sqrt(dx * dx + dy * dy);
            float a  = r <= 0.92f ? 1f : Mathf.Clamp01((1f - r) / 0.08f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        s_circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return s_circleSprite;
    }
}
