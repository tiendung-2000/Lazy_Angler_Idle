using System;
using LazyAngler.Core;
using LazyAngler.Data;

namespace LazyAngler.Systems
{
    /// <summary>
    /// Chỉ số của vòng câu tự động, lấy từ cấp nâng cấp hiện có (Cần câu, Mồi câu, Tỉ lệ cá cắn):
    /// - Bite Speed (Cần câu + Mồi câu): thời gian đợi tối thiểu trước khi cá có thể cắn.
    /// - Bite Chance (Cần câu + Tỉ lệ cá cắn): xác suất cá cắn mỗi lần kiểm tra sau thời gian đợi.
    /// - Miss Chance (Cần câu, giảm theo Mồi câu + Tỉ lệ cá cắn): tỉ lệ câu hụt khi giật cần.
    /// - Fishing Speed (Cần câu): thời gian kéo cần.
    /// Bản đồ hiện tại (ZoneData, nếu có) làm khó thêm: nhân Bite Chance, cộng Miss Chance, kéo và đợi lâu hơn.
    /// </summary>
    public class FishingStatProvider
    {
        private readonly GameConfig _config;
        private readonly Func<int> _rodLevel;
        private readonly Func<int> _biteLevel;
        private readonly Func<int> _baitLevel;
        private readonly Func<ZoneData> _zone;
        private readonly Func<int> _luckLevel;
        private readonly Func<float> _rareBonus;
        private readonly Func<float> _biteSpeedBonus;

        /// <param name="rareBonus">Cộng thêm vào tỉ lệ cá hiếm (nâng cấp ngọc trai), tính cả sau trần.</param>
        /// <param name="biteSpeedBonus">Tăng tốc cá cắn (0,3 = đợi ngắn đi 1/1,3), nâng cấp ngọc trai.</param>
        public FishingStatProvider(GameConfig config, Func<int> rodLevel, Func<int> biteLevel, Func<int> baitLevel = null,
            Func<ZoneData> zone = null, Func<int> luckLevel = null, Func<float> rareBonus = null, Func<float> biteSpeedBonus = null)
        {
            _rareBonus = rareBonus ?? (() => 0f);
            _biteSpeedBonus = biteSpeedBonus ?? (() => 0f);
            _config = config;
            _rodLevel = rodLevel;
            _biteLevel = biteLevel;
            _baitLevel = baitLevel ?? (() => 0);
            _zone = zone ?? (() => null);
            _luckLevel = luckLevel ?? (() => 0);
        }

        /// <summary>Bộ chỉ số giả định ở các cấp cho trước (dùng để xem trước hiệu quả nâng cấp).</summary>
        public FishingStatProvider WithLevels(int rodLevel, int biteLevel, int baitLevel, int? luckLevel = null)
        {
            int luck = luckLevel ?? _luckLevel();
            return new FishingStatProvider(_config, () => rodLevel, () => biteLevel, () => baitLevel, _zone, () => luck,
                _rareBonus, _biteSpeedBonus);
        }

        /// <summary>Bản đồ đang câu (null = không có hệ số độ khó).</summary>
        public ZoneData Zone => _zone();

        /// <summary>Cấp cần câu hiện tại (null nếu chưa cấu hình rodTiers).</summary>
        public RodTierData RodTier => RodTierData.ForLevel(_config.rodTiers, _rodLevel());

        // ---------- Câu → Đợi cá ----------

        public float CastSeconds => Math.Max(0f, _config.castSeconds);

        /// <summary>
        /// Hệ số tốc độ cá cắn câu = hệ số của cần × (1 + baitBiteSpeedPerLevel × cấp Mồi câu), không vượt maxBiteSpeedMultiplier
        /// (trần không thấp hơn hệ số của cần, để cần tốt không bị kẹp).
        /// </summary>
        public float BiteSpeedMultiplier
        {
            get
            {
                float rod = RodBiteSpeed;
                float m = rod * (1f + Math.Max(0f, _config.baitBiteSpeedPerLevel) * Math.Max(0, _baitLevel()));
                return Math.Min(m, BiteSpeedCap);
            }
        }

        private float RodBiteSpeed
        {
            get
            {
                var tier = RodTier;
                return tier != null ? Math.Max(0.1f, tier.biteSpeedMultiplier) : 1f;
            }
        }

        /// <summary>Trần hệ số tốc độ cá cắn.</summary>
        public float BiteSpeedCap => Math.Max(RodBiteSpeed, Math.Max(1f, _config.maxBiteSpeedMultiplier));

        /// <summary>Mồi câu đã đưa tốc độ cá cắn chạm trần (nâng thêm không còn tác dụng).</summary>
        public bool IsBiteSpeedCapped => BiteSpeedMultiplier >= BiteSpeedCap - 1e-4f;

        /// <summary>Thời gian đợi tối thiểu (trung bình) trước khi cá có thể cắn.</summary>
        public float MinBiteWaitSeconds
            => Math.Max(0f, _config.baseBiteWaitSeconds) / (BiteSpeedMultiplier * (1f + Math.Max(0f, _biteSpeedBonus()))) * (Zone != null ? Math.Max(0.1f, Zone.biteWaitMultiplier) : 1f);

        public float BiteWaitRandomness => Math.Clamp(_config.biteWaitRandomness, 0f, 0.9f);

