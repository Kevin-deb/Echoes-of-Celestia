using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Quit-game affordance for the Hub scene. Adds an "Exit" button in the top-right
/// corner and a confirmation dialog ("Quit Game?") with Quit / Cancel buttons and a
/// ✕ close button. The dialog opens from the Exit button or by pressing Esc, and
/// Cancel / ✕ / Esc all return to the game; only Quit actually exits.
///
/// Built from code at runtime and injected only into the Hub scene, the same way the
/// quest HUD and lore reader install themselves — so it never edits Hub.unity or any
/// existing script. Cursor handling mirrors LoreReadingUI / MainStoryQuestUI.
/// </summary>
public sealed class HubExitGameUI : MonoBehaviour
{
    const string HubSceneName = "Hub";

    static readonly Color Panel      = new Color(0.05f, 0.06f, 0.09f, 0.98f);
    static readonly Color Overlay    = new Color(0f, 0f, 0f, 0.72f);
    static readonly Color Border     = new Color(0.32f, 0.45f, 0.62f, 0.65f);
    static readonly Color TitleCol   = new Color(1f, 1f, 1f, 1f);
    static readonly Color PromptCol  = new Color(0.82f, 0.85f, 0.90f, 1f);
    static readonly Color ExitBtnBg  = new Color(0f, 0f, 0f, 0.28f);
    static readonly Color ExitBtnTxt = new Color(0.92f, 0.94f, 0.98f, 1f);
    static readonly Color QuitBg     = new Color(0.42f, 0.13f, 0.13f, 1f);
    static readonly Color QuitTxt    = new Color(1f, 0.82f, 0.82f, 1f);
    static readonly Color CancelBg   = new Color(0.16f, 0.18f, 0.22f, 1f);
    static readonly Color CancelTxt  = new Color(0.85f, 0.90f, 0.96f, 1f);
    static readonly Color CloseBg    = new Color(0.26f, 0.10f, 0.10f, 1f);
    static readonly Color CloseTxt   = new Color(1f, 0.70f, 0.70f, 1f);

    static HubExitGameUI _instance;

    GameObject _exitButtonRoot;
    GameObject _dialogRoot;
    bool _open;

    // Cooperates with the other Hub menus (quest window / lore reader) so Esc backs
    // out of those first instead of opening the quit dialog on the same key press.
    Transform _questWindow;
    bool _questOpenPrev;
    bool _loreOpenPrev;

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

        var go = new GameObject("HubExitGameUI");
        _instance = go.AddComponent<HubExitGameUI>();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        BuildUI();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        bool loreOpen  = LoreReadingUI.IsAnyOpen;
        bool questOpen = QuestWindowOpen();

