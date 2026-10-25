using System;
using System.Collections;
using System.Collections.Generic;
using LazyAngler.Core;
using LazyAngler.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace LazyAngler.Systems
{
    public struct OfflineReport
    {
        public double awaySeconds;
        public double countedSeconds;
        public long coins;
        public long fish;
    }

    /// <summary>
    /// Điều phối vòng lặp: câu (cộng xu ngay) – nâng cấp – hoàn thành bộ sưu tập – mở map mới (khó hơn, cá đắt hơn).
    /// Giữ trạng thái người chơi, tick vòng câu tự động (AutoFishingController) bằng một timer cố định,
    /// mở minigame cá hiếm khi online, lưu/tải và tính thu nhập offline (chỉ cá thường).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        [SerializeField] private RareFishingMinigameController rareMinigame;
        [SerializeField] private float autoSaveInterval = 30f;
        [Tooltip("Chu kỳ tick vòng câu tự động (giây). 0,1–0,25 để tiết kiệm pin.")]
        [SerializeField] private float tickInterval = 0.1f;
        [Tooltip("Thời gian hiện cá hiếm cắn câu (\"!\") trước khi mở minigame (giây).")]
        [SerializeField] private float rareRevealDelay = 0.8f;

        public static GameManager Instance { get; private set; }

        public GameConfig Config => config;
        public RareFishingMinigameController RareMinigame => rareMinigame;
        public long Coins { get; private set; }
        public long Gems { get; private set; }
        public int RodLevel { get; private set; }
        public int BaitLevel { get; private set; }
        public int BiteLevel { get; private set; }
        public int BoatLevel { get; private set; }
        public int VaultLevel { get; private set; }
        /// <summary>Cấp nâng cấp Giá trị cá (hệ số giá = FishValueMultiplier).</summary>
        public int ValueLevel { get; private set; }
        /// <summary>Cấp May mắn (tỉ lệ cá hiếm = Stats.RareChance).</summary>
        public int LuckLevel { get; private set; }

        /// <summary>Ngọc trai chưa tiêu. Tiêu trong Cửa hàng ngọc trai (nâng cấp vĩnh viễn, có cấp tối đa).</summary>
        public int PrestigePearls { get; private set; }
        public int PrestigeCount { get; private set; }
        /// <summary>Xu đã kiếm từ lần tái sinh gần nhất.</summary>
        public long RunEarned { get; private set; }
        /// <summary>Ngọc trai sẽ nhận nếu tái sinh ngay bây giờ.</summary>
        public int PendingPearls => EconomyCalc.PrestigePearls(RunEarned, config.prestigeCoinDivisor);
        /// <summary>Số xu cần kiếm thêm (trong lượt này) để có thêm 1 ngọc trai nữa.</summary>
        public long CoinsToNextPearl => Math.Max(0, EconomyCalc.CoinsForPearls(PendingPearls + 1, config.prestigeCoinDivisor) - RunEarned);
        /// <summary>Hệ số giá cá vĩnh viễn: nâng cấp ngọc trai "Giá cá" + bonus bể cá (cộng dồn).</summary>
        public double PrestigeMultiplier => 1.0 + PearlBonus(PearlUpgradeType.FishValue) + AquariumBonus;
        public bool CanPrestige => PendingPearls >= 1 && !IsRareMinigameActive;
        /// <summary>Nhiệm vụ ngày và nhiệm vụ tuần hoàn.</summary>
        public QuestService Quests { get; private set; }
        public FishingStatProvider Stats { get; private set; }
        public AutoFishingController AutoFishing { get; private set; }
        public IReadOnlyList<FishingRod> Rods => AutoFishing.Rods;
        public IReadOnlyDictionary<string, int> CaughtTotals => _caughtTotals;
        public string EquippedSkinId { get; private set; }
        /// <summary>Skin đang mặc (mặc định skin đầu tiên trong GameConfig.skins), null nếu không có skin nào.</summary>
        public SkinData EquippedSkin => FindSkin(EquippedSkinId) ?? (config.skins.Count > 0 ? config.skins[0] : null);
        public bool IsRareMinigameActive => AutoFishing != null && AutoFishing.IsPaused;
        /// <summary>Vòng câu đang chạy. Tắt (vd. ở Menu chính) thì không câu, không bắt cá, ngư dân đứng chờ.</summary>
        public bool IsFishingActive { get; private set; } = true;

        public IReadOnlyList<ZoneData> Zones => config.zones;
        /// <summary>Bản đồ đang câu (chỉ số trong GameConfig.zones, cũng là FishData.zoneIndex).</summary>
        public int CurrentZoneIndex { get; private set; }
        public ZoneData CurrentZone => CurrentZoneIndex >= 0 && CurrentZoneIndex < config.zones.Count ? config.zones[CurrentZoneIndex] : null;

        /// <summary>Thời gian một lượt câu hiện tại (giây) = baseFishingDuration / fishingSpeedMultiplier.</summary>
        public double CastSeconds => Stats.EffectiveDuration;
        public double CatchesPerSecond => EconomyCalc.CatchesPerSecond(Rods.Count, Stats.ExpectedSecondsPerCatch);
        /// <summary>Thu nhập kỳ vọng/giây từ cá thường (dùng cho offline, không tính cá hiếm).</summary>
        public double IncomePerSecond => EconomyCalc.IncomePerSecond(Rods.Count, Stats.ExpectedSecondsPerCatch,
            _selection.ExpectedValuePerCatch(LuckLevel, allowRare: false));

        /// <summary>Người chơi đang online và thực sự tham gia: app không tạm dừng, có minigame, vừa thao tác gần đây.</summary>
        public bool IsRareAllowed =>
            !_appPaused && rareMinigame != null && rareMinigame.enabled && rareMinigame.gameObject.activeInHierarchy && _selection.HasRareFish &&
            (config.rareIdleTimeout <= 0f || Time.unscaledTime - _lastActivityTime <= config.rareIdleTimeout);

        public event Action<CatchResult> FishCaught;
        /// <summary>Giật cần hụt, cá thoát (không thưởng).</summary>
        public event Action<CatchResult> FishMissed;
        public event Action StateChanged;
        public event Action<OfflineReport> OfflineEarned;
        /// <summary>Vòng câu vừa bật/tắt (SetFishingActive).</summary>
        public event Action<bool> FishingActiveChanged;
        /// <summary>Đã chuyển sang bản đồ khác (chỉ số map mới).</summary>
        public event Action<int> ZoneChanged;
        /// <summary>Vừa hoàn thành bộ sưu tập của một map → map kế tiếp được mở khóa (chỉ số map mới).</summary>
        public event Action<int> ZoneUnlocked;
        /// <summary>Vừa nâng/mua cần câu cấp mới (tên cấp, vd. "Lv3 Cần sợi carbon").</summary>
        public event Action<string> RodLeveledUp;
        /// <summary>Một nhiệm vụ vừa đủ chỉ tiêu (tên nhiệm vụ), chờ người chơi nhận thưởng.</summary>
        public event Action<string> QuestCompleted;
        /// <summary>Vừa đạt mốc bộ sưu tập: nội dung thông báo và số ngọc trai nhận được.</summary>
        public event Action<string, int> CollectionRewardGranted;
        /// <summary>Vừa tái sinh (số ngọc trai nhận được).</summary>
        public event Action<int> Prestiged;

        private readonly Dictionary<string, int> _caughtTotals = new Dictionary<string, int>();
        private readonly HashSet<string> _ownedSkins = new HashSet<string>();
        private readonly Dictionary<string, int> _pearlLevels = new Dictionary<string, int>();
        private readonly Dictionary<string, FishRecord> _records = new Dictionary<string, FishRecord>();
        private readonly HashSet<string> _claimed = new HashSet<string>();
        private FishSelectionService _selection;
        private FishingRewardService _rewards;
        private float _saveTimer;
        private bool _appPaused;
        private bool _wasPaused;
        private bool _changed;
        private float _lastActivityTime;
        private OfflineReport? _pendingReport;
        private Coroutine _tickLoop;
        private IDisposable _activityListener;
        private Coroutine _rareReveal;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            if (config == null)
            {
                Debug.LogError("[GameManager] Chưa gán GameConfig. Chạy Tools > Lazy Angler > Build Prototype.", this);
                enabled = false;
                return;
            }
            Instance = this;
            Application.targetFrameRate = 60;

            _selection = NewSelection(CurrentZoneIndex);
            Stats = new FishingStatProvider(config, () => RodLevel, () => BiteLevel, () => BaitLevel, () => CurrentZone, () => LuckLevel,
                () => (float)RareChanceBonus,
                () => (float)(PearlBonus(PearlUpgradeType.BiteSpeed) + (HasWeather(WeatherFlags.Rain) ? Math.Max(0f, config.rainBiteSpeedBonus) : 0.0)));
            // Câu được cá là cộng xu ngay (không có giỏ / bán cá).
            _rewards = new FishingRewardService(_caughtTotals, AddCoins);
            _rewards.Rewarded += OnRewarded;
            AutoFishing = new AutoFishingController(Stats, _selection, _rewards, () => LuckLevel, () => IsRareAllowed,
                () => config.rewardNormalFishOnRareFail);
            AutoFishing.RareFishHooked += OnRareFishHooked;
            AutoFishing.FishMissed += t => { _changed = true; FishMissed?.Invoke(t != null ? t.Result : default); };

            _weather = ComputeWeather(DateTime.Now);
            Quests = new QuestService(config, () => IncomePerSecond);
            Quests.Completed += (q, p) => { _changed = true; QuestCompleted?.Invoke(q.TitleFor(p.target)); };

            LoadOrNew();
        }

        private void OnEnable()
        {
            _lastActivityTime = Time.unscaledTime;
            _activityListener = InputSystem.onAnyButtonPress.Call(_ => NotifyPlayerActivity());
            _tickLoop = StartCoroutine(TickLoop());
        }

        private void OnDisable()
        {
            _activityListener?.Dispose();
            _activityListener = null;
            if (_tickLoop != null) StopCoroutine(_tickLoop);
            _tickLoop = null;
            StopRareReveal();
            AutoFishing?.CancelRare();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Đăng ký UI xảy ra trong OnEnable/Start của view, nên báo offline ở đây.
            NotifyChanged();
            if (_pendingReport.HasValue)
            {
                OfflineEarned?.Invoke(_pendingReport.Value);
                _pendingReport = null;
            }
        }

        /// <summary>Timer cố định của vòng câu (không xử lý trong Update).</summary>
        private IEnumerator TickLoop()
        {
            float last = Time.time;
            float questCheck = 0f;
            float weatherCheck = 0f;
            var wait = new WaitForSeconds(Mathf.Max(0.02f, tickInterval));
            while (true)
            {
                yield return wait;
                float now = Time.time;
                float dt = now - last;
                last = now;
                // Qua nửa đêm (giờ máy) thì làm mới nhiệm vụ ngày.
                TickBait(dt);
                weatherCheck += dt;
                if (weatherCheck >= 15f)
                {
                    weatherCheck = 0f;
                    UpdateWeather();
                }
                questCheck += dt;
                if (questCheck >= 5f)
                {
                    questCheck = 0f;
                    if (Quests.Refresh(DateTime.Now)) _changed = true;
                }
                if (IsFishingActive) AutoFishing.Tick(dt);
                if (_changed)
                {
                    _changed = false;
                    NotifyChanged();
                }

                _saveTimer += dt;
                if (_saveTimer >= autoSaveInterval) Save();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            _appPaused = paused;
            if (paused)
            {
                // Không giữ minigame khi rời app: hủy, không thưởng, không tích lũy.
                StopRareReveal();
                if (rareMinigame != null) rareMinigame.Stop();
                AutoFishing?.CancelRare();
                Save();
                _wasPaused = true;
            }
            else if (_wasPaused)
            {
                // Unity cũng gọi OnApplicationPause(false) lúc khởi động, chỉ tính khi thật sự quay lại.
                _wasPaused = false;
                NotifyPlayerActivity();
                var data = SaveSystem.Load();
                if (data != null) ApplyOffline(data.lastSaveUtc, invokeNow: true);
            }
        }

        private void OnApplicationQuit() => Save();

        /// <summary>
        /// Bật/tắt vòng câu. Tắt: hủy lượt đang dở (không thưởng, kể cả cá hiếm) và đưa mọi cần về đầu lượt,
        /// nên khi bật lại ngư dân bắt đầu bằng một lần quăng câu mới.
        /// </summary>
        public void SetFishingActive(bool active)
        {
            if (IsFishingActive == active) return;
            IsFishingActive = active;
            if (!active)
            {
                StopRareReveal();
                if (rareMinigame != null) rareMinigame.Stop();
                AutoFishing.SetRodCount(EconomyCalc.ActiveRods(BoatLevel));
            }
            else
            {
                NotifyPlayerActivity();
            }
            FishingActiveChanged?.Invoke(active);
            NotifyChanged();
        }

        /// <summary>Đánh dấu người chơi vừa thao tác (cá hiếm chỉ xuất hiện khi người chơi đang hoạt động).</summary>
        public void NotifyPlayerActivity() => _lastActivityTime = Time.unscaledTime;

        // ---------- Gameplay ----------

        /// <summary>Xu thưởng ngoài vòng câu (sự kiện cá vàng...). Tính bằng giây thu nhập, tối thiểu minCoins.</summary>
        public long GrantBonusCoins(double incomeSeconds, long minCoins)
        {
            long amount = Math.Max(minCoins, (long)(IncomePerSecond * incomeSeconds));
            AddCoins(amount);
            _changed = true;
            NotifyChanged();
            return amount;
        }

        /// <summary>Mọi nguồn thu xu đều qua đây để RunEarned (căn cứ tính ngọc trai) luôn đúng.</summary>
        private void AddCoins(long amount)
        {
            if (amount <= 0) return;
            Coins += amount;
            RunEarned += amount;
            ReportQuest(QuestMetric.EarnCoins, amount);
        }

        /// <summary>Báo một việc đã làm cho hệ nhiệm vụ (cá vàng, bắt cá, kiếm xu...).</summary>
        public void ReportQuest(QuestMetric metric, long amount)
        {
            if (Quests != null && Quests.Report(metric, amount)) _changed = true;
        }

        private void OnRewarded(CatchResult r)
        {
            _changed = true;
            if (r.fish != null)
            {
                RecordCatch(r);
                if (r.fish.isBoss) GrantBossRewards(r.fish);
                ReportQuest(QuestMetric.CatchFish, 1);
                if (r.fish.rarity >= Rarity.Rare) ReportQuest(QuestMetric.CatchRare, 1);
                if (r.isVariant) ReportQuest(QuestMetric.CatchVariant, 1);
            }
            FishCaught?.Invoke(r);
            if (r.fish != null) CheckCollectionRewards(r.fish.zoneIndex);

            // Loài mới hoàn thành bộ sưu tập của map → mở map kế tiếp.
            if (r.fish == null || r.fish.IsEventFish || r.fish.isBoss || !_caughtTotals.TryGetValue(r.fishId, out int n) || n != 1) return;
            int zone = r.fish.zoneIndex;
            if (zone + 1 < config.zones.Count && IsZoneCollectionComplete(zone))
            {
                Save();
                ZoneUnlocked?.Invoke(zone + 1);
            }
        }

        // ---------- Kỷ lục, biến thể, thưởng bộ sưu tập ----------

        /// <summary>Con cá vừa bắt phá kỷ lục kích cỡ của loài đó (không tính lần bắt đầu tiên).</summary>
        public bool LastCatchWasRecord { get; private set; }

        /// <summary>Kỷ lục của loài (null nếu chưa bắt).</summary>
        public FishRecord GetRecord(string fishId) => fishId != null && _records.TryGetValue(fishId, out var r) ? r : null;

        public bool IsRewardClaimed(string key) => _claimed.Contains(key);

        public static string RarityRewardKey(int zone, Rarity rarity) => $"r:{zone}:{(int)rarity}";
        public static string VariantRewardKey(int zone) => $"v:{zone}";

        /// <summary>Ngọc trai thưởng khi hoàn thành bộ một độ hiếm trong map (đã nhân theo map).</summary>
        public int RarityRewardPearls(int zone, Rarity rarity)
        {
            var table = config.rarityCompletionPearls;
            int baseReward = table != null && (int)rarity < table.Length ? Math.Max(0, table[(int)rarity]) : 0;
            return baseReward * (zone + 1);
        }

        public static string EventRewardKey(int zone) => $"e:{zone}";
        public int EventRewardPearls(int zone) => Math.Max(0, config.eventSetPearls) * (zone + 1);

        public int VariantRewardPearls(int zone) => Math.Max(0, config.variantCompletionPearls) * (zone + 1);

        /// <summary>Số loài đã có biến thể / tổng số loài của map.</summary>
        public (int have, int total) VariantCollection(int zone)
        {
            int have = 0, total = 0;
            foreach (var f in config.fish)
            {
                if (f == null || f.zoneIndex != zone || f.IsEventFish || f.isBoss) continue;
                total++;
                if (_records.TryGetValue(f.fishId, out var rec) && rec.variants > 0) have++;
            }
            return (have, total);
        }

        /// <summary>Số loài của một độ hiếm trong map: đã bắt / tổng.</summary>
        public (int caught, int total) RarityCollection(int zone, Rarity rarity)
        {
            int caught = 0, total = 0;
            foreach (var f in config.fish)
            {
                if (f == null || f.zoneIndex != zone || f.rarity != rarity || f.IsEventFish || f.isBoss) continue;
                total++;
                if (_caughtTotals.TryGetValue(f.fishId, out int c) && c > 0) caught++;
            }
            return (caught, total);
        }

        private void RecordCatch(CatchResult r)
        {
            if (!_records.TryGetValue(r.fishId, out var rec))
            {
                rec = new FishRecord { fishId = r.fishId };
                _records[r.fishId] = rec;
            }
            LastCatchWasRecord = rec.maxSize > 0f && r.size > rec.maxSize;
            if (r.size > rec.maxSize) rec.maxSize = r.size;
            if (r.isVariant) rec.variants++;
        }

        /// <summary>Trao ngọc trai cho mốc bộ sưu tập vừa đạt (mỗi mốc một lần): đủ một độ hiếm của map, đủ biến thể của map.</summary>
        private void CheckCollectionRewards(int zone)
        {
            int pearls = 0;
            var message = new System.Text.StringBuilder();

            for (int i = 0; i < LootTable.RarityCount; i++)
            {
                var rarity = (Rarity)i;
                string key = RarityRewardKey(zone, rarity);
                if (_claimed.Contains(key)) continue;
                var (caught, total) = RarityCollection(zone, rarity);
                if (total == 0 || caught < total) continue;
                _claimed.Add(key);
                int p = RarityRewardPearls(zone, rarity);
                pearls += p;
                if (p > 0) message.Append($"Đủ cá {Utils.RarityText.Name(rarity)} · +{p} ngọc trai\n");
            }

            string vKey = VariantRewardKey(zone);
            if (!_claimed.Contains(vKey))
            {
                var (have, total) = VariantCollection(zone);
                if (total > 0 && have >= total)
                {
                    _claimed.Add(vKey);
                    int p = VariantRewardPearls(zone);
                    pearls += p;
                    if (p > 0) message.Append($"Đủ cá biến thể · +{p} ngọc trai\n");
                }
            }

            string eKey = EventRewardKey(zone);
            if (!_claimed.Contains(eKey))
            {
                var (caught, total) = EventCollection(zone);
                if (total > 0 && caught >= total)
                {
                    _claimed.Add(eKey);
                    int p = EventRewardPearls(zone);
                    pearls += p;
                    if (p > 0) message.Append($"Đủ cá sự kiện · +{p} ngọc trai\n");
                }
            }

            if (message.Length == 0 && pearls == 0) return;
            PrestigePearls += pearls;
            _changed = true;
            Save();
            if (pearls > 0) CollectionRewardGranted?.Invoke(message.ToString().TrimEnd('\n'), pearls);
        }

        // ---------- Bản đồ ----------

        /// <summary>Số loài đã bắt / tổng số loài của map.</summary>
        public (int caught, int total) ZoneCollection(int zone)
        {
            int caught = 0, total = 0;
            foreach (var f in config.fish)
            {
                if (f == null || f.zoneIndex != zone || f.IsEventFish || f.isBoss) continue; // cá sự kiện/boss là phần thưởng thêm, không chặn mở map
                total++;
                if (_caughtTotals.TryGetValue(f.fishId, out int c) && c > 0) caught++;
            }
            return (caught, total);
        }

        /// <summary>Số cá sự kiện (chỉ có theo giờ/thời tiết) đã bắt / tổng của map.</summary>
        public (int caught, int total) EventCollection(int zone)
        {
            int caught = 0, total = 0;
            foreach (var f in config.fish)
            {
                if (f == null || f.zoneIndex != zone || !f.IsEventFish) continue;
                total++;
                if (_caughtTotals.TryGetValue(f.fishId, out int c) && c > 0) caught++;
            }
            return (caught, total);
        }

        /// <summary>Cá boss của map (null nếu map không có).</summary>
        public FishData BossOf(int zone)
        {
            foreach (var f in config.fish)
                if (f != null && f.isBoss && f.zoneIndex == zone) return f;
            return null;
        }

        public bool IsBossCaught(int zone)
        {
            var boss = BossOf(zone);
            return boss != null && _caughtTotals.TryGetValue(boss.fishId, out int n) && n > 0;
        }

        public int BossPearlsPerCatch(int zone) => Math.Max(0, config.bossPearlsPerCatch) * (zone + 1);
        public int BossFirstCatchPearls(int zone) => Math.Max(0, config.bossFirstCatchPearls) * (zone + 1);
        public static string BossRewardKey(int zone) => $"b:{zone}";

        /// <summary>Hạ được cá boss (tên boss, ngọc trai nhận được, có phải lần đầu của map không).</summary>
        public event Action<string, int, bool> BossCaught;

        private void GrantBossRewards(FishData boss)
        {
            int zone = boss.zoneIndex;
            int pearls = BossPearlsPerCatch(zone);
            bool first = _claimed.Add(BossRewardKey(zone));
            if (first) pearls += BossFirstCatchPearls(zone);
            if (pearls <= 0) return;
            PrestigePearls += pearls;
            _changed = true;
            Save();
            BossCaught?.Invoke(boss.displayName, pearls, first);
        }

        public bool IsZoneCollectionComplete(int zone)
        {
            var (caught, total) = ZoneCollection(zone);
            return total > 0 && caught >= total;
        }

        /// <summary>Map đầu luôn mở; map sau mở khi đã hoàn thành bộ sưu tập của map trước.</summary>
        public bool IsZoneUnlocked(int zone)
            => zone >= 0 && zone < config.zones.Count && (zone == 0 || IsZoneCollectionComplete(zone - 1));

        /// <summary>Đã mở khóa và đủ cấp cần câu / nâng cấp tối thiểu.</summary>
        public bool MeetsZoneRequirements(int zone)
        {
            var z = zone >= 0 && zone < config.zones.Count ? config.zones[zone] : null;
            return z != null && RodTierIndex >= z.requiredRodTier && BiteLevel >= z.requiredBiteLevel && BaitLevel >= z.requiredBaitLevel;
        }

        public bool CanFishZone(int zone) => IsZoneUnlocked(zone) && MeetsZoneRequirements(zone);

        /// <summary>Các điều kiện của map (để hiển thị đạt/thiếu): bộ sưu tập map trước, cần câu, nâng cấp.</summary>
        public List<(string label, bool met)> ZoneRequirements(int zone)
        {
            var list = new List<(string, bool)>();
            if (zone < 0 || zone >= config.zones.Count) return list;
            var z = config.zones[zone];
            if (zone > 0)
            {
                var (caught, total) = ZoneCollection(zone - 1);
                list.Add(($"Bộ sưu tập {config.zones[zone - 1].displayName} ({caught}/{total})", caught >= total && total > 0));
            }
            if (z.requiredRodTier > 0)
            {
                var tier = RodTierData.ForLevel(config.rodTiers, z.requiredRodTier);
                string name = tier != null ? $"Lv{z.requiredRodTier + 1} {tier.displayName}" : $"Lv{z.requiredRodTier + 1}";
                list.Add(($"Cần câu {name}", RodTierIndex >= z.requiredRodTier));
            }
            if (z.requiredBiteLevel > 0)
                list.Add(($"{UpgradeName(config.biteUpgrade, "Tỉ lệ cá cắn")} cấp {z.requiredBiteLevel} ({BiteLevel})", BiteLevel >= z.requiredBiteLevel));
            if (z.requiredBaitLevel > 0)
                list.Add(($"{UpgradeName(config.baitUpgrade, "Mồi câu")} cấp {z.requiredBaitLevel} ({BaitLevel})", BaitLevel >= z.requiredBaitLevel));
            return list;
        }

        private static string UpgradeName(UpgradeDefinition def, string fallback) => def != null ? def.displayName : fallback;

        /// <summary>
        /// Chuyển sang map khác: đổi bể cá, hủy lượt đang dở, cần câu bắt đầu lại. Không đổi khi đang minigame cá hiếm.
        /// </summary>
        public bool TrySetZone(int zone)
        {
            NotifyPlayerActivity();
            if (zone == CurrentZoneIndex || !CanFishZone(zone) || IsRareMinigameActive) return false;
            StopRareReveal();
            ApplyZone(zone);
            NotifyChanged();
            Save();
            return true;
        }

        private void ApplyZone(int zone)
        {
            CurrentZoneIndex = zone;
            _selection = NewSelection(zone);
            AutoFishing.SetSelection(_selection);
            AutoFishing.SetRodCount(EconomyCalc.ActiveRods(BoatLevel));
            ZoneChanged?.Invoke(zone);
        }

        /// <summary>Map đã lưu không còn câu được (đổi dữ liệu, reset): lùi về map gần nhất câu được.</summary>
        private int ValidZone(int zone)
        {
            zone = Mathf.Clamp(zone, 0, Mathf.Max(0, config.zones.Count - 1));
            while (zone > 0 && !CanFishZone(zone)) zone--;
            return zone;
        }

        private void OnRareFishHooked(CatchTicket ticket)
        {
            _changed = true;
            if (rareMinigame == null || ticket.Result.fish == null)
            {
                AutoFishing.CancelRare();
                return;
            }
            // Cho người chơi thấy cá hiếm cắn câu ("!") một chút rồi mới mở minigame.
            StopRareReveal();
            _rareReveal = StartCoroutine(RevealRare(ticket));
        }

        private IEnumerator RevealRare(CatchTicket ticket)
        {
            if (rareRevealDelay > 0f) yield return new WaitForSeconds(rareRevealDelay);
            _rareReveal = null;
            if (AutoFishing.PendingRare != ticket) yield break; // đã bị hủy trong lúc chờ
            var fish = ticket.Result.fish;
            if (fish == null || !rareMinigame.TryStart(fish, OnRareMinigameFinished))
                AutoFishing.CancelRare();
            NotifyChanged();
        }

        private void StopRareReveal()
        {
            if (_rareReveal != null) StopCoroutine(_rareReveal);
            _rareReveal = null;
        }

        private void OnRareMinigameFinished(MinigameResult result)
        {
            if (result.status == MinigameStatus.Cancelled) AutoFishing.CancelRare();
            else AutoFishing.CompleteRare(result.Success);
            NotifyChanged();
            Save();
        }

        private int PriceOf(string fishId)
        {
            if (fishId == CatchResult.TrashId) return config.trashPrice;
            var fish = FindFish(fishId);
            return fish != null ? fish.basePrice : 0;
        }

        public FishData FindFish(string fishId)
        {
            foreach (var f in config.fish)
                if (f != null && f.fishId == fishId) return f;
            return null;
        }

        // ---------- Nâng cấp ----------

        public UpgradeDefinition GetUpgrade(UpgradeType type) => type switch
        {
            UpgradeType.Rod => config.rodUpgrade,
            UpgradeType.Bait => config.baitUpgrade,
            UpgradeType.BiteChance => config.biteUpgrade,
            UpgradeType.Boat => config.boatUpgrade,
            UpgradeType.Vault => config.vaultUpgrade,
            UpgradeType.FishValue => config.fishValueUpgrade,
            UpgradeType.Luck => config.luckUpgrade,
            _ => null,
        };

        public int GetLevel(UpgradeType type) => type switch
        {
            UpgradeType.Rod => RodLevel,
            UpgradeType.Bait => BaitLevel,
            UpgradeType.BiteChance => BiteLevel,
            UpgradeType.Boat => BoatLevel,
            UpgradeType.Vault => VaultLevel,
            UpgradeType.FishValue => ValueLevel,
            UpgradeType.Luck => LuckLevel,
            _ => 0,
        };

        /// <summary>Số lần đã mua nâng cấp (Thuyền/Két bắt đầu từ cấp khởi đầu, không phải 0).</summary>
        private int PurchasedLevels(UpgradeType type) => type switch
        {
            UpgradeType.Boat => BoatLevel - config.startingBoatLevel,
            UpgradeType.Vault => VaultLevel - config.startingVaultLevel,
            _ => GetLevel(type),
        };

        public long GetUpgradeCost(UpgradeType type)
        {
            var def = GetUpgrade(type);
            if (def == null) return long.MaxValue;
            // Cần câu có bảng cấp: giá lấy từ cấp kế tiếp.
            if (type == UpgradeType.Rod && HasRodTiers)
            {
                var next = RodTierData.ForLevel(config.rodTiers, RodLevel + 1);
                return next != null ? next.unlockCost : long.MaxValue;
            }
            return (long)Math.Ceiling(EconomyCalc.UpgradeCost(def.baseCost, PurchasedLevels(type), def.costGrowth));
        }

        public bool IsUpgradeMaxed(UpgradeType type)
        {
            if (type == UpgradeType.Rod && HasRodTiers) return RodLevel >= config.rodTiers.Count - 1;
            // Giới hạn cứng của công thức: tối đa MaxRods cần, Két tối đa MaxVaultLevel.
            if (type == UpgradeType.Boat && BoatLevel >= EconomyCalc.MaxRods) return true;
            if (type == UpgradeType.Vault && VaultLevel >= EconomyCalc.MaxVaultLevel) return true;
            // Chỉ số có trần: chạm trần thì nâng thêm không còn tác dụng → báo Tối đa.
            if (type == UpgradeType.BiteChance && Stats.IsBiteChanceCapped) return true;
            if (type == UpgradeType.Bait && Stats.IsBiteSpeedCapped) return true;
            if (type == UpgradeType.Luck && LuckLevel >= Stats.MaxLuckLevel) return true;
            var def = GetUpgrade(type);
            return def == null || def.IsMaxed(GetLevel(type));
        }

        /// <summary>Hệ số giá cá hiện tại (nâng cấp Giá trị cá).</summary>
        public double FishValueMultiplier => EconomyCalc.FishValueMultiplier(ValueLevel, config.fishValuePerLevel) * PrestigeMultiplier * WeatherValueMultiplier;

        /// <summary>Nhãn cấp hiển thị trên nút nâng cấp, vd. "Lv2 Cần tre" hoặc "Cấp 3".</summary>
        public string GetLevelLabel(UpgradeType type)
        {
            if (type == UpgradeType.Rod && HasRodTiers)
            {
                var tier = Stats.RodTier;
                int lv = Mathf.Min(RodLevel, config.rodTiers.Count - 1) + 1;
                return tier != null ? $"Lv{lv} {tier.displayName}" : $"Lv{lv}";
            }
            if (type == UpgradeType.Boat) return $"{Rods.Count} cần";
            return $"Cấp {GetLevel(type)}";
        }

        /// <summary>
        /// Hiệu quả của nâng cấp: chỉ số hiện tại → sau khi nâng (hoặc chỉ hiện tại nếu đã tối đa).
        /// Tỉ lệ cá cắn: tỉ lệ cá cắn câu (trần maxBiteChance); Mồi câu: tốc độ cá cắn (trần maxBiteSpeedMultiplier);
        /// May mắn: tỉ lệ cá hiếm (trần maxRareChance); Cần câu: kéo, cắn, hụt. Tỉ lệ cắn + Mồi câu giảm câu hụt.
        /// Thuyền: số cần câu song song; Két: giờ offline tối đa; Giá trị cá: hệ số xu mỗi con.
        /// Tỉ lệ cắn hiển thị là của người chơi (chưa nhân độ khó map) để khớp với trần.
        /// </summary>
        public string GetUpgradeEffect(UpgradeType type)
        {
            var now = Stats;
            bool maxed = IsUpgradeMaxed(type);
            var next = Stats.WithLevels(
                RodLevel + (type == UpgradeType.Rod ? 1 : 0),
                BiteLevel + (type == UpgradeType.BiteChance ? 1 : 0),
                BaitLevel + (type == UpgradeType.Bait ? 1 : 0),
                LuckLevel + (type == UpgradeType.Luck ? 1 : 0));

            string Pct(float a, float b) => maxed || Mathf.Approximately(a, b) ? $"{a * 100:0}%" : $"{a * 100:0}→{b * 100:0}%";
            string Sec(float a, float b) => maxed || Mathf.Approximately(a, b) ? $"{a:0.#}s" : $"{a:0.#}→{b:0.#}s";
            string Mul(float a, float b) => maxed || Mathf.Approximately(a, b) ? $"×{a:0.##}" : $"×{a:0.##}→{b:0.##}";

            switch (type)
            {
                case UpgradeType.BiteChance:
                    return $"Cắn {Pct(now.PlayerBiteChance, next.PlayerBiteChance)} <size=85%>(tối đa {now.BiteChanceCap * 100:0}%)</size> · Hụt {Pct(now.MissChance, next.MissChance)}";
                case UpgradeType.Bait:
                    return $"Tốc độ cắn {Mul(now.BiteSpeedMultiplier, next.BiteSpeedMultiplier)} <size=85%>(tối đa ×{now.BiteSpeedCap:0.##})</size> · Hụt {Pct(now.MissChance, next.MissChance)}";
                case UpgradeType.Luck:
                    return $"Tỉ lệ cá hiếm {Pct(now.RareChance, next.RareChance)} <size=85%>(tối đa {now.RareChanceCap * 100:0}%)</size>";
                case UpgradeType.Rod:
                    return $"Kéo {Sec(now.EffectiveDuration, next.EffectiveDuration)} · Cắn {Pct(now.BiteChance, next.BiteChance)} · Hụt {Pct(now.MissChance, next.MissChance)}";
                case UpgradeType.Boat:
                {
                    int rods = EconomyCalc.ActiveRods(BoatLevel), more = EconomyCalc.ActiveRods(BoatLevel + 1);
                    return maxed ? $"{rods} cần câu cùng lúc (tối đa)" : $"Cần câu cùng lúc {rods}→{more} · Thu nhập ×{more / (float)rods:0.##}";
                }
                case UpgradeType.Vault:
                {
                    double h = EconomyCalc.VaultCapSeconds(VaultLevel) / 3600.0, h2 = EconomyCalc.VaultCapSeconds(VaultLevel + 1) / 3600.0;
                    return maxed ? $"Tính offline tối đa {h:0}g" : $"Tính offline tối đa {h:0}g→{h2:0}g";
                }
                case UpgradeType.FishValue:
                {
                    float v = (float)FishValueMultiplier;
                    float v2 = (float)(EconomyCalc.FishValueMultiplier(ValueLevel + 1, config.fishValuePerLevel) * PrestigeMultiplier * WeatherValueMultiplier);
                    return $"Giá mỗi con cá {Mul(v, v2)}";
                }
                default:
                    return "";
            }
        }

        private bool HasRodTiers => config.rodTiers != null && config.rodTiers.Count > 0;

        // ---------- Cửa hàng: cần câu ----------

        /// <summary>Cấp cần câu hiện tại (chỉ số trong GameConfig.rodTiers). Cần cấp thấp hơn coi như đã sở hữu.</summary>
        public int RodTierIndex => HasRodTiers ? Mathf.Min(RodLevel, config.rodTiers.Count - 1) : 0;

        /// <summary>Chỉ mua được cần ngay sau cần hiện tại (mua theo thứ tự, như nâng cấp Cần câu).</summary>
        public bool CanBuyRod(int tierIndex) => HasRodTiers && tierIndex == RodTierIndex + 1 && CanUpgrade(UpgradeType.Rod);

        public bool TryBuyRod(int tierIndex) => HasRodTiers && tierIndex == RodTierIndex + 1 && TryUpgrade(UpgradeType.Rod);

        // ---------- Cửa hàng: trang phục ----------

        public SkinData FindSkin(string skinId)
        {
            if (string.IsNullOrEmpty(skinId)) return null;
            foreach (var s in config.skins)
                if (s != null && s.skinId == skinId) return s;
            return null;
        }

        public bool IsSkinOwned(SkinData skin) => skin != null && (skin.IsFree || _ownedSkins.Contains(skin.skinId));
        public bool IsSkinEquipped(SkinData skin) => skin != null && EquippedSkin == skin;
        public bool CanBuySkin(SkinData skin) => skin != null && !IsSkinOwned(skin) && Coins >= skin.price;

        /// <summary>Mua skin và mặc luôn.</summary>
        public bool TryBuySkin(SkinData skin)
        {
            NotifyPlayerActivity();
            if (!CanBuySkin(skin)) return false;
            Coins -= skin.price;
            _ownedSkins.Add(skin.skinId);
            EquippedSkinId = skin.skinId;
            NotifyChanged();
            Save();
            return true;
        }

        public bool EquipSkin(SkinData skin)
        {
            NotifyPlayerActivity();
            if (!IsSkinOwned(skin) || IsSkinEquipped(skin)) return false;
            EquippedSkinId = skin.skinId;
            NotifyChanged();
            Save();
            return true;
        }

        public bool CanUpgrade(UpgradeType type)
            => GetUpgrade(type) != null && !IsUpgradeMaxed(type) && Coins >= GetUpgradeCost(type);

        public bool TryUpgrade(UpgradeType type)
        {
            NotifyPlayerActivity();
            if (!CanUpgrade(type)) return false;
            long cost = GetUpgradeCost(type);
            bool rodLeveledUp = false;
            switch (type)
            {
                case UpgradeType.Rod: RodLevel++; rodLeveledUp = true; break; // Fishing Speed
                case UpgradeType.Bait: BaitLevel++; break;       // tỉ lệ độ hiếm
                case UpgradeType.BiteChance: BiteLevel++; break; // Bite Chance
                case UpgradeType.Boat:                           // thêm một cần câu song song
                    BoatLevel++;
                    AutoFishing.EnsureRodCount(EconomyCalc.ActiveRods(BoatLevel));
                    break;
                case UpgradeType.Vault: VaultLevel++; break;     // giới hạn giờ offline
                case UpgradeType.FishValue: ValueLevel++; break; // giá mỗi con cá
                case UpgradeType.Luck: LuckLevel++; break;       // tỉ lệ cá hiếm
                default: return false;
            }
            Coins -= cost;
            NotifyChanged();
            Save();
            if (rodLeveledUp)
            {
                var tier = Stats.RodTier;
                int lv = Mathf.Min(RodLevel, Math.Max(0, config.rodTiers.Count - 1)) + 1;
                RodLeveledUp?.Invoke(tier != null ? $"Lv{lv} {tier.displayName}" : $"Cấp {RodLevel}");
            }
            return true;
        }

        // ---------- Tái sinh (Prestige) ----------

        /// <summary>
        /// Tái sinh: đổi xu đã kiếm lượt này lấy ngọc trai (bonus giá cá vĩnh viễn), rồi làm lại từ đầu:
        /// xu, cấp nâng cấp, cần câu, thuyền, két về mức khởi đầu và quay về map đầu.
        /// Giữ lại: ngọc trai, bộ sưu tập cá, trang phục. Trả về số ngọc trai nhận được (0 nếu chưa đủ điều kiện).
        /// </summary>
        public int TryPrestige()
        {
            NotifyPlayerActivity();
            if (!CanPrestige) return 0;
            int gained = PendingPearls;

            PrestigePearls += gained;
            PrestigeCount++;
            RunEarned = 0;
            Coins = config.startingCoins + (long)PearlBonus(PearlUpgradeType.StartingCoins);
            RodLevel = BaitLevel = BiteLevel = ValueLevel = LuckLevel = 0;
            BoatLevel = config.startingBoatLevel;
            VaultLevel = config.startingVaultLevel;
            ApplyZone(0); // đặt lại số cần câu theo thuyền và báo ZoneChanged để cảnh nạp lại map đầu

            NotifyChanged();
            Save();
            Prestiged?.Invoke(gained);
            return gained;
        }

        // ---------- Bể cá trưng bày ----------

        /// <summary>Một lựa chọn có thể đặt vào bể: loài, dạng (thường/biến thể) và bonus giá cá nếu đặt.</summary>
        public struct AquariumCandidate
        {
            public FishData fish;
            public bool variant;
            public float bonus;
        }

        private readonly List<AquariumEntry> _aquarium = new List<AquariumEntry>();
        private int _aquariumExtraSlots;

        public IReadOnlyList<AquariumEntry> Aquarium => _aquarium;
        public int AquariumCapacity => Math.Max(1, config.aquariumBaseSlots) + _aquariumExtraSlots;

        /// <summary>Giá (ngọc trai) mở ô tiếp theo; -1 nếu đã mở hết.</summary>
        public int NextAquariumSlotCost
        {
            get
            {
                var costs = config.aquariumSlotPearlCosts;
                return costs != null && _aquariumExtraSlots < costs.Length ? Math.Max(0, costs[_aquariumExtraSlots]) : -1;
            }
        }

        /// <summary>Tổng bonus giá cá của mọi con đang trong bể (0,05 = +5%).</summary>
        public double AquariumBonus
        {
            get
            {
                double sum = 0;
                foreach (var e in _aquarium)
                {
                    var f = FindFish(e.fishId);
                    if (f != null) sum += AquariumEntryBonus(f, e.variant);
                }
                return sum;
            }
        }

        public float AquariumEntryBonus(FishData fish, bool variant)
        {
            var table = config.aquariumRarityBonus;
            int i = (int)fish.rarity;
            float b = table != null && i >= 0 && i < table.Length ? Math.Max(0f, table[i]) : 0f;
            return variant ? b * Math.Max(1f, config.aquariumVariantMultiplier) : b;
        }

        public bool IsInAquarium(FishData fish, bool variant)
            => fish != null && _aquarium.Exists(e => e.fishId == fish.fishId && e.variant == variant);

        /// <summary>Đã bắt được dạng đó và đủ điều kiện độ hiếm.</summary>
        public bool IsAquariumEligible(FishData fish, bool variant)
        {
            if (fish == null) return false;
            if (variant) return GetRecord(fish.fishId)?.variants > 0;
            return fish.rarity >= config.aquariumMinRarity && _caughtTotals.TryGetValue(fish.fishId, out int n) && n > 0;
        }

        /// <summary>Các con có thể đặt vào bể (chưa có trong bể), bonus cao xếp trước.</summary>
        public List<AquariumCandidate> AquariumCandidates()
        {
            var list = new List<AquariumCandidate>();
            foreach (var f in config.fish)
            {
                if (f == null) continue;
                foreach (bool variant in new[] { false, true })
                    if (IsAquariumEligible(f, variant) && !IsInAquarium(f, variant))
                        list.Add(new AquariumCandidate { fish = f, variant = variant, bonus = AquariumEntryBonus(f, variant) });
            }
            list.Sort((a, b) => b.bonus != a.bonus ? b.bonus.CompareTo(a.bonus) : string.CompareOrdinal(a.fish.fishId, b.fish.fishId));
            return list;
        }

        public bool TryAddToAquarium(FishData fish, bool variant)
        {
            NotifyPlayerActivity();
            if (_aquarium.Count >= AquariumCapacity || !IsAquariumEligible(fish, variant) || IsInAquarium(fish, variant)) return false;
            _aquarium.Add(new AquariumEntry { fishId = fish.fishId, variant = variant });
            NotifyChanged();
            Save();
            return true;
        }

        public bool TryRemoveFromAquarium(int index)
        {
            NotifyPlayerActivity();
            if (index < 0 || index >= _aquarium.Count) return false;
            _aquarium.RemoveAt(index);
            NotifyChanged();
            Save();
            return true;
        }

        public bool CanBuyAquariumSlot => NextAquariumSlotCost >= 0 && PrestigePearls >= NextAquariumSlotCost;

        public bool TryBuyAquariumSlot()
        {
            NotifyPlayerActivity();
            if (!CanBuyAquariumSlot) return false;
            PrestigePearls -= NextAquariumSlotCost;
            _aquariumExtraSlots++;
            NotifyChanged();
            Save();
            return true;
        }

        // ---------- Mồi tiêu hao ----------

        private readonly Dictionary<string, int> _baitStock = new Dictionary<string, int>();
        private string _activeBaitId;
        private float _activeBaitRemaining;

        /// <summary>Mồi tiêu hao hết hiệu lực (tên mồi).</summary>
        public event Action<string> BaitExpired;

        public IReadOnlyList<BaitData> Baits => config.baits;

        /// <summary>Mồi đang dùng (null nếu không). Có thể thuộc map khác map đang câu: khi đó tạm dừng, không có tác dụng.</summary>
        public BaitData ActiveBait => FindBait(_activeBaitId);
        public float ActiveBaitRemaining => _activeBaitRemaining;
        /// <summary>Mồi đang dùng và đúng map đang câu: hiệu quả và đồng hồ đều chạy.</summary>
        public bool IsBaitInEffect => ActiveBait != null && ActiveBait.zoneIndex == CurrentZoneIndex;

        /// <summary>Tổng cộng thêm vào tỉ lệ cá hiếm: nâng cấp ngọc trai + mồi đang có tác dụng.</summary>
        public double RareChanceBonus => PearlBonus(PearlUpgradeType.RareLuck) + (IsBaitInEffect ? ActiveBait.rareBonus : 0.0)
                                         + (HasWeather(WeatherFlags.Night) ? Math.Max(0f, config.nightRareBonus) : 0.0);
        /// <summary>Cộng thêm vào xác suất cá biến thể từ mồi đang có tác dụng.</summary>
        public double VariantChanceBonus => IsBaitInEffect ? ActiveBait.variantBonus : 0.0;

        private FishSelectionService NewSelection(int zone)
            => new FishSelectionService(config, zone, null, () => FishValueMultiplier, () => RareChanceBonus, () => VariantChanceBonus,
                () => ActiveWeather);

        // ---------- Thời gian & thời tiết ----------

        private WeatherFlags _weather;
        private WeatherFlags? _weatherOverride;
        private bool _ignoreWeather;

        /// <summary>Giờ/thời tiết đang diễn ra (giờ máy). Khi tính thu nhập offline thì bỏ qua, tránh lợi dụng giờ quay lại.</summary>
        public WeatherFlags ActiveWeather => _ignoreWeather ? WeatherFlags.None : (_weatherOverride ?? _weather);

        /// <summary>Thời tiết vừa đổi (trạng thái mới).</summary>
        public event Action<WeatherFlags> WeatherChanged;

        private bool HasWeather(WeatherFlags f) => (ActiveWeather & f) != 0;

        /// <summary>Hệ số giá cá của thời điểm hiện tại (hoàng hôn cộng thêm).</summary>
        public double WeatherValueMultiplier => HasWeather(WeatherFlags.Sunset) ? 1.0 + Math.Max(0f, config.sunsetValueBonus) : 1.0;

        /// <summary>Mô tả tác dụng của thời tiết hiện tại, vd. "Giá cá +25%", rỗng nếu ban ngày không mưa.</summary>
        public string WeatherEffectText()
        {
            var parts = new List<string>();
            if (HasWeather(WeatherFlags.Sunset)) parts.Add($"Giá cá +{config.sunsetValueBonus * 100:0.#}%");
            if (HasWeather(WeatherFlags.Night)) parts.Add($"Cá hiếm +{config.nightRareBonus * 100:0.#}%");
            if (HasWeather(WeatherFlags.Rain)) parts.Add($"Cá cắn nhanh +{config.rainBiteSpeedBonus * 100:0.#}%");
            if (HasWeather(WeatherFlags.Night | WeatherFlags.Rain)) parts.Add("có cá sự kiện");
            return string.Join(" · ", parts);
        }

        /// <summary>Tính giờ/thời tiết từ đồng hồ máy: hoàng hôn, đêm theo giờ; mưa theo các đợt cố định trong ngày.</summary>
        public WeatherFlags ComputeWeather(DateTime now)
        {
            var w = WeatherFlags.None;
            int h = now.Hour;
            bool wraps = config.nightStartHour > config.nightEndHour; // đêm qua nửa đêm (19 → 5)
            bool night = wraps ? (h >= config.nightStartHour || h < config.nightEndHour)
                               : (h >= config.nightStartHour && h < config.nightEndHour);
            if (night) w |= WeatherFlags.Night;
            else if (h >= config.sunsetStartHour && h < config.nightStartHour) w |= WeatherFlags.Sunset;

            // Các đợt mưa cố định theo ngày (cùng ngày thì cùng lịch mưa, kể cả khi thoát vào lại).
            if (config.rainWindowsPerDay > 0)
            {
                var rng = new System.Random(now.Year * 10000 + now.Month * 100 + now.Day + 7919);
                int minute = now.Hour * 60 + now.Minute;
                for (int i = 0; i < config.rainWindowsPerDay; i++)
                {
                    int length = (int)Mathf.Lerp(config.rainMinutes.x, Mathf.Max(config.rainMinutes.x, config.rainMinutes.y), (float)rng.NextDouble());
                    int start = rng.Next(0, Math.Max(1, 1440 - length));
                    if (minute >= start && minute < start + length) w |= WeatherFlags.Rain;
                }
            }
            return w;
        }

        private void UpdateWeather()
        {
            var before = ActiveWeather;
            _weather = ComputeWeather(DateTime.Now);
            if (ActiveWeather == before) return;
            _changed = true;
            WeatherChanged?.Invoke(ActiveWeather);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Test: ép thời tiết (null = theo đồng hồ máy).</summary>
        public void DebugSetWeather(WeatherFlags? weather)
        {
            var before = ActiveWeather;
            _weatherOverride = weather;
            if (ActiveWeather == before) return;
            _changed = true;
            WeatherChanged?.Invoke(ActiveWeather);
        }
#endif

        public BaitData FindBait(string baitId)
        {
            if (string.IsNullOrEmpty(baitId)) return null;
            foreach (var b in config.baits)
                if (b != null && b.baitId == baitId) return b;
            return null;
        }

        public int GetBaitCount(BaitData bait)
            => bait != null && bait.baitId != null && _baitStock.TryGetValue(bait.baitId, out int n) ? n : 0;

        /// <summary>Chỉ mua được mồi của map đã mở khóa.</summary>
        public bool CanBuyBait(BaitData bait) => bait != null && IsZoneUnlocked(bait.zoneIndex) && Coins >= bait.price;

        public bool TryBuyBait(BaitData bait)
        {
            NotifyPlayerActivity();
            if (!CanBuyBait(bait)) return false;
            Coins -= bait.price;
            _baitStock[bait.baitId] = GetBaitCount(bait) + 1;
            NotifyChanged();
            Save();
            return true;
        }

        public bool CanUseBait(BaitData bait) => bait != null && GetBaitCount(bait) > 0 && ActiveBait == null;

        public bool TryUseBait(BaitData bait)
        {
            NotifyPlayerActivity();
            if (!CanUseBait(bait)) return false;
            _baitStock[bait.baitId] = GetBaitCount(bait) - 1;
            _activeBaitId = bait.baitId;
            _activeBaitRemaining = bait.durationSeconds;
            NotifyChanged();
            Save();
            return true;
        }

        /// <summary>Đếm ngược mồi đang dùng khi đang câu thật ở đúng map; hết giờ thì mồi biến mất.</summary>
        private void TickBait(float dt)
        {
            if (_activeBaitId == null || !IsFishingActive || _appPaused || !IsBaitInEffect) return;
            _activeBaitRemaining -= dt;
            if (_activeBaitRemaining > 0f) return;
            var bait = ActiveBait;
            _activeBaitId = null;
            _activeBaitRemaining = 0f;
            _changed = true;
            if (bait != null) BaitExpired?.Invoke(bait.displayName);
            Save();
        }

        // ---------- Nhiệm vụ ----------

        /// <summary>Giây còn lại tới lúc làm mới nhiệm vụ ngày (nửa đêm giờ máy).</summary>
        public double SecondsToQuestReset => Math.Max(0, (DateTime.Today.AddDays(1) - DateTime.Now).TotalSeconds);

        public bool HasClaimableQuest => Quests != null && Quests.HasClaimable();

        /// <summary>Nhận thưởng nhiệm vụ đã đủ chỉ tiêu. Nhiệm vụ tuần hoàn sang vòng sau ngay.</summary>
        public bool ClaimQuest(QuestProgress progress)
        {
            NotifyPlayerActivity();
            var result = Quests.Claim(progress);
            if (!result.HasValue) return false;
            var (quest, cycle) = result.Value;
            float amount = quest.RewardFor(cycle);
            if (quest.rewardKind == QuestRewardKind.Pearls) PrestigePearls += Math.Max(1, Mathf.RoundToInt(amount));
            else GrantBonusCoins(amount, 50);
            NotifyChanged();
            Save();
            return true;
        }

        /// <summary>Chữ thưởng của nhiệm vụ ở vòng cycle, vd. "+2 ngọc trai" hoặc "~1,2K xu".</summary>
        public string QuestRewardText(QuestData quest, int cycle)
        {
            float amount = quest.RewardFor(cycle);
            if (quest.rewardKind == QuestRewardKind.Pearls) return $"+{Math.Max(1, Mathf.RoundToInt(amount))} ngọc trai";
            return $"~{Utils.Fmt.Number(Math.Max(50, (long)(IncomePerSecond * amount)))} xu";
        }

        // ---------- Cửa hàng ngọc trai ----------

        public IReadOnlyList<PearlUpgradeData> PearlUpgrades => config.pearlUpgrades;

        /// <summary>Hiệu suất thu nhập offline: 50% cơ bản + nâng cấp ngọc trai (tối đa 100%).</summary>
        public double OfflineEfficiency => Math.Min(1.0, EconomyCalc.OfflineEfficiency + PearlBonus(PearlUpgradeType.OfflineEfficiency));

        public int GetPearlLevel(PearlUpgradeData def)
            => def != null && !string.IsNullOrEmpty(def.upgradeId) && _pearlLevels.TryGetValue(def.upgradeId, out int l) ? l : 0;

        public bool IsPearlMaxed(PearlUpgradeData def) => def == null || def.IsMaxed(GetPearlLevel(def));

        public int GetPearlCost(PearlUpgradeData def) => def != null ? def.CostAt(GetPearlLevel(def)) : int.MaxValue;

        public bool CanBuyPearlUpgrade(PearlUpgradeData def)
            => def != null && !IsPearlMaxed(def) && PrestigePearls >= GetPearlCost(def);

        public bool TryBuyPearlUpgrade(PearlUpgradeData def)
        {
            NotifyPlayerActivity();
            if (!CanBuyPearlUpgrade(def)) return false;
            PrestigePearls -= GetPearlCost(def);
            _pearlLevels[def.upgradeId] = GetPearlLevel(def) + 1;
            NotifyChanged();
            Save();
            return true;
        }

        /// <summary>Tổng hiệu quả (valuePerLevel × cấp) của mọi nâng cấp ngọc trai cùng loại.</summary>
        public double PearlBonus(PearlUpgradeType type)
        {
            double sum = 0;
            if (config == null || config.pearlUpgrades == null) return 0;
            foreach (var def in config.pearlUpgrades)
                if (def != null && def.type == type) sum += def.valuePerLevel * Math.Min(GetPearlLevel(def), def.MaxLevel);
            return sum;
        }

        // ---------- Lưu / tải ----------

        public void Save()
        {
            _saveTimer = 0f;
            var data = new SaveData
            {
                coins = Coins,
                gems = Gems,
                rodLevel = RodLevel,
                baitLevel = BaitLevel,
                biteLevel = BiteLevel,
                boatLevel = BoatLevel,
                vaultLevel = VaultLevel,
                valueLevel = ValueLevel,
                luckLevel = LuckLevel,
                prestigePearls = PrestigePearls,
                prestigeCount = PrestigeCount,
                runEarned = RunEarned,
                equippedSkin = EquippedSkinId,
                currentZone = CurrentZoneIndex,
            };
            data.ownedSkins.AddRange(_ownedSkins);
            foreach (var kv in _pearlLevels) data.pearlLevels.Add(new PearlLevel { id = kv.Key, level = kv.Value });
            data.records.AddRange(_records.Values);
            data.aquarium.AddRange(_aquarium);
            data.aquariumExtraSlots = _aquariumExtraSlots;
            foreach (var kv in _baitStock) if (kv.Value > 0) data.baitStock.Add(new FishStack { fishId = kv.Key, count = kv.Value });
            data.activeBaitId = _activeBaitId;
            data.activeBaitRemaining = _activeBaitRemaining;
            Quests.Save(data);
            data.claimedRewards.AddRange(_claimed);
            foreach (var kv in _caughtTotals) data.caughtTotals.Add(new FishStack { fishId = kv.Key, count = kv.Value });
            SaveSystem.Save(data);
        }

        private void LoadOrNew()
        {
            var data = SaveSystem.Load();
            if (data == null)
            {
                Coins = config.startingCoins;
                BoatLevel = config.startingBoatLevel;
                VaultLevel = config.startingVaultLevel;
                AutoFishing.SetRodCount(EconomyCalc.ActiveRods(BoatLevel));
                Quests.Refresh(DateTime.Now);
                return;
            }

            Coins = data.coins;
            Gems = data.gems;
            RodLevel = data.rodLevel;
            BaitLevel = data.baitLevel;
            BiteLevel = data.biteLevel;
            BoatLevel = data.boatLevel;
            VaultLevel = data.vaultLevel;
            ValueLevel = data.valueLevel;
            LuckLevel = data.luckLevel;
            PrestigePearls = data.prestigePearls;
            PrestigeCount = data.prestigeCount;
            RunEarned = data.runEarned;
            _pearlLevels.Clear();
            if (data.pearlLevels != null) foreach (var p in data.pearlLevels) _pearlLevels[p.id] = p.level;
            _records.Clear();
            if (data.records != null) foreach (var rec in data.records) if (!string.IsNullOrEmpty(rec.fishId)) _records[rec.fishId] = rec;
            _claimed.Clear();
            if (data.claimedRewards != null) _claimed.UnionWith(data.claimedRewards);
            _aquarium.Clear();
            if (data.aquarium != null) foreach (var e in data.aquarium) if (FindFish(e.fishId) != null && _aquarium.Count < 64) _aquarium.Add(e);
            _aquariumExtraSlots = Math.Max(0, data.aquariumExtraSlots);
            // Bể không chứa quá số ô (đổi dữ liệu): bỏ phần thừa.
            while (_aquarium.Count > AquariumCapacity) _aquarium.RemoveAt(_aquarium.Count - 1);
            _baitStock.Clear();
            if (data.baitStock != null) foreach (var s in data.baitStock) if (s.count > 0) _baitStock[s.fishId] = s.count;
            _activeBaitId = FindBait(data.activeBaitId) != null && data.activeBaitRemaining > 0f ? data.activeBaitId : null;
            _activeBaitRemaining = _activeBaitId != null ? data.activeBaitRemaining : 0f;
            Quests.Load(data);
            // Save cũ còn cá trong giỏ (trước khi bỏ tính năng bán cá): quy ra xu một lần để không mất.
            if (data.basket != null)
                foreach (var s in data.basket) Coins += (long)PriceOf(s.fishId) * Math.Max(0, s.count);

            _caughtTotals.Clear();
            foreach (var s in data.caughtTotals) _caughtTotals[s.fishId] = s.count;
            _ownedSkins.Clear();
            if (data.ownedSkins != null) _ownedSkins.UnionWith(data.ownedSkins);
            EquippedSkinId = data.equippedSkin;

            // ApplyZone cũng đặt lại cần câu; thu nhập offline tính theo map đang câu.
            ApplyZone(ValidZone(data.currentZone));
            Quests.Refresh(DateTime.Now); // trước ApplyOffline để xu offline vẫn tính vào nhiệm vụ hôm nay
            ApplyOffline(data.lastSaveUtc, invokeNow: false);
        }

        /// <summary>Tính thu nhập offline bằng giá trị kỳ vọng của cá thường (không roll cá thật, không cá hiếm).</summary>
        private void ApplyOffline(long lastSaveTicks, bool invokeNow)
        {
            if (lastSaveTicks <= 0) return;
            var report = ComputeOffline(new DateTime(lastSaveTicks, DateTimeKind.Utc), DateTime.UtcNow);
            if (report.coins <= 0 && report.fish <= 0) return;

            AddCoins(report.coins);
            Save();
            if (invokeNow)
            {
                NotifyChanged();
                OfflineEarned?.Invoke(report);
            }
            else
            {
                _pendingReport = report;
            }
        }

        public OfflineReport ComputeOffline(DateTime lastSaveUtc, DateTime nowUtc)
        {
            double away = EconomyCalc.AwaySeconds(lastSaveUtc, nowUtc);
            // Offline không hưởng thời tiết/giờ lúc quay lại (tránh lợi dụng giờ mở game).
            bool prevIgnore = _ignoreWeather;
            _ignoreWeather = true;
            try
            {
                return new OfflineReport
                {
                    awaySeconds = away,
                    countedSeconds = EconomyCalc.OfflineCountedSeconds(away, VaultLevel),
                    coins = EconomyCalc.OfflineCoins(away, VaultLevel, IncomePerSecond, OfflineEfficiency),
                    fish = EconomyCalc.OfflineFish(away, VaultLevel, CatchesPerSecond, OfflineEfficiency),
                };
            }
            finally
            {
                _ignoreWeather = prevIgnore;
            }
        }

        /// <summary>Xóa save và bắt đầu lại (dùng khi test).</summary>
        [ContextMenu("Reset Save")]
        public void ResetProgress()
        {
            if (rareMinigame != null) rareMinigame.Stop();
            SaveSystem.Delete();
            _caughtTotals.Clear();
            _ownedSkins.Clear();
            EquippedSkinId = null;
            Gems = 0;
            PrestigePearls = PrestigeCount = 0;
            RunEarned = 0;
            _pearlLevels.Clear();
            _records.Clear();
            _claimed.Clear();
            Quests.Reset();
            Quests.Refresh(DateTime.Now);
            _baitStock.Clear();
            _activeBaitId = null;
            _activeBaitRemaining = 0f;
            _aquarium.Clear();
            _aquariumExtraSlots = 0;
            RodLevel = BaitLevel = BiteLevel = ValueLevel = LuckLevel = 0;
            Coins = config.startingCoins;
            BoatLevel = config.startingBoatLevel;
            VaultLevel = config.startingVaultLevel;
            ApplyZone(0);
            NotifyChanged();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Test: bắt mỗi loài còn thiếu của map hiện tại một con (qua đường trao thưởng thật, nên mở khóa map như khi chơi).</summary>
        [ContextMenu("Debug: Complete Current Zone Collection")]
        public void DebugCompleteZoneCollection()
        {
            long id = long.MaxValue / 2;
            foreach (var f in config.fish)
            {
                if (f == null || f.zoneIndex != CurrentZoneIndex) continue;
                if (_caughtTotals.TryGetValue(f.fishId, out int c) && c > 0) continue;
                _rewards.Grant(new CatchTicket(id++, new CatchResult
                {
                    fish = f, fishId = f.fishId, price = f.basePrice, size = f.sizeRange.x,
                }));
            }
            NotifyChanged();
        }
#endif

        // ---------- Nội bộ ----------

        private void NotifyChanged() => StateChanged?.Invoke();
    }
}