        /// <summary>
        /// Tỉ lệ cá cắn của người chơi (chưa tính map): cơ sở (của cấp cần câu, hoặc baseBiteChance)
        /// cộng thêm theo cấp Tỉ lệ cá cắn, kẹp bởi trần maxBiteChance.
        /// </summary>
        public float PlayerBiteChance
        {
            get
            {
                var tier = RodTier;
                float baseChance = tier != null ? tier.biteChance : _config.baseBiteChance;
                float p = baseChance + _config.biteChancePerLevel * Math.Max(0, _biteLevel());
                return Math.Clamp(p, 0f, BiteChanceCap);
            }
        }

        /// <summary>Trần tỉ lệ cá cắn của người chơi (không thấp hơn tỉ lệ gốc của cần đang dùng).</summary>
        public float BiteChanceCap
        {
            get
            {
                var tier = RodTier;
                float baseChance = tier != null ? tier.biteChance : _config.baseBiteChance;
                return Math.Max(Math.Clamp(baseChance, 0f, 1f), Math.Clamp(_config.maxBiteChance, 0f, 1f));
            }
        }

        /// <summary>Nâng cấp Tỉ lệ cá cắn đã chạm trần.</summary>
        public bool IsBiteChanceCapped => PlayerBiteChance >= BiteChanceCap - 1e-4f;

        /// <summary>Xác suất cá cắn mỗi lần kiểm tra thực tế (0..1) = tỉ lệ của người chơi × độ khó của map.</summary>
        public float BiteChance
            => Math.Clamp(PlayerBiteChance * (Zone != null ? Math.Max(0f, Zone.biteChanceMultiplier) : 1f), 0f, 1f);

        // ---------- Cá hiếm ----------

        /// <summary>Tỉ lệ cá hiếm (Hiếm, Sử thi, Huyền thoại) mỗi lần cá cắn khi người chơi đang online.</summary>
        public float RareChance
            => Math.Min(1f, (float)EconomyCalc.RareChance(_config.baseRareChance, _config.rareChancePerLevel, _config.maxRareChance, _luckLevel())
                            + Math.Max(0f, _rareBonus()));

        public float RareChanceCap => Math.Clamp(_config.maxRareChance, 0f, 1f);

        /// <summary>Số cấp May mắn tối đa có tác dụng (tới trần maxRareChance).</summary>
        public int MaxLuckLevel => EconomyCalc.LevelsToCap(_config.baseRareChance, _config.rareChancePerLevel, _config.maxRareChance);

        public float BiteCheckInterval => Math.Max(0.05f, _config.biteCheckInterval);

        /// <summary>Thời gian đợi cá kỳ vọng = đợi tối thiểu + kiểm tra mỗi interval với BiteChance.</summary>
        public double ExpectedWaitSeconds
        {
            get
            {
                float p = BiteChance;
                return p <= 0f ? double.PositiveInfinity : MinBiteWaitSeconds + BiteCheckInterval / p;
            }
        }

        // ---------- Cá cắn → Kéo cần / Câu hụt ----------

        public float BiteReactSeconds => Math.Max(0f, _config.biteReactSeconds);

        /// <summary>
        /// Tỉ lệ câu hụt khi giật cần: cơ sở của cần câu (hoặc baseMissChance) trừ theo tổng cấp
        /// Mồi câu + Tỉ lệ cá cắn, không thấp hơn minMissChance.
        /// </summary>
        public float MissChance
        {
            get
            {
                var tier = RodTier;
                float baseMiss = tier != null ? tier.missChance : _config.baseMissChance;
                int upgrades = Math.Max(0, _baitLevel()) + Math.Max(0, _biteLevel());
                float m = baseMiss - _config.missReductionPerUpgradeLevel * upgrades + (Zone != null ? Math.Max(0f, Zone.missChanceBonus) : 0f);
                float floor = Math.Min(Math.Clamp(_config.minMissChance, 0f, 1f), Math.Clamp(baseMiss, 0f, 1f));
                return Math.Clamp(m, floor, 1f);
            }
        }

        public float MissRecoverSeconds => Math.Max(0f, _config.missRecoverSeconds);

        public float BaseFishingDuration => Math.Max(0.05f, _config.baseFishingDuration);

        /// <summary>
        /// Hệ số tốc độ kéo (> 0). Lấy từ cấp cần câu nếu có rodTiers; nếu không, suy ra từ công thức cũ
        /// (cơ sở × hệ số^cấp, có sàn): multiplier = base / CastSeconds(cấp).
        /// </summary>
        public float FishingSpeedMultiplier
        {
            get
            {
                var tier = RodTier;
                if (tier != null) return Math.Max(0.01f, tier.fishingSpeedMultiplier);
                double cast = EconomyCalc.CastSeconds(BaseFishingDuration, _config.castReductionPerLevel,
                    _config.minCastSeconds, _rodLevel());
                return cast > 0 ? (float)(BaseFishingDuration / cast) : 1f;
            }
        }

        /// <summary>Thời gian kéo cần: baseFishingDuration / fishingSpeedMultiplier.</summary>
        public float EffectiveDuration
            => (float)EconomyCalc.EffectiveFishingDuration(BaseFishingDuration, FishingSpeedMultiplier)
               * (Zone != null ? Math.Max(0.1f, Zone.reelDurationMultiplier) : 1f);

        /// <summary>
        /// Thời gian kỳ vọng để một cần bắt được một con cá, tính cả các lần câu hụt
        /// (dùng cho thu nhập offline/hiển thị).
        /// </summary>
        public double ExpectedSecondsPerCatch
            => EconomyCalc.SecondsPerCatch(CastSeconds, ExpectedWaitSeconds, BiteReactSeconds,
                EffectiveDuration, MissChance, MissRecoverSeconds);
    }
}
