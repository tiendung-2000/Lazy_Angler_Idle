using System.Collections.Generic;
using LazyAngler.Data;
using UnityEditor;

namespace LazyAngler.Tools
{
    /// <summary>
    /// Tạo nhiệm vụ ngày (Assets/_Project/ScriptableObjects/Quests/Daily) và nhiệm vụ tuần hoàn (.../Repeating)
    /// rồi gán vào GameConfig. Asset đã có thì giữ nguyên số liệu đã chỉnh trong Inspector.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        private const string QuestFolder = "Assets/_Project/ScriptableObjects/Quests";

        public static void BuildQuests()
        {
            EnsureFolder(QuestFolder);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null) return;

            // Nhiệm vụ ngày: mỗi ngày rút 3 trong số này. Thưởng tính bằng giây thu nhập nên tự lớn theo tiến độ.
            config.dailyQuestPool = new List<QuestData>
            {
                Quest("d_catch_25", "Câu {0} con cá", QuestMetric.CatchFish, 25, 90f),
                Quest("d_catch_60", "Câu {0} con cá", QuestMetric.CatchFish, 60, 200f),
                Quest("d_catch_120", "Câu {0} con cá", QuestMetric.CatchFish, 120, 1f, QuestRewardKind.Pearls),
                Quest("d_rare_2", "Câu {0} con cá hiếm trở lên", QuestMetric.CatchRare, 2, 240f),
                Quest("d_earn", "Kiếm {0} xu", QuestMetric.EarnCoins, 500, 180f, targetIncomeSeconds: 900f),
                Quest("d_bonus_2", "Chạm {0} cá vàng", QuestMetric.TapBonusFish, 2, 150f),
                Quest("d_variant_1", "Câu {0} cá biến thể", QuestMetric.CatchVariant, 1, 2f, QuestRewardKind.Pearls),
            };
            config.dailyQuestCount = 3;

            // Nhiệm vụ tuần hoàn: nhận thưởng xong thì lặp lại, chỉ tiêu và thưởng tăng dần.
            config.repeatingQuests = new List<QuestData>
            {
                Quest("r_catch", "Ngư dân cần mẫn: câu {0} con cá", QuestMetric.CatchFish, 50, 300f, targetGrowth: 1.6f, rewardGrowth: 1.35f),
                Quest("r_rare", "Thợ săn hiếm: câu {0} cá hiếm", QuestMetric.CatchRare, 5, 1f, QuestRewardKind.Pearls, targetGrowth: 1.5f, rewardGrowth: 1.25f),
                Quest("r_earn", "Đại gia: kiếm {0} xu", QuestMetric.EarnCoins, 2000, 1800f, QuestRewardKind.IncomeSeconds, targetIncomeSeconds: 3600f, targetGrowth: 1.7f, rewardGrowth: 1.4f),
                Quest("r_bonus", "Mắt tinh: chạm {0} cá vàng", QuestMetric.TapBonusFish, 3, 400f, targetGrowth: 1.4f, rewardGrowth: 1.3f),
                Quest("r_variant", "Nhà sưu tầm: câu {0} cá biến thể", QuestMetric.CatchVariant, 2, 2f, QuestRewardKind.Pearls, targetGrowth: 1.5f, rewardGrowth: 1.3f),
            };

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static QuestData Quest(string id, string title, QuestMetric metric, long target, float reward,
            QuestRewardKind kind = QuestRewardKind.IncomeSeconds, float targetIncomeSeconds = 0f,
            float targetGrowth = 1f, float rewardGrowth = 1f)
        {
            string path = $"{QuestFolder}/Quest_{id}.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<QuestData>(path) == null;
            var q = LoadOrCreate<QuestData>(path);
            if (isNew)
            {
                q.questId = id;
                q.title = title;
                q.metric = metric;
                q.baseTarget = target;
                q.targetIncomeSeconds = targetIncomeSeconds;
                q.targetGrowth = targetGrowth;
                q.rewardKind = kind;
                q.rewardAmount = reward;
                q.rewardGrowth = rewardGrowth;
                EditorUtility.SetDirty(q);
            }
            return q;
        }
    }
}
