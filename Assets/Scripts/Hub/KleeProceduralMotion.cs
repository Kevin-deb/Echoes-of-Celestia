using UnityEngine;

/// <summary>
/// 角色「程序化走路反馈」组件：
/// 当 FBX 没有真正的动画 clip 时，用模型根节点的位移/旋转模拟走路与待机的视觉反馈。
/// 一旦后续替换为带正式 walk/idle clip 的 FBX，可直接禁用此组件。
/// </summary>
public sealed class KleeProceduralMotion : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("驱动的角色根（带 CharacterController 的 Player）。留空则自动取父级。")]
    [SerializeField] Transform driverRoot;

    [Header("Walk Bob")]
    [SerializeField] float walkBobAmplitude = 0.06f;
    [SerializeField] float walkBobFrequency = 6.5f;
    [SerializeField] float walkSwayAmplitude = 4.5f;
    [SerializeField] float walkLeanForward = 6f;

    [Header("Sprint Boost")]
    [SerializeField] float sprintFrequencyMultiplier = 1.35f;
    [SerializeField] float sprintLeanForward = 11f;

    [Header("Idle Breathing")]
    [SerializeField] float idleBreathAmplitude = 0.012f;
    [SerializeField] float idleBreathFrequency = 1.4f;

    [Header("Smoothing")]
    [SerializeField] float blendSpeed = 8f;

    Vector3 _basePos;
    Quaternion _baseRot;
    CharacterController _cc;
    Vector3 _lastDriverPos;
    float _walkPhase;
    float _walkBlend;
    float _speedSmoothed;
    bool _initialized;

    void Awake()
    {
        if (driverRoot == null)
            driverRoot = transform.parent != null ? transform.parent : transform;

        _cc = driverRoot.GetComponent<CharacterController>();
        _basePos = transform.localPosition;
        _baseRot = transform.localRotation;
        _lastDriverPos = driverRoot.position;
        _initialized = true;
    }

    void LateUpdate()
    {
        if (!_initialized || driverRoot == null) return;

        var dt = Time.deltaTime;
        if (dt <= 0f) return;

        var horizontalDelta = driverRoot.position - _lastDriverPos;
        horizontalDelta.y = 0f;
        var horizontalSpeed = horizontalDelta.magnitude / dt;
        _lastDriverPos = driverRoot.position;

        var grounded = _cc != null ? _cc.isGrounded : true;

        _speedSmoothed = Mathf.Lerp(_speedSmoothed, horizontalSpeed, blendSpeed * dt);
        var moving = _speedSmoothed > 0.4f && grounded;
        var sprinting = _speedSmoothed > 7.5f;

        _walkBlend = Mathf.Lerp(_walkBlend, moving ? 1f : 0f, blendSpeed * dt);

        var freq = walkBobFrequency * (sprinting ? sprintFrequencyMultiplier : 1f);
        _walkPhase += dt * freq * (moving ? 1f : 0f);

        var bobY = Mathf.Abs(Mathf.Sin(_walkPhase * Mathf.PI)) * walkBobAmplitude * _walkBlend;
        var swayDeg = Mathf.Sin(_walkPhase * Mathf.PI * 0.5f) * walkSwayAmplitude * _walkBlend;
        var leanDeg = Mathf.Lerp(walkLeanForward, sprintLeanForward, sprinting ? 1f : 0f) * _walkBlend;

        var idleBreathY = (1f - _walkBlend) * Mathf.Sin(Time.time * Mathf.PI * idleBreathFrequency) * idleBreathAmplitude;

        var targetPos = _basePos + new Vector3(0f, bobY + idleBreathY, 0f);
        var targetRot = _baseRot * Quaternion.Euler(leanDeg, 0f, swayDeg);

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, blendSpeed * dt);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, blendSpeed * dt);
    }

    public void ResetTransform()
    {
        transform.localPosition = _basePos;
        transform.localRotation = _baseRot;
    }

    public void RebindBaseTransform(Transform newDriverRoot = null)
    {
        if (newDriverRoot != null)
            driverRoot = newDriverRoot;
        else if (driverRoot == null)
            driverRoot = transform.parent != null ? transform.parent : transform;

        _cc = driverRoot.GetComponent<CharacterController>();
        _basePos = transform.localPosition;
        _baseRot = transform.localRotation;
        _lastDriverPos = driverRoot.position;
        _walkPhase = 0f;
        _walkBlend = 0f;
        _speedSmoothed = 0f;
        _initialized = true;
    }
}
