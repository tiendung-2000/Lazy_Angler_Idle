using System.Collections.Generic;
using LazyAngler.Core;
using LazyAngler.Data;
using LazyAngler.Systems;
using UnityEngine;

namespace LazyAngler.Visual
{
    /// <summary>
    /// Vùng nước có cá. Vẽ vùng bằng một Collider2D (Box/Circle/Polygon...) trong prefab map: cá spawn ngẫu nhiên
    /// và bơi lang thang trong vùng đó. Thỉnh thoảng một con bỏ chạy, rồi một con khác (độ hiếm chọn theo tỉ lệ,
    /// hạn chế trùng độ hiếm) xuất hiện lại. Chỉ là hình ảnh: kết quả câu do AutoFishingController quyết định,
    /// presenter "mượn" một con phù hợp (TakeFor) để diễn cảnh cắn câu.
    /// Đừng scale object này; đổi kích thước bằng collider.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class FishZone : MonoBehaviour
    {
        [Header("Số lượng")]
        [Min(1)] [SerializeField] private int fishCount = 6;
        [Tooltip("Số cá tối đa cùng một độ hiếm trong vùng: Common, Uncommon, Rare, Epic, Legendary.")]
        [SerializeField] private int[] maxPerRarity = { 3, 2, 1, 1, 1 };

        [Header("Cá bỏ đi / xuất hiện (giây)")]
        [Tooltip("Khoảng chờ ngẫu nhiên giữa hai lần có một con cá bỏ chạy. Đặt (0,0) để tắt.")]
        [SerializeField] private Vector2 leaveInterval = new Vector2(6f, 14f);
        [Tooltip("Sau khi cá đi, chờ khoảng này rồi cá mới xuất hiện.")]
        [SerializeField] private Vector2 respawnDelay = new Vector2(0.8f, 2.5f);

        [Header("Cá vàng sự kiện (chạm để nhận xu)")]
        [Tooltip("Khoảng chờ ngẫu nhiên giữa hai lần cá vàng bơi qua. Đặt (0,0) để tắt.")]
        [SerializeField] private Vector2 bonusInterval = new Vector2(35f, 80f);
        [Tooltip("Xu thưởng = bấy nhiêu giây thu nhập hiện tại.")]
        [SerializeField] private float bonusRewardSeconds = 45f;
        [SerializeField] private int bonusMinReward = 25;

        private float _nextBonusAt;
        private BonusFish _bonus;
        private readonly List<FishSwimmer> _fish = new List<FishSwimmer>();
        private Collider2D _area;
        private Transform _container;
        private float _nextSpawnAt;
        private float _nextLeaveAt;
        private float[] _weights;

        private void Awake()
        {
            _area = GetComponent<Collider2D>();
            _area.isTrigger = true; // chỉ là vùng, không chặn vật lý
            _container = new GameObject("Fish").transform;
            _container.SetParent(transform, false);
        }

        private void Start()
        {
            Physics2D.SyncTransforms(); // để collider.bounds đúng ngay khi prefab vừa được tạo
            for (int i = 0; i < fishCount; i++) Spawn(RollRarity(), fadeIn: false);
            ScheduleLeave();
            ScheduleBonus();
        }

        private void Update()
        {
            if (bonusInterval.y > 0f && Time.time >= _nextBonusAt)
            {
                var gm = GameManager.Instance;
                if (_bonus == null && gm != null && gm.IsFishingActive)
                {
                    var b = _area.bounds;
                    _bonus = BonusFish.Spawn(_container, Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y),
                        bonusRewardSeconds, bonusMinReward);
                }
                ScheduleBonus();
            }

            _fish.RemoveAll(f => f == null);

            if (_fish.Count < fishCount && Time.time >= _nextSpawnAt)
            {
                Spawn(RollRarity(), fadeIn: true);
                _nextSpawnAt = Time.time + 0.25f;
            }

            if (leaveInterval.y > 0f && Time.time >= _nextLeaveAt)
            {
                LetOneLeave();
                ScheduleLeave();
            }
        }

        // ---------- API cho presenter ----------

        /// <summary>
        /// Lấy một con cá cho kết quả câu (rời khỏi vùng, người gọi chịu trách nhiệm hủy).
        /// Ưu tiên cá cùng độ hiếm gần nhất; không có thì đổi màu con gần nhất; vùng trống thì tạo mới.
        /// </summary>
        public FishSwimmer TakeFor(Rarity rarity, Vector3 point)
        {
            var fish = Nearest(point, f => f.Rarity == rarity) ?? Nearest(point, null);
            if (fish == null) fish = Spawn(rarity, fadeIn: true);
            else if (fish.Rarity != rarity) fish.SetRarity(rarity);
            _fish.Remove(fish);
            return fish;
        }

