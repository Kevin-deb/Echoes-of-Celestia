using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Attach to the Player or any driveable vehicle to receive damage from enemies.
/// Also provides a runtime fallback: if the Editor setup script has not run, this
/// component is added to the Player automatically at scene load.
/// On death the Player model turns greyscale and a 10-second English countdown begins,
/// after which the Player respawns at their starting position.
/// </summary>
public sealed class HubCombatTarget : MonoBehaviour
{
    [SerializeField] int   maxHealth    = 100;
    [SerializeField] bool  destroyOnDeath;
    [SerializeField] float respawnDelay = 10f;

    int  _health;
    bool _dead;

    // Spawn point recorded in Start(); used to teleport the player back on respawn.
    Vector3    _spawnPosition;
    Quaternion _spawnRotation;

    // Snapshot of each renderer's original material colours.
    // Restored on respawn so the model returns to its normal appearance.
    System.Collections.Generic.Dictionary<Renderer, Color[]> _originalColors;

    // Countdown UI (shared static instance)
    static GameObject s_countdownRoot;
    static Text       s_countdownText;

    public int  MaxHealth     => maxHealth;
    public int  CurrentHealth => _health;
    public bool IsAlive       => !_dead && _health > 0;

    public event Action<HubCombatTarget> Died;

    // ── Runtime fallback install ──────────────────────────────────────────────

    /// <summary>
    /// Ensures HubCombatTarget is present on the Player at runtime even if the
    /// Editor setup script did not run (e.g. after a scene restore by the guardian).
    /// </summary>
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
        Debug.Log("[HubCombatTarget] Component was missing from Player — added automatically at runtime (maxHealth=100).");
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _health = maxHealth;
    }

    void Start()
    {
        // Record the spawn point and snapshot material colours before any damage is taken.
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
        SnapshotOriginalColors();
    }

    // ── Damage ────────────────────────────────────────────────────────────────

    public void TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0) return;

        // Vehicle armour fully blocks laser damage while the player is driving.
        if (CompareTag("Player") && SpaceVehicleSeat.IsOccupied) return;

        _health = Mathf.Max(0, _health - amount);
        if (_health > 0) return;

        _dead = true;
        OnDefeated();
        Died?.Invoke(this);

        if (destroyOnDeath)
            Destroy(gameObject);
    }

    // ── Death / Respawn ───────────────────────────────────────────────────────

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
        // Disable the CharacterController before teleporting so it does not
        // override the new position during the same physics step.
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
            // Reset the camera to the spawn-point facing direction.
            controller.SendMessage("SnapCameraToCharacterFacing", SendMessageOptions.DontRequireReceiver);
        }
    }

    // ── Colour snapshot / restore ─────────────────────────────────────────────

    void SnapshotOriginalColors()
    {
        _originalColors = new System.Collections.Generic.Dictionary<Renderer, Color[]>();

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            var mats   = r.materials;
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

    // ── Countdown UI ──────────────────────────────────────────────────────────

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
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Semi-transparent full-screen black background
        var bg = new GameObject("Background",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // Centred countdown text
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

    // ── Public API ────────────────────────────────────────────────────────────

    public void ResetHealth()
    {
        _dead   = false;
        _health = maxHealth;
    }
}
