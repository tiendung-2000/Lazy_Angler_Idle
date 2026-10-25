using UnityEngine;

namespace LazyAngler.Data
{
    /// <summary>
    /// Một bản đồ câu cá (Ao nhà, Suối, Sông...). Cá thuộc map qua FishData.zoneIndex = vị trí map trong GameConfig.zones.
    /// Mở khóa: hoàn thành bộ sưu tập của map trước. Câu được: đủ cấp cần câu và cấp nâng cấp tối thiểu.
    /// Độ khó nhân lên chỉ số câu (cá cắn ít hơn, hụt nhiều hơn, kéo lâu hơn) để nâng cấp luôn có ý nghĩa.
    /// </summary>
    [CreateAssetMenu(menuName = "LazyAngler/Zone", fileName = "Zone_")]
    public class ZoneData : ScriptableObject
    {
        public string zoneId;
        public string displayName;
        [TextArea] public string description;

        [Header("Yêu cầu để câu")]
        [Tooltip("Cấp cần câu tối thiểu (chỉ số trong GameConfig.rodTiers, 0 = cần đầu tiên).")]
        [Min(0)] public int requiredRodTier;
        [Min(0)] public int requiredBiteLevel;
        [Min(0)] public int requiredBaitLevel;

        [Header("Độ khó (nhân lên chỉ số câu)")]
        [Tooltip("Nhân tỉ lệ cá cắn (sau nâng cấp). < 1 = khó hơn.")]
        [Range(0.1f, 1.5f)] public float biteChanceMultiplier = 1f;
        [Tooltip("Cộng thêm vào tỉ lệ câu hụt.")]
        [Range(0f, 0.5f)] public float missChanceBonus;
        [Tooltip("Nhân thời gian kéo cá. > 1 = cá khỏe hơn, kéo lâu hơn.")]
        [Range(0.5f, 3f)] public float reelDurationMultiplier = 1f;
        [Tooltip("Nhân thời gian đợi tối thiểu trước khi cá cắn.")]
        [Range(0.5f, 3f)] public float biteWaitMultiplier = 1f;

        [Header("Cảnh")]
        [Tooltip("Prefab bố cục map (có component MapView ở gốc). Được nạp khi chơi hoặc chuyển sang map này.")]
        public GameObject mapPrefab;
        [Tooltip("Màu đại diện map trong danh sách chọn map (UI).")]
        public Color themeColor = new Color(0.22f, 0.58f, 0.78f);

        /// <summary>Độ khó hiển thị 1..6 (suy từ hệ số cắn câu: 1,0 → 1, 0,5 → 6).</summary>
        public int DifficultyLevel => Mathf.Clamp(Mathf.RoundToInt((1f - biteChanceMultiplier) * 10f) + 1, 1, 6);
    }
}
