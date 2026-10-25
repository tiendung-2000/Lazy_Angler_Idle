using System.Collections.Generic;
using UnityEngine;

namespace LazyAngler.Data
{
    /// <summary>
    /// Một cấp cần câu: chỉ số (đọc bởi FishingStatProvider) và hình ảnh (đọc bởi RodVisual).
    /// Cấp cần câu = cấp của nâng cấp "Cần câu" (UpgradeType.Rod): cấp 0 là tier đầu tiên.
    /// </summary>
    [CreateAssetMenu(menuName = "LazyAngler/Rod Tier", fileName = "Rod_")]
    public class RodTierData : ScriptableObject
    {
        public string displayName;

        [Header("Chỉ số")]
        [Tooltip("Hệ số tốc độ câu: thời gian lượt = baseFishingDuration / hệ số.")]
        [Min(0.01f)] public float fishingSpeedMultiplier = 1f;
        [Tooltip("Tỉ lệ cá cắn cơ sở của cần (0..1). Nâng cấp Tỉ lệ cá cắn cộng thêm trên giá trị này.")]
        [Range(0f, 1f)] public float biteChance = 0.1f;
        [Tooltip("Hệ số tốc độ cá cắn câu: thời gian chờ tối thiểu = baseBiteWaitSeconds / (hệ số này × hệ số mồi).")]
        [Min(0.1f)] public float biteSpeedMultiplier = 1f;
        [Tooltip("Tỉ lệ câu hụt cơ sở (0..1) khi giật cần. Nâng cấp Mồi câu / Tỉ lệ cá cắn giảm thêm.")]
        [Range(0f, 1f)] public float missChance = 0.3f;
        [Tooltip("Giá để nâng lên cấp này (cấp đầu tiên bỏ qua).")]
        public long unlockCost;
        [HideInInspector] public int dataVersion;

        [Header("Hình ảnh (pixel art, ghép nối tiếp từ tay cầm tới đầu cần)")]
        public Sprite buttSprite;
        public Sprite midSprite;
        public Sprite tipSprite;
        [Tooltip("Số đoạn giữa (mid) giữa tay cầm và đầu cần.")]
        [Range(0, 4)] public int midSegments = 2;
        [Tooltip("Lấp lánh quanh cần (cấp huyền thoại).")]
        public bool sparkle;
        public Color sparkleColor = new Color(1f, 0.9f, 0.4f);

        /// <summary>Tier ứng với cấp nâng cấp; vượt quá thì lấy tier cao nhất. Danh sách rỗng trả null.</summary>
        public static RodTierData ForLevel(IReadOnlyList<RodTierData> tiers, int level)
        {
            if (tiers == null || tiers.Count == 0) return null;
            for (int i = Mathf.Clamp(level, 0, tiers.Count - 1); i >= 0; i--)
                if (tiers[i] != null) return tiers[i];
            return null;
        }
    }
}
