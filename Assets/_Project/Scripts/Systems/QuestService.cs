using System;
using System.Collections.Generic;
using LazyAngler.Data;

namespace LazyAngler.Systems
{
    /// <summary>
    /// Nhiệm vụ ngày + nhiệm vụ tuần hoàn. Không biết gì về UI/xu: GameManager báo việc đã làm (Report),
    /// UI đọc danh sách và gọi Claim; phần thưởng do GameManager trao.
    /// - Ngày: mỗi ngày (giờ máy) rút ngẫu nhiên N nhiệm vụ khác nhau từ dailyQuestPool; qua ngày thì làm mới.
    /// - Tuần hoàn: luôn có đủ mọi mẫu trong repeatingQuests; nhận thưởng xong thì sang vòng sau (chỉ tiêu và thưởng tăng),
    ///   tiến độ vượt chỉ tiêu được chuyển sang vòng sau.
    /// </summary>
    public class QuestService
    {
        private readonly GameConfig _config;
        private readonly Func<double> _income;
        private readonly Dictionary<string, QuestData> _byId = new Dictionary<string, QuestData>();

        public string DateKey { get; private set; } = "";
        public List<QuestProgress> Daily { get; } = new List<QuestProgress>();
        public List<QuestProgress> Repeating { get; } = new List<QuestProgress>();

        /// <summary>Một nhiệm vụ vừa đủ chỉ tiêu (chưa nhận thưởng).</summary>
        public event Action<QuestData, QuestProgress> Completed;

        public QuestService(GameConfig config, Func<double> incomePerSecond)
        {
            _config = config;
            _income = incomePerSecond ?? (() => 0.0);
            IndexQuests();
        }

        public QuestData Find(string questId) => questId != null && _byId.TryGetValue(questId, out var q) ? q : null;

        public static string TodayKey(DateTime now) => now.ToString("yyyyMMdd");

        // ---------- Lưu / tải ----------

        public void Load(SaveData data)
        {
            DateKey = data.questDate ?? "";
            Daily.Clear();
            Repeating.Clear();
            if (data.dailyQuests != null) Daily.AddRange(data.dailyQuests);
            if (data.repeatingQuests != null) Repeating.AddRange(data.repeatingQuests);
            Daily.RemoveAll(p => Find(p.questId) == null);
            Repeating.RemoveAll(p => Find(p.questId) == null);
            EnsureRepeating();
        }

        public void Save(SaveData data)
        {
            data.questDate = DateKey;
            data.dailyQuests.AddRange(Daily);
            data.repeatingQuests.AddRange(Repeating);
        }

        public void Reset()
        {
            DateKey = "";
            Daily.Clear();
            Repeating.Clear();
        }

        // ---------- Làm mới ----------

        /// <summary>Gọi định kỳ: sang ngày mới thì rút bộ nhiệm vụ ngày mới; đảm bảo nhiệm vụ tuần hoàn đầy đủ. Trả về true nếu có thay đổi.</summary>
        public bool Refresh(DateTime now)
        {
            bool changed = EnsureRepeating();
            string key = TodayKey(now);
            if (key == DateKey && Daily.Count > 0) return changed;
            DateKey = key;
            RollDaily(key);
            return true;
        }

        private void RollDaily(string key)
        {
            Daily.Clear();
            var pool = new List<QuestData>();
            foreach (var q in _config.dailyQuestPool) if (q != null && !string.IsNullOrEmpty(q.questId)) pool.Add(q);
            // Cùng ngày thì cùng bộ nhiệm vụ (kể cả khi thoát vào lại).
            var rng = new System.Random(int.TryParse(key, out int seed) ? seed : 1);
            int count = Math.Min(Math.Max(1, _config.dailyQuestCount), pool.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = rng.Next(pool.Count);
                var q = pool[pick];
                pool.RemoveAt(pick);
                Daily.Add(new QuestProgress { questId = q.questId, target = q.TargetFor(0, _income()) });
            }
        }

        private bool EnsureRepeating()
        {
            bool changed = false;
            foreach (var q in _config.repeatingQuests)
            {
                if (q == null || string.IsNullOrEmpty(q.questId)) continue;
                if (Repeating.Exists(p => p.questId == q.questId)) continue;
                Repeating.Add(new QuestProgress { questId = q.questId, target = q.TargetFor(0, _income()) });
                changed = true;
            }
            return changed;
        }

        // ---------- Tiến độ & nhận thưởng ----------

        /// <summary>Báo đã làm được amount việc thuộc metric. Trả về true nếu có nhiệm vụ nào thay đổi.</summary>
        public bool Report(QuestMetric metric, long amount)
        {
            if (amount <= 0) return false;
            bool changed = false;
            foreach (var p in Daily) changed |= Add(p, metric, amount, daily: true);
            foreach (var p in Repeating) changed |= Add(p, metric, amount, daily: false);
            return changed;
        }

        private bool Add(QuestProgress p, QuestMetric metric, long amount, bool daily)
        {
            var q = Find(p.questId);
            if (q == null || q.metric != metric) return false;
            if (daily && p.claimed) return false;
            bool wasComplete = p.IsComplete;
            p.progress += amount;
            if (!wasComplete && p.IsComplete) Completed?.Invoke(q, p);
            return true;
        }

        public bool CanClaim(QuestProgress p) => p != null && p.IsComplete && !p.claimed;

        public bool HasClaimable()
        {
            foreach (var p in Daily) if (CanClaim(p)) return true;
            foreach (var p in Repeating) if (CanClaim(p)) return true;
            return false;
        }

        /// <summary>
        /// Nhận thưởng: nhiệm vụ ngày được đánh dấu đã nhận; nhiệm vụ tuần hoàn sang vòng sau.
        /// Trả về (mẫu, vòng đã hoàn thành) để GameManager trao thưởng; null nếu chưa thể nhận.
        /// </summary>
        public (QuestData quest, int cycle)? Claim(QuestProgress p)
        {
            if (!CanClaim(p)) return null;
            var q = Find(p.questId);
            if (q == null) return null;
            int cycle = p.cycle;
            if (Daily.Contains(p))
            {
                p.claimed = true;
            }
            else
            {
                long carry = Math.Max(0, p.progress - p.target);
                p.cycle++;
                p.target = q.TargetFor(p.cycle, _income());
                p.progress = carry;
                // Phần dư đã đủ cả vòng mới thì cũng báo hoàn thành.
                if (p.IsComplete) Completed?.Invoke(q, p);
            }
            return (q, cycle);
        }

        private void IndexQuests()
        {
            void Add(QuestData q)
            {
                if (q != null && !string.IsNullOrEmpty(q.questId)) _byId[q.questId] = q;
            }
            foreach (var q in _config.dailyQuestPool) Add(q);
            foreach (var q in _config.repeatingQuests) Add(q);
        }
    }
}
