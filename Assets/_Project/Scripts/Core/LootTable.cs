using System;

namespace LazyAngler.Core
{
    /// <summary>
    /// Tính tỉ lệ độ hiếm (đã nhân mồi và chuẩn hóa) và roll loot.
    /// Thuần C#, chỉ làm việc với mảng chỉ số độ hiếm: 0 Common, 1 Uncommon,
    /// 2 Rare, 3 Epic, 4 Legendary.
    /// </summary>
    public static class LootTable
    {
        public const int RarityCount = 5;
        public const int CommonIndex = 0;
        /// <summary>Độ hiếm từ chỉ số này trở lên được mồi buff (Rare, Epic, Legendary).</summary>
        public const int FirstBaitBoostedIndex = 2;

        /// <summary>Trọng số độ hiếm đã chốt (%): Common, Uncommon, Rare, Epic, Legendary.</summary>
        public static readonly double[] DefaultRarityWeights = { 60, 25, 10, 4, 1 };

        /// <summary>Roll độ hiếm từ trọng số (có nhân mồi). roll ∈ [0, 1), ngoài khoảng sẽ bị kẹp.</summary>
        public static int RollRarity(double[] baseWeights, int baitLevel, double roll)
        {
            roll = Math.Clamp(roll, 0.0, 0.999999999);
            return PickRarity(RarityProbabilities(baseWeights, baitLevel), roll);
        }

        /// <summary>
        /// Trả về xác suất mỗi độ hiếm, tổng bằng 1.
        /// Rare/Epic/Legendary nhân (1 + 0,03 × cấp mồi), Uncommon giữ nguyên,
        /// Common = phần còn lại cho đủ 100%. Độ hiếm không có cá (available[i] = false) bị bỏ qua.
        /// </summary>
        public static double[] RarityProbabilities(double[] baseWeights, int baitLevel, bool[] available = null)
        {
            if (baseWeights == null || baseWeights.Length != RarityCount)
                throw new ArgumentException($"baseWeights phải có {RarityCount} phần tử.");

            double total = 0;
            foreach (var w in baseWeights) total += Math.Max(0, w);
            if (total <= 0) throw new ArgumentException("Tổng trọng số phải > 0.");

            double mult = EconomyCalc.BaitMultiplier(baitLevel);
            var p = new double[RarityCount];
            double nonCommon = 0;
            for (int i = 1; i < RarityCount; i++)
            {
                double w = Math.Max(0, baseWeights[i]) / total;
                if (i >= FirstBaitBoostedIndex) w *= mult;
                p[i] = w;
                nonCommon += w;
            }

            // Common lấp phần còn lại. Nếu buff quá lớn làm vượt 100%, co các loại khác lại.
            if (nonCommon >= 1.0)
            {
                for (int i = 1; i < RarityCount; i++) p[i] /= nonCommon;
                p[CommonIndex] = 0;
            }
            else
            {
                p[CommonIndex] = 1.0 - nonCommon;
            }

            if (available != null) MaskAndNormalize(p, available);
            return p;
        }

        /// <summary>
        /// Xác suất mỗi độ hiếm khi tổng nhóm cá hiếm (Rare, Epic, Legendary) bằng đúng rareShare (0..1).
        /// Trong mỗi nhóm (thường: Common/Uncommon, hiếm: Rare/Epic/Legendary) giữ tỉ lệ theo trọng số.
        /// Độ hiếm không có cá bị bỏ qua rồi chuẩn hóa lại (vùng không có cá hiếm → chỉ ra cá thường).
        /// </summary>
        public static double[] RarityProbabilitiesForRareShare(double[] baseWeights, double rareShare, bool[] available = null)
        {
            if (baseWeights == null || baseWeights.Length != RarityCount)
                throw new ArgumentException($"baseWeights phải có {RarityCount} phần tử.");

            double normal = 0, rare = 0;
            for (int i = 0; i < RarityCount; i++)
            {
                double w = Math.Max(0, baseWeights[i]);
                if (available != null && (i >= available.Length || !available[i])) w = 0;
                if (i >= FirstBaitBoostedIndex) rare += w;
                else normal += w;
            }
            if (normal + rare <= 0) throw new InvalidOperationException("Không có độ hiếm nào khả dụng.");

            double share = Math.Clamp(rareShare, 0, 1);
            if (rare <= 0) share = 0;
            else if (normal <= 0) share = 1;

            var p = new double[RarityCount];
            for (int i = 0; i < RarityCount; i++)
            {
                double w = Math.Max(0, baseWeights[i]);
                if (available != null && (i >= available.Length || !available[i])) continue;
                p[i] = i >= FirstBaitBoostedIndex ? w / rare * share : w / normal * (1 - share);
            }
            return p;
        }

        private static void MaskAndNormalize(double[] p, bool[] available)
        {
            double sum = 0;
            for (int i = 0; i < RarityCount; i++)
            {
                if (i >= available.Length || !available[i]) p[i] = 0;
                sum += p[i];
            }
            if (sum <= 0) throw new InvalidOperationException("Không có độ hiếm nào khả dụng.");
            for (int i = 0; i < RarityCount; i++) p[i] /= sum;
        }

        /// <summary>Chọn chỉ số độ hiếm từ số ngẫu nhiên roll ∈ [0, 1).</summary>
        public static int PickRarity(double[] probabilities, double roll)
        {
            double acc = 0;
            int last = 0;
            for (int i = 0; i < probabilities.Length; i++)
            {
                if (probabilities[i] <= 0) continue;
                acc += probabilities[i];
                last = i;
                if (roll < acc) return i;
            }
            return last; // sai số làm tròn
        }

        /// <summary>
        /// Giá trị kỳ vọng mỗi lần bắt (xu), dùng cho thu nhập online ước tính và offline.
        /// avgPriceByRarity[i] là giá trung bình của các loài thuộc độ hiếm i.
        /// </summary>
        public static double ExpectedValuePerCatch(double[] probabilities, double[] avgPriceByRarity,
            double trashChance, double trashPrice)
        {
            double fishEv = 0;
            for (int i = 0; i < RarityCount; i++) fishEv += probabilities[i] * avgPriceByRarity[i];
            double t = Math.Clamp(trashChance, 0, 1);
            return t * trashPrice + (1 - t) * fishEv;
        }
    }
}
