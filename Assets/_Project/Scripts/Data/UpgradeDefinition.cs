using UnityEngine;

namespace LazyAngler.Data
{
    /// <summary>
    /// Loại nâng cấp. Asset lưu theo chỉ số nên chỉ thêm vào cuối; Basket/AutoSeller không còn dùng (đã bỏ giỏ cá).
    /// </summary>
    public enum UpgradeType { Rod, Bait, Boat, Basket, Vault, AutoSeller, BiteChance, FishValue, Luck }

    /// <summary>Giá trị cơ sở của một nâng cấp. Chi phí = baseCost × costGrowth^(số lần đã nâng).</summary>
    [CreateAssetMenu(menuName = "LazyAngler/Upgrade Definition", fileName = "Upgrade_")]
    public class UpgradeDefinition : ScriptableObject
    {
        public UpgradeType type;
        public string displayName;
        public double baseCost = 10;
        [Tooltip("Hệ số nhân chi phí mỗi cấp (1,15 = +15%/cấp). Nâng cấp mạnh như Thuyền nên dùng hệ số lớn.")]
        [Min(1f)] public double costGrowth = 1.15;
        [Tooltip("0 = không giới hạn.")]
        public int maxLevel = 0;
        [TextArea] public string description;

        public bool IsMaxed(int level) => maxLevel > 0 && level >= maxLevel;
    }
}
