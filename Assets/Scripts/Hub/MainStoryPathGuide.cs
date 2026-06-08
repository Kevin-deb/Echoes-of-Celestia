using UnityEngine;

/// <summary>
/// Genshin-style ground path guidance. While story mode is active, spawns a trail of
/// gold glowing dots on the terrain in front of the player, pointing toward the object
/// bound to the next unread main-story chapter. Dots are snapped to the ground via
/// downward raycasts so they never float, and pulse with a flowing animation.
/// Created and owned by <see cref="MainStoryQuestUI"/>; only ever exists in the Hub scene.
/// </summary>
public sealed class MainStoryPathGuide : MonoBehaviour
{
    const int   DotCount      = 16;
    const float Spacing       = 1.6f;
    const float StartOffset   = 1.8f;   // distance of first dot ahead of the player
    const float DotSize       = 0.55f;
    const float GroundRayUp   = 40f;
    const float GroundRayLen  = 120f;
    const float GroundLift    = 0.06f;  // tiny lift to avoid z-fighting with terrain
    const float ArriveDistance = 5.5f;  // stop guiding once this close to the target

    static readonly Color Gold = new Color(1f, 0.84f, 0.32f, 1f);

    Transform   _player;
    Transform[] _dots;
    Material    _dotMat;
    Texture2D   _dotTex;

    /// <summary>The object to guide the player toward; null hides the trail.</summary>
    public Transform Target { get; set; }

    void Awake()
    {
        BuildDots();
    }

    void OnDestroy()
    {
        if (_dotMat != null) Destroy(_dotMat);
        if (_dotTex != null) Destroy(_dotTex);
    }

    void LateUpdate()
    {
        // Hide while reading lore.
        if (LoreReadingUI.IsAnyOpen) { HideAll(); return; }

        if (Target == null) { HideAll(); return; }

        // Guide from the vehicle the player is driving, otherwise from the player.
        Transform origin = SpaceVehicleSeat.ActiveOccupiedTransform;
        if (origin == null)
        {
            if (_player == null)
            {
                var pg = GameObject.FindGameObjectWithTag("Player");
                if (pg != null) _player = pg.transform;
                else { HideAll(); return; }
            }
            origin = _player;
        }

        var from = origin.position;
        var flat = Target.position - from;
        flat.y = 0f;
        var distToTarget = flat.magnitude;
        if (distToTarget < ArriveDistance) { HideAll(); return; }

        var dir = flat / distToTarget;

        for (int i = 0; i < _dots.Length; i++)
        {
            float d = StartOffset + i * Spacing;
            if (d >= distToTarget - 0.4f)
            {
                if (_dots[i].gameObject.activeSelf) _dots[i].gameObject.SetActive(false);
                continue;
            }

            var samplePos = from + dir * d;
            samplePos.y = TryGround(samplePos, out var gy) ? gy + GroundLift : from.y + GroundLift;

            _dots[i].position = samplePos;
            _dots[i].rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat on the ground

            // Flowing pulse travelling toward the target.
            float phase = Time.time * 2.4f - i * 0.55f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(phase);
            float scale = DotSize * (0.65f + 0.5f * pulse);
            _dots[i].localScale = new Vector3(scale, scale, scale);

            if (!_dots[i].gameObject.activeSelf) _dots[i].gameObject.SetActive(true);
        }
    }

    void HideAll()
    {
        if (_dots == null) return;
        foreach (var d in _dots)
            if (d != null && d.gameObject.activeSelf) d.gameObject.SetActive(false);
    }

    // ── Construction ──────────────────────────────────────────────────────────

    void BuildDots()
    {
        _dotTex = BuildRadialTexture();

        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        _dotMat = new Material(shader) { mainTexture = _dotTex, color = Color.white };

        _dots = new Transform[DotCount];
        for (int i = 0; i < DotCount; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "GuideDot";

            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            quad.transform.SetParent(transform, false);
            quad.transform.localScale = Vector3.one * DotSize;
            quad.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);

            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial    = _dotMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;

            quad.SetActive(false);
            _dots[i] = quad.transform;
        }
    }

    static Texture2D BuildRadialTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - c) / c;
            float dy = (y + 0.5f - c) / c;
            float r  = Mathf.Sqrt(dx * dx + dy * dy);
            float a  = Mathf.Clamp01(1f - r);
            a = a * a;                 // bright core, soft glowing edge
            tex.SetPixel(x, y, new Color(Gold.r, Gold.g, Gold.b, a));
        }
        tex.Apply();
        return tex;
    }

    bool TryGround(Vector3 pos, out float groundY)
    {
        groundY = 0f;
        var origin = pos + Vector3.up * GroundRayUp;
        var hits   = Physics.RaycastAll(origin, Vector3.down, GroundRayLen, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;          // ignore the dots
            if (h.collider.GetComponentInParent<SpaceVehicleSeat>() != null) continue; // ignore vehicles
            groundY = h.point.y;
            return true;
        }
        return false;
    }
}
