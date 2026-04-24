using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// Kid-friendly solar system explorer: ray pick, smooth camera focus, facts panel, overview reset, arrow keys to strafe.
/// </summary>
[RequireComponent(typeof(Camera))]
public class SolarSystemExplorer : MonoBehaviour
{
    [Header("Explore (arrow keys)")]
    [Tooltip("Camera strafe speed when holding arrow keys / WASD")]
    public float arrowStrafeSpeed = 4f;

    [Header("Click to focus")]
    [Tooltip("Camera distance from the body center when focused")]
    public float focusDistance = 3.2f;

    [Tooltip("Position smoothing time (smaller = snappier)")]
    public float positionSmoothTime = 0.45f;

    [Tooltip("Rotation smoothing factor")]
    public float rotationSmooth = 6f;

    [Header("Visual feedback")]
    [Tooltip("Brief scale pulse multiplier when a body is clicked")]
    public float pulseScaleMultiplier = 1.12f;

    [Tooltip("Scale pulse duration in seconds")]
    public float pulseDuration = 0.35f;

    [Header("Audio (optional)")]
    public AudioSource clickAudioSource;

    [Header("UI")]
    [Tooltip("Quit button width in pixels")]
    public float quitButtonWidth = 100f;

    [Tooltip("Quit button height in pixels")]
    public float quitButtonHeight = 34f;

    Camera _cam;
    Vector3 _overviewPosition;
    Quaternion _overviewRotation;
    bool _hasOverview;

    Transform _focusTarget;
    Vector3 _goalPosition;
    Quaternion _goalRotation;
    Vector3 _velPos;

    string _panelTitle = "";
    string _panelBody = "";
    bool _showPanel;

    GUIStyle _titleStyle;
    GUIStyle _bodyStyle;
    GUIStyle _hintStyle;
    GUIStyle _quitButtonStyle;
    bool _guiInited;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (clickAudioSource == null)
            clickAudioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        _overviewPosition = transform.position;
        _overviewRotation = transform.rotation;
        _hasOverview = true;
        _goalPosition = transform.position;
        _goalRotation = transform.rotation;
    }

    void Update()
    {
        HandleArrowExploration();
        HandleClickSelect();
        HandleReturnInput();
        SmoothTowardGoal();
    }

    void HandleArrowExploration()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
            return;

        Vector3 delta = (transform.right * h + transform.up * v) * (arrowStrafeSpeed * Time.deltaTime);
        transform.position += delta;
        _overviewPosition += delta;
        _goalPosition += delta;
    }

    void HandleClickSelect()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 2000f);
        if (hits == null || hits.Length == 0)
            return;

        var ordered = hits.OrderBy(h => h.distance).ToArray();
        foreach (RaycastHit hit in ordered)
        {
            CelestialObjectData data = hit.collider.GetComponentInParent<CelestialObjectData>();
            if (data == null)
                continue;

            FocusOn(data, hit.collider.transform);
            break;
        }
    }

    void HandleReturnInput()
    {
        if (!_hasOverview)
            return;

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Escape))
            ReturnToOverview();
    }

    void FocusOn(CelestialObjectData data, Transform hitTransform)
    {
        _focusTarget = hitTransform;
        Vector3 targetPos = hitTransform.position;
        Vector3 fromCam = transform.position - targetPos;
        if (fromCam.sqrMagnitude < 0.01f)
            fromCam = -transform.forward;
        fromCam.Normalize();

        _goalPosition = targetPos + fromCam * focusDistance;
        _goalRotation = Quaternion.LookRotation(targetPos - _goalPosition, Vector3.up);
        _velPos = Vector3.zero;

        _panelTitle = data.displayName;
        _panelBody = data.kidFriendlyFact;
        _showPanel = !string.IsNullOrEmpty(_panelBody);

        if (data.clickSound != null && clickAudioSource != null)
            clickAudioSource.PlayOneShot(data.clickSound);

        StopAllCoroutines();
        StartCoroutine(PulseScale(hitTransform));
    }

    IEnumerator PulseScale(Transform t)
    {
        Vector3 baseScale = t.localScale;
        float half = pulseDuration * 0.5f;
        float tUp = 0f;
        while (tUp < half)
        {
            tUp += Time.deltaTime;
            float k = Mathf.Clamp01(tUp / half);
            t.localScale = Vector3.Lerp(baseScale, baseScale * pulseScaleMultiplier, k);
            yield return null;
        }

        float tDown = 0f;
        while (tDown < half)
        {
            tDown += Time.deltaTime;
            float k = Mathf.Clamp01(tDown / half);
            t.localScale = Vector3.Lerp(baseScale * pulseScaleMultiplier, baseScale, k);
            yield return null;
        }

        t.localScale = baseScale;
    }

    void ReturnToOverview()
    {
        _focusTarget = null;
        _goalPosition = _overviewPosition;
        _goalRotation = _overviewRotation;
        _velPos = Vector3.zero;
        _showPanel = false;
        _panelTitle = "";
        _panelBody = "";
    }

    void SmoothTowardGoal()
    {
        transform.position = Vector3.SmoothDamp(transform.position, _goalPosition, ref _velPos, positionSmoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, _goalRotation, Time.deltaTime * rotationSmooth);

        if (_focusTarget != null)
        {
            Vector3 lookPoint = _focusTarget.position;
            Quaternion look = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * rotationSmooth);
        }
    }

    void OnGUI()
    {
        InitGuiStylesIfNeeded();

        const float margin = 16f;
        float hintMaxW = Mathf.Max(120f, Screen.width - quitButtonWidth - margin * 3f);
        GUI.Label(new Rect(margin, margin, hintMaxW, 52f),
            "Arrows / WASD: move view  |  Left-click: select a body  |  Right-click / R / Esc: overview",
            _hintStyle);

        float qx = Screen.width - quitButtonWidth - margin;
        if (GUI.Button(new Rect(qx, margin, quitButtonWidth, quitButtonHeight), "Quit", _quitButtonStyle))
            QuitGame();

        if (!_showPanel)
            return;

        float boxH = 150f;
        float boxW = Mathf.Min(720f, Screen.width - margin * 2);
        float x = margin;
        float y = Screen.height - boxH - margin;

        GUI.Box(new Rect(x, y, boxW, boxH), GUIContent.none);
        GUI.Label(new Rect(x + 12f, y + 10f, boxW - 24f, 32f), _panelTitle, _titleStyle);
        GUI.Label(new Rect(x + 12f, y + 44f, boxW - 24f, boxH - 54f), _panelBody, _bodyStyle);
    }

    void InitGuiStylesIfNeeded()
    {
        if (_guiInited)
            return;
        _guiInited = true;

        _hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = new Color(0.7f, 0.95f, 1f) }
        };

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.7f, 0.95f) }
        };

        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            wordWrap = true,
            normal = { textColor = new Color(1f, 0.99f, 0.95f) }
        };

        _quitButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.4f, 0.2f, 0.5f) }
        };
    }

    static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
