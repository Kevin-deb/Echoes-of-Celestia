using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any vehicle root GameObject to enable F-to-enter / V-to-exit interaction.
/// Proximity is detected using the vehicle's renderer world bounds, so it works correctly
/// regardless of where the pivot is placed.
/// </summary>
public sealed class SpaceVehicleSeat : MonoBehaviour
{
    public enum Mode { Ground, Aircraft }

    [Header("Basic Settings")]
    [Tooltip("Ground = land rover, Aircraft = spaceship / plane")]
    public Mode vehicleMode = Mode.Ground;
    [Tooltip("Name shown in the on-screen prompt")]
    public string displayName = "Vehicle";
    [Tooltip("Maximum distance from the player to the nearest point on the vehicle's bounds (metres)")]
    [SerializeField] float enterDistance = 3.5f;
    [Tooltip("Tick if the visible front of the model is opposite to transform.forward (W drives backwards)")]
    public bool invertForward = false;
    [Tooltip("Print debug information to the Console while playing")]
    [SerializeField] bool debugLog = true;

    // The true forward direction of the vehicle, flipped once when invertForward is set.
    // Used by all driving and camera logic so they stay consistent.
    Vector3 ForwardDir => invertForward ? -transform.forward : transform.forward;

    [Header("Ground Vehicle")]
    [SerializeField] float groundMoveSpeed = 13f;
    [SerializeField] float groundTurnSpeed = 90f;

    [Header("Aircraft")]
    [SerializeField] float aircraftForwardSpeed = 18f;
    [SerializeField] float aircraftStrafeSpeed = 14f;
    [SerializeField] float aircraftLiftSpeed = 10f;
    [SerializeField] float aircraftFallSpeed = 5.5f;
    [SerializeField] float aircraftTurnSpeed = 80f;
    [Tooltip("Minimum hover height above the terrain surface (metres)")]
    [SerializeField] float minHoverHeight = 2.5f;

    [Header("Camera (auto-scales with vehicle size)")]
    [Tooltip("Minimum camera height above the vehicle centre. Final height = max(this, max side length * heightSizeMultiplier)")]
    [SerializeField] float camHeightOffset = 2.5f;
    [Tooltip("Minimum camera distance behind the vehicle centre")]
    [SerializeField] float camDistanceBehind = 6f;
    [Tooltip("Height auto-scale multiplier (x max side length of the vehicle bounds)")]
    [SerializeField] float heightSizeMultiplier = 0.55f;
    [Tooltip("Distance auto-scale multiplier (x max side length of the vehicle bounds)")]
    [SerializeField] float distanceSizeMultiplier = 1.25f;
    [SerializeField] float camFollowSpeed = 10f;

    // ── Runtime state ─────────────────────────────────────────────────────────
    static SpaceVehicleSeat s_activeVehicle;

    /// <summary>Transform of the vehicle the player is currently driving; null when on foot.</summary>
    public static Transform ActiveOccupiedTransform =>
        s_activeVehicle != null ? s_activeVehicle.transform : null;

    public static bool IsOccupied => s_activeVehicle != null;

    static GameObject s_promptRoot;
    static Text       s_promptText;

    GameObject           _player;
    HubSimpleThirdPerson _playerController;
    CharacterController  _characterController;

    // Snapshot of each renderer's enabled state taken on entry.
    // Restored on exit to avoid re-enabling renderers that were disabled by HubKleeModelRuntimeFit
    // (which would cause a duplicate character model to appear after exiting the vehicle).
    Dictionary<Renderer, bool> _playerRendererState;

    Renderer[] _ownRenderers;
    Bounds     _ownBounds;
    bool       _hasBounds;

    // Vertical offset from the vehicle pivot to the bottom of its bounds.
    // Used to snap the vehicle so its base (tyres / hull) sits on the terrain surface.
    float _bottomOffset;

    bool  _occupied;
    float _verticalVelocity;

    // Free-fall landing state for an unoccupied aircraft
    bool  _isLanding;
    float _landingVelocity;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _ownRenderers = GetComponentsInChildren<Renderer>(true);
        RecomputeBounds();
        _bottomOffset = _hasBounds ? Mathf.Max(0f, transform.position.y - _ownBounds.min.y) : 0f;
        EnsurePromptUI();
        TryCachePlayer();

