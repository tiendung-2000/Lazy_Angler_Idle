using System;

namespace LazyAngler.Core
{
    /// <summary>
    /// Công thức kinh tế thuần (không phụ thuộc UnityEngine) để dùng chung
    /// cho online, offline và simulator. Mọi con số lấy từ thiết kế đã chốt.
    /// </summary>
    public static class EconomyCalc
    {
        public const double OfflineEfficiency = 0.5;
        public const int MaxVaultLevel = 12;
        public const int MaxRods = 10;

        /// <summary>Chi phí nâng cấp: Cơ sở × 1.15^Cấp.</summary>
        public static double UpgradeCost(double baseCost, int level)
            => baseCost * Math.Pow(1.15, level);

        /// <summary>Chi phí nâng cấp với hệ số tăng riêng: Cơ sở × growth^Cấp (growth < 1 coi như 1).</summary>
        public static double UpgradeCost(double baseCost, int level, double growth)
            => baseCost * Math.Pow(Math.Max(1.0, growth), Math.Max(0, level));

        /// <summary>Tỉ lệ cá hiếm (Rare trở lên) mỗi lần cá cắn: cơ sở + perLevel × cấp May mắn, không vượt max.</summary>
        public static double RareChance(double baseChance, double perLevel, double maxChance, int luckLevel)
            => Math.Clamp(baseChance + Math.Max(0, perLevel) * Math.Max(0, luckLevel), 0, Math.Clamp(maxChance, 0, 1));

        /// <summary>Số cấp cần để chỉ số từ base chạm trần max (làm tròn lên); perLevel ≤ 0 → 0.</summary>
        public static int LevelsToCap(double baseValue, double perLevel, double maxValue)
            // Trừ 1e-4 để sai số float (0,3 − 0,08)/0,01 = 22,000001 không làm tròn lên thành 23.
            => perLevel <= 0 || maxValue <= baseValue ? 0 : (int)Math.Ceiling((maxValue - baseValue) / perLevel - 1e-4);

        /// <summary>Hệ số giá cá theo cấp Giá trị cá: 1 + perLevel × cấp.</summary>
        public static double FishValueMultiplier(int level, double perLevel)
            => 1.0 + Math.Max(0, perLevel) * Math.Max(0, level);

        /// <summary>Ngọc trai nhận khi tái sinh: floor(sqrt(xu kiếm được lượt này / divisor)).</summary>
        public static int PrestigePearls(long runEarned, double divisor)
            => divisor <= 0 ? 0 : (int)Math.Floor(Math.Sqrt(Math.Max(0, runEarned) / divisor));

        /// <summary>Xu cần kiếm trong lượt để đạt đúng số ngọc trai đó (nghịch đảo của PrestigePearls).</summary>
        public static long CoinsForPearls(int pearls, double divisor)
            => (long)Math.Ceiling(Math.Max(0, pearls) * (double)Math.Max(0, pearls) * Math.Max(0, divisor));

        /// <summary>Két: 2 + 2 × (cấp − 1) giờ, tính bằng giây.</summary>
        public static double VaultCapSeconds(int vaultLevel)
        {
            int lvl = Math.Clamp(vaultLevel, 1, MaxVaultLevel);
            return (2 + 2 * (lvl - 1)) * 3600.0;
        }

        /// <summary>Số cần câu song song: cấp thuyền 1 là 1 cần, tối đa 10.</summary>
        public static int ActiveRods(int boatLevel)
            => Math.Clamp(boatLevel, 1, MaxRods);

        /// <summary>Hệ số nhân mồi cho Rare, Epic, Legendary: 1 + 0,03 × cấp.</summary>
        public static double BaitMultiplier(int baitLevel)
            => 1.0 + 0.03 * Math.Max(0, baitLevel);

        /// <summary>Chu kỳ quăng mồi: cơ sở × hệ số^cấp, không thấp hơn minSeconds.</summary>
        public static double CastSeconds(double baseSeconds, double reductionPerLevel, double minSeconds, int rodLevel)
            => Math.Max(minSeconds, baseSeconds * Math.Pow(reductionPerLevel, Math.Max(0, rodLevel)));

