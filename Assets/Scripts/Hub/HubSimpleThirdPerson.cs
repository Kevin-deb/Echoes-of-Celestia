using UnityEngine;

/// <summary>
/// Lightweight third-person character controller:
/// - Mouse controls camera orbit (Yaw / Pitch)
/// - Character moves relative to camera direction with smooth rotation
/// - Space to jump; camera follows smoothly and avoids geometry occlusion
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class HubSimpleThirdPerson : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed         = 5.8f;
    [SerializeField] float sprintSpeed       = 15.0f;
    [SerializeField] float rotationSpeed     = 14f;
    [SerializeField] float airControlPercent = 0.45f;
    [SerializeField] float jumpHeight        = 1.8f;
    [SerializeField] float gravity           = -24f;
    [SerializeField] float coyoteTime        = 0.12f;
    [SerializeField] float jumpBufferTime    = 0.16f;

    [Header("Stamina")]
    [SerializeField] float staminaMax              = 100f;
    [SerializeField] float sprintDrainPerSecond    = 30f;
    [SerializeField] float staminaRecoverPerSecond = 24f;
    [SerializeField] float staminaRecoverDelay     = 0.7f;

    [Header("Camera Orbit")]
    [SerializeField] float     lookSensitivityX      = 380f;
    [SerializeField] float     lookSensitivityY      = 270f;
    [SerializeField] float     minPitch              = -20f;
    [SerializeField] float     maxPitch              = 60f;
    [SerializeField] float     cameraDistance        = 4.6f;
    [SerializeField] float     cameraHeight          = 1.65f;
    [SerializeField] float     cameraSmoothTime      = 0.015f;
    [SerializeField] float     cameraCollisionRadius = 0.25f;
    [SerializeField] LayerMask cameraCollisionMask   = ~0;
    [SerializeField] bool      lockCursorOnPlay      = true;

    // ── Runtime state ─────────────────────────────────────────────────────────
    CharacterController _cc;
    Camera    _cam;
    Animator  _anim;
    Vector3   _horizontalVelocity;
    float     _vertical;
    float     _yaw;
    float     _pitch           = 12f;
    float     _stamina;
    float     _lastSprintTime  = -999f;
    float     _jumpPressedTime = -999f;
    float     _lastGroundedTime;
    Vector3   _cameraSmoothVelocity;


    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _cc    = GetComponent<CharacterController>();
        _anim  = GetComponentInChildren<Animator>();
        _stamina = staminaMax;
    }

    void Start()
    {
        _cam = Camera.main;
        if (_cam == null)
        {
            Debug.LogError("HubSimpleThirdPerson: A camera tagged MainCamera is required in the scene.");
            enabled = false;
            return;
        }

        _yaw = transform.eulerAngles.y;
        SnapCameraToCharacterFacing();

        if (lockCursorOnPlay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
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

        // Move relative to camera facing — standard third-person feel.
        var moveWorld = (camForward * input.z + camRight * input.x);
        if (moveWorld.sqrMagnitude > 1f) moveWorld.Normalize();

        var grounded = _cc.isGrounded;
        if (grounded) _lastGroundedTime = Time.time;

        if (Input.GetButtonDown("Jump"))
            _jumpPressedTime = Time.time;

        var wantsSprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        var isMoving    = moveWorld.sqrMagnitude > 0.01f;
        var canSprint   = wantsSprint && isMoving && grounded && _stamina > 0.01f;
        var targetSpeed = canSprint ? sprintSpeed : moveSpeed;

        if (isMoving)
            transform.forward = Vector3.Slerp(
                transform.forward, moveWorld.normalized, rotationSpeed * Time.deltaTime);

        var control         = grounded ? 1f : airControlPercent;
        var targetHorizontal = moveWorld * targetSpeed * control;
        _horizontalVelocity = Vector3.Lerp(
            _horizontalVelocity, targetHorizontal, (grounded ? 18f : 6f) * Time.deltaTime);

        var canUseCoyoteJump = Time.time - _lastGroundedTime <= coyoteTime;
        var hasBufferedJump  = Time.time - _jumpPressedTime  <= jumpBufferTime;

        if (grounded && _vertical < 0f) _vertical = -2f;

        if (hasBufferedJump && canUseCoyoteJump)
        {
            _vertical        = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpPressedTime = -999f;
            grounded         = false;
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
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    void UpdateCameraInput()
    {
        // Pause mouse-look whenever the cursor is unlocked (a menu is open or the
        // player is holding Alt to interact with the HUD). This keeps the camera
        // still while clicking on-screen UI.
        if (Cursor.lockState != CursorLockMode.Locked) return;

        var mouseX = Input.GetAxis("Mouse X");
        var mouseY = Input.GetAxis("Mouse Y");
        _yaw   += mouseX * lookSensitivityX * Time.deltaTime;
        _pitch -= mouseY * lookSensitivityY * Time.deltaTime;
        _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);
    }

    void UpdateCameraFollow()
    {
        var pivot      = transform.position + Vector3.up * cameraHeight;
        var lookRot    = Quaternion.Euler(_pitch, _yaw, 0f);
        var desiredPos = pivot + lookRot * new Vector3(0f, 0f, -cameraDistance);
        var finalPos   = ResolveCameraCollision(pivot, desiredPos);
        var smoothed   = Vector3.SmoothDamp(
            _cam.transform.position, finalPos, ref _cameraSmoothVelocity, cameraSmoothTime);
        _cam.transform.SetPositionAndRotation(smoothed, lookRot);
    }

    void SnapCameraToCharacterFacing()
    {
        if (_cam == null) return;
        var pivot      = transform.position + Vector3.up * cameraHeight;
        var lookRot    = Quaternion.Euler(_pitch, _yaw, 0f);
        var desiredPos = pivot + lookRot * new Vector3(0f, 0f, -cameraDistance);
        var finalPos   = ResolveCameraCollision(pivot, desiredPos);
        _cameraSmoothVelocity = Vector3.zero;
        _cam.transform.SetPositionAndRotation(finalPos, lookRot);
    }

    Vector3 ResolveCameraCollision(Vector3 pivot, Vector3 desiredPos)
    {
        var dir  = desiredPos - pivot;
        var dist = dir.magnitude;
        if (dist <= 0.0001f) return desiredPos;
        dir /= dist;

        if (Physics.SphereCast(pivot, cameraCollisionRadius, dir, out var hit,
                dist, cameraCollisionMask, QueryTriggerInteraction.Ignore))
        {
            var safeDist = Mathf.Max(0.1f, hit.distance - cameraCollisionRadius);
            return pivot + dir * safeDist;
        }
        return desiredPos;
    }

    // ── Stamina ───────────────────────────────────────────────────────────────

    void UpdateStamina(bool sprinting)
    {
        if (sprinting)
        {
            _stamina -= sprintDrainPerSecond * Time.deltaTime;
            _stamina  = Mathf.Max(0f, _stamina);
            _lastSprintTime = Time.time;
        }
        else
        {
            if (Time.time - _lastSprintTime >= staminaRecoverDelay)
            {
                _stamina += staminaRecoverPerSecond * Time.deltaTime;
                _stamina  = Mathf.Min(staminaMax, _stamina);
            }
        }

        // Stamina bar display removed — mechanic runs silently in the background.
    }

    // ── Animator ──────────────────────────────────────────────────────────────

    void UpdateAnimator(bool isMoving, bool sprinting)
    {
        if (_anim == null) return;
        var grounded = _cc.isGrounded;
        _anim.SetFloat("MoveSpeed",    isMoving ? (sprinting ? 1f : 0.6f) : 0f, 0.1f, Time.deltaTime);
        _anim.SetBool("Grounded",      grounded);
        _anim.SetFloat("VerticalSpeed", _vertical);
        _anim.SetBool("Sprinting",     sprinting);
    }
}
