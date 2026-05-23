using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 轻量第三人称控制器（偏原神手感）：
/// - 鼠标控制镜头环绕（Yaw/Pitch）
/// - 角色按相机朝向移动并平滑转身
/// - 空格跳跃，镜头平滑跟随与遮挡避让
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class HubSimpleThirdPerson : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 5.8f;
    [SerializeField] float sprintSpeed = 10.8f;
    [SerializeField] float rotationSpeed = 14f;
    [SerializeField] float airControlPercent = 0.45f;
    [SerializeField] float jumpHeight = 1.8f;
    [SerializeField] float gravity = -24f;
    [SerializeField] float coyoteTime = 0.12f;
    [SerializeField] float jumpBufferTime = 0.16f;

    [Header("Stamina")]
    [SerializeField] float staminaMax = 100f;
    [SerializeField] float sprintDrainPerSecond = 30f;
    [SerializeField] float staminaRecoverPerSecond = 24f;
    [SerializeField] float staminaRecoverDelay = 0.7f;
    [SerializeField] Image staminaFillImage;
    [SerializeField] CanvasGroup staminaBarGroup;
    [SerializeField] bool autoCreateWorldStaminaBar = true;

    [Header("Camera Orbit")]
    [SerializeField] float lookSensitivityX = 380f;
    [SerializeField] float lookSensitivityY = 270f;
    [SerializeField] float minPitch = -20f;
    [SerializeField] float maxPitch = 60f;
    [SerializeField] float cameraDistance = 4.6f;
    [SerializeField] float cameraHeight = 1.65f;
    [SerializeField] float cameraSmoothTime = 0.015f;
    [SerializeField] float cameraCollisionRadius = 0.25f;
    [SerializeField] LayerMask cameraCollisionMask = ~0;
    [SerializeField] bool lockCursorOnPlay = true;

    CharacterController _cc;
    Camera _cam;
    Animator _anim;
    Vector3 _horizontalVelocity;
    float _vertical;
    float _yaw;
    float _pitch = 12f;
    float _stamina;
    float _lastSprintTime = -999f;
    float _jumpPressedTime = -999f;
    float _lastGroundedTime = -999f;
    Vector3 _cameraSmoothVelocity;
    Transform _staminaBarRoot;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _anim = GetComponentInChildren<Animator>();
        _stamina = staminaMax;
    }

    void Start()
    {
        _cam = Camera.main;
        if (_cam == null)
        {
            Debug.LogError("HubSimpleThirdPerson: 场景中需要 Tag 为 MainCamera 的摄像机。");
            enabled = false;
            return;
        }

        _yaw = transform.eulerAngles.y;
        EnsureStaminaBar();
        SetStaminaBarVisible(false);
        SnapCameraToCharacterFacing();
        if (lockCursorOnPlay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (_cam == null) return;

        UpdateCameraInput();

        var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();

        var camForward = _cam.transform.forward;
        camForward.y = 0f;
        camForward.Normalize();
        var camRight = _cam.transform.right;
        camRight.y = 0f;
        camRight.Normalize();

        // 基于相机朝向移动：更接近常见第三人称游戏手感。
        var moveWorld = (camForward * input.z + camRight * input.x);
        if (moveWorld.sqrMagnitude > 1f) moveWorld.Normalize();

        var grounded = _cc.isGrounded;
        if (grounded)
            _lastGroundedTime = Time.time;

        if (Input.GetButtonDown("Jump"))
            _jumpPressedTime = Time.time;

        var wantsSprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        var isMoving = moveWorld.sqrMagnitude > 0.01f;
        var canSprint = wantsSprint && isMoving && grounded && _stamina > 0.01f;
        var targetSpeed = canSprint ? sprintSpeed : moveSpeed;

        if (isMoving)
            transform.forward = Vector3.Slerp(transform.forward, moveWorld.normalized, rotationSpeed * Time.deltaTime);

        var control = grounded ? 1f : airControlPercent;
        var targetHorizontal = moveWorld * targetSpeed * control;
        _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, targetHorizontal, (grounded ? 18f : 6f) * Time.deltaTime);

        var canUseCoyoteJump = Time.time - _lastGroundedTime <= coyoteTime;
        var hasBufferedJump = Time.time - _jumpPressedTime <= jumpBufferTime;

        if (grounded && _vertical < 0f)
            _vertical = -2f;

        if (hasBufferedJump && canUseCoyoteJump)
        {
            _vertical = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpPressedTime = -999f;
            grounded = false;
        }

        _vertical += gravity * Time.deltaTime;

        var velocity = _horizontalVelocity + Vector3.up * _vertical;
        _cc.Move(velocity * Time.deltaTime);

        UpdateStamina(canSprint);
        UpdateAnimator(isMoving, canSprint);
    }

    void LateUpdate()
    {
        if (_cam == null) return;
        UpdateCameraFollow();
        UpdateStaminaBarFacing();
    }

    void UpdateCameraInput()
    {
        var mouseX = Input.GetAxis("Mouse X");
        var mouseY = Input.GetAxis("Mouse Y");
        _yaw += mouseX * lookSensitivityX * Time.deltaTime;
        _pitch -= mouseY * lookSensitivityY * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
    }

    void UpdateCameraFollow()
    {
        var pivot = transform.position + Vector3.up * cameraHeight;
        var lookRot = Quaternion.Euler(_pitch, _yaw, 0f);
        var desiredPos = pivot + lookRot * new Vector3(0f, 0f, -cameraDistance);
        var finalPos = ResolveCameraCollision(pivot, desiredPos);
        var smoothedPos = Vector3.SmoothDamp(_cam.transform.position, finalPos, ref _cameraSmoothVelocity, cameraSmoothTime);
        _cam.transform.SetPositionAndRotation(smoothedPos, lookRot);
    }

    void SnapCameraToCharacterFacing()
    {
        if (_cam == null) return;
        var pivot = transform.position + Vector3.up * cameraHeight;
        var lookRot = Quaternion.Euler(_pitch, _yaw, 0f);
        var desiredPos = pivot + lookRot * new Vector3(0f, 0f, -cameraDistance);
        var finalPos = ResolveCameraCollision(pivot, desiredPos);
        _cameraSmoothVelocity = Vector3.zero;
        _cam.transform.SetPositionAndRotation(finalPos, lookRot);
    }

    void UpdateStamina(bool sprinting)
    {
        if (sprinting)
        {
            _stamina -= sprintDrainPerSecond * Time.deltaTime;
            _stamina = Mathf.Max(0f, _stamina);
            _lastSprintTime = Time.time;
        }
        else
        {
            if (Time.time - _lastSprintTime >= staminaRecoverDelay)
            {
                _stamina += staminaRecoverPerSecond * Time.deltaTime;
                _stamina = Mathf.Min(staminaMax, _stamina);
            }
        }

        if (staminaFillImage != null)
            staminaFillImage.fillAmount = staminaMax <= 0f ? 0f : _stamina / staminaMax;
        var staminaNotFull = _stamina < staminaMax - 0.01f;
        SetStaminaBarVisible(sprinting || staminaNotFull);
    }

    void UpdateAnimator(bool isMoving, bool sprinting)
    {
        if (_anim == null) return;
        var grounded = _cc.isGrounded;
        _anim.SetFloat("MoveSpeed", isMoving ? (sprinting ? 1f : 0.6f) : 0f, 0.1f, Time.deltaTime);
        _anim.SetBool("Grounded", grounded);
        _anim.SetFloat("VerticalSpeed", _vertical);
        _anim.SetBool("Sprinting", sprinting);
    }

    Vector3 ResolveCameraCollision(Vector3 pivot, Vector3 desiredPos)
    {
        var dir = desiredPos - pivot;
        var dist = dir.magnitude;
        if (dist <= 0.0001f) return desiredPos;
        dir /= dist;

        if (Physics.SphereCast(
                pivot,
                cameraCollisionRadius,
                dir,
                out var hit,
                dist,
                cameraCollisionMask,
                QueryTriggerInteraction.Ignore))
        {
            var safeDist = Mathf.Max(0.1f, hit.distance - cameraCollisionRadius);
            return pivot + dir * safeDist;
        }

        return desiredPos;
    }

    void EnsureStaminaBar()
    {
        if (!autoCreateWorldStaminaBar) return;
        if (staminaFillImage != null)
        {
            var existingCanvas = staminaFillImage.canvas;
            if (existingCanvas != null && existingCanvas.renderMode == RenderMode.WorldSpace)
            {
                _staminaBarRoot = existingCanvas.transform;
                if (staminaBarGroup == null)
                    staminaBarGroup = existingCanvas.GetComponent<CanvasGroup>();
                return;
            }
        }

        var rootGo = new GameObject("StaminaWorldBar");
        _staminaBarRoot = rootGo.transform;
        _staminaBarRoot.SetParent(transform, false);
        _staminaBarRoot.localPosition = new Vector3(-0.85f, 1.55f, 0f);
        _staminaBarRoot.localRotation = Quaternion.identity;

        var canvas = rootGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = _cam;
        canvas.sortingOrder = 15;
        rootGo.AddComponent<GraphicRaycaster>();
        staminaBarGroup = rootGo.AddComponent<CanvasGroup>();

        var rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(24f, 180f);
        rootRt.localScale = Vector3.one * 0.01f;

        var bgGo = new GameObject("BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(rootGo.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.32f);

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
        staminaFillImage = fillGo.GetComponent<Image>();
        staminaFillImage.color = new Color(1f, 1f, 1f, 0.98f);
        staminaFillImage.type = Image.Type.Filled;
        staminaFillImage.fillMethod = Image.FillMethod.Vertical;
        staminaFillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
        staminaFillImage.fillAmount = staminaMax <= 0f ? 0f : _stamina / staminaMax;
    }

    void SetStaminaBarVisible(bool visible)
    {
        if (staminaBarGroup == null) return;
        staminaBarGroup.alpha = visible ? 1f : 0f;
        staminaBarGroup.blocksRaycasts = visible;
        staminaBarGroup.interactable = visible;
    }

    void UpdateStaminaBarFacing()
    {
        if (_staminaBarRoot == null || _cam == null) return;
        var toCam = _staminaBarRoot.position - _cam.transform.position;
        if (toCam.sqrMagnitude > 0.0001f)
            _staminaBarRoot.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
    }
}
