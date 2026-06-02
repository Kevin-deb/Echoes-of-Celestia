using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在任意载具根 GameObject 上即可启用 F 进入 / V 离开交互。
/// 距离判定基于载具渲染器的世界 Bounds，所以无论载具 pivot 在哪都能稳定触发。
/// </summary>
public sealed class SpaceVehicleSeat : MonoBehaviour
{
    public enum Mode { Ground, Aircraft }

    [Header("基本设置")]
    [Tooltip("漫游车 = Ground，飞船/飞机 = Aircraft")]
    public Mode vehicleMode = Mode.Ground;
    [Tooltip("在 UI 上显示的名称")]
    public string displayName = "载具";
    [Tooltip("玩家到载具外壳的最大距离（米）")]
    [SerializeField] float enterDistance = 3.5f;
    [Tooltip("若模型的可见车头与 transform.forward 相反（按 W 反而后退），勾上以翻转前后方向。")]
    public bool invertForward = false;
    [Tooltip("勾选后会在 Console 输出调试信息，定位问题时打开")]
    [SerializeField] bool debugLog = true;

    // 真正"车头"方向：根据 invertForward 翻转一次，驾驶 / 相机均使用此向量。
    Vector3 ForwardDir => invertForward ? -transform.forward : transform.forward;

    [Header("地面载具")]
    [SerializeField] float groundMoveSpeed = 13f;
    [SerializeField] float groundTurnSpeed = 90f;

    [Header("飞行载具")]
    [SerializeField] float aircraftForwardSpeed = 18f;
    [SerializeField] float aircraftStrafeSpeed = 14f;
    [SerializeField] float aircraftLiftSpeed = 10f;
    [SerializeField] float aircraftFallSpeed = 5.5f;
    [SerializeField] float aircraftTurnSpeed = 80f;
    [Tooltip("距地面的最小悬停高度（米）")]
    [SerializeField] float minHoverHeight = 2.5f;

    [Header("相机（值会与载具尺寸取较大者，保证看到整车）")]
    [Tooltip("相机距载具中心的最低高度，最终高度 = max(此值, 载具最大边长 × heightSizeMultiplier)")]
    [SerializeField] float camHeightOffset = 2.5f;
    [Tooltip("相机距载具中心的最低水平距离")]
    [SerializeField] float camDistanceBehind = 6f;
    [Tooltip("相机高度自适应系数（× 载具最大边长）")]
    [SerializeField] float heightSizeMultiplier = 0.55f;
    [Tooltip("相机距离自适应系数（× 载具最大边长）")]
    [SerializeField] float distanceSizeMultiplier = 1.25f;
    [SerializeField] float camFollowSpeed = 10f;

    // ── 运行时状态 ────────────────────────────────────────────────────────────
    static SpaceVehicleSeat s_activeVehicle;

    /// <summary>玩家当前正在驾驶的载具 Transform；未驾驶时为 null。</summary>
    public static Transform ActiveOccupiedTransform =>
        s_activeVehicle != null ? s_activeVehicle.transform : null;

    public static bool IsOccupied => s_activeVehicle != null;
    static GameObject       s_promptRoot;
    static Text             s_promptText;

    GameObject               _player;
    HubSimpleThirdPerson     _playerController;
    CharacterController      _characterController;
    // Key = Renderer, Value = 进入载具前的 enabled 状态。退出时按这份快照恢复，
    // 避免把本来就该被禁用的渲染器（如运行时被替换掉的占位 FBX）也点亮，
    // 导致退出后出现两个角色模型。
    Dictionary<Renderer, bool> _playerRendererState;

    Renderer[] _ownRenderers;
    Bounds     _ownBounds;
    bool       _hasBounds;
    // 载具 pivot 距底部包围盒的 Y 偏移，用于让轮胎贴地而非陷地。Awake/Start 后只算一次。
    float      _bottomOffset;

    bool       _occupied;
    float      _verticalVelocity;
    // 飞行载具空载下落状态
    bool       _isLanding;
    float      _landingVelocity;

    // ── Unity 生命周期 ────────────────────────────────────────────────────────

