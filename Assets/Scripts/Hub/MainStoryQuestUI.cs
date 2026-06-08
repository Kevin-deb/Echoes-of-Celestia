using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Main-story quest HUD for the Hub scene (Genshin-style):
/// - A gold diamond + "Main Story" icon in the top-left corner.
/// - Click the icon (or press J) to open the progress overview window.
/// - The window shows a node-based progress track (one node per chapter) plus
///   Start / Quit buttons to enter or leave story mode, and a close (X) button.
/// - Path guidance (gold ground dots) is only shown while story mode is active,
///   and story mode can only be entered from the Hub scene.
///
/// The UI is created from code at runtime and only exists in the Hub scene, so it
/// does not affect any other scene or system.
/// </summary>
public sealed class MainStoryQuestUI : MonoBehaviour
{
    const string HubSceneName = "Hub";

    static readonly Color Gold     = new Color(1f, 0.84f, 0.32f, 1f);
    static readonly Color GoldDim  = new Color(0.45f, 0.40f, 0.24f, 1f);
    static readonly Color Panel    = new Color(0.05f, 0.06f, 0.09f, 0.98f);
    static readonly Color TrackBg  = new Color(0.22f, 0.22f, 0.24f, 1f);

    public static bool StoryModeActive { get; private set; }

    static MainStoryQuestUI _instance;

    // Icon
    GameObject _iconRoot;

    // Window
    GameObject _windowRoot;
    Text       _summaryText;
    Image      _trackFill;
    Image[]    _nodeDots;
    Text[]     _nodeLabels;
    Text       _modeStatusText;
    bool       _windowOpen;

    // Guidance
    MainStoryPathGuide _guide;

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
        _instance       = this;
        StoryModeActive = false;

        BuildUI();

