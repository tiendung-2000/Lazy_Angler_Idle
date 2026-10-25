using System.Collections.Generic;
using LazyAngler.Core;
using LazyAngler.Data;
using LazyAngler.Systems;
using NUnit.Framework;
using UnityEngine;

namespace LazyAngler.Tests
{
    /// <summary>Kiểm thử hệ thống câu tự động + cá hiếm, theo mục 9 của brief.</summary>
    public class FishingSystemTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // ---------- Dựng dữ liệu ----------

        private FishData Fish(string id, Rarity rarity, MinigameType type = MinigameType.None)
        {
            var f = ScriptableObject.CreateInstance<FishData>();
            f.fishId = id;
            f.displayName = id;
            f.rarity = rarity;
            f.basePrice = 10 * ((int)rarity + 1);
            f.minigame = new RareMinigameSettings { type = type };
            _created.Add(f);
            return f;
        }

        private GameConfig Config(float bite, float duration, params FishData[] fish)
        {
            var c = ScriptableObject.CreateInstance<GameConfig>();
            c.fish = new List<FishData>(fish);
            c.trashChance = 0f;
            c.baseBiteChance = bite;
            c.biteChancePerLevel = 0.1f;
            c.maxBiteChance = 1f;
            c.biteCheckInterval = 1f;
            c.baseFishingDuration = duration;
            c.castReductionPerLevel = 0.5f;
            c.minCastSeconds = 0.1f;
            // Tắt nhịp quăng/đợi/câu hụt để các test cũ giữ nguyên ý nghĩa (test riêng ở phần dưới).
            c.castSeconds = 0f;
            c.baseBiteWaitSeconds = 0f;
            c.biteWaitRandomness = 0f;
            c.biteReactSeconds = 0f;
            c.baseMissChance = 0f;
            c.missRecoverSeconds = 0f;
            _created.Add(c);
            return c;
        }

        private class Harness
        {
            public int RodLevel, BiteLevel, BaitLevel;
            public bool AllowRare = true;
            public long Coins;
            public readonly Dictionary<string, int> Collection = new Dictionary<string, int>();
            public FishingStatProvider Stats;
            public FishSelectionService Selection;
            public FishingRewardService Rewards;
            public AutoFishingController Auto;
            public int RewardEvents;

            public Harness(GameConfig cfg, int seed = 1)
            {
                Stats = new FishingStatProvider(cfg, () => RodLevel, () => BiteLevel, () => BaitLevel);
                Selection = new FishSelectionService(cfg, 0, seed);
                Rewards = new FishingRewardService(Collection, c => Coins += c);
                Rewards.Rewarded += _ => RewardEvents++;
                Auto = new AutoFishingController(Stats, Selection, Rewards, () => 0, () => AllowRare, null, seed + 1);
                Auto.SetRodCount(1);
            }

            public void Run(float seconds, float dt = 0.1f)
            {
                for (float t = 0; t < seconds - 1e-4f; t += dt) Auto.Tick(dt);
            }
        }

        // ---------- 1. Câu tự động liên tục ----------

        [Test]
        public void NormalFishing_RunsAutomatically_AndContinuously()
        {
            var h = new Harness(Config(1f, 2f, Fish("a", Rarity.Common)));
            // Bite 100%, kiểm tra mỗi 1s, kéo 2s → mỗi con 3s.
            h.Run(30f);
            Assert.AreEqual(10, h.Collection["a"], 1);
            Assert.AreEqual(h.Collection["a"], h.RewardEvents);
            Assert.AreEqual(h.Collection["a"] * 10, h.Coins, "Mỗi con cộng xu ngay bằng giá cá");
            Assert.IsFalse(h.Auto.IsPaused);
        }

        [Test]
        public void Catch_AddsCoinsImmediately_AndNeverStops()
        {
            var h = new Harness(Config(1f, 1f, Fish("a", Rarity.Common)));
            // Bite 100%, kiểm tra mỗi 1s, kéo 1s → mỗi con 2s; không có giỏ nên không bao giờ phải dừng.
            h.Run(2.05f);
            Assert.AreEqual(1, h.RewardEvents);
            Assert.AreEqual(10, h.Coins, "Câu lên là cộng xu ngay");
            h.Run(100f);
            Assert.AreEqual(51, h.RewardEvents, 1);
            Assert.AreEqual(h.RewardEvents * 10, h.Coins);
        }