    void Awake()
    {
        _ownRenderers = GetComponentsInChildren<Renderer>(true);
        RecomputeBounds();
        _bottomOffset = _hasBounds ? Mathf.Max(0f, transform.position.y - _ownBounds.min.y) : 0f;
        EnsurePromptUI();
        TryCachePlayer();

        if (debugLog)
        {
            Debug.Log($"[SpaceVehicleSeat] '{name}' 启动 | mode={vehicleMode} " +
                      $"| renderers={_ownRenderers.Length} | bounds={_ownBounds} " +
                      $"| bottomOffset={_bottomOffset:F2} " +
                      $"| player={(_player != null ? _player.name : "<未找到>")}");
        }
    }

    void Update()
    {
        // 无人飞行载具的自由落体着陆——独立于玩家交互，每帧都跑一次。
        if (!_occupied && _isLanding && vehicleMode == Mode.Aircraft)
            TickLanding();

        if (_player == null) { TryCachePlayer(); return; }

        if (_occupied) { HandleOccupied(); return; }

        if (s_activeVehicle != null) return;

        RecomputeBounds();
        var playerPos   = GetPlayerCenter();
        var closest     = _hasBounds ? _ownBounds.ClosestPoint(playerPos) : transform.position;
        var dist        = Vector3.Distance(playerPos, closest);

        if (dist <= enterDistance)
        {
            ShowPrompt($"按 F 进入 {displayName}");
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (debugLog) Debug.Log($"[SpaceVehicleSeat] F 按下，进入 '{name}' | dist={dist:F2}m");
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

    // ── 进入 / 离开 ───────────────────────────────────────────────────────────

    void Enter()
    {
        _occupied = true;
        s_activeVehicle = this;
        _verticalVelocity = 0f;
        // 玩家进入时取消任何残留的着陆下落
        _isLanding = false;
        _landingVelocity = 0f;

        if (_player == null) TryCachePlayer();

        if (_playerController != null) _playerController.enabled = false;
        if (_characterController != null) _characterController.enabled = false;
        HidePlayerSnapshot();

        FollowCamera(snap: true);

        var modeHint = vehicleMode == Mode.Aircraft
            ? "WASD 飞行，按住空格升空，V 离开"
            : "WASD 驾驶，V 离开";
        ShowPrompt($"{displayName}：{modeHint}");
    }

    void Exit()
    {
        // 1) 从载具右侧 bounds 外 + 安全距离 选一个候选下车点。
        var sideOffset = (_hasBounds ? _ownBounds.extents.x : 2f) + 1.5f;
        var probePos = transform.position
                     - transform.right * sideOffset
                     + Vector3.up * 5f; // 起测高度，向下射地面

        // 2) 用射线打到地面，避免悬空。
        var snapped     = SnapToGround(probePos);
        var groundFound = !Mathf.Approximately(snapped.y, probePos.y);
        var groundY     = groundFound ? snapped.y : transform.position.y; // 兜底：用载具高度

        // 3) 把玩家放到 "脚底刚好贴地 + 一点冗余" 的位置，CC 才不会陷进地里。
        var exitPos = new Vector3(snapped.x, groundY, snapped.z);
        if (_characterController != null)
        {
            // CharacterController 脚底相对 transform 的偏移
            var footYOffset = _characterController.center.y - _characterController.height * 0.5f;
            // 让 (transform.y + footYOffset) ≈ groundY + 0.05
            exitPos.y = groundY + 0.05f - footYOffset;
        }

        if (_player != null)
        {
            _player.transform.position = exitPos;
            _player.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        // 4) 强制同步 Transform，避免 CharacterController.enable 后用旧位置做物理求解。
        Physics.SyncTransforms();

        if (_characterController != null) _characterController.enabled = true;
        if (_playerController != null) _playerController.enabled = true;
        RestorePlayerSnapshot();

        _occupied = false;
        if (s_activeVehicle == this) s_activeVehicle = null;
        HidePrompt();

        // 飞行载具如果在空中被离开，开启自由落体着陆
        if (vehicleMode == Mode.Aircraft)
        {
            _isLanding = true;
            _landingVelocity = 0f;
        }

        if (debugLog)
        {
            Debug.Log($"[SpaceVehicleSeat] 玩家离开 '{name}' | exitPos={exitPos} " +
                      $"| groundY={groundY:F2} | groundFound={groundFound}");
        }
    }

    /// <summary>
    /// 无人飞行载具的自由落体：重力加速 + 终端速度上限，落到 bounds 底部贴地后停止。
    /// </summary>
    void TickLanding()
    {
        // 用 aircraftFallSpeed 作为终端下落速度，避免砸得太狠。
        _landingVelocity -= 9.8f * Time.deltaTime;
        _landingVelocity = Mathf.Max(_landingVelocity, -aircraftFallSpeed);

        var pos = transform.position;
        pos.y += _landingVelocity * Time.deltaTime;
        transform.position = pos;

        if (TryGetGroundY(transform.position, out var groundY))
        {
            var restY = groundY + _bottomOffset;
            if (transform.position.y <= restY)
            {
                pos = transform.position;
                pos.y = restY;
                transform.position = pos;
                _isLanding = false;
                _landingVelocity = 0f;
                if (debugLog) Debug.Log($"[SpaceVehicleSeat] '{name}' 着陆完成 | groundY={groundY:F2}");
            }
        }
    }

    // ── 载具控制 ──────────────────────────────────────────────────────────────

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
        var fwd  = Input.GetAxisRaw("Vertical");
        var side = Input.GetAxisRaw("Horizontal");
        var liftTarget = Input.GetKey(KeyCode.Space) ? aircraftLiftSpeed : -aircraftFallSpeed;

        _verticalVelocity = Mathf.Lerp(_verticalVelocity, liftTarget, 3f * Time.deltaTime);

        var move = ForwardDir        * (fwd  * aircraftForwardSpeed)
                 + transform.right   * (side * aircraftStrafeSpeed)
                 + Vector3.up        * _verticalVelocity;

        transform.position += move * Time.deltaTime;

        if (Mathf.Abs(side) > 0.01f)
            transform.Rotate(0f, side * aircraftTurnSpeed * 0.35f * Time.deltaTime, 0f, Space.World);

        if (TryGetGroundY(transform.position, out var groundY))
        {
            // 飞行最低高度 = 地表 + bottomOffset（让底盘对齐地面） + 悬停高度
            var minY = groundY + _bottomOffset + minHoverHeight;
            if (transform.position.y < minY)
            {
                transform.position = new Vector3(transform.position.x, minY, transform.position.z);
                if (_verticalVelocity < 0f) _verticalVelocity = 0f;
            }
        }
    }

    /// <summary>
    /// 把载具按 bounds 底部对齐地面，避免轮胎陷进地里。
    /// </summary>
    void SnapVehicleToGround()
    {
        if (!TryGetGroundY(transform.position, out var groundY)) return;
        var pos = transform.position;
        pos.y = groundY + _bottomOffset;
        transform.position = pos;
    }

    bool TryGetGroundY(Vector3 pos, out float groundY)
    {
        groundY = 0f;
        var origin = pos + Vector3.up * 200f;
        var hits = Physics.RaycastAll(origin, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
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

    // ── 相机跟随 ──────────────────────────────────────────────────────────────

    void FollowCamera(bool snap)
    {
        var cam = Camera.main;
        if (cam == null) return;

        // 根据载具实际尺寸自适应：保证镜头足够远，能完整看到载具。
        RecomputeBounds();
        var pivot = _hasBounds ? _ownBounds.center : transform.position;
        var sizeMax = _hasBounds
            ? Mathf.Max(_ownBounds.size.x, _ownBounds.size.y, _ownBounds.size.z)
            : 4f;

        var distance = Mathf.Max(camDistanceBehind, sizeMax * distanceSizeMultiplier);
        var height   = Mathf.Max(camHeightOffset,   sizeMax * heightSizeMultiplier);

        // 相机要在"车头"反方向（车尾后方），所以用 -ForwardDir
        var target    = pivot - ForwardDir * distance + Vector3.up * height;
        var lookAt    = pivot + Vector3.up * (sizeMax * 0.15f); // 略上抬一点，地平线居中
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

    // ── 辅助方法 ──────────────────────────────────────────────────────────────

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
        var hits = Physics.RaycastAll(origin, Vector3.down, 180f, ~0, QueryTriggerInteraction.Ignore);
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

    // ── Prompt UI ──────────────────────────────────────────────────────────────

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
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panelGo = new GameObject("VehiclePromptPanel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        var rt = panelGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 110f);
        rt.sizeDelta = new Vector2(820f, 64f);
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

    // ── 编辑器辅助：在 Scene 视图把交互范围画出来 ───────────────────────────

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
