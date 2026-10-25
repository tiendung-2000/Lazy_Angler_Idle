using System;
using System.Collections.Generic;
using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>Một ô sprite trong sheet: vùng pixel và pivot (chuẩn hóa 0..1 theo ô).</summary>
    public struct PixelCell
    {
        public string name;
        public RectInt rect;
        public Vector2 pivot;
    }

    /// <summary>
    /// Bảng vẽ pixel art đơn giản (gốc tọa độ ở góc dưới trái, giống Texture2D).
    /// Mọi lệnh vẽ đều theo tọa độ cục bộ của ô hiện tại (BeginCell), tự cắt trong ô.
    /// </summary>
    public class PixelCanvas
    {
        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public readonly int Width;
        public readonly int Height;
        public readonly Color32[] Pixels;
        public readonly List<PixelCell> Cells = new List<PixelCell>();

        private RectInt _cell;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color32[width * height];
        }

        /// <summary>Bắt đầu vẽ một ô mới tại (x, y) kích thước (w, h).</summary>
        public void BeginCell(string name, int x, int y, int w, int h, Vector2 pivot)
        {
            _cell = new RectInt(x, y, w, h);
            Cells.Add(new PixelCell { name = name, rect = _cell, pivot = pivot });
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= _cell.width || y >= _cell.height) return;
            Pixels[(_cell.y + y) * Width + _cell.x + x] = c;
        }

        public Color32 Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _cell.width || y >= _cell.height) return Clear;
            return Pixels[(_cell.y + y) * Width + _cell.x + x];
        }

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    Set(x + i, y + j, c);
        }

        public void HLine(int x0, int x1, int y, Color32 c)
        {
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++) Set(x, y, c);
        }

        public void VLine(int x, int y0, int y1, Color32 c)
        {
            for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++) Set(x, y, c);
        }

        /// <summary>Đoạn thẳng dày (bút vuông cạnh size) từ (x0, y0) tới (x1, y1).</summary>
        public void Line(int x0, int y0, int x1, int y1, int size, Color32 c)
        {
            int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                int x = (int)Math.Round(x0 + (x1 - x0) * t), y = (int)Math.Round(y0 + (y1 - y0) * t);
                Rect(x, y, size, size, c);
            }
        }

        /// <summary>Elip đặc tâm (cx, cy) bán kính (rx, ry), tính theo tâm pixel.</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int y = 0; y < _cell.height; y++)
                for (int x = 0; x < _cell.width; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        /// <summary>Đổi màu mọi pixel đã vẽ thỏa điều kiện (dùng để tô bóng).</summary>
        public void Recolor(Func<int, int, Color32, bool> where, Color32 c)
        {
            for (int y = 0; y < _cell.height; y++)
                for (int x = 0; x < _cell.width; x++)
                {
                    var p = Get(x, y);
                    if (p.a > 0 && where(x, y, p)) Set(x, y, c);
                }
        }

        /// <summary>Viền 1px (4 hướng) quanh mọi pixel đã vẽ trong ô.</summary>
        public void Outline(Color32 c)
        {
            var add = new List<Vector2Int>();
            for (int y = 0; y < _cell.height; y++)
                for (int x = 0; x < _cell.width; x++)
                {
                    if (Get(x, y).a > 0) continue;
                    if (Get(x - 1, y).a > 0 || Get(x + 1, y).a > 0 || Get(x, y - 1).a > 0 || Get(x, y + 1).a > 0)
                        add.Add(new Vector2Int(x, y));
                }
            foreach (var p in add) Set(p.x, p.y, c);
        }

        public Texture2D ToTexture(string name)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(Pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}
