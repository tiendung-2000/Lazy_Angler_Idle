using System;
using System.Collections.Generic;
using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Sprite placeholder tạo bằng code (không cần file ảnh). Sprite màu trắng/xám để tô màu
    /// bằng SpriteRenderer.color. Mỗi loại chỉ tạo một lần và được cache.
    /// </summary>
    public static class ProceduralSprites
    {
        public const string UrpUnlitShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        public const string LegacySpriteShader = "Sprites/Default";

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static Material _material;

        /// <summary>
        /// Material dùng cho mọi SpriteRenderer/LineRenderer tạo bằng code. Nên được gán từ scene
        /// (FishingSceneBootstrap) để shader chắc chắn có trong build; nếu không, tìm shader URP 2D Unlit.
        /// </summary>
        public static Material Material
        {
            get
            {
                if (_material == null) _material = CreateFallbackMaterial();
                return _material;
            }
            set => _material = value;
        }

        private static Material CreateFallbackMaterial()
        {
            var shader = Shader.Find(UrpUnlitShader);
            if (shader == null) shader = Shader.Find(LegacySpriteShader);
            return shader != null ? new Material(shader) { name = "LA_SpriteUnlit (runtime)" } : null;
        }

        // ---------- Sprite cơ bản ----------

        /// <summary>Ô vuông trắng 1×1 đơn vị.</summary>
        public static Sprite Square() => Get("square", () => Build(8, 8, 8, (x, y) => Color.white));

        /// <summary>Hình tròn trắng đường kính 1 đơn vị, viền khử răng cưa.</summary>
        public static Sprite Circle() => Get("circle", () => Build(64, 64, 64, (x, y) =>
            White(CircleAlpha(x, y, 32f, 32f, 31f))));

        /// <summary>Vòng tròn rỗng (hiệu ứng gợn nước).</summary>
        public static Sprite Ring() => Get("ring", () => Build(64, 64, 64, (x, y) =>
        {
            float d = Dist(x + 0.5f, y + 0.5f, 32f, 32f);
            return White(Mathf.Clamp01(1f - Mathf.Abs(d - 28f) / 3f));
        }));

        /// <summary>Vòng tròn dày (đặc từ bán kính 22 đến 31 trên 64px), dùng cho vòng minigame và cung Filled Radial.</summary>
        public static Sprite ThickRing() => Get("thick_ring", () => Build(128, 128, 128, (x, y) =>
        {
            float d = Dist(x + 0.5f, y + 0.5f, 64f, 64f);
            return White(Mathf.Clamp01(Mathf.Min(d - 44f, 62f - d)));
        }));

        /// <summary>Dải chuyển màu dọc 1×1 đơn vị (dùng cho trời và nước).</summary>
        public static Sprite VerticalGradient(Color top, Color bottom)
        {
            string key = $"grad_{ColorUtility.ToHtmlStringRGBA(top)}_{ColorUtility.ToHtmlStringRGBA(bottom)}";
            return Get(key, () => Build(64, 64, 64, (x, y) => Color.Lerp(bottom, top, y / 63f)));
        }

        /// <summary>
        /// Cá quay mặt sang phải, rộng 1 đơn vị, cao 0,5. Thân sáng dần xuống bụng, mắt tối.
        /// Lật bằng scale.x âm.
        /// </summary>
        public static Sprite Fish() => Get("fish", () => Build(96, 48, 96, (x, y) =>
        {
            float px = x + 0.5f, py = y + 0.5f;

            // Mắt
            if (Dist(px, py, 78f, 28f) < 4f) return new Color(0.08f, 0.08f, 0.12f, 1f);

            // Thân elip
            float ex = (px - 56f) / 36f, ey = (py - 24f) / 17f;
            float body = Mathf.Clamp01((1f - (ex * ex + ey * ey)) * 12f);

            // Đuôi hình quạt bên trái
            float tail = 0f;
            if (px >= 3f && px <= 26f)
            {
                float half = Mathf.Lerp(17f, 4f, (px - 3f) / 23f);
                tail = Mathf.Clamp01((half - Mathf.Abs(py - 24f)) * 0.8f);
            }

            // Vây lưng
            float fin = 0f;
            if (px >= 46f && px <= 64f && py >= 36f)
            {
                float top = Mathf.Lerp(46f, 38f, (px - 46f) / 18f);
                fin = Mathf.Clamp01((top - py) * 0.8f);
            }

            float a = Mathf.Max(body, Mathf.Max(tail * 0.9f, fin * 0.9f));
            float shade = Mathf.Lerp(1f, 0.72f, py / 48f); // bụng sáng, lưng tối
            if (body <= 0f) shade *= 0.85f;                // đuôi, vây hơi tối
            // Vạch mang
            if (Mathf.Abs(px - 70f) < 1.2f && Mathf.Abs(py - 24f) < 10f && body > 0.5f) shade *= 0.75f;
            return new Color(shade, shade, shade, a);
        }));

        /// <summary>Phao câu: nửa trên đỏ, nửa dưới trắng, cần nhỏ phía trên.</summary>
        public static Sprite Bobber() => Get("bobber", () => Build(48, 64, 64, (x, y) =>
        {
            float px = x + 0.5f, py = y + 0.5f;
            if (Mathf.Abs(px - 24f) < 2f && py > 40f && py < 62f) return new Color(0.15f, 0.15f, 0.15f, 1f);
            float a = CircleAlpha(x, y, 24f, 24f, 20f);
            if (a <= 0f) return Color.clear;
            Color c = py > 24f ? new Color(0.92f, 0.2f, 0.18f) : Color.white;
            if (Mathf.Abs(py - 24f) < 1.5f) c = new Color(0.2f, 0.2f, 0.2f);
            c.a = a;
            return c;
        }, new Vector2(0.5f, 24f / 64f)));

        /// <summary>Bong bóng vàng có dấu "!" (cá cắn câu).</summary>
        public static Sprite Alert() => Get("alert", () => Build(64, 64, 64, (x, y) =>
        {
            float px = x + 0.5f, py = y + 0.5f;
            float a = CircleAlpha(x, y, 32f, 32f, 30f);
            if (a <= 0f) return Color.clear;
            bool bar = Mathf.Abs(px - 32f) < 5f && py > 26f && py < 54f;
            bool dot = Dist(px, py, 32f, 15f) < 5f;
            Color c = bar || dot ? new Color(0.15f, 0.1f, 0.05f) : new Color(1f, 0.82f, 0.15f);
            c.a = a;
            return c;
        }));

        /// <summary>Hình thang (thùng đựng cá), miệng rộng hơn đáy. Pivot ở đáy.</summary>
        public static Sprite Bucket() => Get("bucket", () => Build(64, 64, 64, (x, y) =>
        {
            float px = x + 0.5f, py = y + 0.5f;
            float half = Mathf.Lerp(22f, 31f, py / 64f);
            float a = Mathf.Clamp01(half - Mathf.Abs(px - 32f));
            if (a <= 0f) return Color.clear;
            bool band = Mathf.Abs(py - 20f) < 2.5f || Mathf.Abs(py - 46f) < 2.5f || py > 59f;
            float s = band ? 0.65f : 1f;
            return new Color(s, s, s, a);
        }, new Vector2(0.5f, 0f)));

        // ---------- Tạo GameObject ----------

        public static SpriteRenderer Create(string name, Transform parent, Sprite sprite, Color color,
            Vector3 localPos, Vector2 scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (Material != null) sr.sharedMaterial = Material;
            return sr;
        }

        // ---------- Nội bộ ----------

        private static Sprite Get(string key, Func<Sprite> factory)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            s = factory();
            s.name = "LA_" + key;
            Cache[key] = s;
            return s;
        }

        private static Sprite Build(int w, int h, float ppu, Func<int, int, Color> pixel,
            Vector2? pivot = null)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = pixel(x, y);
            tex.SetPixels(pixels);
            tex.Apply(false, true);

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), ppu,
                0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static float Dist(float x, float y, float cx, float cy)
        {
            float dx = x - cx, dy = y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float CircleAlpha(int x, int y, float cx, float cy, float r)
            => Mathf.Clamp01(r - Dist(x + 0.5f, y + 0.5f, cx, cy));

        private static Color White(float a) => new Color(1f, 1f, 1f, a);
    }
}