        [Test]
        public void FishValueMultiplier_RaisesAveragePrice_EvenForCheapFish()
        {
            var cfg = Config(1f, 1f, Fish("a", Rarity.Common)); // giá 10
            var sel = new FishSelectionService(cfg, 0, 3, () => 1.25);
            long sum = 0;
            for (int i = 0; i < 4000; i++) sum += sel.Select(0, false).price;
            Assert.AreEqual(12.5, sum / 4000.0, 0.1, "Giá trung bình = giá gốc × hệ số (phần lẻ làm tròn theo xác suất)");
            Assert.AreEqual(12.5, sel.ExpectedValuePerCatch(0, false), 1e-9);
        }

        [Test]
        public void RareChance_ControlsShareOfRareFish_AndIsCapped()
        {
            var cfg = Config(1f, 1f, Fish("a", Rarity.Common), Fish("u", Rarity.Uncommon),
                Fish("r", Rarity.Rare, MinigameType.Radial), Fish("e", Rarity.Epic, MinigameType.Radial),
                Fish("l", Rarity.Legendary, MinigameType.Radial));
            cfg.baseRareChance = 0.1f;
            cfg.rareChancePerLevel = 0.05f;
            cfg.maxRareChance = 0.3f;

            var p = LootTable.RarityProbabilitiesForRareShare(new double[] { 60, 25, 10, 4, 1 }, 0.3);
            Assert.AreEqual(1.0, p[0] + p[1] + p[2] + p[3] + p[4], 1e-9);
            Assert.AreEqual(0.3, p[2] + p[3] + p[4], 1e-9, "Tổng cá hiếm đúng bằng tỉ lệ cá hiếm");
            Assert.AreEqual(10.0 / 4.0, p[2] / p[3], 1e-9, "Trong nhóm giữ tỉ lệ trọng số");

            var sel = new FishSelectionService(cfg, 0, 9);
            int rare0 = 0, rareMax = 0;
            for (int i = 0; i < 20000; i++)
            {
                if (sel.Select(0, true).IsRare) rare0++;
                if (sel.Select(100, true).IsRare) rareMax++;
            }
            Assert.AreEqual(0.1, rare0 / 20000.0, 0.01, "Cấp 0 = tỉ lệ cơ sở");
            Assert.AreEqual(0.3, rareMax / 20000.0, 0.015, "May mắn cao vẫn không vượt trần");

            var h = new Harness(cfg);
            Assert.AreEqual(4, h.Stats.MaxLuckLevel, "0,1 + 4 × 0,05 = 0,3 (trần)");
        }

        [Test]
        public void BiteChance_PlayerCap_AppliesBeforeZoneDifficulty()
        {
            var cfg = Config(0.1f, 1f, Fish("a", Rarity.Common));
            cfg.biteChancePerLevel = 0.02f;
            cfg.maxBiteChance = 0.5f;
            var zone = ScriptableObject.CreateInstance<ZoneData>();
            zone.biteChanceMultiplier = 0.5f;
            _created.Add(zone);
            var h = new Harness(cfg) { BiteLevel = 100 };
            Assert.AreEqual(0.5f, h.Stats.PlayerBiteChance, 1e-5f);
            Assert.IsTrue(h.Stats.IsBiteChanceCapped);

            var inZone = new FishingStatProvider(cfg, () => 0, () => 100, () => 0, () => zone);
            Assert.AreEqual(0.5f, inZone.PlayerBiteChance, 1e-5f);
            Assert.AreEqual(0.25f, inZone.BiteChance, 1e-5f, "Map khó nhân sau trần: 50% × 0,5");
        }

