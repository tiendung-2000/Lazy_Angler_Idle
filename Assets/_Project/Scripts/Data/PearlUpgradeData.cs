using UnityEngine;

namespace LazyAngler.Data
{
    /// <summary>Loại nâng cấp ngọc trai. Asset lưu theo chỉ số nên chỉ thêm vào cuối.</summary>
    public enum PearlUpgradeType
    {
        /// <summary>Giá cá: + valuePerLevel × cấp (0,1 = +10%/cấp).</summary>
        FishValue,
        /// <summary>Tỉ lệ cá hiếm: cộng thêm valuePerLevel × cấp (0,005 = +0,5%/cấp), tính cả sau khi chạm trần.</summary>
        RareLuck,
        /// <summary>Cá cắn nhanh: thời gian đợi cá chia cho (1 + valuePerLevel × cấp).</summary>
        BiteSpeed,
        /// <summary>Hiệu suất thu nhập offline: cộng thêm valuePerLevel × cấp (0,05 = +5%), tối đa 100%.</summary>
        OfflineEfficiency,
        /// <summary>Xu khởi đầu sau mỗi lần tái sinh: valuePerLevel × cấp xu.</summary>
        StartingCoins,
    }

    /// <summary>
    /// Một nâng cấp trong Cửa hàng ngọc trai (trả bằng ngọc trai, giữ vĩnh viễn qua tái sinh).
    /// Mỗi nâng cấp có cấp tối đa; chi phí = ceil(baseCost × costGrowth^cấp hiện tại).
    /// </summary>
    [CreateAssetMenu(menuName = "LazyAngler/Pearl Upgrade", fileName = "Pearl_")]
    public class PearlUpgradeData : ScriptableObject
    {
        public PearlUpgradeType type;
        [Tooltip("Mã lưu trong save. Không đổi sau khi phát hành.")]
        public string upgradeId;
        public string displayName;
        [TextArea] public string description;
        [Min(1)] public int baseCost = 1;
        [Min(1f)] public float costGrowth = 1.5f;
        [Tooltip("Cấp tối đa (bắt buộc ≥ 1). Đạt cấp này thì nút báo Tối đa.")]
        [Min(1)] public int maxLevel = 10;
        [Tooltip("Hiệu quả mỗi cấp, ý nghĩa theo loại (xem PearlUpgradeType).")]
        public float valuePerLevel = 0.1f;

        public int MaxLevel => Mathf.Max(1, maxLevel);
        public bool IsMaxed(int level) => level >= MaxLevel;

        public int CostAt(int currentLevel)
            => (int)System.Math.Min(int.MaxValue, System.Math.Ceiling(baseCost * System.Math.Pow(System.Math.Max(1f, costGrowth), System.Math.Max(0, currentLevel))));
    }
}
