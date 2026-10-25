using System.Collections.Generic;
using LazyAngler.Core;
using LazyAngler.Data;

namespace LazyAngler.Systems
{
    public struct CatchResult
    {
        /// <summary>fishId của rác (dùng chung cho thống kê đã bắt).</summary>
        public const string TrashId = "trash";

        public FishData fish;   // null nếu là rác
        public string fishId;
        public int price;
        public float size;
        /// <summary>Cá biến thể (màu khác, rất hiếm): giá cao hơn và tính vào bộ sưu tập biến thể.</summary>
        public bool isVariant;
        public bool IsTrash => fish == null;
        /// <summary>Cá hiếm: cần thắng minigame mới được nhận.</summary>
        public bool IsRare => fish != null && fish.IsRareMinigameFish;
    }

    /// <summary>
    /// Chọn kết quả sau khi cá cắn: rác → độ hiếm (tỉ lệ cá hiếm theo May mắn, trọng số trong nhóm) → loài trong vùng.
    /// Phân biệt cá thường và cá hiếm (FishData.minigame.type != None).
    /// Tỉ lệ cá hiếm chỉ đến từ May mắn (có trần), hoàn toàn độc lập với Bite Chance.
    /// Khi allowRare = false (offline, người chơi không hoạt động) chỉ chọn trong cá thường.
    /// </summary>
    public class FishSelectionService
    {
        private readonly GameConfig _config;
        private readonly Pool _all;
        private readonly Pool _normalOnly;
        private readonly System.Random _rng;
        private readonly System.Func<double> _priceMultiplier;
        private readonly System.Func<double> _rareBonus;
        private readonly System.Func<double> _variantBonus;
        private readonly System.Func<WeatherFlags> _weather;
        private readonly FishData _boss;

        /// <param name="priceMultiplier">Hệ số giá (nâng cấp Giá trị cá); null = ×1.</param>
        /// <param name="rareBonus">Cộng thêm vào tỉ lệ cá hiếm (mồi, ngọc trai), tính cả sau trần May mắn; null = 0.</param>
        /// <param name="variantBonus">Cộng thêm vào xác suất cá biến thể (mồi); null = 0.</param>
        /// <param name="weather">Thời tiết/giờ đang diễn ra: cá sự kiện chỉ xuất hiện khi điều kiện của chúng đang diễn ra; null = không có.</param>
        public FishSelectionService(GameConfig config, int zoneIndex, int? seed = null, System.Func<double> priceMultiplier = null,
            System.Func<double> rareBonus = null, System.Func<double> variantBonus = null, System.Func<WeatherFlags> weather = null)
        {
            _weather = weather ?? (() => WeatherFlags.None);
            _config = config;
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            _priceMultiplier = priceMultiplier ?? (() => 1.0);
            _rareBonus = rareBonus ?? (() => 0.0);
            _variantBonus = variantBonus ?? (() => 0.0);
            foreach (var f in config.fish)
                if (f != null && f.isBoss && f.zoneIndex == zoneIndex) { _boss = f; break; }
            _all = new Pool(config, zoneIndex, includeRare: true);
            _normalOnly = new Pool(config, zoneIndex, includeRare: false);
        }

        /// <summary>Có ít nhất một loài cá hiếm trong vùng.</summary>
        public bool HasRareFish => _all.Count > _normalOnly.Count;

        /// <param name="luckLevel">Cấp May mắn: quyết định tỉ lệ cá hiếm (GameConfig.baseRareChance/rareChancePerLevel/maxRareChance).</param>
        public CatchResult Select(int luckLevel, bool allowRare)
        {
            var pool = allowRare ? _all : _normalOnly;

            // Cá boss: rất hiếm, chỉ khi người chơi đang online (allowRare) vì cần minigame.
            if (allowRare && _boss != null && _boss.IsRareMinigameFish && _rng.NextDouble() < _config.bossChancePerBite)
                return MakeResult(_boss);

            if (pool.Count == 0 || _rng.NextDouble() < _config.trashChance)
                return Trash();

            int rarity = LootTable.PickRarity(pool.Probabilities(luckLevel, _rareBonus()), _rng.NextDouble());
            var fish = pool.Pick(rarity, _rng, _weather(), _config.eventFishWeight);
            if (fish == null) return Trash();
            return MakeResult(fish);
        }

        private CatchResult MakeResult(FishData fish)
        {
            float t = (float)_rng.NextDouble();
            bool variant = _rng.NextDouble() < System.Math.Clamp(_config.variantChance + _variantBonus(), 0.0, 1.0);
            int price = Price(fish.basePrice);
            if (variant) price = (int)System.Math.Min(int.MaxValue, (long)System.Math.Round(price * (double)System.Math.Max(1f, _config.variantPriceMultiplier)));
            return new CatchResult
            {
                fish = fish,
                fishId = fish.fishId,
                price = price,
                size = fish.sizeRange.x + (fish.sizeRange.y - fish.sizeRange.x) * t,
                isVariant = variant,
            };
        }