        /// <summary>
        /// Thời gian một lượt câu: baseFishingDuration / fishingSpeedMultiplier.
        /// Hệ số ≤ 0 không hợp lệ, coi như 1.
        /// </summary>
        public static double EffectiveFishingDuration(double baseDuration, double speedMultiplier)
            => baseDuration / (speedMultiplier > 0 ? speedMultiplier : 1.0);

        /// <summary>
        /// Thời gian kỳ vọng để bắt một con cá trên một cần: chờ cá cắn (kiểm tra mỗi checkInterval
        /// với xác suất biteChance, kỳ vọng checkInterval / biteChance) + thời gian lượt câu.
        /// biteChance = 0 thì không bao giờ bắt được (trả về vô cực).
        /// </summary>
        public static double SecondsPerCatch(double biteChance, double checkInterval, double fishingDuration)
        {
            double p = Math.Clamp(biteChance, 0, 1);
            if (p <= 0) return double.PositiveInfinity;
            return Math.Max(0, checkInterval) / p + Math.Max(0, fishingDuration);
        }

        /// <summary>
        /// Thời gian kỳ vọng mỗi con cá khi có câu hụt. Một lượt = quăng + đợi + cá cắn + (kéo nếu dính,
        /// hoặc thu dây nếu hụt); trung bình cần 1/(1 - miss) lượt cho một con. miss ≥ 1 → vô cực.
        /// </summary>
        public static double SecondsPerCatch(double castSeconds, double waitSeconds, double reactSeconds,
            double reelSeconds, double missChance, double missRecoverSeconds)
        {
            double m = Math.Clamp(missChance, 0, 1);
            if (m >= 1 || double.IsInfinity(waitSeconds)) return double.PositiveInfinity;
            double attempt = Math.Max(0, castSeconds) + Math.Max(0, waitSeconds) + Math.Max(0, reactSeconds)
                             + (1 - m) * Math.Max(0, reelSeconds) + m * Math.Max(0, missRecoverSeconds);
            return attempt / (1 - m);
        }

        /// <summary>Tốc độ bắt cá/giây của toàn bộ cần câu song song.</summary>
        public static double CatchesPerSecond(int rods, double castSeconds)
            => castSeconds > 0 ? rods / castSeconds : 0;

        /// <summary>Thu nhập kỳ vọng/giây = tốc độ bắt × giá trị kỳ vọng mỗi lần bắt.</summary>
        public static double IncomePerSecond(int rods, double castSeconds, double evPerCatch)
            => CatchesPerSecond(rods, castSeconds) * evPerCatch;

        /// <summary>Thời gian vắng mặt tính từ hai mốc UTC; âm (đổi giờ hệ thống) kẹp về 0.</summary>
        public static double AwaySeconds(DateTime lastSaveUtc, DateTime nowUtc)
            => Math.Max(0, (nowUtc - lastSaveUtc).TotalSeconds);

        /// <summary>Thời gian được tính khi offline, đã kẹp theo Két.</summary>
        public static double OfflineCountedSeconds(double awaySec, int vaultLevel)
            => Math.Clamp(awaySec, 0, VaultCapSeconds(vaultLevel));

        /// <summary>Xu offline kỳ vọng, làm tròn xuống.</summary>
        public static long OfflineCoins(double awaySec, int vaultLevel, double incomePerSec, double efficiency = OfflineEfficiency)
            => (long)Math.Floor(OfflineCountedSeconds(awaySec, vaultLevel) * incomePerSec * Math.Clamp(efficiency, 0, 1));

        /// <summary>Số cá offline kỳ vọng, làm tròn xuống.</summary>
        public static long OfflineFish(double awaySec, int vaultLevel, double catchesPerSec, double efficiency = OfflineEfficiency)
            => (long)Math.Floor(OfflineCountedSeconds(awaySec, vaultLevel) * catchesPerSec * Math.Clamp(efficiency, 0, 1));
    }
}
