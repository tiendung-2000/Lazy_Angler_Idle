using System.Collections.Generic;
using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Nguồn duy nhất của pixel art cảnh câu cá, vẽ bằng code (PPU 32, nền trong suốt).
    /// - Editor: FishingArtExporter xuất ra PNG + cắt sprite (Point, không mipmap) vào Art/Sprites.
    /// - Runtime: nếu thiếu asset, BuildRuntime() tạo sprite trong bộ nhớ từ cùng dữ liệu (không null).
    ///
    /// Sheet:
    ///   Fisherman.png  256×40, 8 khung 32×40, pivot ở mép cầu (16, 8). Khung: 0–3 ngồi chờ,
    ///                  4 phát hiện cá cắn, 5 giật cần, 6 thu dây, 7 thất bại.
    ///   Rods.png       48×30, lưới 16×6: hàng = cấp cần (1..5), cột = Butt / Mid / Tip, pivot trái-giữa.
    ///   FishingProps.png 128×64, các ô kích thước khác nhau (xem PaintProps).
    /// </summary>
    public static class FishingPixelArt
    {
        public const int PixelsPerUnit = 32;
        public const int FrameWidth = 32, FrameHeight = 40;
        public const int FrameCount = 8;
        public const int RodCellWidth = 16, RodCellHeight = 6, RodTierCount = 5;

        public const string FishermanSheet = "Fisherman";
        public const string RodSheet = "Rods";
        public const string PropSheet = "FishingProps";

        /// <summary>Pivot của khung nhân vật: mép trên cầu gỗ dưới mông nhân vật.</summary>
        public static readonly Vector2 FishermanPivot = new Vector2(16f / FrameWidth, 8f / FrameHeight);

        // ---------- Bảng màu ----------
        private static readonly Color32 Outline = new Color32(36, 28, 40, 255);
        private static readonly Color32 Skin = new Color32(241, 194, 150, 255);
        private static readonly Color32 SkinShade = new Color32(207, 152, 112, 255);
        private static readonly Color32 Shirt = new Color32(238, 120, 46, 255);
        private static readonly Color32 ShirtShade = new Color32(190, 82, 28, 255);
        private static readonly Color32 Pants = new Color32(48, 62, 112, 255);
        private static readonly Color32 PantsShade = new Color32(32, 40, 78, 255);
        private static readonly Color32 Shoe = new Color32(74, 46, 30, 255);
        private static readonly Color32 Hair = new Color32(92, 58, 36, 255);
        private static readonly Color32 Eye = new Color32(30, 24, 30, 255);
        private static readonly Color32 Hat = new Color32(242, 204, 96, 255);
        private static readonly Color32 HatShade = new Color32(200, 156, 58, 255);
        private static readonly Color32 HatBand = new Color32(140, 88, 40, 255);

        // ---------- Nhân vật ----------

        private struct Pose
        {
            public int dx, dy, hx, hy, eye; // eye: 0 mở, 1 nhắm, 2 mở to
            public Pose(int dx, int dy, int hx, int hy, int eye) { this.dx = dx; this.dy = dy; this.hx = hx; this.hy = hy; this.eye = eye; }
        }

        private static readonly Pose[] Poses =
        {
            new Pose(0, 0, 0, 0, 0),   // 0 ngồi chờ
            new Pose(0, -1, 0, -1, 0), // 1 thở ra
            new Pose(0, 0, 0, 0, 1),   // 2 chớp mắt
            new Pose(0, 0, 1, 0, 0),   // 3 gật nhẹ
            new Pose(1, 0, 1, 1, 2),   // 4 phát hiện cá cắn
            new Pose(-2, 0, -3, 0, 0), // 5 giật cần (ngả ra sau)
            new Pose(-1, -1, -2, -1, 0), // 6 thu dây
            new Pose(1, -1, 1, -2, 1), // 7 thất bại (gục đầu)
        };

        /// <summary>Vị trí vai (gốc tay cầm cần) theo pixel trong từng khung.</summary>
        public static Vector2Int ShoulderPixel(int frame)
        {
            var p = Poses[Mathf.Clamp(frame, 0, Poses.Length - 1)];
            return new Vector2Int(14 + p.dx, 20 + p.dy);
        }

        public static PixelCanvas PaintFisherman()
        {
            var c = new PixelCanvas(FrameWidth * FrameCount, FrameHeight);
            for (int f = 0; f < FrameCount; f++)
            {
                c.BeginCell($"Fisherman_{f}", f * FrameWidth, 0, FrameWidth, FrameHeight, FishermanPivot);
                DrawFisherman(c, Poses[f]);
            }
            return c;
        }

        private static void DrawFisherman(PixelCanvas c, Pose p)
        {
            // Chân ngồi trên mép cầu (y = 8), cẳng chân thả xuống phía trước.
            c.Rect(11, 8, 12, 4, Pants);
            c.HLine(11, 22, 8, PantsShade);
            c.Rect(20, 1, 4, 8, Pants);
            c.VLine(20, 1, 8, PantsShade);
            c.Rect(20, 0, 6, 2, Shoe);
            c.Rect(9, 8, 6, 5, Pants);

            // Thân áo cam.
            int tx = 9 + p.dx, top = 23 + p.dy;
            c.Rect(tx, 12, 10, top - 11, Shirt);
            c.Rect(tx, 12, 2, top - 11, ShirtShade);
            c.HLine(tx, tx + 9, 12, ShirtShade);

            // Cổ và đầu.
            int hl = 11 + p.hx, hb = 25 + p.hy;
            for (int y = top + 1; y < hb; y++) c.HLine(13 + p.hx, 15 + p.hx, y, SkinShade);
            c.Rect(hl, hb, 8, 7, Skin);
            c.Set(hl, hb, PixelCanvas.Clear);
            c.Set(hl + 7, hb, PixelCanvas.Clear);
            c.Rect(hl, hb + 2, 2, 4, Hair);
            c.Set(hl + 2, hb + 2, SkinShade);
            c.Set(hl + 2, hb + 3, SkinShade);
            c.Set(hl + 8, hb + 2, Skin); // mũi
            c.Set(hl + 6, hb + 1, SkinShade); // miệng
            switch (p.eye)
            {
                case 1:
                    c.HLine(hl + 5, hl + 6, hb + 3, SkinShade);
                    break;
                case 2:
                    c.VLine(hl + 6, hb + 3, hb + 4, Eye);
                    break;
                default:
                    c.Set(hl + 6, hb + 3, Eye);
                    break;
            }

            // Mũ rơm: vành 2 hàng, chóp có dải băng.
            c.HLine(hl - 3, hl + 10, hb + 7, HatShade);
            c.HLine(hl - 3, hl + 10, hb + 8, Hat);
            c.Rect(hl, hb + 9, 8, 3, Hat);
            c.HLine(hl, hl + 7, hb + 9, HatBand);
            c.HLine(hl + 1, hl + 6, hb + 11, HatShade);

            c.Outline(Outline);
        }

        // ---------- Cần câu ----------

        private struct RodPalette
        {
            public Color32 shaft, light, grip, gripLight, reel, reelDark, guide, tip, node;
            public bool nodes;
        }

        private static readonly RodPalette[] RodPalettes =
        {
            new RodPalette { shaft = C(139, 90, 52), light = C(184, 128, 78), grip = C(92, 58, 34), gripLight = C(128, 84, 50),
                             reel = C(150, 150, 160), reelDark = C(90, 90, 100), guide = C(210, 210, 210), tip = C(240, 240, 240) },
            new RodPalette { shaft = C(98, 150, 62), light = C(156, 202, 96), grip = C(120, 80, 40), gripLight = C(160, 112, 60),
                             reel = C(150, 150, 160), reelDark = C(90, 90, 100), guide = C(220, 220, 200), tip = C(240, 240, 220),
                             node = C(62, 104, 40), nodes = true },
            new RodPalette { shaft = C(62, 120, 210), light = C(140, 190, 245), grip = C(30, 40, 70), gripLight = C(60, 74, 120),
                             reel = C(96, 106, 136), reelDark = C(56, 62, 86), guide = C(255, 255, 255), tip = C(255, 255, 255) },
            new RodPalette { shaft = C(40, 40, 46), light = C(96, 96, 108), grip = C(22, 22, 26), gripLight = C(60, 60, 66),
                             reel = C(206, 44, 44), reelDark = C(130, 24, 24), guide = C(220, 50, 50), tip = C(230, 60, 60) },
            new RodPalette { shaft = C(226, 176, 52), light = C(255, 236, 140), grip = C(150, 90, 30), gripLight = C(196, 128, 48),
                             reel = C(255, 214, 90), reelDark = C(184, 130, 30), guide = C(255, 255, 255), tip = C(255, 250, 200) },
        };

        public static string RodCellName(int tier, int segment)
            => $"Rod{tier + 1}_{(segment == 0 ? "Butt" : segment == 1 ? "Mid" : "Tip")}";

        public static PixelCanvas PaintRods()
        {
            var c = new PixelCanvas(RodCellWidth * 3, RodCellHeight * RodTierCount);
            var pivot = new Vector2(0f, 0.5f);
            for (int t = 0; t < RodTierCount; t++)
            {
                var p = RodPalettes[t];
                int y = t * RodCellHeight;

                c.BeginCell(RodCellName(t, 0), 0, y, RodCellWidth, RodCellHeight, pivot);
                c.Rect(0, 2, 7, 3, p.grip);
                c.HLine(0, 6, 4, p.gripLight);
                c.Ellipse(5.5f, 1.6f, 2.2f, 1.7f, p.reel);
                c.Set(5, 1, p.reelDark);
                c.Rect(7, 2, 9, 2, p.shaft);
                c.HLine(7, 15, 3, p.light);

                c.BeginCell(RodCellName(t, 1), RodCellWidth, y, RodCellWidth, RodCellHeight, pivot);
                c.Rect(0, 2, 16, 2, p.shaft);
                c.HLine(0, 15, 3, p.light);
                c.HLine(8, 9, 1, p.guide);
                if (p.nodes) { c.VLine(4, 2, 3, p.node); c.VLine(12, 2, 3, p.node); }

                c.BeginCell(RodCellName(t, 2), RodCellWidth * 2, y, RodCellWidth, RodCellHeight, pivot);
                c.HLine(0, 7, 2, p.shaft);
                c.HLine(0, 15, 3, p.shaft);
                c.HLine(0, 9, 3, p.light);
                c.Set(14, 2, p.guide);
                c.Set(15, 3, p.tip);
                if (p.nodes) c.Set(6, 3, p.node);
            }
            return c;
        }

        // ---------- Đạo cụ ----------

        public static PixelCanvas PaintProps()
        {
            var c = new PixelCanvas(128, 64);
            var center = new Vector2(0.5f, 0.5f);
            var dark = C(40, 40, 55);

            // Tay trước (tay áo + bàn tay), pivot ở vai.
            c.BeginCell("Arm", 0, 0, 12, 5, new Vector2(1.5f / 12f, 0.5f));
            c.Rect(1, 1, 7, 3, Shirt);
            c.HLine(1, 7, 1, ShirtShade);
            c.Rect(8, 1, 3, 3, Skin);
            c.Set(10, 3, PixelCanvas.Clear);
            c.Set(8, 1, SkinShade);
            c.Outline(Outline);

            // Phao: trên đỏ, dưới trắng, cần ăng-ten. Pivot ở mép nước (giữa thân).
            c.BeginCell("Bobber", 14, 0, 9, 11, new Vector2(0.5f, 4.5f / 11f));
            c.Ellipse(4.5f, 4.5f, 3.4f, 3.4f, C(224, 52, 48));
            c.Recolor((x, y, col) => y < 4, C(250, 250, 250));
            c.Recolor((x, y, col) => y == 4, C(60, 40, 40));
            c.VLine(4, 8, 10, dark);
            c.Outline(Outline);

            // Bong bóng "!" khi cá cắn.
            c.BeginCell("Alert", 24, 0, 11, 13, new Vector2(0.5f, 0f));
            c.Rect(1, 3, 9, 9, C(255, 214, 64));
            c.Set(1, 3, PixelCanvas.Clear); c.Set(9, 3, PixelCanvas.Clear);
            c.Set(1, 11, PixelCanvas.Clear); c.Set(9, 11, PixelCanvas.Clear);
            c.HLine(4, 6, 2, C(255, 214, 64));
            c.Set(5, 1, C(255, 214, 64));
            c.VLine(5, 7, 10, dark);
            c.Set(5, 5, dark);
            c.Outline(Outline);

            c.BeginCell("Sparkle", 36, 0, 5, 5, center);
            c.VLine(2, 0, 4, C(255, 240, 160));
            c.HLine(0, 4, 2, C(255, 240, 160));
            c.Set(2, 2, C(255, 255, 255));

            c.BeginCell("Sun", 42, 0, 14, 14, center);
            c.Ellipse(7f, 7f, 6.6f, 6.6f, C(255, 226, 100));
            c.Ellipse(6f, 8f, 3.6f, 3.6f, C(255, 244, 170));

            c.BeginCell("DockPlank", 57, 0, 16, 6, center);
            c.Rect(0, 0, 16, 6, C(146, 94, 52));
            c.HLine(0, 15, 5, C(186, 128, 76));
            c.HLine(0, 15, 0, C(96, 60, 34));
            c.VLine(7, 1, 4, C(110, 70, 40));
            c.VLine(15, 1, 4, C(110, 70, 40));

            c.BeginCell("DockPost", 74, 0, 4, 24, new Vector2(0.5f, 1f));
            c.Rect(0, 0, 4, 24, C(112, 72, 42));
            c.VLine(0, 0, 23, C(76, 48, 28));
            c.VLine(3, 0, 23, C(146, 98, 58));

            // Thùng tôn đựng cá, miệng rộng hơn đáy. Pivot ở đáy.
            c.BeginCell("Bucket", 79, 0, 16, 14, new Vector2(0.5f, 0f));
            for (int y = 0; y < 12; y++)
            {
                int half = 5 + (y * 2) / 8;
                c.HLine(8 - half, 7 + half, y, C(150, 162, 172));
            }
            c.Recolor((x, y, col) => y == 3 || y == 8, C(112, 122, 134));
            c.Recolor((x, y, col) => x < 5, C(124, 134, 146));
            c.HLine(0, 15, 12, C(196, 206, 214));
            c.Outline(Outline);

            // Cá xám (tô màu theo độ hiếm bằng SpriteRenderer.color), quay mặt sang phải.
            c.BeginCell("FishSmall", 96, 0, 16, 8, center);
            DrawFish(c, 9.5f, 4f, 5.4f, 3.1f, 1, 4, 12, 4, dark);
            c.BeginCell("FishLarge", 0, 26, 22, 11, center);
            DrawFish(c, 13f, 5.5f, 7.4f, 4.2f, 1, 6, 17, 6, dark);

            c.BeginCell("Cloud", 24, 26, 26, 10, center);
            c.Ellipse(8f, 4f, 6f, 3.6f, C(255, 255, 255));
            c.Ellipse(14f, 5.5f, 6.4f, 4.3f, C(255, 255, 255));
            c.Ellipse(20f, 4f, 5f, 3.4f, C(255, 255, 255));
            c.Recolor((x, y, col) => y < 2, C(214, 232, 248));

            // Đảo xa có ngọn hải đăng và cây dừa (nền).
            c.BeginCell("Island", 52, 26, 48, 18, new Vector2(0.5f, 0f));
            c.Ellipse(24f, 3f, 22f, 4f, C(222, 204, 150));
            c.Ellipse(22f, 4.5f, 16f, 8f, C(78, 156, 92));
            c.Recolor((x, y, col) => y > 8 && col.g == 156, C(108, 182, 112));
            c.Rect(10, 8, 4, 9, C(244, 244, 236));
            c.HLine(10, 13, 11, C(206, 64, 60));
            c.HLine(10, 13, 14, C(206, 64, 60));
            c.Rect(10, 17, 4, 1, C(255, 220, 110));
            for (int i = 0; i < 9; i++) c.Set(33 + i / 3, 5 + i, C(120, 82, 50));
            c.HLine(31, 41, 14, C(52, 128, 70));
            c.HLine(32, 39, 15, C(52, 128, 70));
            c.Set(30, 13, C(52, 128, 70)); c.Set(42, 13, C(52, 128, 70));
            return c;
        }

        // ---------- UI minigame Radial ----------

        public const string MinigameSheet = "MinigameUI";

        /// <summary>
        /// Sheet UI minigame (256×96): RingBase (vòng nhiều lớp, nền tối ở giữa), RingTrack (dải trắng của đường chạy,
        /// dùng cho Image Filled Radial: track + vùng mục tiêu), Needle (pivot ở tâm), Hub, PipFull/PipEmpty, Check, Cross.
        /// Bán kính dải đường chạy: 38–46 px trên ô 96 px.
        /// </summary>
        public static PixelCanvas PaintMinigameUI()
        {
            var c = new PixelCanvas(256, 96);
            var center = new Vector2(0.5f, 0.5f);
            var outline = C(20, 12, 18);

            c.BeginCell("RingBase", 0, 0, 96, 96, center);
            for (int y = 0; y < 96; y++)
                for (int x = 0; x < 96; x++)
                {
                    float dx = x + 0.5f - 48f, dy = y + 0.5f - 48f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    bool tick = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 30f) * 30f)) < 2.2f;
                    Color32 col;
                    if (d > 47.5f) continue;
                    if (d > 46f) col = outline;
                    else if (d > 38f) col = tick ? C(64, 20, 28) : d > 44f ? C(138, 52, 58) : C(110, 36, 44);
                    else if (d > 36f) col = C(40, 14, 22);
                    else if (d > 34f) col = C(90, 30, 38);
                    else if (d > 32f) col = C(30, 10, 18);
                    else if (d > 30f) col = C(26, 34, 50);
                    else col = C(16, 22, 34);
                    c.Set(x, y, col);
                }

            c.BeginCell("RingTrack", 96, 0, 96, 96, center);
            for (int y = 0; y < 96; y++)
                for (int x = 0; x < 96; x++)
                {
                    float dx = x + 0.5f - 48f, dy = y + 0.5f - 48f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 38f && d <= 46f) c.Set(x, y, C(255, 255, 255));
                }

            // Kim: pivot ở chân (tâm vòng), đầu kim hình thoi.
            c.BeginCell("Needle", 192, 0, 7, 44, new Vector2(0.5f, 2f / 44f));
            c.Rect(2, 3, 3, 33, C(245, 245, 245));
            c.HLine(1, 5, 36, C(245, 245, 245));
            c.HLine(1, 5, 37, C(245, 245, 245));
            c.HLine(2, 4, 38, C(245, 245, 245));
            c.HLine(2, 4, 39, C(245, 245, 245));
            c.Set(3, 40, C(245, 245, 245));
            c.Set(3, 41, C(245, 245, 245));
            c.Ellipse(3.5f, 2f, 2.4f, 2f, C(245, 245, 245));
            c.Outline(outline);

            c.BeginCell("Hub", 200, 0, 11, 11, center);
            for (int y = 0; y < 11; y++)
                for (int x = 0; x < 11; x++)
                {
                    int m = Mathf.Abs(x - 5) + Mathf.Abs(y - 5);
                    if (m <= 4) c.Set(x, y, m <= 2 ? C(255, 250, 220) : C(255, 206, 80));
                }
            c.Outline(outline);

            c.BeginCell("PipFull", 212, 0, 9, 9, center);
            c.Ellipse(4.5f, 4.5f, 3.6f, 3.6f, C(255, 255, 255));
            c.Outline(outline);

            c.BeginCell("PipEmpty", 222, 0, 9, 9, center);
            c.Ellipse(4.5f, 4.5f, 3.6f, 3.6f, C(255, 255, 255));
            c.Recolor((x, y, col) => (x - 4) * (x - 4) + (y - 4) * (y - 4) <= 2, PixelCanvas.Clear);
            c.Outline(outline);

            c.BeginCell("Check", 232, 0, 16, 16, center);
            c.Line(2, 8, 5, 4, 3, C(96, 226, 116));
            c.Line(5, 4, 12, 12, 3, C(96, 226, 116));
            c.Outline(outline);

            c.BeginCell("Cross", 232, 16, 16, 16, center);
            c.Line(2, 2, 12, 12, 3, C(238, 72, 62));
            c.Line(2, 12, 12, 2, 3, C(238, 72, 62));
            c.Outline(outline);
            return c;
        }

        private static void DrawFish(PixelCanvas c, float cx, float cy, float rx, float ry, int tailX0, int tailX1,
            int eyeX, int eyeY, Color32 outline)
        {
            c.Ellipse(cx, cy, rx, ry, C(206, 206, 210));
            c.Recolor((x, y, col) => y < cy - ry * 0.35f, C(238, 238, 240));
            c.Recolor((x, y, col) => y > cy + ry * 0.4f, C(168, 170, 178));
            for (int x = tailX0; x <= tailX1; x++)
            {
                int h = 1 + (tailX1 - x) * 3 / Mathf.Max(1, tailX1 - tailX0);
                c.VLine(x, Mathf.RoundToInt(cy - 0.5f) - h, Mathf.RoundToInt(cy - 0.5f) + h, C(180, 182, 190));
            }
            int fin = Mathf.RoundToInt(cx);
            c.HLine(fin - 2, fin + 1, Mathf.RoundToInt(cy + ry), C(168, 170, 178));
            c.VLine(eyeX - 2, Mathf.RoundToInt(cy - ry * 0.5f), Mathf.RoundToInt(cy + ry * 0.3f), C(168, 170, 178));
            c.Set(eyeX, eyeY, C(20, 20, 26));
            c.Outline(outline);
        }

        private static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        // ---------- Runtime fallback ----------

        /// <summary>Tạo sprite trong bộ nhớ cho mọi ô của sheet (Point filter, FullRect để dùng Tiled).</summary>
        public static Dictionary<string, Sprite> ToSprites(PixelCanvas canvas, string textureName)
        {
            var tex = canvas.ToTexture(textureName);
            tex.hideFlags = HideFlags.DontSave;
            var result = new Dictionary<string, Sprite>();
            foreach (var cell in canvas.Cells)
            {
                var r = cell.rect;
                var s = Sprite.Create(tex, new Rect(r.x, r.y, r.width, r.height), cell.pivot, PixelsPerUnit,
                    0, SpriteMeshType.FullRect);
                s.name = cell.name;
                s.hideFlags = HideFlags.DontSave;
                result[cell.name] = s;
            }
            return result;
        }

        /// <summary>Toàn bộ sprite cảnh câu dựng tại runtime (dùng khi chưa xuất asset).</summary>
        public static Dictionary<string, Sprite> BuildRuntime()
        {
            var all = new Dictionary<string, Sprite>();
            foreach (var kv in ToSprites(PaintFisherman(), FishermanSheet)) all[kv.Key] = kv.Value;
            foreach (var kv in ToSprites(PaintRods(), RodSheet)) all[kv.Key] = kv.Value;
            foreach (var kv in ToSprites(PaintProps(), PropSheet)) all[kv.Key] = kv.Value;
            foreach (var kv in ToSprites(PaintMinigameUI(), MinigameSheet)) all[kv.Key] = kv.Value;
            return all;
        }
    }
}
