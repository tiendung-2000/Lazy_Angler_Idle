using System.Collections.Generic;
using LazyAngler.Data;
using UnityEditor;

namespace LazyAngler.Tools
{
    /// <summary>
    /// Tạo các nâng cấp của Cửa hàng ngọc trai (Assets/_Project/ScriptableObjects/Pearls) và gán vào GameConfig.
    /// Asset đã có thì giữ nguyên số liệu đã chỉnh trong Inspector. Cấp tối đa (maxLevel) luôn ≥ 1.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        private const string PearlFolder = "Assets/_Project/ScriptableObjects/Pearls";

        public static void BuildPearlUpgrades()
        {
            EnsureFolder(PearlFolder);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null) return;

            var list = new List<PearlUpgradeData>
            {
                Pearl("gia_ca", PearlUpgradeType.FishValue, "Giá cá", "Mỗi con cá bán được nhiều xu hơn.", 1, 1.6f, 20, 0.10f),
                Pearl("ca_hiem", PearlUpgradeType.RareLuck, "Điềm may", "Tăng tỉ lệ gặp cá hiếm, kể cả khi May mắn đã chạm trần.", 2, 1.7f, 10, 0.005f),
                Pearl("ca_can_nhanh", PearlUpgradeType.BiteSpeed, "Mồi thơm", "Cá cắn câu nhanh hơn.", 2, 1.6f, 15, 0.03f),
                Pearl("offline", PearlUpgradeType.OfflineEfficiency, "Ngư dân chăm chỉ", "Tăng hiệu suất thu nhập khi offline.", 3, 1.8f, 10, 0.05f),
                Pearl("xu_khoi_dau", PearlUpgradeType.StartingCoins, "Vốn liếng", "Có sẵn xu khi bắt đầu lượt mới sau tái sinh.", 1, 1.5f, 10, 250f),
            };
            config.pearlUpgrades = list;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static PearlUpgradeData Pearl(string id, PearlUpgradeType type, string name, string desc, int cost, float growth, int max, float perLevel)
        {
            string path = $"{PearlFolder}/Pearl_{id}.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<PearlUpgradeData>(path) == null;
            var p = LoadOrCreate<PearlUpgradeData>(path);
            if (isNew)
            {
                p.upgradeId = id;
                p.type = type;
                p.displayName = name;
                p.description = desc;
                p.baseCost = cost;
                p.costGrowth = growth;
                p.maxLevel = max;
                p.valuePerLevel = perLevel;
                EditorUtility.SetDirty(p);
            }
            return p;
        }
    }
}