        if (debugLog)
        {
            Debug.Log($"[SpaceVehicleSeat] '{name}' started | mode={vehicleMode} " +
                      $"| renderers={_ownRenderers.Length} | bounds={_ownBounds} " +
                      $"| bottomOffset={_bottomOffset:F2} " +
                      $"| player={(_player != null ? _player.name : "<not found>")}");
        }
    }

    void Update()
    {
        // Unoccupied aircraft free-fall — runs every frame independently of player input.
        if (!_occupied && _isLanding && vehicleMode == Mode.Aircraft)
            TickLanding();

        if (_player == null) { TryCachePlayer(); return; }
        if (_occupied) { HandleOccupied(); return; }
        if (s_activeVehicle != null) return;

        RecomputeBounds();
        var playerPos = GetPlayerCenter();
        var closest   = _hasBounds ? _ownBounds.ClosestPoint(playerPos) : transform.position;
        var dist      = Vector3.Distance(playerPos, closest);

        if (dist <= enterDistance)
        {
            ShowPrompt($"Press F to enter {displayName}");
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (debugLog) Debug.Log($"[SpaceVehicleSeat] F pressed, entering '{name}' | dist={dist:F2}m");
                Enter();
            }
        }
        else if (IsShowingMyPrompt())
        {
            HidePrompt();
        }
    }

    void LateUpdate()
    {
        if (!_occupied) return;
        FollowCamera(snap: false);
    }

    // ── Enter / Exit ──────────────────────────────────────────────────────────

    void Enter()
    {
        _occupied         = true;
        s_activeVehicle   = this;
        _verticalVelocity = 0f;
        // Cancel any in-progress free-fall landing when the player re-enters.
        _isLanding        = false;
        _landingVelocity  = 0f;

        if (_player == null) TryCachePlayer();

        if (_playerController    != null) _playerController.enabled    = false;
        if (_characterController != null) _characterController.enabled = false;
        HidePlayerSnapshot();

        FollowCamera(snap: true);

        var hint = vehicleMode == Mode.Aircraft
            ? "WASD to fly  |  Hold Space to ascend  |  V to exit"
            : "WASD to drive  |  V to exit";
        ShowPrompt($"{displayName}:  {hint}");
    }

    void Exit()
    {
        // 1) Pick an exit position to the right of the vehicle, outside its bounds.
        var sideOffset = (_hasBounds ? _ownBounds.extents.x : 2f) + 1.5f;
        var probePos   = transform.position
                       - transform.right * sideOffset
                       + Vector3.up * 5f; // raycast origin elevated above ground

        // 2) Snap to terrain so the player doesn't spawn in mid-air or underground.
        var snapped     = SnapToGround(probePos);
        var groundFound = !Mathf.Approximately(snapped.y, probePos.y);
        var groundY     = groundFound ? snapped.y : transform.position.y; // fallback: use vehicle Y

        // 3) Compute the position so the player's foot bottom sits just above the terrain.
        //    CharacterController's pivot is at waist height, so we offset by half height.
        var exitPos = new Vector3(snapped.x, groundY, snapped.z);
        if (_characterController != null)
        {
            var footYOffset = _characterController.center.y - _characterController.height * 0.5f;
            exitPos.y = groundY + 0.05f - footYOffset;
        }

        if (_player != null)
        {
            _player.transform.position = exitPos;
            _player.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        // 4) Force physics transform sync before re-enabling the CharacterController
        //    so it uses the new position rather than the old one.
        Physics.SyncTransforms();

        if (_characterController != null) _characterController.enabled = true;
        if (_playerController    != null) _playerController.enabled    = true;
        RestorePlayerSnapshot();

        _occupied = false;
        if (s_activeVehicle == this) s_activeVehicle = null;
        HidePrompt();

        // Aircraft exited in mid-air: begin gravity-driven descent.
        if (vehicleMode == Mode.Aircraft)
        {
            _isLanding       = true;
            _landingVelocity = 0f;
        }

        if (debugLog)
        {
            Debug.Log($"[SpaceVehicleSeat] Player exited '{name}' | exitPos={exitPos} " +
                      $"| groundY={groundY:F2} | groundFound={groundFound}");
        }
    }

    /// <summary>
    /// Gravity-driven descent for an unoccupied aircraft.
    /// Accelerates up to a terminal velocity cap, then stops when the vehicle base touches the terrain.
    /// </summary>
    void TickLanding()
    {
        // aircraftFallSpeed is used as the terminal velocity to prevent a hard crash impact.
        _landingVelocity -= 9.8f * Time.deltaTime;
        _landingVelocity  = Mathf.Max(_landingVelocity, -aircraftFallSpeed);

        var pos = transform.position;
        pos.y += _landingVelocity * Time.deltaTime;
        transform.position = pos;

        if (TryGetGroundY(transform.position, out var groundY))
        {
            var restY = groundY + _bottomOffset;
            if (transform.position.y <= restY)
            {
                pos   = transform.position;
                pos.y = restY;
                transform.position = pos;
                _isLanding       = false;
                _landingVelocity = 0f;
                if (debugLog) Debug.Log($"[SpaceVehicleSeat] '{name}' landed | groundY={groundY:F2}");
            }
        }
    }

    // ── Vehicle controls ──────────────────────────────────────────────────────

    void HandleOccupied()
    {
        if (Input.GetKeyDown(KeyCode.V)) { Exit(); return; }

        if (vehicleMode == Mode.Ground) DriveGround();
        else                            FlyAircraft();
    }

    void DriveGround()
    {
        var fwd  = Input.GetAxisRaw("Vertical");
        var turn = Input.GetAxisRaw("Horizontal");

        if (Mathf.Abs(turn) > 0.01f)
            transform.Rotate(0f, turn * groundTurnSpeed * Time.deltaTime, 0f, Space.World);

        if (Mathf.Abs(fwd) > 0.01f)
            transform.position += ForwardDir * fwd * groundMoveSpeed * Time.deltaTime;

        SnapVehicleToGround();
    }

    void FlyAircraft()
    {
        var fwd        = Input.GetAxisRaw("Vertical");
        var side       = Input.GetAxisRaw("Horizontal");
        var liftTarget = Input.GetKey(KeyCode.Space) ? aircraftLiftSpeed : -aircraftFallSpeed;

        _verticalVelocity = Mathf.Lerp(_verticalVelocity, liftTarget, 3f * Time.deltaTime);

        var move = ForwardDir      * (fwd  * aircraftForwardSpeed)
                 + transform.right * (side * aircraftStrafeSpeed)
                 + Vector3.up      * _verticalVelocity;

        transform.position += move * Time.deltaTime;

        if (Mathf.Abs(side) > 0.01f)
            transform.Rotate(0f, side * aircraftTurnSpeed * 0.35f * Time.deltaTime, 0f, Space.World);

        if (TryGetGroundY(transform.position, out var groundY))
        {
            // Minimum flight altitude = terrain + bottomOffset (hull base) + hover buffer
            var minY = groundY + _bottomOffset + minHoverHeight;
            if (transform.position.y < minY)
            {
                transform.position = new Vector3(transform.position.x, minY, transform.position.z);
                if (_verticalVelocity < 0f) _verticalVelocity = 0f;
            }
        }
    }

    /// <summary>Snap the vehicle so its bounds base aligns with the terrain, preventing the hull from sinking.</summary>
    void SnapVehicleToGround()
    {
        if (!TryGetGroundY(transform.position, out var groundY)) return;
        var pos = transform.position;
        pos.y   = groundY + _bottomOffset;
        transform.position = pos;
    }

    bool TryGetGroundY(Vector3 pos, out float groundY)
    {
        groundY = 0f;
        var origin = pos + Vector3.up * 200f;
        var hits   = Physics.RaycastAll(origin, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            groundY = h.point.y;
            return true;
        }
        return false;
    }

    // ── Camera follow ─────────────────────────────────────────────────────────

    void FollowCamera(bool snap)
    {
        var cam = Camera.main;
        if (cam == null) return;

        // Auto-scale camera offset from the vehicle's actual size so the full vehicle is visible.
        RecomputeBounds();
        var pivot   = _hasBounds ? _ownBounds.center : transform.position;
        var sizeMax = _hasBounds
            ? Mathf.Max(_ownBounds.size.x, _ownBounds.size.y, _ownBounds.size.z)
            : 4f;

        var distance  = Mathf.Max(camDistanceBehind, sizeMax * distanceSizeMultiplier);
        var height    = Mathf.Max(camHeightOffset,   sizeMax * heightSizeMultiplier);

        // Camera sits behind the front of the vehicle, so we use -ForwardDir.
        var target    = pivot - ForwardDir * distance + Vector3.up * height;
        var lookAt    = pivot + Vector3.up * (sizeMax * 0.15f); // slightly above centre keeps horizon centred
        var targetRot = Quaternion.LookRotation((lookAt - target).normalized, Vector3.up);

        if (snap)
        {
            cam.transform.SetPositionAndRotation(target, targetRot);
        }
        else
        {
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, camFollowSpeed * Time.deltaTime);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRot, camFollowSpeed * Time.deltaTime);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void RecomputeBounds()
    {
        _hasBounds = false;
        if (_ownRenderers == null || _ownRenderers.Length == 0) return;

        var first = true;
        foreach (var r in _ownRenderers)
        {
            if (r == null || !r.enabled) continue;
            if (first) { _ownBounds = r.bounds; first = false; }
            else       { _ownBounds.Encapsulate(r.bounds); }
        }
        _hasBounds = !first;
    }

    Vector3 GetPlayerCenter()
    {
        if (_characterController != null && _characterController.enabled)
            return _player.transform.TransformPoint(_characterController.center);
        return _player.transform.position + Vector3.up * 0.9f;
    }

    Vector3 SnapToGround(Vector3 pos)
    {
        var origin = pos + Vector3.up * 80f;
        var hits   = Physics.RaycastAll(origin, Vector3.down, 180f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            return new Vector3(pos.x, h.point.y, pos.z);
        }
        return pos;
    }

    void TryCachePlayer()
    {
        _player = GameObject.FindGameObjectWithTag("Player");
        if (_player == null) return;

        _playerController    = _player.GetComponent<HubSimpleThirdPerson>();
        _characterController = _player.GetComponent<CharacterController>();
    }

    void HidePlayerSnapshot()
    {
        if (_player == null) return;
        _playerRendererState = new Dictionary<Renderer, bool>();
        foreach (var r in _player.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            _playerRendererState[r] = r.enabled;
            r.enabled = false;
        }
    }

    void RestorePlayerSnapshot()
    {
        if (_playerRendererState == null) return;
        foreach (var kv in _playerRendererState)
            if (kv.Key != null) kv.Key.enabled = kv.Value;
        _playerRendererState = null;
    }

    // ── Prompt UI ─────────────────────────────────────────────────────────────

    static void EnsurePromptUI()
    {
        if (s_promptRoot != null && s_promptText != null) return;

        var existing = GameObject.Find("VehiclePromptCanvas");
        if (existing != null)
        {
            var panel = existing.transform.Find("VehiclePromptPanel");
            if (panel != null)
            {
                s_promptRoot = panel.gameObject;
                s_promptText = s_promptRoot.GetComponentInChildren<Text>(true);
                if (s_promptText != null) return;
            }
            Object.Destroy(existing);
        }

        var canvasGo = new GameObject("VehiclePromptCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panelGo = new GameObject("VehiclePromptPanel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        var rt = panelGo.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 110f);
        rt.sizeDelta        = new Vector2(820f, 64f);
        panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
        panelGo.SetActive(false);

        var textGo = new GameObject("Text",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(panelGo.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize  = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color     = Color.white;

        s_promptRoot = panelGo;
        s_promptText = text;
    }

    static void ShowPrompt(string msg)
    {
        EnsurePromptUI();
        if (s_promptRoot != null) s_promptRoot.SetActive(true);
        if (s_promptText != null) s_promptText.text = msg;
    }

    static void HidePrompt()
    {
        if (s_promptRoot != null) s_promptRoot.SetActive(false);
    }

    bool IsShowingMyPrompt()
    {
        return s_promptRoot != null
            && s_promptRoot.activeSelf
            && s_promptText != null
            && !string.IsNullOrEmpty(s_promptText.text)
            && s_promptText.text.Contains(displayName);
    }

    // ── Editor Gizmo: visualise the interaction range in the Scene view ───────

    void OnDrawGizmosSelected()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
        var expanded = b;
        expanded.Expand(enterDistance * 2f);
        Gizmos.DrawWireCube(expanded.center, expanded.size);

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.85f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
