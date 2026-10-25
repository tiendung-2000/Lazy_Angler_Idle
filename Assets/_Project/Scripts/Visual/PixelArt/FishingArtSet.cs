using System.Collections.Generic;
using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Bộ sprite pixel art của cảnh câu (nhân vật, đạo cụ). Sprite cần câu nằm trong RodTierData.
    /// Được FishingArtExporter tạo và gán; có thể thay sprite bằng art vẽ tay trong Inspector
    /// (giữ kích thước khung và pivot để vai/tay khớp).
    /// </summary>
    [CreateAssetMenu(menuName = "LazyAngler/Fishing Art Set", fileName = "FishingArtSet")]
    public class FishingArtSet : ScriptableObject
    {
        public int pixelsPerUnit = FishingPixelArt.PixelsPerUnit;

        [Header("Ngư dân (8 khung: 0–3 chờ, 4 cá cắn, 5 giật cần, 6 thu dây, 7 thất bại)")]
        public Sprite[] fishermanFrames = new Sprite[FishingPixelArt.FrameCount];
        [Tooltip("Vị trí vai theo pixel trong từng khung (gốc dưới trái của khung).")]
        public Vector2Int[] shoulderPixels = new Vector2Int[FishingPixelArt.FrameCount];
        public Sprite arm;

        [Header("Dây câu, phao")]
        public Sprite bobber;
        public Sprite alert;
        public Sprite sparkle;

        [Header("Cảnh")]
        public Sprite bucket;
        public Sprite dockPlank;
        public Sprite dockPost;
        public Sprite sun;
        public Sprite cloud;
        public Sprite island;

        [Header("Cá (xám, tô màu theo độ hiếm)")]
        public Sprite fishSmall;
        public Sprite fishLarge;

        [Header("UI minigame Radial")]
        public Sprite ringBase;
        public Sprite ringTrack;
        public Sprite needle;
        public Sprite hub;
        public Sprite pipFull;
        public Sprite pipEmpty;
        public Sprite check;
        public Sprite cross;

        /// <summary>Gán sprite theo tên ô trong sheet (dùng chung cho Editor và runtime).</summary>
        public void Assign(IReadOnlyDictionary<string, Sprite> sprites)
        {
            Sprite Get(string n) => sprites.TryGetValue(n, out var s) ? s : null;

            pixelsPerUnit = FishingPixelArt.PixelsPerUnit;
            fishermanFrames = new Sprite[FishingPixelArt.FrameCount];
            shoulderPixels = new Vector2Int[FishingPixelArt.FrameCount];
            for (int i = 0; i < FishingPixelArt.FrameCount; i++)
            {
                fishermanFrames[i] = Get($"Fisherman_{i}");
                shoulderPixels[i] = FishingPixelArt.ShoulderPixel(i);
            }
            arm = Get("Arm");
            bobber = Get("Bobber");
            alert = Get("Alert");
            sparkle = Get("Sparkle");
            bucket = Get("Bucket");
            dockPlank = Get("DockPlank");
            dockPost = Get("DockPost");
            sun = Get("Sun");
            cloud = Get("Cloud");
            island = Get("Island");
            fishSmall = Get("FishSmall");
            fishLarge = Get("FishLarge");
            ringBase = Get("RingBase");
            ringTrack = Get("RingTrack");
            needle = Get("Needle");
            hub = Get("Hub");
            pipFull = Get("PipFull");
            pipEmpty = Get("PipEmpty");
            check = Get("Check");
            cross = Get("Cross");
        }

        /// <summary>Đủ sprite tối thiểu để dựng nhân vật.</summary>
        public bool IsComplete
        {
            get
            {
                if (fishermanFrames == null || fishermanFrames.Length < FishingPixelArt.FrameCount) return false;
                foreach (var f in fishermanFrames) if (f == null) return false;
                return arm != null && bobber != null && alert != null && bucket != null && dockPlank != null
                       && dockPost != null && fishSmall != null && fishLarge != null;
            }
        }

        public float Unit(int pixels) => pixels / (float)Mathf.Max(1, pixelsPerUnit);

        /// <summary>Bộ dựng tại runtime từ FishingPixelArt (khi chưa có asset).</summary>
        public static FishingArtSet CreateRuntime(out Dictionary<string, Sprite> sprites)
        {
            sprites = FishingPixelArt.BuildRuntime();
            var set = CreateInstance<FishingArtSet>();
            set.name = "FishingArtSet (runtime)";
            set.hideFlags = HideFlags.DontSave;
            set.Assign(sprites);
            return set;
        }
    }
}
