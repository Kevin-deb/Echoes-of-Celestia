using UnityEngine;

/// <summary>
/// Self-contained combat feedback effects for the Hub scene.
/// Generates procedural laser-shot and explosion audio clips at runtime (no external
/// audio assets required) and spawns a short expanding flash for enemy destruction.
/// </summary>
public static class HubCombatFx
{
    static AudioClip _laserClip;
    static AudioClip _explosionClip;

    // ── Audio ─────────────────────────────────────────────────────────────────

    static AudioClip LaserClip()
    {
        if (_laserClip != null) return _laserClip;

        const int rate = 44100;
        const float dur = 0.20f;
        int n = (int)(rate * dur);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t    = i / (float)rate;
            float prog = t / dur;
            float freq = Mathf.Lerp(1500f, 350f, prog);          // descending "pew"
            float env  = Mathf.Exp(-5f * prog);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
            data[i] = tone * env * 0.4f;
        }

        _laserClip = AudioClip.Create("VehicleLaserShot", n, 1, rate, false);
        _laserClip.SetData(data, 0);
        return _laserClip;
    }

    static AudioClip ExplosionClip()
    {
        if (_explosionClip != null) return _explosionClip;

        const int rate = 44100;
        const float dur = 0.75f;
        int n = (int)(rate * dur);
        var data = new float[n];
        var rng = new System.Random(20260608);
        float low = 0f;
        for (int i = 0; i < n; i++)
        {
            float prog  = i / (float)n;
            float env   = Mathf.Exp(-4.5f * prog);
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += 0.025f * (white - low);                        // crude low-pass rumble
            float s = Mathf.Lerp(white, low, 0.65f);
            data[i] = s * env * 0.7f;
        }

        _explosionClip = AudioClip.Create("EnemyExplosion", n, 1, rate, false);
        _explosionClip.SetData(data, 0);
        return _explosionClip;
    }

    public static void PlayLaser(Vector3 pos)
    {
        var c = LaserClip();
        if (c != null) AudioSource.PlayClipAtPoint(c, pos, 0.7f);
    }

    public static void PlayExplosion(Vector3 pos)
    {
        var c = ExplosionClip();
        if (c != null) AudioSource.PlayClipAtPoint(c, pos, 0.9f);
    }

    // ── Explosion visual ───────────────────────────────────────────────────────

    public static void SpawnExplosion(Vector3 pos, float size)
    {
        var go = new GameObject("HubExplosionFx");
        go.transform.position = pos;
        var fx = go.AddComponent<HubExplosionFx>();
        fx.Begin(Mathf.Max(2f, size));
        PlayExplosion(pos);
    }
}

/// <summary>Expanding, fading billboard flash used when an enemy is destroyed.</summary>
public sealed class HubExplosionFx : MonoBehaviour
{
    static Texture2D s_glowTex;

    MeshRenderer _renderer;
    Material     _mat;
    float        _size;
    float        _life = 0.7f;
    float        _t;

    public void Begin(float size)
    {
        _size = size;
        BuildQuad();
    }

    void BuildQuad()
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Flash";
        var col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);
        quad.transform.SetParent(transform, false);

        _renderer = quad.GetComponent<MeshRenderer>();
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows    = false;

        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        _mat = new Material(shader) { mainTexture = GlowTexture() };
        _renderer.sharedMaterial = _mat;
    }

    void Update()
    {
        _t += Time.deltaTime;
        float p = Mathf.Clamp01(_t / _life);

        float scale = Mathf.Lerp(_size * 0.4f, _size * 2.2f, p);
        transform.localScale = Vector3.one * scale;

        // Bright yellow-white core fading to transparent.
        var c = Color.Lerp(new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.5f, 0.1f, 0f), p);
        if (_mat != null) _mat.color = c;

        var cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);

        if (p >= 1f)
        {
            if (_mat != null) Destroy(_mat);
            Destroy(gameObject);
        }
    }

    static Texture2D GlowTexture()
    {
        if (s_glowTex != null) return s_glowTex;

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
            a = a * a;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        s_glowTex = tex;
        return s_glowTex;
    }
}
