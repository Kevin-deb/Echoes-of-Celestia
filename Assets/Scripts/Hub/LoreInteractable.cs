using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any prop in the Hub. When the player gets close and presses F, it
/// opens the lore reading window. Every prop shares one prompt UI instance, so you
/// never end up with two prompt boxes on screen at once.
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
    [Tooltip("Max interaction distance from this object's pivot to the player, in metres.")]
    [SerializeField] float interactRange = 7f;
    [Tooltip("Short hint shown along the bottom of the screen.")]
    [SerializeField] string interactPrompt = "Press F to read records";

    [Header("Lore")]
    [Tooltip("Category label shown at the top of the window, e.g. Main Chronicle · Volume I.")]
    [SerializeField] string categoryLabel = "Main Chronicle";
    [Tooltip("Title of this lore entry.")]
    [SerializeField] string entryTitle = "Untitled Entry";
    [Tooltip("One element per page of text; use \\n for a line break.")]
    [TextArea(5, 14)]
    [SerializeField] string[] pages = { "No content." };

    // Shared prompt UI — one instance across every interactable
    static GameObject        s_promptRoot;
    static Text              s_promptText;
    static LoreInteractable  s_activePromptOwner;

    // Cached lookups
    Transform _playerTransform;

    // Unity lifecycle

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
        // While the reading window is open, nothing should react
        if (LoreReadingUI.IsAnyOpen) { HideMyPrompt(); return; }

        // Don't react while the player is sitting in a vehicle
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

    // Prompt UI

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

    // Gizmo — draws the interaction range in the Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
        Gizmos.DrawSphere(transform.position, 0.25f);
    }
}