        [Test]
        public void EnsureRodCount_AddsRods_WithoutResettingExisting()
        {
            var h = new Harness(Config(1f, 5f, Fish("a", Rarity.Common)));
            h.Run(1.55f); // cần 1 đang kéo
            var first = h.Auto.Rods[0];
            var state = first.State;
            float timer = first.Timer;
            h.Auto.EnsureRodCount(3);
            Assert.AreEqual(3, h.Auto.Rods.Count);
            Assert.AreSame(first, h.Auto.Rods[0]);
            Assert.AreEqual(state, first.State, "Cần cũ không bị đặt lại");
            Assert.AreEqual(timer, first.Timer, 1e-6f);
            h.Run(60f);
            Assert.Greater(h.RewardEvents, 25, "3 cần câu cùng lúc (kéo 5s, mỗi cần ~6s/con)");
        }

        // ---------- 2, 3. Hai chỉ số độc lập ----------

        [Test]
        public void BiteChance_And_FishingSpeed_AreIndependent()
        {
            var h = new Harness(Config(0.5f, 4f, Fish("a", Rarity.Common)));
            float bite0 = h.Stats.BiteChance, dur0 = h.Stats.EffectiveDuration;

            h.BiteLevel = 3;
            Assert.AreEqual(0.8f, h.Stats.BiteChance, 1e-5f);
            Assert.AreEqual(dur0, h.Stats.EffectiveDuration, 1e-5f, "Bite Chance không đổi thời gian câu");

            h.BiteLevel = 0;
            h.RodLevel = 1;
            Assert.AreEqual(bite0, h.Stats.BiteChance, 1e-5f, "Fishing Speed không đổi Bite Chance");
            Assert.Less(h.Stats.EffectiveDuration, dur0);
        }

        [Test]
        public void FishingSpeed_FormulaIsBaseDividedByMultiplier()
        {
            Assert.AreEqual(2.5, EconomyCalc.EffectiveFishingDuration(5, 2.0), 1e-9);
            Assert.AreEqual(5.0, EconomyCalc.EffectiveFishingDuration(5, 0.0), 1e-9, "Hệ số ≤ 0 coi như 1");

            var h = new Harness(Config(1f, 4f, Fish("a", Rarity.Common)));
            h.RodLevel = 1; // castReductionPerLevel 0,5 → hệ số 2
            Assert.AreEqual(2f, h.Stats.FishingSpeedMultiplier, 1e-4f);
            Assert.AreEqual(2f, h.Stats.EffectiveDuration, 1e-4f);
        }

        [Test]
        public void HigherBiteChance_CatchesMoreFish_InSameTime()
        {
            var low = new Harness(Config(0.2f, 1f, Fish("a", Rarity.Common)), seed: 7);
            var high = new Harness(Config(0.9f, 1f, Fish("a", Rarity.Common)), seed: 7);
            low.Run(300f);
            high.Run(300f);
            Assert.Greater(high.RewardEvents, low.RewardEvents * 2);
        }

        [Test]
        public void BiteChanceZero_NeverCatches()
        {
            var h = new Harness(Config(0f, 1f, Fish("a", Rarity.Common)));
            h.Run(60f);
            Assert.AreEqual(0, h.RewardEvents);
            Assert.IsTrue(double.IsPositiveInfinity(h.Stats.ExpectedSecondsPerCatch));
        }

        // ---------- Nhịp câu, tốc độ cá cắn, câu hụt ----------

        [Test]
        public void Flow_Cast_Wait_Bite_Reel_InOrder()
        {
            var cfg = Config(1f, 1f, Fish("a", Rarity.Common));
            cfg.castSeconds = 1f;
            cfg.baseBiteWaitSeconds = 2f;
            cfg.biteReactSeconds = 0.5f;
            var h = new Harness(cfg);
            var seen = new List<RodState>();
            for (int i = 0; i < 70; i++)
            {
                var s = h.Auto.Rods[0].State;
                if (seen.Count == 0 || seen[seen.Count - 1] != s) seen.Add(s);
                h.Auto.Tick(0.1f);
            }
            // Quăng 1s → đợi tối thiểu 2s + 1 lần kiểm tra (1s) → cá cắn 0,5s → kéo 1s → câu lại.
            CollectionAssert.AreEqual(new[] { RodState.Casting, RodState.Waiting, RodState.Biting, RodState.Reeling,
                RodState.Casting, RodState.Waiting }, seen);
            Assert.AreEqual(1, h.RewardEvents, "Chỉ một con sau khi kéo xong (5,5s)");
        }

