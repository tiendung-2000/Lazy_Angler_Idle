using UnityEngine;

namespace LazyAngler.Data
{
    /// <summary>Việc người chơi làm được đếm vào nhiệm vụ. Asset lưu theo chỉ số nên chỉ thêm vào cuối.</summary>
    public enum QuestMetric
    {
        /// <summary>Mỗi con cá bắt được (không tính rác).</summary>
        CatchFish,
        /// <summary>Cá Hiếm trở lên bắt được.</summary>
        CatchRare,
        /// <summary>Cá biến thể bắt được.</summary>
        CatchVariant,
        /// <summary>Xu kiếm được (mọi nguồn).</summary>
        EarnCoins,
        /// <summary>Số lần chạm trúng cá vàng sự kiện.</summary>
        TapBonusFish,
    }

    public enum QuestRewardKind
    {
        /// <summary>Xu bằng bấy nhiêu giây thu nhập hiện tại (tự lớn theo tiến độ).</summary>
        IncomeSeconds,
        /// <summary>Ngọc trai.</summary>
        Pearls,
    }

    /// <summary>
    /// Một mẫu nhiệm vụ. Dùng cho cả nhiệm vụ ngày (rút ngẫu nhiên từ GameConfig.dailyQuestPool mỗi ngày)
    /// và nhiệm vụ tuần hoàn (GameConfig.repeatingQuests: xong và nhận thưởng thì lặp lại với chỉ tiêu cao hơn).
    /// </summary>
    [CreateAssetMenu(menuName = "LazyAngler/Quest", fileName = "Quest_")]
    public class QuestData : ScriptableObject
    {
        [Tooltip("Mã lưu trong save. Không đổi sau khi phát hành.")]
        public string questId;
        [Tooltip("Tên hiển thị; {0} được thay bằng chỉ tiêu, vd. \"Câu {0} con cá\".")]
        public string title = "Câu {0} con cá";
        public QuestMetric metric;

        [Header("Chỉ tiêu")]
        [Min(1)] public long baseTarget = 10;
        [Tooltip("> 0: chỉ tiêu tối thiểu bằng bấy nhiêu giây thu nhập (dùng cho Xu kiếm được, để theo kịp tiến độ).")]
        public float targetIncomeSeconds;
        [Tooltip("Nhiệm vụ tuần hoàn: mỗi vòng chỉ tiêu nhân hệ số này (1 = không đổi).")]
        [Min(1f)] public float targetGrowth = 1f;

        [Header("Thưởng")]
        public QuestRewardKind rewardKind = QuestRewardKind.IncomeSeconds;
        [Tooltip("Số giây thu nhập hoặc số ngọc trai (tùy loại thưởng).")]
        [Min(0f)] public float rewardAmount = 60f;
        [Tooltip("Nhiệm vụ tuần hoàn: mỗi vòng thưởng nhân hệ số này.")]
        [Min(1f)] public float rewardGrowth = 1f;

        public long TargetFor(int cycle, double incomePerSecond)
        {
            double t = baseTarget;
            if (targetIncomeSeconds > 0f) t = System.Math.Max(t, incomePerSecond * targetIncomeSeconds);
            return (long)System.Math.Ceiling(t * System.Math.Pow(System.Math.Max(1f, targetGrowth), System.Math.Max(0, cycle)));
        }

        public float RewardFor(int cycle)
            => rewardAmount * Mathf.Pow(Mathf.Max(1f, rewardGrowth), Mathf.Max(0, cycle));

        public string TitleFor(long target) => string.Format(title, target);
    }
}
