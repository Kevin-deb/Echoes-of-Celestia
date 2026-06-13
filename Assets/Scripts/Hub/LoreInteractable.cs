using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在 Hub 场景任意道具上。玩家靠近并按 F 键后，打开剧情阅读窗口。
/// 所有道具共享一个提示 UI 实例，不会同时出现多个提示框。
/// </summary>
public sealed class LoreInteractable : MonoBehaviour
{
    // Registry of all active lore interactables, used by the main-story path guide
    // to locate the object bound to a given chapter by title.
    public static readonly List<LoreInteractable> All = new List<LoreInteractable>();

    public string EntryTitle    => entryTitle;
    public string CategoryLabel => categoryLabel;
    public bool   IsMainStory   =>
        !string.IsNullOrEmpty(categoryLabel) && categoryLabel.Contains("Main Chronicle");

    [Header("Interaction")]
    [Tooltip("从对象 pivot 到玩家的最大交互距离（米）")]
    [SerializeField] float interactRange = 7f;
    [Tooltip("出现在屏幕底部的简短提示文字（全英文）")]
    [SerializeField] string interactPrompt = "Press F to read records";

    [Header("Lore")]
    [Tooltip("显示在窗口顶部的分类标签，如 Main Chronicle · Volume I")]
    [SerializeField] string categoryLabel = "Main Chronicle";
    [Tooltip("该段剧情的标题")]
    [SerializeField] string entryTitle = "Untitled Entry";
    [Tooltip("每个元素为窗口中的一页文本，支持 \\n 换行")]
    [TextArea(5, 14)]
    [SerializeField] string[] pages = { "No content." };

    // ── 共享提示 UI ───────────────────────────────────────────────────────────
    static GameObject        s_promptRoot;
    static Text              s_promptText;
    static LoreInteractable  s_activePromptOwner;

    // ── 缓存 ──────────────────────────────────────────────────────────────────
    Transform _playerTransform;

    // ── Unity 生命周期 ────────────────────────────────────────────────────────

    void Awake()
    {
        EnsurePromptUI();
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void Start()
    {
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null) _playerTransform = playerGo.transform;
    }

    void Update()
    {
        // 阅读 UI 打开时，所有 LoreInteractable 暂停检测
        if (LoreReadingUI.IsAnyOpen) { HideMyPrompt(); return; }

        // 玩家在载具内时不检测
        if (SpaceVehicleSeat.IsOccupied) { HideMyPrompt(); return; }

        if (_playerTransform == null)
        {
            var pg = GameObject.FindGameObjectWithTag("Player");
            if (pg != null) _playerTransform = pg.transform;
            else return;
        }

        float dist = Vector3.Distance(transform.position, _playerTransform.position);
        if (dist <= interactRange)
        {
            // Main-story chapters are gated: unread chapters can only be opened in story mode,
            // strictly in journey order. Already-read chapters can be revisited freely.
            if (IsMainStory)
            {
                var gate = MainStoryFlow.GetChapterGate(entryTitle, out var lockReason);
                switch (gate)
                {
                    case MainStoryFlow.ChapterGate.AlreadyRead:
                        ShowPrompt("Press F to revisit this chapter  [ F ]");
                        if (Input.GetKeyDown(KeyCode.F))
                            LoreReadingUI.Instance?.Open(categoryLabel, entryTitle, pages);
                        break;

                    case MainStoryFlow.ChapterGate.ReadableNow:
                        ShowPrompt($"{interactPrompt}  [ F ]");
                        if (Input.GetKeyDown(KeyCode.F))
                        {
                            LoreReadingUI.Instance?.Open(categoryLabel, entryTitle, pages);
                            MainStoryProgress.MarkReadByTitle(entryTitle);
                        }
                        break;

                    case MainStoryFlow.ChapterGate.NeedStoryMode:
                        ShowPrompt("Main Story chapter — open Main Story [ J ] and press Start to begin");
                        break;

                    default: // LockedByOrder
                        ShowPrompt(string.IsNullOrEmpty(lockReason)
                            ? "Sealed — complete the current objective first"
                            : $"Sealed — current objective: {lockReason}");
                        break;
                }
            }
            else
            {
                ShowPrompt($"{interactPrompt}  [ F ]");
                if (Input.GetKeyDown(KeyCode.F))
                    LoreReadingUI.Instance?.Open(categoryLabel, entryTitle, pages);
            }
        }
        else
        {
            HideMyPrompt();
        }
    }

    void OnDisable()
    {
        HideMyPrompt();
        All.Remove(this);
    }

    /// <summary>Opens this entry's reading window directly (used by the Main Story window
    /// to let the player revisit recovered chapters).</summary>
    public void OpenReader() => LoreReadingUI.Instance?.Open(categoryLabel, entryTitle, pages);

    // ── 提示 UI ───────────────────────────────────────────────────────────────

    void ShowPrompt(string msg)
    {
        EnsurePromptUI();
        s_activePromptOwner = this;
        if (s_promptRoot != null) s_promptRoot.SetActive(true);
        if (s_promptText != null) s_promptText.text = msg;
    }

    void HideMyPrompt()
    {
        if (s_activePromptOwner != this) return;
        s_activePromptOwner = null;
        if (s_promptRoot != null) s_promptRoot.SetActive(false);
    }

    static void EnsurePromptUI()
    {
        if (s_promptRoot != null && s_promptText != null) return;

        var existing = GameObject.Find("LorePromptCanvas");
        if (existing != null)
        {
            s_promptRoot = existing.transform.Find("LorePromptPanel")?.gameObject;
            if (s_promptRoot != null)
            {
                s_promptText = s_promptRoot.GetComponentInChildren<Text>(true);
                if (s_promptText != null) return;
            }
            Object.Destroy(existing);
        }

        var canvasGo = new GameObject("LorePromptCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panel = new GameObject("LorePromptPanel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);

        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 100f);
        rt.sizeDelta = new Vector2(700f, 52f);

        panel.GetComponent<Image>().color = new Color(0.03f, 0.06f, 0.12f, 0.82f);
        panel.SetActive(false);

        var textGo = new GameObject("Text",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(panel.transform, false);

        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize  = 22;
        text.alignment = TextAnchor.MiddleCenter;
        text.color     = new Color(0.55f, 0.85f, 1f);
        text.fontStyle = FontStyle.Bold;

        s_promptRoot = panel;
        s_promptText = text;
    }

    // ── Gizmo（Scene 视图显示交互范围）────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
        Gizmos.DrawSphere(transform.position, 0.25f);
    }
}