        [Test]
        public void BiteSpeed_FromRodTierAndBait_ShortensWait()
        {
            var cfg = Config(1f, 1f, Fish("a", Rarity.Common));
            cfg.baseBiteWaitSeconds = 4f;
            cfg.baitBiteSpeedPerLevel = 0.05f;
            var tier = ScriptableObject.CreateInstance<RodTierData>();
            tier.biteChance = 0.5f;
            tier.biteSpeedMultiplier = 1.5f;
            _created.Add(tier);
            cfg.rodTiers = new List<RodTierData> { tier };
            var h = new Harness(cfg);

            Assert.AreEqual(1.5f, h.Stats.BiteSpeedMultiplier, 1e-5f);
            Assert.AreEqual(4f / 1.5f, h.Stats.MinBiteWaitSeconds, 1e-4f);
            h.BaitLevel = 10;
            Assert.AreEqual(1.5f * 1.5f, h.Stats.BiteSpeedMultiplier, 1e-5f, "Mồi câu cấp 10: +50%");
            Assert.AreEqual(4f / 2.25f, h.Stats.MinBiteWaitSeconds, 1e-4f);
            Assert.AreEqual(4f / 2.25f + 1f / 0.5f, h.Stats.ExpectedWaitSeconds, 1e-4f, "Đợi kỳ vọng = tối thiểu + interval / Bite Chance");
        }

        [Test]
        public void MissChance_FromRodTier_ReducedByUpgrades_WithFloor()
        {
            var cfg = Config(1f, 1f, Fish("a", Rarity.Common));
            cfg.missReductionPerUpgradeLevel = 0.01f;
            cfg.minMissChance = 0.02f;
            var tier = ScriptableObject.CreateInstance<RodTierData>();
            tier.missChance = 0.3f;
            _created.Add(tier);
            cfg.rodTiers = new List<RodTierData> { tier };
            var h = new Harness(cfg);

            Assert.AreEqual(0.30f, h.Stats.MissChance, 1e-5f);
            h.BaitLevel = 5;
            h.BiteLevel = 5;
            Assert.AreEqual(0.20f, h.Stats.MissChance, 1e-5f, "Mỗi cấp Mồi câu / Tỉ lệ cá cắn giảm 1%");
            h.BaitLevel = 100;
            Assert.AreEqual(0.02f, h.Stats.MissChance, 1e-5f, "Không thấp hơn sàn");
        }

        [Test]
        public void Missed_GivesNothing_RaisesEvent_ThenCastsAgain()
        {
            var cfg = Config(1f, 1f, Fish("a", Rarity.Common));
            cfg.baseMissChance = 1f;
            cfg.minMissChance = 1f;
            cfg.missRecoverSeconds = 0.5f;
            var h = new Harness(cfg);
            int missed = 0;
            h.Auto.FishMissed += _ => missed++;
            bool sawMissed = false, castAgain = false;
            for (int i = 0; i < 300; i++)
            {
                h.Auto.Tick(0.1f);
                var s = h.Auto.Rods[0].State;
                if (s == RodState.Missed) sawMissed = true;
                else if (sawMissed && s == RodState.Casting || sawMissed && s == RodState.Waiting) castAgain = true;
            }
            Assert.IsTrue(sawMissed);
            Assert.IsTrue(castAgain, "Hụt xong thì câu lại");
            Assert.Greater(missed, 5);
            Assert.AreEqual(0, h.RewardEvents, "Câu hụt không có cá");
            Assert.AreEqual(0, h.RewardEvents);
            Assert.IsTrue(double.IsPositiveInfinity(h.Stats.ExpectedSecondsPerCatch));
        }

