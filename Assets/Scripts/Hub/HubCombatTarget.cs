using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 挂在 Player 或可驾驶载具上，接收敌人攻击伤害。
/// Player 死亡后显示 10 秒英文倒计时，倒计时结束后在出生点重生。
/// </summary>
public sealed class HubCombatTarget : MonoBehaviour
{
    [SerializeField] int  maxHealth     = 100;
    [SerializeField] bool destroyOnDeath;
    [SerializeField] float respawnDelay = 10f;

    int  _health;
    bool _dead;

    // 记录进入游戏时的出生点位置与朝向
    Vector3    _spawnPosition;
    Quaternion _spawnRotation;

    // 用于恢复模型颜色（key = Renderer, value = 各材质实例的原始颜色列表）
    System.Collections.Generic.Dictionary<Renderer, Color[]> _originalColors;

    // 倒计时 UI
    static GameObject  s_countdownRoot;
    static Text        s_countdownText;

    public int  MaxHealth     => maxHealth;
    public int  CurrentHealth => _health;
    public bool IsAlive       => !_dead && _health > 0;

    public event Action<HubCombatTarget> Died;

    // ── 运行时保底安装 ───────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstallOnPlayer()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall(scene);

    static void TryInstall(Scene scene)
    {
        if (!scene.IsValid()) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        if (player.GetComponent<HubCombatTarget>() != null) return;

        player.AddComponent<HubCombatTarget>();
        Debug.Log("[HubCombatTarget] Player 上组件缺失，运行时已自动添加（maxHealth=100）。");
    }

    // ── Unity 生命周期 ────────────────────────────────────────────────────────

    void Awake()
    {
        _health = maxHealth;
    }

    void Start()
    {
        // 记录出生点，并快照当前模型的原始材质颜色，以便重生时恢复。
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
        SnapshotOriginalColors();
    }

    // ── 受击 ──────────────────────────────────────────────────────────────────

    public void TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0) return;

        // 玩家在载具内时，装甲完全阻挡激光，不承受任何伤害。
        if (CompareTag("Player") && SpaceVehicleSeat.IsOccupied) return;

        _health = Mathf.Max(0, _health - amount);
        if (_health > 0) return;

        _dead = true;
        OnDefeated();
        Died?.Invoke(this);

        if (destroyOnDeath)
            Destroy(gameObject);
    }

    // ── 死亡 / 重生 ───────────────────────────────────────────────────────────

    void OnDefeated()
    {
        if (!CompareTag("Player")) return;

        var controller = GetComponent<HubSimpleThirdPerson>();
        if (controller != null)
            controller.enabled = false;

        ApplyGrayscaleToModel();

        StartCoroutine(RespawnCountdown());
    }

    IEnumerator RespawnCountdown()
    {
        EnsureCountdownUI();

        var remaining = respawnDelay;
        while (remaining > 0f)
        {
            var seconds = Mathf.CeilToInt(remaining);
            ShowCountdown(
                $"You have been defeated.\n\nRespawning in {seconds} second{(seconds == 1 ? "" : "s")}...");
            yield return new WaitForSeconds(Mathf.Min(remaining, 1f));
            remaining -= 1f;
        }

        HideCountdown();
        Respawn();
    }

    void Respawn()
    {
        // 先把 CharacterController 临时禁用，再传送，避免 CC 把位移重置。
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;

        _health = maxHealth;
        _dead   = false;

        RestoreOriginalColors();

        var controller = GetComponent<HubSimpleThirdPerson>();
        if (controller != null)
        {
            controller.enabled = true;
            // 重置相机到出生朝向，避免死亡期间残留的相机角度
            controller.SendMessage("SnapCameraToCharacterFacing", SendMessageOptions.DontRequireReceiver);
        }
    }

    // ── 颜色快照 / 恢复 ───────────────────────────────────────────────────────

    void SnapshotOriginalColors()
    {
        _originalColors =
            new System.Collections.Generic.Dictionary<Renderer, Color[]>();

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            var mats = r.materials;
            var colors = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
                colors[i] = mats[i] != null && mats[i].HasProperty("_Color")
                    ? mats[i].color
                    : Color.white;
            _originalColors[r] = colors;
        }
    }

    void ApplyGrayscaleToModel()
    {
        var gray = new Color(0.42f, 0.42f, 0.42f, 1f);
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            var mats = r.materials;
            foreach (var mat in mats)
            {
                if (mat == null) continue;
                if (mat.HasProperty("_Color"))     mat.color = gray;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gray);
            }
        }
    }

    void RestoreOriginalColors()
    {
        if (_originalColors == null) { SnapshotOriginalColors(); return; }

        foreach (var kv in _originalColors)
        {
            var r = kv.Key;
            if (r == null) continue;
            var mats   = r.materials;
            var colors = kv.Value;
            for (int i = 0; i < mats.Length && i < colors.Length; i++)
            {
                if (mats[i] == null) continue;
                if (mats[i].HasProperty("_Color"))     mats[i].color = colors[i];
                if (mats[i].HasProperty("_BaseColor")) mats[i].SetColor("_BaseColor", colors[i]);
            }
        }
    }

    // ── 倒计时 UI ─────────────────────────────────────────────────────────────

    static void EnsureCountdownUI()
    {
        if (s_countdownRoot != null && s_countdownText != null) return;

        var existing = GameObject.Find("RespawnCountdownCanvas");
        if (existing != null)
        {
            var t = existing.GetComponentInChildren<Text>(true);
            if (t != null) { s_countdownText = t; s_countdownRoot = existing; return; }
            UnityEngine.Object.Destroy(existing);
        }

        var canvasGo = new GameObject("RespawnCountdownCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 半透明黑色全屏遮罩
        var bg = new GameObject("Background",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // 倒计时文字（居中）
        var textGo = new GameObject("CountdownText",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(canvasGo.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.2f, 0.35f);
        trt.anchorMax = new Vector2(0.8f, 0.65f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize  = 42;
        text.alignment = TextAnchor.MiddleCenter;
        text.color     = new Color(1f, 0.88f, 0.88f, 1f);

        canvasGo.SetActive(false);
        s_countdownRoot = canvasGo;
        s_countdownText = text;
    }

    static void ShowCountdown(string msg)
    {
        EnsureCountdownUI();
        if (s_countdownRoot != null) s_countdownRoot.SetActive(true);
        if (s_countdownText != null) s_countdownText.text = msg;
    }

    static void HideCountdown()
    {
        if (s_countdownRoot != null) s_countdownRoot.SetActive(false);
    }

    // ── 外部接口 ──────────────────────────────────────────────────────────────

    public void ResetHealth()
    {
        _dead   = false;
        _health = maxHealth;
    }
}