        /// <summary>Báo vùng đang thiếu cá: cá mới xuất hiện sau một khoảng trễ.</summary>
        public void Refill(float delay = 0.4f) => _nextSpawnAt = Mathf.Max(_nextSpawnAt, Time.time + delay);

        /// <summary>Điểm ngẫu nhiên bên trong collider (cá dùng để chọn nơi bơi tới).</summary>
        public Vector2 RandomPoint()
        {
            if (_area == null) _area = GetComponent<Collider2D>();
            var b = _area.bounds;
            for (int i = 0; i < 20; i++)
            {
                var p = new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y));
                if (_area.OverlapPoint(p)) return p;
            }
            return b.center;
        }

        // ---------- Nội bộ ----------

        private FishSwimmer Spawn(Rarity rarity, bool fadeIn)
        {
            var go = new GameObject("Fish");
            go.transform.SetParent(_container, false);
            go.transform.position = RandomPoint();
            var fish = go.AddComponent<FishSwimmer>();
            fish.Init(rarity, this, fadeIn);
            _fish.Add(fish);
            return fish;
        }

        /// <summary>Một con đang bơi tự do chạy đi mất, chỗ trống sẽ được lấp bằng cá mới sau respawnDelay.</summary>
        private void LetOneLeave()
        {
            var candidates = _fish.FindAll(f => f != null && f.IsAvailable);
            if (candidates.Count == 0) return;
            var fish = candidates[Random.Range(0, candidates.Count)];
            _fish.Remove(fish);
            // Chạy về phía mép gần hơn của vùng.
            float awayFromX = fish.transform.position.x + (fish.transform.position.x < _area.bounds.center.x ? 1f : -1f);
            fish.Flee(awayFromX);
            Refill(Random.Range(respawnDelay.x, respawnDelay.y));
        }

        private void ScheduleBonus()
            => _nextBonusAt = Time.time + Random.Range(Mathf.Max(0f, bonusInterval.x), Mathf.Max(bonusInterval.x, bonusInterval.y));

        private void ScheduleLeave()
            => _nextLeaveAt = Time.time + Random.Range(Mathf.Max(0f, leaveInterval.x), Mathf.Max(leaveInterval.x, leaveInterval.y));

        /// <summary>
        /// Chọn độ hiếm cho cá mới: theo tỉ lệ GameConfig.rarityWeights, bỏ độ hiếm đã đủ maxPerRarity,
        /// và giảm một nửa tỉ lệ cho mỗi con cùng độ hiếm đang có để vùng đa dạng hơn.
        /// </summary>
        private Rarity RollRarity()
        {
            var baseWeights = BaseWeights();
            var counts = new int[LootTable.RarityCount];
            foreach (var f in _fish) if (f != null) counts[(int)f.Rarity]++;

            double[] w = new double[LootTable.RarityCount];
            double total = 0;
            for (int i = 0; i < w.Length; i++)
            {
                int cap = maxPerRarity != null && i < maxPerRarity.Length ? maxPerRarity[i] : int.MaxValue;
                w[i] = counts[i] >= cap ? 0 : baseWeights[i] * System.Math.Pow(0.5, counts[i]);
                total += w[i];
            }
            if (total <= 0) w = System.Array.ConvertAll(baseWeights, x => (double)x); // mọi độ hiếm đã đủ: bỏ giới hạn
            return (Rarity)Pick(w);
        }

        private static int Pick(double[] weights)
        {
            double total = 0;
            foreach (var x in weights) total += x;
            double roll = Random.value * total, acc = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (roll < acc) return i;
            }
            return 0;
        }

        private float[] BaseWeights()
        {
            if (_weights != null) return _weights;
            var cfg = GameManager.Instance != null ? GameManager.Instance.Config : null;
            _weights = cfg != null && cfg.rarityWeights != null && cfg.rarityWeights.Length == LootTable.RarityCount
                ? cfg.rarityWeights
                : System.Array.ConvertAll(LootTable.DefaultRarityWeights, x => (float)x);
            return _weights;
        }

        private FishSwimmer Nearest(Vector3 point, System.Func<FishSwimmer, bool> filter)
        {
            FishSwimmer best = null;
            float bestDist = float.MaxValue;
            foreach (var f in _fish)
            {
                if (f == null || f.CurrentMode != FishSwimmer.Mode.Wander) continue;
                if (filter != null && !filter(f)) continue;
                float d = (f.transform.position - point).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = f;
                }
            }
            return best;
        }
    }
}