        [Test]
        public void MissRate_MatchesMissChance()
        {
            var cfg = Config(1f, 0.1f, Fish("a", Rarity.Common));
            cfg.baseMissChance = 0.3f;
            var h = new Harness(cfg, seed: 11);
            int missed = 0;
            h.Auto.FishMissed += _ => missed++;
            h.Run(3000f, 0.05f);
            int total = missed + h.Collection["a"];
            Assert.Greater(total, 500);
            Assert.AreEqual(0.3, missed / (double)total, 0.05);
        }

        [Test]
        public void RareFish_NotAffectedByMiss()
        {
            var cfg = Config(1f, 1f, Fish("r", Rarity.Rare, MinigameType.Radial));
            cfg.baseMissChance = 1f;
            cfg.minMissChance = 1f;
            var h = new Harness(cfg);
            int missed = 0;
            h.Auto.FishMissed += _ => missed++;
            h.Run(1.05f);
            Assert.IsTrue(h.Auto.IsPaused, "Cá hiếm vào minigame, không bị câu hụt");
            Assert.AreEqual(0, missed);
        }

        [Test]
        public void SecondsPerCatch_IncludesMissedAttempts()
        {
            // lượt = 1 + 2 + 0,5 + 0,5×1 + 0,5×1 = 4,5s; 50% hụt → 9s/con.
            Assert.AreEqual(9.0, EconomyCalc.SecondsPerCatch(1, 2, 0.5, 1, 0.5, 1), 1e-9);
            Assert.AreEqual(3.5, EconomyCalc.SecondsPerCatch(1, 2, 0.5, 0, 0, 1), 1e-9);
            Assert.IsTrue(double.IsPositiveInfinity(EconomyCalc.SecondsPerCatch(1, 2, 0.5, 1, 1, 1)));
        }

        [Test]
        public void BiteChanceUpgrade_Preview_RaisesBiteChance_AndLowersMiss()
        {
            var cfg = Config(0.10f, 1f, Fish("a", Rarity.Common));
            cfg.biteChancePerLevel = 0.03f;
            cfg.baseMissChance = 0.3f;
            cfg.missReductionPerUpgradeLevel = 0.01f;
            var h = new Harness(cfg);
            var next = h.Stats.WithLevels(h.RodLevel, h.BiteLevel + 1, h.BaitLevel);
            Assert.AreEqual(0.10f, h.Stats.BiteChance, 1e-5f);
            Assert.AreEqual(0.13f, next.BiteChance, 1e-5f, "Tỉ lệ cá cắn +1 cấp: tỉ lệ cá cắn +3%");
            Assert.AreEqual(0.29f, next.MissChance, 1e-5f);
            Assert.Less(next.ExpectedWaitSeconds, h.Stats.ExpectedWaitSeconds, "Cắn nhiều hơn → đợi ít hơn");
            Assert.AreEqual(h.Stats.EffectiveDuration, next.EffectiveDuration, 1e-5f, "Không đổi tốc độ kéo");
        }

        // ---------- 4, 14. Không thưởng trùng ----------

        [Test]
        public void Ticket_IsRewardedExactlyOnce()
        {
            var h = new Harness(Config(1f, 1f, Fish("a", Rarity.Common)));
            var ticket = new CatchTicket(1, h.Selection.Select(0, false));
            Assert.IsTrue(h.Rewards.Grant(ticket));
            Assert.IsFalse(h.Rewards.Grant(ticket));
            Assert.AreEqual(1, h.Collection["a"]);
            Assert.AreEqual(1, h.RewardEvents);
            Assert.AreEqual(1, h.RewardEvents);
        }

        [Test]
        public void RareFish_Success_RewardedOnce_EvenIfCompletedTwice()
        {
            var h = new Harness(Config(1f, 1f, Fish("r", Rarity.Rare, MinigameType.Radial)));
            h.Run(1.05f);
            Assert.IsTrue(h.Auto.IsPaused);
            Assert.IsTrue(h.Auto.CompleteRare(true));
            Assert.IsFalse(h.Auto.CompleteRare(true));
            Assert.AreEqual(1, h.Collection["r"]);
            Assert.AreEqual(1, h.RewardEvents);
        }

