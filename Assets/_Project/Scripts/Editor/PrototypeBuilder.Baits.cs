using System.Collections.Generic;
using LazyAngler.Data;
using UnityEditor;

namespace LazyAngler.Tools
{
    /// <summary>
    /// Tạo mồi tiêu hao của từng map (Assets/_Project/ScriptableObjects/Baits) và gán vào GameConfig.baits.
    /// Mỗi map có 2 mồi: tăng tỉ lệ cá hiếm và tăng tỉ lệ cá biến thể. Giá tăng theo map.
    /// Asset đã có thì giữ nguyên số liệu đã chỉnh trong Inspector.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        private const string BaitFolder = "Assets/_Project/ScriptableObjects/Baits";

        private static readonly (string rare, string shine)[] BaitNames =
        {
            ("Giun đất béo", "Cám thơm lấp lánh"),
            ("Tôm suối tươi", "Bột sao suối"),
            ("Cá nhỏ làm mồi", "Phù sa lấp lánh"),
            ("Tép biển", "Vỏ sò dạ quang"),
            ("Mực san hô", "Bột ngọc trai"),
            ("Sứa đèn lồng", "Tinh thể vực sâu"),
        };

        // Giá mồi hiếm theo map (mồi biến thể đắt gấp rưỡi). Một mồi chạy 5 phút nên cỡ vài phút thu nhập của map đó.
        private static readonly long[] BaitPrices = { 250, 800, 2500, 8000, 25000, 80000 };

        public static void BuildBaits()
        {
            EnsureFolder(BaitFolder);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null) return;

            var list = new List<BaitData>();
            for (int z = 0; z < BaitNames.Length; z++)
            {
                list.Add(Bait($"rare_{z}", BaitNames[z].rare, $"Dụ cá hiếm xuất hiện nhiều hơn ở map này.", z, BaitPrices[z], 0.10f, 0f));
                list.Add(Bait($"shine_{z}", BaitNames[z].shine, $"Tăng cơ hội câu được cá biến thể ở map này.", z, BaitPrices[z] * 3 / 2, 0f, 0.07f));
            }
            config.baits = list;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static BaitData Bait(string id, string name, string desc, int zone, long price, float rare, float variant)
        {
            string path = $"{BaitFolder}/Bait_{id}.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<BaitData>(path) == null;
            var b = LoadOrCreate<BaitData>(path);
            if (isNew)
            {
                b.baitId = id;
                b.displayName = name;
                b.description = desc;
                b.zoneIndex = zone;
                b.price = price;
                b.durationSeconds = 300f;
                b.rareBonus = rare;
                b.variantBonus = variant;
                EditorUtility.SetDirty(b);
            }
            return b;
        }
    }
}
