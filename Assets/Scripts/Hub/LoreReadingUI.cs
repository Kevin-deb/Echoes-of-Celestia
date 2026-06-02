using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局单例 UI：展示剧情文本窗口，支持翻页与关闭。
/// 由 LoreInteractable 调用 Open / Close；运行时自动创建，无需挂在场景对象上。
/// </summary>
public sealed class LoreReadingUI : MonoBehaviour
{
    public static LoreReadingUI Instance { get; private set; }
    public static bool IsAnyOpen => Instance != null && Instance._isOpen;

    // ── UI 节点 ───────────────────────────────────────────────────────────────
    GameObject _root;
    Text       _categoryText;
    Text       _titleText;
    Text       _bodyText;
    Text       _pageCountText;
    Button     _prevBtn;
    Button     _nextBtn;

    // ── 运行时状态 ────────────────────────────────────────────────────────────
    bool     _isOpen;
    string[] _pages;
    int      _pageIndex;

    // ── 自动安装 ──────────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => TryCreate(s);

    static void TryCreate(Scene scene)
    {
        if (!scene.IsValid()) return;
        if (Instance != null) return;
        var go = new GameObject("LoreReadingUI");
        go.AddComponent<LoreReadingUI>();
    }

    // ── Unity 生命周期 ────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
    }

    void Update()
    {
        if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    // ── 公开接口 ──────────────────────────────────────────────────────────────
    public void Open(string category, string title, string[] pages)
    {
        if (pages == null || pages.Length == 0) return;
        _pages     = pages;
        _pageIndex = 0;
        _isOpen    = true;

        if (_categoryText != null) _categoryText.text = category;
        if (_titleText    != null) _titleText.text    = title;
        UpdateDisplay();
        // 显示整个 Canvas（含全屏遮罩 + 内容面板）
        gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void Close()
    {
        _isOpen = false;
        // 隐藏整个 Canvas，遮罩和内容面板同时消失
        gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── 翻页 ──────────────────────────────────────────────────────────────────
    void GoNext()
    {
        if (_pageIndex < _pages.Length - 1) { _pageIndex++; UpdateDisplay(); }
    }

    void GoPrev()
    {
        if (_pageIndex > 0) { _pageIndex--; UpdateDisplay(); }
    }

    void UpdateDisplay()
    {
        if (_bodyText      != null) _bodyText.text      = _pages[_pageIndex];
        if (_pageCountText != null) _pageCountText.text = $"— {_pageIndex + 1} / {_pages.Length} —";
        if (_prevBtn       != null) _prevBtn.interactable = _pageIndex > 0;
        if (_nextBtn       != null) _nextBtn.interactable = _pageIndex < _pages.Length - 1;
    }

    // ── UI 构建 ───────────────────────────────────────────────────────────────
    void BuildUI()
    {
        // ── 根 Canvas ────────────────────────────────────────────────────────
        var canvasGo = gameObject;
        var canvas   = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // ── 全屏暗色遮罩 ──────────────────────────────────────────────────────
        var overlayGo = MakeGo("Overlay", canvasGo);
        var overlayRt = overlayGo.GetComponent<RectTransform>();
        StretchFull(overlayRt);
        var overlayImg = overlayGo.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.72f);
        overlayGo.AddComponent<CanvasRenderer>();

        // ── 主内容面板 ────────────────────────────────────────────────────────
        _root = MakeGo("LorePanel", canvasGo);
        var panelRt = _root.GetComponent<RectTransform>();
        CenterPanel(panelRt, new Vector2(980f, 700f));

        var panelImg = _root.AddComponent<Image>();
        panelImg.color = new Color(0.04f, 0.06f, 0.11f, 0.97f);

        // 边框（亮色细边）
        AddBorder(_root, new Color(0.25f, 0.55f, 0.85f, 0.6f));

        // ── 分类标签（顶部左侧，蓝色小字）────────────────────────────────────
        var catGo = MakeTextGo("Category", _root, 19, new Color(0.35f, 0.75f, 1f));
        var catRt = catGo.GetComponent<RectTransform>();
        catRt.anchorMin = new Vector2(0f, 1f);
        catRt.anchorMax = new Vector2(1f, 1f);
        catRt.pivot     = new Vector2(0.5f, 1f);
        catRt.sizeDelta = new Vector2(-100f, 36f);
        catRt.anchoredPosition = new Vector2(0f, -22f);
        _categoryText = catGo.GetComponent<Text>();
        _categoryText.alignment = TextAnchor.MiddleLeft;
        _categoryText.fontStyle = FontStyle.Italic;

        // ── 标题（粗体白色）──────────────────────────────────────────────────
        var titleGo = MakeTextGo("Title", _root, 28, Color.white);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot     = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(-100f, 46f);
        titleRt.anchoredPosition = new Vector2(0f, -56f);
        _titleText = titleGo.GetComponent<Text>();
        _titleText.alignment = TextAnchor.MiddleLeft;
        _titleText.fontStyle = FontStyle.Bold;

        // ── 分割线 ────────────────────────────────────────────────────────────
        var divGo  = MakeGo("Divider", _root);
        var divImg = divGo.AddComponent<Image>();
        divImg.color = new Color(0.25f, 0.55f, 0.85f, 0.45f);
        divGo.AddComponent<CanvasRenderer>();
        var divRt = divGo.GetComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0f, 1f);
        divRt.anchorMax = new Vector2(1f, 1f);
        divRt.pivot     = new Vector2(0.5f, 1f);
        divRt.sizeDelta = new Vector2(-60f, 2f);
        divRt.anchoredPosition = new Vector2(0f, -107f);

        // ── 正文区域 ──────────────────────────────────────────────────────────
        var bodyGo = MakeTextGo("Body", _root, 19, new Color(0.87f, 0.87f, 0.80f));
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.sizeDelta = new Vector2(-80f, -180f);
        bodyRt.anchoredPosition = new Vector2(0f, -34f);
        _bodyText = bodyGo.GetComponent<Text>();
        _bodyText.alignment  = TextAnchor.UpperLeft;
        _bodyText.lineSpacing = 1.45f;

        // ── 页码 ──────────────────────────────────────────────────────────────
        var countGo = MakeTextGo("PageCount", _root, 17, new Color(0.45f, 0.55f, 0.65f));
        var countRt = countGo.GetComponent<RectTransform>();
        countRt.anchorMin = new Vector2(0.3f, 0f);
        countRt.anchorMax = new Vector2(0.7f, 0f);
        countRt.pivot     = new Vector2(0.5f, 0f);
        countRt.sizeDelta = new Vector2(0f, 38f);
        countRt.anchoredPosition = new Vector2(0f, 32f);
        _pageCountText = countGo.GetComponent<Text>();
        _pageCountText.alignment = TextAnchor.MiddleCenter;

        // ── 上一页按钮 ────────────────────────────────────────────────────────
        var prevGo = MakeButton("PrevBtn", _root, "◄  Prev",
            new Color(0.12f, 0.22f, 0.38f), new Color(0.6f, 0.85f, 1f));
        var prevRt = prevGo.GetComponent<RectTransform>();
        prevRt.anchorMin = prevRt.anchorMax = new Vector2(0f, 0f);
        prevRt.pivot     = new Vector2(0f, 0f);
        prevRt.sizeDelta = new Vector2(160f, 44f);
        prevRt.anchoredPosition = new Vector2(50f, 24f);
        _prevBtn = prevGo.GetComponent<Button>();
        _prevBtn.onClick.AddListener(GoPrev);

        // ── 下一页按钮 ────────────────────────────────────────────────────────
        var nextGo = MakeButton("NextBtn", _root, "Next  ►",
            new Color(0.12f, 0.22f, 0.38f), new Color(0.6f, 0.85f, 1f));
        var nextRt = nextGo.GetComponent<RectTransform>();
        nextRt.anchorMin = nextRt.anchorMax = new Vector2(1f, 0f);
        nextRt.pivot     = new Vector2(1f, 0f);
        nextRt.sizeDelta = new Vector2(160f, 44f);
        nextRt.anchoredPosition = new Vector2(-50f, 24f);
        _nextBtn = nextGo.GetComponent<Button>();
        _nextBtn.onClick.AddListener(GoNext);

        // ── 关闭按钮（右上角 ✕）─────────────────────────────────────────────
        var closeGo = MakeButton("CloseBtn", _root, "✕",
            new Color(0.28f, 0.10f, 0.10f), new Color(1f, 0.6f, 0.6f));
        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot     = new Vector2(1f, 1f);
        closeRt.sizeDelta = new Vector2(46f, 46f);
        closeRt.anchoredPosition = new Vector2(-14f, -14f);
        var closeBtn = closeGo.GetComponent<Button>();
        closeBtn.onClick.AddListener(Close);
        closeGo.GetComponentInChildren<Text>().fontSize = 22;

        // 初始整个 Canvas 隐藏，避免全屏遮罩在 UI 未打开时影响画面亮度
        gameObject.SetActive(false);
    }

    // ── UI 辅助方法 ────────────────────────────────────────────────────────────
    static GameObject MakeGo(string name, GameObject parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void CenterPanel(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    static void AddBorder(GameObject panel, Color color)
    {
        // 用四条细长 Image 模拟外框线
        var rt    = panel.GetComponent<RectTransform>();
        float bw  = 1.5f;
        CreateEdge("B_Top",    panel, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, bw), color);
        CreateEdge("B_Bottom", panel, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, bw), color);
        CreateEdge("B_Left",   panel, new Vector2(0, 0), new Vector2(0, 1), new Vector2(bw, 0), color);
        CreateEdge("B_Right",  panel, new Vector2(1, 0), new Vector2(1, 1), new Vector2(bw, 0), color);
    }

    static void CreateEdge(string name, GameObject parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color color)
    {
        var go  = MakeGo(name, parent);
        var img = go.AddComponent<Image>();
        img.color = color;
        go.AddComponent<CanvasRenderer>();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = size;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static GameObject MakeTextGo(string name, GameObject parent, int fontSize, Color color)
    {
        var go   = MakeGo(name, parent);
        go.AddComponent<CanvasRenderer>();
        var text = go.AddComponent<Text>();
        text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize  = fontSize;
        text.color     = color;
        text.supportRichText = true;
        return go;
    }

    static GameObject MakeButton(string name, GameObject parent,
        string label, Color bgColor, Color textColor)
    {
        var go  = MakeGo(name, parent);
        go.AddComponent<CanvasRenderer>();
        var img  = go.AddComponent<Image>();
        img.color = bgColor;
        var btn  = go.AddComponent<Button>();
        var cb   = btn.colors;
        cb.normalColor      = bgColor;
        cb.highlightedColor = bgColor + new Color(0.15f, 0.15f, 0.15f, 0f);
        cb.pressedColor     = bgColor - new Color(0.1f, 0.1f, 0.1f, 0f);
        btn.colors = cb;

        var labelGo  = MakeTextGo("Label", go, 20, textColor);
        var labelRt  = labelGo.GetComponent<RectTransform>();
        StretchFull(labelRt);
        var labelText       = labelGo.GetComponent<Text>();
        labelText.text      = label;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.fontStyle = FontStyle.Bold;
        return go;
    }
}