        [Test]
        public void RareFish_Success_AddsCoinsImmediately()
        {
            var h = new Harness(Config(1f, 1f, Fish("r", Rarity.Rare, MinigameType.Radial)));
            h.Run(1.05f);
            Assert.AreEqual(0, h.Coins, "Chưa thắng minigame thì chưa có xu");
            h.Auto.CompleteRare(true);
            Assert.AreEqual(30, h.Coins);
            Assert.AreEqual(1, h.Collection["r"]);
        }

        // ---------- 5, 9, 10. Cá hiếm và vòng câu ----------

        [Test]
        public void RareFish_PausesLoop_UntilMinigameEnds_ThenResumes()
        {
            var h = new Harness(Config(1f, 1f, Fish("r", Rarity.Rare, MinigameType.Pendulum)));
            CatchTicket hooked = null;
            int hookedCount = 0;
            h.Auto.RareFishHooked += t => { hooked = t; hookedCount++; };

            h.Run(1.05f);
            Assert.IsNotNull(hooked);
            Assert.AreEqual(RodState.AwaitingMinigame, h.Auto.Rods[0].State);

            h.Run(30f);
            Assert.AreEqual(1, hookedCount, "Không mở minigame thứ hai khi đang chờ");
            Assert.AreEqual(0, h.RewardEvents, "Vòng câu dừng hẳn");

            h.Auto.CompleteRare(false);
            Assert.IsFalse(h.Auto.IsPaused);
            Assert.AreEqual(RodState.Casting, h.Auto.Rods[0].State, "Xong minigame thì quăng câu lại");
            Assert.IsFalse(h.Collection.ContainsKey("r"), "Thua minigame không được cá hiếm");

            h.Run(1.05f);
            Assert.AreEqual(2, hookedCount, "Vòng câu chạy lại sau khi minigame kết thúc");
        }

        [Test]
        public void RareFail_WithConsolationEnabled_GivesNormalFishOnly()
        {
            var cfg = Config(1f, 1f, Fish("r", Rarity.Rare, MinigameType.Radial), Fish("a", Rarity.Common));
            var h = new Harness(cfg);
            var auto = new AutoFishingController(h.Stats, h.Selection, h.Rewards, () => 0, () => true, () => true, 3);
            auto.SetRodCount(1);
            for (int i = 0; i < 200 && !auto.IsPaused; i++) auto.Tick(0.5f);
            Assert.IsTrue(auto.IsPaused, "Phải gặp cá hiếm");
            int before = h.Collection.TryGetValue("a", out int a) ? a : 0;
            auto.CompleteRare(false);
            Assert.IsFalse(h.Collection.ContainsKey("r"));
            Assert.AreEqual(before + 1, h.Collection["a"]);
        }

        [Test]
        public void CancelRare_GivesNothing_AndIsSafeToRepeat()
        {
            var h = new Harness(Config(1f, 1f, Fish("r", Rarity.Rare, MinigameType.Radial)));
            h.Run(1.05f);
            h.Auto.CancelRare();
            h.Auto.CancelRare();
            Assert.IsFalse(h.Auto.IsPaused);
            Assert.AreEqual(0, h.RewardEvents);
            Assert.IsFalse(h.Auto.CompleteRare(true), "Đã hủy thì không trao được nữa");
        }

        [Test]
        public void Controller_OpensMatchingMode_AndRefusesSecondMinigame()
        {
            var go = new GameObject("rare");
            _created.Add(go);
            var ctrl = go.AddComponent<RareFishingMinigameController>();

            Assert.IsTrue(ctrl.TryStart(Fish("p", Rarity.Epic, MinigameType.Pendulum), null));
            Assert.AreEqual(MinigameType.Pendulum, ctrl.Current.Type);
            Assert.IsFalse(ctrl.TryStart(Fish("r", Rarity.Rare, MinigameType.Radial), null), "Không chạy hai minigame cùng lúc");

            ctrl.Stop();
            Assert.IsFalse(ctrl.IsRunning);
            Assert.IsTrue(ctrl.TryStart(Fish("r2", Rarity.Rare, MinigameType.Radial), null));
            Assert.AreEqual(MinigameType.Radial, ctrl.Current.Type);
            Assert.IsFalse(ctrl.TryStart(Fish("n", Rarity.Common), null), "Cá thường không mở minigame");
        }