        /// <summary>Giá trị kỳ vọng mỗi lần bắt. Offline dùng allowRare = false.</summary>
        public double ExpectedValuePerCatch(int luckLevel, bool allowRare)
        {
            var pool = allowRare ? _all : _normalOnly;
            if (pool.Count == 0) return Price(_config.trashPrice);
            return _priceMultiplier() * LootTable.ExpectedValuePerCatch(pool.Probabilities(luckLevel, _rareBonus()), pool.AvgPrice,
                _config.trashChance, _config.trashPrice);
        }

        private CatchResult Trash() => new CatchResult { fishId = CatchResult.TrashId, price = Price(_config.trashPrice) };

        /// <summary>
        /// Giá thực nhận = giá gốc × hệ số Giá trị cá. Phần lẻ làm tròn lên theo xác suất (2 × 1,1 = 2,2 → 20% ra 3 xu)
        /// để cá rẻ vẫn hưởng đúng % nâng cấp tính trung bình.
        /// </summary>
        private int Price(int basePrice)
        {
            double value = basePrice * System.Math.Max(1.0, _priceMultiplier());
            int whole = (int)System.Math.Floor(value);
            return whole + (_rng.NextDouble() < value - whole ? 1 : 0);
        }

        /// <summary>Danh sách loài theo độ hiếm cho một vùng, có cache xác suất theo cấp mồi.</summary>
        private class Pool
        {
            private readonly GameConfig _config;
            private readonly List<FishData>[] _byRarity = new List<FishData>[LootTable.RarityCount];
            // Cá sự kiện (chỉ xuất hiện theo giờ/thời tiết) tách riêng: không tính vào xác suất độ hiếm, chỉ được chọn
            // thay cho cá thường của cùng độ hiếm khi điều kiện đang diễn ra.
            private readonly List<FishData>[] _events = new List<FishData>[LootTable.RarityCount];
            private readonly bool[] _available = new bool[LootTable.RarityCount];
            private int _cachedLuck = -1;
            private double _cachedBonus;
            private double[] _probabilities;

            public readonly double[] AvgPrice = new double[LootTable.RarityCount];
            public int Count { get; }

            public Pool(GameConfig config, int zone, bool includeRare)
            {
                _config = config;
                for (int i = 0; i < _byRarity.Length; i++)
                {
                    _byRarity[i] = new List<FishData>();
                    _events[i] = new List<FishData>();
                }
                foreach (var f in config.fish)
                {
                    if (f == null || f.zoneIndex != zone) continue;
                    if (!includeRare && f.IsRareMinigameFish) continue;
                    int r = (int)f.rarity;
                    if (r < 0 || r >= LootTable.RarityCount) continue;
                    if (f.isBoss) continue; // cá boss có đường chọn riêng (FishSelectionService.Select)
                    if (f.IsEventFish)
                    {
                        _events[r].Add(f);
                        continue;
                    }
                    _byRarity[r].Add(f);
                    Count++;
                }
                for (int i = 0; i < _byRarity.Length; i++)
                {
                    _available[i] = _byRarity[i].Count > 0;
                    if (!_available[i]) continue;
                    double sum = 0;
                    foreach (var f in _byRarity[i]) sum += f.basePrice;
                    AvgPrice[i] = sum / _byRarity[i].Count;
                }
            }

            public double[] Probabilities(int luckLevel, double rareBonus = 0.0)
            {
                if (luckLevel != _cachedLuck || rareBonus != _cachedBonus || _probabilities == null)
                {
                    var w = new double[LootTable.RarityCount];
                    for (int i = 0; i < w.Length; i++)
                        w[i] = _config.rarityWeights != null && i < _config.rarityWeights.Length ? _config.rarityWeights[i] : 0;
                    // Bonus (mồi, ngọc trai) cộng sau khi kẹp trần May mắn, khớp FishingStatProvider.RareChance.
                    double rareShare = System.Math.Min(1.0, EconomyCalc.RareChance(_config.baseRareChance, _config.rareChancePerLevel,
                        _config.maxRareChance, luckLevel) + System.Math.Max(0.0, rareBonus));
                    _probabilities = LootTable.RarityProbabilitiesForRareShare(w, rareShare, _available);
                    _cachedLuck = luckLevel;
                    _cachedBonus = rareBonus;
                }
                return _probabilities;
            }

            /// <summary>Chọn một loài của độ hiếm: cá thường trọng số 1, cá sự kiện đang hoạt động trọng số eventWeight.</summary>
            public FishData Pick(int rarity, System.Random rng, WeatherFlags weather, float eventWeight)
            {
                var regular = _byRarity[rarity];
                var events = _events[rarity];
                double total = regular.Count;
                int active = 0;
                foreach (var e in events) if (e != null && WeatherUtil.IsActive(e.requiredCondition, weather)) active++;
                total += active * System.Math.Max(1f, eventWeight);
                if (total <= 0) return null;

                double roll = rng.NextDouble() * total;
                if (roll < regular.Count) return regular[(int)roll]; // asset bị hủy giữa chừng thì trả null (rác)
                if (active == 0) return regular.Count > 0 ? regular[rng.Next(regular.Count)] : null;
                roll = (roll - regular.Count) / System.Math.Max(1f, eventWeight);
                int pick = System.Math.Min(active - 1, (int)roll);
                foreach (var e in events)
                {
                    if (e == null || !WeatherUtil.IsActive(e.requiredCondition, weather)) continue;
                    if (pick-- == 0) return e;
                }
                return null;
            }
        }
    }
}
