using LazyAngler.Data;
using UnityEditor;
using UnityEngine;

namespace LazyAngler.Tools
{
    /// <summary>
    /// Bộ số cân bằng hiện hành. Asset đã có giữ số đã chỉnh trong Inspector, nên mỗi lần đổi bộ số mặc định
    /// thì tăng <see cref="BalanceVersion"/>: builder áp bộ số mới đúng một lần cho asset cũ (GameConfig.balanceVersion).
    /// </summary>
    public static partial class PrototypeBuilder
    {
        private const int BalanceVersion = 2;

        // v2: các chỉ số có trần. Cắn tối đa 50%, cá hiếm 8% → tối đa 30%, tốc độ cá cắn tối đa ×2,5, hụt tối thiểu 5%.
        // Mồi câu không còn tăng cá hiếm (đã tách thành May mắn).
        private static readonly float[] RodBiteChance = { 0.10f, 0.14f, 0.18f, 0.23f, 0.28f };

        // Yêu cầu map theo trần mới: cần huyền thoại (28%) chỉ cần 11 cấp Tỉ lệ cắn để chạm 50%, mồi tối đa ~10 cấp.
        private static readonly (int bite, int bait)[] ZoneRequirementsV2 = { (0, 0), (0, 0), (3, 0), (5, 3), (7, 5), (9, 8) };

        [MenuItem("Tools/Lazy Angler/Apply Balance")]
        public static void ApplyBalance()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null || config.balanceVersion >= BalanceVersion) return;

            config.maxBiteChance = 0.5f;
            config.biteChancePerLevel = 0.02f;
            config.baitBiteSpeedPerLevel = 0.04f;
            config.maxBiteSpeedMultiplier = 2.5f;
            config.minMissChance = 0.05f;
            config.baseRareChance = 0.08f;
            config.rareChancePerLevel = 0.01f;
            config.maxRareChance = 0.3f;

            for (int i = 0; i < config.rodTiers.Count && i < RodBiteChance.Length; i++)
            {
                var tier = config.rodTiers[i];
                if (tier == null) continue;
                tier.biteChance = RodBiteChance[i];
                EditorUtility.SetDirty(tier);
            }

            SetUpgrade(config.biteUpgrade, 20, 1.18, 0, "Tăng tỉ lệ cá cắn câu mỗi lần kiểm tra (tối đa 50%).");
            SetUpgrade(config.baitUpgrade, 25, 1.2, 0, "Cá cắn câu nhanh hơn và ít hụt hơn (tốc độ tối đa ×2,5).");
            SetUpgrade(config.luckUpgrade, 50, 1.3, 0, "Tăng tỉ lệ gặp cá hiếm (tối đa 30%).");

            for (int i = 0; i < config.zones.Count && i < ZoneRequirementsV2.Length; i++)
            {
                var zone = config.zones[i];
                if (zone == null) continue;
                zone.requiredBiteLevel = ZoneRequirementsV2[i].bite;
                zone.requiredBaitLevel = ZoneRequirementsV2[i].bait;
                EditorUtility.SetDirty(zone);
            }

            config.balanceVersion = BalanceVersion;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LazyAngler] Đã áp bộ số cân bằng v{BalanceVersion}.");
        }

        /// <summary>Trần do chỉ số quyết định (maxLevel 0): popup tự báo Tối đa khi chỉ số chạm trần.</summary>
        private static void SetUpgrade(UpgradeDefinition def, double baseCost, double growth, int maxLevel, string desc)
        {
            if (def == null) return;
            def.baseCost = baseCost;
            def.costGrowth = growth;
            def.maxLevel = maxLevel;
            def.description = desc;
            EditorUtility.SetDirty(def);
        }
    }
}