        var guideGo = new GameObject("MainStoryPathGuide");
        guideGo.transform.SetParent(transform, false);
        _guide = guideGo.AddComponent<MainStoryPathGuide>();
        _guide.Target = null;
    }

    void OnEnable()  { MainStoryProgress.Changed += OnProgressChanged; }
    void OnDisable() { MainStoryProgress.Changed -= OnProgressChanged; }

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
    }

    bool _altCursorActive;

    /// <summary>
    /// Holding Alt temporarily frees the cursor so the player can click HUD elements
    /// (such as the Main Story icon). Releasing Alt restores gameplay cursor lock.
    /// While a menu owns the cursor, this does nothing.
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

    void OnProgressChanged()
    {
        RefreshWindow();
        RefreshGuideTarget();
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
        // Ensure layout rects are resolved before computing the progress fill width.
        Canvas.ForceUpdateCanvases();
        RefreshWindow();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void CloseWindow()
    {
        _windowOpen = false;
        _windowRoot.SetActive(false);

        // Only re-lock the cursor if no other full-screen UI needs it.
        if (!LoreReadingUI.IsAnyOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    // ── Story mode ────────────────────────────────────────────────────────────

    void StartStoryMode()
    {
        StoryModeActive = true;
        RefreshGuideTarget();
        CloseWindow();
    }

    void QuitStoryMode()
    {
        StoryModeActive = false;
        if (_guide != null) _guide.Target = null;
        RefreshWindow();
    }

    void RefreshGuideTarget()
    {
        if (_guide == null) return;

        if (!StoryModeActive)
        {
            _guide.Target = null;
            return;
        }

        var next = MainStoryProgress.NextUnreadIndex;
        if (next < 0)
        {
            _guide.Target = null; // everything recovered
            return;
        }

        var title = MainStoryProgress.Chapters[next].Title;
        _guide.Target = FindLoreObject(title);
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

        // Transparent button covering the whole icon area.
        var btnImg = _iconRoot.AddComponent<Image>();
        btnImg.color = new Color(0f, 0f, 0f, 0.28f);
        var btn = _iconRoot.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(ToggleWindow);

        // Gold diamond (rotated square).
        var diamond = MakeGo("Diamond", _iconRoot);
        var dImg = diamond.AddComponent<Image>();
        dImg.color = Gold;
        var dRt = diamond.GetComponent<RectTransform>();
        dRt.anchorMin = dRt.anchorMax = new Vector2(0f, 0.5f);
        dRt.pivot     = new Vector2(0.5f, 0.5f);
        dRt.sizeDelta = new Vector2(26f, 26f);
        dRt.anchoredPosition = new Vector2(28f, 0f);
        dRt.localRotation = Quaternion.Euler(0f, 0f, 45f);

        // "Main Story" gold label.
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

    void BuildWindow()
    {
        _windowRoot = MakeGo("QuestWindow", gameObject);
        var winRt = _windowRoot.GetComponent<RectTransform>();
        winRt.anchorMin = winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot     = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1040f, 560f);
        winRt.anchoredPosition = Vector2.zero;

        var bg = _windowRoot.AddComponent<Image>();
        bg.color = Panel;
        AddBorder(_windowRoot, Gold, 2f);

        // Title
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

        // Summary (X / N recovered)
        var summary = MakeText("Summary", _windowRoot, 20, new Color(0.85f, 0.85f, 0.85f));
        var sRt = summary.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 1f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot     = new Vector2(0.5f, 1f);
        sRt.sizeDelta = new Vector2(-80f, 30f);
        sRt.anchoredPosition = new Vector2(0f, -74f);
        _summaryText = summary.GetComponent<Text>();
        _summaryText.alignment = TextAnchor.MiddleLeft;

        BuildProgressTrack();

        // Mode status text
        var status = MakeText("ModeStatus", _windowRoot, 18, new Color(0.7f, 0.7f, 0.7f));
        var stRt = status.GetComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0.5f, 0f);
        stRt.anchorMax = new Vector2(0.5f, 0f);
        stRt.pivot     = new Vector2(0.5f, 0f);
        stRt.sizeDelta = new Vector2(900f, 28f);
        stRt.anchoredPosition = new Vector2(0f, 92f);
        _modeStatusText = status.GetComponent<Text>();
        _modeStatusText.alignment = TextAnchor.MiddleCenter;
        _modeStatusText.fontStyle = FontStyle.Italic;

        // Start button
        var startGo = MakeButton("StartBtn", _windowRoot, "Start",
            new Color(0.16f, 0.13f, 0.05f), Gold);
        var startRt = startGo.GetComponent<RectTransform>();
        startRt.anchorMin = startRt.anchorMax = new Vector2(0.5f, 0f);
        startRt.pivot     = new Vector2(1f, 0f);
        startRt.sizeDelta = new Vector2(190f, 50f);
        startRt.anchoredPosition = new Vector2(-20f, 28f);
        startGo.GetComponent<Button>().onClick.AddListener(StartStoryMode);
        AddBorder(startGo, Gold, 1.5f);

        // Quit button
        var quitGo = MakeButton("QuitBtn", _windowRoot, "Quit",
            new Color(0.12f, 0.12f, 0.14f), new Color(0.85f, 0.85f, 0.85f));
        var quitRt = quitGo.GetComponent<RectTransform>();
        quitRt.anchorMin = quitRt.anchorMax = new Vector2(0.5f, 0f);
        quitRt.pivot     = new Vector2(0f, 0f);
        quitRt.sizeDelta = new Vector2(190f, 50f);
        quitRt.anchoredPosition = new Vector2(20f, 28f);
        quitGo.GetComponent<Button>().onClick.AddListener(QuitStoryMode);

        // Close (X)
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
        int n = MainStoryProgress.TotalChapters;

        // Container for the track, centred in the window.
        var track = MakeGo("Track", _windowRoot);
        var trackRt = track.GetComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0f, 0.5f);
        trackRt.anchorMax = new Vector2(1f, 0.5f);
        trackRt.pivot     = new Vector2(0.5f, 0.5f);
        trackRt.offsetMin = new Vector2(110f, -20f);
        trackRt.offsetMax = new Vector2(-110f, 40f);

        // Background line
        var line = MakeGo("Line", track);
        var lineImg = line.AddComponent<Image>();
        lineImg.color = TrackBg;
        var lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 0.5f);
        lineRt.anchorMax = new Vector2(1f, 0.5f);
        lineRt.pivot     = new Vector2(0.5f, 0.5f);
        lineRt.sizeDelta = new Vector2(0f, 6f);
        lineRt.anchoredPosition = Vector2.zero;

        // Gold fill line (width set in RefreshWindow)
        var fill = MakeGo("Fill", track);
        _trackFill = fill.AddComponent<Image>();
        _trackFill.color = Gold;
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0.5f);
        fillRt.anchorMax = new Vector2(0f, 0.5f);
        fillRt.pivot     = new Vector2(0f, 0.5f);
        fillRt.sizeDelta = new Vector2(0f, 6f);
        fillRt.anchoredPosition = Vector2.zero;

        _nodeDots   = new Image[n];
        _nodeLabels = new Text[n];

        var circle = GetCircleSprite();

        for (int i = 0; i < n; i++)
        {
            float t = n > 1 ? i / (float)(n - 1) : 0.5f;

            var node = MakeGo($"Node{i}", track);
            var nodeImg = node.AddComponent<Image>();
            nodeImg.sprite = circle;
            nodeImg.type   = Image.Type.Simple;
            var nodeRt = node.GetComponent<RectTransform>();
            nodeRt.anchorMin = new Vector2(t, 0.5f);
            nodeRt.anchorMax = new Vector2(t, 0.5f);
            nodeRt.pivot     = new Vector2(0.5f, 0.5f);
            nodeRt.sizeDelta = new Vector2(34f, 34f);
            nodeRt.anchoredPosition = Vector2.zero;
            _nodeDots[i] = nodeImg;

            // Label below the node: "Volume X" + chapter title.
            var label = MakeText($"NodeLabel{i}", track, 16, GoldDim);
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(t, 0.5f);
            labelRt.anchorMax = new Vector2(t, 0.5f);
            labelRt.pivot     = new Vector2(0.5f, 1f);
            labelRt.sizeDelta = new Vector2(210f, 70f);
            labelRt.anchoredPosition = new Vector2(0f, -28f);
            var labelTxt = label.GetComponent<Text>();
            labelTxt.alignment = TextAnchor.UpperCenter;
            labelTxt.fontStyle = FontStyle.Bold;
            _nodeLabels[i] = labelTxt;
        }
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    void RefreshWindow()
    {
        if (_summaryText == null) return;

        int total = MainStoryProgress.TotalChapters;
        int read  = MainStoryProgress.ReadCount;

        _summaryText.text = $"Chapters Recovered:  <color=#FFD24C>{read}</color> / {total}";

        for (int i = 0; i < total; i++)
        {
            bool isRead = MainStoryProgress.IsRead(i);
            if (_nodeDots[i] != null)
                _nodeDots[i].color = isRead ? Gold : new Color(0.3f, 0.3f, 0.32f, 1f);

            if (_nodeLabels[i] != null)
            {
                var ch = MainStoryProgress.Chapters[i];
                _nodeLabels[i].text  = $"{ch.Volume}\n{(isRead ? ch.Title : "??? ")}";
                _nodeLabels[i].color = isRead ? Gold : GoldDim;
            }
        }

        // Gold fill stretches across the recovered portion of the track.
        if (_trackFill != null && total > 1)
        {
            float frac = Mathf.Clamp01((read) / (float)(total));
            var parentRt = _trackFill.transform.parent as RectTransform;
            float trackWidth = parentRt != null ? parentRt.rect.width : 0f;
            var fillRt = _trackFill.rectTransform;
            fillRt.sizeDelta = new Vector2(trackWidth * frac, 6f);
        }

        if (_modeStatusText != null)
        {
            if (MainStoryProgress.NextUnreadIndex < 0)
                _modeStatusText.text = "All chapters recovered. The echoes are complete.";
            else if (StoryModeActive)
                _modeStatusText.text = "Story mode active — follow the golden trail to the next chapter.";
            else
                _modeStatusText.text = "Press Start to reveal a trail toward the next chapter.";
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