        [Test]
        public void Controller_RestartDoesNotDuplicateHandlers_AndCompletesOnce()
        {
            var go = new GameObject("rare");
            _created.Add(go);
            var ctrl = go.AddComponent<RareFishingMinigameController>();
            int inputs = 0, completed = 0, callbacks = 0;
            ctrl.InputEvaluated += _ => inputs++;
            ctrl.Completed += (_, __) => completed++;

            var fish = Fish("r", Rarity.Rare, MinigameType.Radial);
            fish.minigame.maxAttempts = 5;
            ctrl.TryStart(fish, _ => callbacks++);
            ctrl.Stop();
            ctrl.TryStart(fish, _ => callbacks++);
            ctrl.SubmitInput();
            Assert.AreEqual(1, inputs, "Một input chỉ được đánh giá một lần dù đã mở lại");
            ctrl.SubmitInput(); // cùng frame: bị chặn
            Assert.AreEqual(1, inputs);
            ctrl.Stop();
            Assert.AreEqual(2, completed);
            Assert.AreEqual(2, callbacks);
        }

        // ---------- 11. Offline chỉ cá thường ----------

        [Test]
        public void Offline_SelectionNeverReturnsRareFish()
        {
            var cfg = Config(1f, 1f, Fish("a", Rarity.Common), Fish("r", Rarity.Rare, MinigameType.Radial),
                Fish("l", Rarity.Legendary, MinigameType.Pendulum));
            var sel = new FishSelectionService(cfg, 0, 5);
            int rareOnline = 0;
            for (int i = 0; i < 5000; i++)
            {
                Assert.IsFalse(sel.Select(30, allowRare: false).IsRare);
                if (sel.Select(30, allowRare: true).IsRare) rareOnline++;
            }
            Assert.Greater(rareOnline, 0, "Online vẫn có cá hiếm");
            Assert.AreEqual(10.0, sel.ExpectedValuePerCatch(0, allowRare: false), 1e-9, "EV offline chỉ tính cá thường");
        }

        [Test]
        public void RareNotAllowed_LoopNeverPauses()
        {
            var h = new Harness(Config(1f, 1f, Fish("a", Rarity.Common), Fish("r", Rarity.Rare, MinigameType.Radial)));
            h.AllowRare = false;
            int hooked = 0;
            h.Auto.RareFishHooked += _ => hooked++;
            h.Run(200f);
            Assert.AreEqual(0, hooked);
            Assert.IsFalse(h.Collection.ContainsKey("r"));
            Assert.Greater(h.Collection["a"], 50);
        }

        // ---------- 13. Dữ liệu thiếu / bị hủy ----------

        [Test]
        public void MissingOrDestroyedData_DoesNotThrow()
        {
            var gone = Fish("gone", Rarity.Common);
            var cfg = Config(1f, 1f, null, gone);
            Object.DestroyImmediate(gone);
            var h = new Harness(cfg);
            Assert.DoesNotThrow(() => h.Run(10f));
            Assert.Greater(h.RewardEvents, 0, "Không có cá hợp lệ thì ra rác, không lỗi");

            var empty = new Harness(Config(1f, 1f));
            Assert.DoesNotThrow(() => empty.Run(5f));

            var go = new GameObject("rare");
            _created.Add(go);
            var ctrl = go.AddComponent<RareFishingMinigameController>();
            Assert.IsFalse(ctrl.TryStart(null, null));
            var noSettings = Fish("x", Rarity.Rare);
            noSettings.minigame = null;
            Assert.IsFalse(ctrl.TryStart(noSettings, null));
            Assert.DoesNotThrow(() => ctrl.SubmitInput());
            Assert.DoesNotThrow(() => ctrl.Stop());
        }
    }
}