        // Hide the Exit button while a full-screen menu is up, matching how the quest
        // icon hides itself during the journey window / lore reading.
        bool hideButton = _open || loreOpen || questOpen;
        if (_exitButtonRoot != null && _exitButtonRoot.activeSelf == hideButton)
            _exitButtonRoot.SetActive(!hideButton);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_open)
                CloseDialog();
            else if (!loreOpen && !questOpen && !_loreOpenPrev && !_questOpenPrev)
                OpenDialog();
        }

        _loreOpenPrev  = loreOpen;
        _questOpenPrev = questOpen;
    }

    void LateUpdate()
    {
        // While the dialog is up, keep the cursor free regardless of the Alt-cursor
        // logic in MainStoryQuestUI (which runs in Update). LateUpdate wins.
        if (_open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
    }

    bool QuestWindowOpen()
    {
        if (_questWindow == null)
        {
            var qui = GameObject.Find("MainStoryQuestUI");
            if (qui != null) _questWindow = qui.transform.Find("QuestWindow");
        }
        return _questWindow != null && _questWindow.gameObject.activeSelf;
    }

    // ── Open / close ──────────────────────────────────────────────────────────

    void OpenDialog()
    {
        _open = true;
        if (_dialogRoot != null) _dialogRoot.SetActive(true);
        if (_exitButtonRoot != null) _exitButtonRoot.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void CloseDialog()
    {
        _open = false;
        if (_dialogRoot != null) _dialogRoot.SetActive(false);

        // Restore the gameplay cursor unless another menu still owns it.
        if (!LoreReadingUI.IsAnyOpen && !QuestWindowOpen())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── UI construction ───────────────────────────────────────────────────────

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // above the quest HUD (40) and lore reader (80)

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        gameObject.AddComponent<GraphicRaycaster>();

        BuildExitButton();
        BuildDialog();
    }

    void BuildExitButton()
    {
        _exitButtonRoot = MakeGo("ExitButton", gameObject);
        var rt = _exitButtonRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-40f, -36f);
        rt.sizeDelta = new Vector2(168f, 52f);

        var img = _exitButtonRoot.AddComponent<Image>();
        img.color = ExitBtnBg;
        var btn = _exitButtonRoot.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.highlightedColor = ExitBtnBg + new Color(0.12f, 0.12f, 0.12f, 0.10f);
        cb.pressedColor     = ExitBtnBg + new Color(0.04f, 0.04f, 0.04f, 0.18f);
        btn.colors = cb;
        btn.onClick.AddListener(OpenDialog);

        var label = MakeText("Label", _exitButtonRoot, 22, ExitBtnTxt);
        var lRt = label.GetComponent<RectTransform>();
        lRt.anchorMin = Vector2.zero;
        lRt.anchorMax = Vector2.one;
        lRt.offsetMin = lRt.offsetMax = Vector2.zero;
        var lTxt = label.GetComponent<Text>();
        lTxt.text      = "Exit  [ Esc ]";
        lTxt.alignment = TextAnchor.MiddleCenter;
        lTxt.fontStyle = FontStyle.Bold;
    }

    void BuildDialog()
    {
        _dialogRoot = MakeGo("ExitDialog", gameObject);
        var rootRt = _dialogRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        // Full-screen dim overlay (also blocks clicks to the HUD behind the dialog).
        var overlay = _dialogRoot.AddComponent<Image>();
        overlay.color = Overlay;

        // Centered panel.
        var panel = MakeGo("Panel", _dialogRoot);
        var pRt = panel.GetComponent<RectTransform>();
        pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot     = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(520f, 250f);
        pRt.anchoredPosition = Vector2.zero;
        var pImg = panel.AddComponent<Image>();
        pImg.color = Panel;
        AddBorder(panel, Border, 2f);

        // Title.
        var title = MakeText("Title", panel, 27, TitleCol);
        var tRt = title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot     = new Vector2(0.5f, 1f);
        tRt.sizeDelta = new Vector2(-60f, 44f);
        tRt.anchoredPosition = new Vector2(0f, -26f);
        var tTxt = title.GetComponent<Text>();
        tTxt.text      = "Quit Game?";
        tTxt.alignment = TextAnchor.MiddleCenter;
        tTxt.fontStyle = FontStyle.Bold;

        // Prompt.
        var prompt = MakeText("Prompt", panel, 19, PromptCol);
        var prRt = prompt.GetComponent<RectTransform>();
        prRt.anchorMin = new Vector2(0f, 1f);
        prRt.anchorMax = new Vector2(1f, 1f);
        prRt.pivot     = new Vector2(0.5f, 1f);
        prRt.sizeDelta = new Vector2(-72f, 70f);
        prRt.anchoredPosition = new Vector2(0f, -84f);
        var prTxt = prompt.GetComponent<Text>();
        prTxt.text      = "Are you sure you want to quit Echoes of Celestia?";
        prTxt.alignment = TextAnchor.MiddleCenter;

        // Quit (confirm) button.
        var quitGo = MakeButton("QuitBtn", panel, "Quit Game", QuitBg, QuitTxt);
        var qRt = quitGo.GetComponent<RectTransform>();
        qRt.anchorMin = qRt.anchorMax = new Vector2(0.5f, 0f);
        qRt.pivot     = new Vector2(1f, 0f);
        qRt.sizeDelta = new Vector2(180f, 52f);
        qRt.anchoredPosition = new Vector2(-16f, 34f);
        quitGo.GetComponent<Button>().onClick.AddListener(QuitGame);

        // Cancel button.
        var cancelGo = MakeButton("CancelBtn", panel, "Cancel", CancelBg, CancelTxt);
        var caRt = cancelGo.GetComponent<RectTransform>();
        caRt.anchorMin = caRt.anchorMax = new Vector2(0.5f, 0f);
        caRt.pivot     = new Vector2(0f, 0f);
        caRt.sizeDelta = new Vector2(180f, 52f);
        caRt.anchoredPosition = new Vector2(16f, 34f);
        cancelGo.GetComponent<Button>().onClick.AddListener(CloseDialog);

        // ✕ close button (top-right of the panel).
        var closeGo = MakeButton("CloseBtn", panel, "✕", CloseBg, CloseTxt);
        var clRt = closeGo.GetComponent<RectTransform>();
        clRt.anchorMin = clRt.anchorMax = new Vector2(1f, 1f);
        clRt.pivot     = new Vector2(1f, 1f);
        clRt.sizeDelta = new Vector2(40f, 40f);
        clRt.anchoredPosition = new Vector2(-10f, -10f);
        closeGo.GetComponent<Button>().onClick.AddListener(CloseDialog);

        _dialogRoot.SetActive(false);
    }

    // ── UI helpers (same style as the other Hub HUDs) ─────────────────────────

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
        txt.font            = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize        = fontSize;
        txt.color           = color;
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
}
