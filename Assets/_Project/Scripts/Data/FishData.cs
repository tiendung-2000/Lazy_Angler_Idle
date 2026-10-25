using LazyAngler.Core;
using UnityEngine;

namespace LazyAngler.Data
{
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    [CreateAssetMenu(menuName = "LazyAngler/Fish Data", fileName = "Fish_")]
    public class FishData : ScriptableObject
    {
        public string fishId;
        public string displayName;
        public Rarity rarity;
        public int zoneIndex;
        public int basePrice;
        public Vector2 sizeRange = new Vector2(10f, 50f);
        public Sprite icon;
        [TextArea] public string description;

        [Header("Cá boss")]
        [Tooltip("Cá boss của map: rất hiếm, khổng lồ, cần minigame thanh ngang (type Boss). Không nằm trong tỉ lệ độ hiếm thường, " +
                 "không bắt buộc để mở map kế tiếp; mỗi map nên có đúng một cá boss.")]
        public bool isBoss;

        [Header("Cá sự kiện")]
        [Tooltip("Always: cá thường của map. Khác: chỉ xuất hiện khi điều kiện này đang diễn ra (cá sự kiện, không bắt buộc để mở khóa map kế tiếp).")]
        public FishCondition requiredCondition = FishCondition.Always;

        /// <summary>Cá chỉ xuất hiện theo thời gian/thời tiết.</summary>
        public bool IsEventFish => requiredCondition != FishCondition.Always;

        [Header("Cá hiếm")]
        [Tooltip("type = None: cá thường, câu tự động. Radial/Pendulum: cá hiếm, mở minigame khi online.")]
        public RareMinigameSettings minigame = new RareMinigameSettings();
        [Tooltip("Preset độ khó Radial dùng chung. Có giá trị thì thay cho cấu hình 'minigame' ở trên.")]
        public RadialFishingConfig radialConfig;

        /// <summary>Thông số minigame thực tế: preset Radial nếu có, nếu không là cấu hình riêng.</summary>
        public RareMinigameSettings EffectiveMinigame => radialConfig != null ? radialConfig.ToSettings() : minigame;

        public bool IsRareMinigameFish
        {
            get
            {
                var s = EffectiveMinigame;
                return s != null && s.type != MinigameType.None;
            }
        }
    }
}
