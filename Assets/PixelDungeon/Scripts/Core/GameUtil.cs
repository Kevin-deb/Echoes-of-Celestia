using UnityEngine;

namespace PixelDungeon
{
    /// <summary>
    /// Small helpers shared across the game: procedural pixel-art sprite generation
    /// (so projectiles / particles / HUD don't depend on hand-authored art) and
    /// element colours. All generated textures use point filtering to stay crisp.
    /// </summary>
    public static class GameUtil
    {
        public const float PixelsPerUnit = 16f;

        public static Color ElementColor(Element e) => e switch
        {
            Element.Fire => new Color(1f, 0.55f, 0.12f),
            Element.Ice => new Color(0.45f, 0.85f, 1f),
            Element.Lightning => new Color(1f, 0.95f, 0.35f),
            Element.Poison => new Color(0.55f, 0.9f, 0.3f),
            _ => Color.white,
        };

        private static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color32[w * h];
            t.SetPixels32(clear);
            return t;
        }

        private static Sprite ToSprite(Texture2D t, float ppu = PixelsPerUnit)
        {
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Solid filled rounded dot — used for magic bolts / particles.</summary>
        public static Sprite MakeDot(int size = 6, Color? color = null)
        {
            var c = color ?? Color.white;
            var t = NewTex(size, size);
            float r = size / 2f, cx = (size - 1) / 2f, cy = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d <= r) t.SetPixel(x, y, c);
            }
            return ToSprite(t);
        }

        /// <summary>Thin horizontal bolt/arrow shape (points +X), used for arrows.</summary>
        public static Sprite MakeArrow()
        {
            int w = 10, h = 5;
            var t = NewTex(w, h);
            var shaft = new Color(0.86f, 0.74f, 0.45f);
            var head = new Color(0.95f, 0.95f, 0.98f);
            for (int x = 0; x < 7; x++) t.SetPixel(x, 2, shaft);
            // arrow head
            t.SetPixel(7, 2, head); t.SetPixel(8, 1, head); t.SetPixel(8, 3, head); t.SetPixel(9, 2, head);
            // fletching
            t.SetPixel(0, 1, head); t.SetPixel(0, 3, head);
            return ToSprite(t);
        }

        /// <summary>Solid square sprite (1x1) for bars, walls, flashes — tint via SpriteRenderer/Image colour.</summary>
        public static Sprite MakeSquare()
        {
            var t = NewTex(1, 1);
            t.SetPixel(0, 0, Color.white);
            return ToSprite(t, 1f);
        }

        /// <summary>Small heart icon for the HUD.</summary>
        public static Sprite MakeHeart()
        {
            int s = 9;
            var t = NewTex(s, s);
            var c = new Color(0.9f, 0.18f, 0.27f);
            // rows from top(8) to bottom(0), classic pixel heart
            string[] rows =
            {
                "011011110", // y=8
                "111111111", // 7
                "111111111", // 6
                "111111111", // 5
                "011111110", // 4
                "001111100", // 3
                "000111000", // 2
                "000010000", // 1
                "000000000", // 0
            };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                if (rows[8 - y][x] == '1') t.SetPixel(x, y, c);
            return ToSprite(t);
        }

        /// <summary>Builds a 16x16 floor/wall tile from a source ground-atlas texture by picking the
        /// most-opaque cell (a guaranteed solid fill tile), optionally darkening for walls.</summary>
        public static Sprite ExtractTile(Texture2D atlas, bool wall)
        {
            const int cell = 16;
            int cols = atlas.width / cell, rows = atlas.height / cell;
            int bestX = 0, bestY = 0, bestScore = -1;
            for (int ry = 0; ry < rows; ry++)
            for (int rx = 0; rx < cols; rx++)
            {
                var px = atlas.GetPixels(rx * cell, ry * cell, cell, cell);
                int score = 0;
                foreach (var p in px) if (p.a > 0.99f) score++;
                if (score > bestScore) { bestScore = score; bestX = rx * cell; bestY = ry * cell; }
            }
            var src = atlas.GetPixels(bestX, bestY, cell, cell);
            var t = new Texture2D(cell, cell, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            if (wall)
            {
                for (int i = 0; i < src.Length; i++)
                {
                    var p = src[i];
                    src[i] = new Color(p.r * 0.42f, p.g * 0.42f, p.b * 0.5f, 1f);
                }
            }
            t.SetPixels(src);
            return ToSprite(t);
        }

        /// <summary>Loads a Texture2D from a PNG on disk regardless of import "readable" flag. Editor/standalone safe.</summary>
        public static Texture2D LoadPng(string absolutePath)
        {
            if (!System.IO.File.Exists(absolutePath)) return null;
            var bytes = System.IO.File.ReadAllBytes(absolutePath);
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            return t.LoadImage(bytes) ? t : null;
        }

        /// <summary>
        /// Pipeline-agnostic global 2D light. Under URP's 2D Renderer, lit sprite materials render
        /// black without a Light2D; under the Built-in pipeline (this host project) the type doesn't
        /// exist and sprites are unlit anyway — so resolve it via reflection and no-op when absent.
        /// </summary>
        public static void TryAddGlobalLight2D()
        {
            var type = System.Type.GetType(
                "UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
            if (type == null) return;
            if (Object.FindObjectOfType(type) != null) return;

            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent(type);
            var lightTypeProp = type.GetProperty("lightType");
            var enumType = type.GetNestedType("LightType");
            if (lightTypeProp != null && enumType != null)
                lightTypeProp.SetValue(light, System.Enum.Parse(enumType, "Global"));
            type.GetProperty("intensity")?.SetValue(light, 1f);
            type.GetProperty("color")?.SetValue(light, Color.white);
        }
    }
}
