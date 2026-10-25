using System;
using LazyAngler.Core;
using LazyAngler.Systems;
using NUnit.Framework;

namespace LazyAngler.Tests
{
    /// <summary>Bám theo mục 8 "Checklist kiểm thử" của tài liệu thiết kế.</summary>
    public class EconomyCalcTests
    {
        private static readonly double[] Weights = { 60, 25, 10, 4, 1 };
        private const double Hour = 3600;

        [Test]
        public void Vault_Level1_Is2Hours_Level12_Is24Hours()
        {
            Assert.AreEqual(2 * Hour, EconomyCalc.VaultCapSeconds(1));
            Assert.AreEqual(24 * Hour, EconomyCalc.VaultCapSeconds(12));
            Assert.AreEqual(24 * Hour, EconomyCalc.VaultCapSeconds(99), "Vượt cấp tối đa vẫn là 24 giờ");
        }

        [Test]
        public void Offline_AwayLongerThanVault_IsCappedAtVault()
        {
            Assert.AreEqual(2 * Hour, EconomyCalc.OfflineCountedSeconds(10 * Hour, 1));
            // 2 giờ × 1 xu/giây × 0,5
            Assert.AreEqual(3600, EconomyCalc.OfflineCoins(10 * Hour, 1, 1.0));
        }

        [Test]
        public void Offline_NegativeTime_ClampedToZero()
        {
            var now = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(0, EconomyCalc.AwaySeconds(now.AddHours(3), now));
            Assert.AreEqual(0, EconomyCalc.OfflineCoins(-500, 1, 10));
            Assert.AreEqual(0, EconomyCalc.OfflineFish(-500, 1, 10));
        }

        [Test]
        public void Offline_IsHalfOfOnline_ForSameInput()
        {
            double income = EconomyCalc.IncomePerSecond(3, 2.5, 7.0);
            double seconds = 1800;
            long online = (long)Math.Floor(seconds * income);
            long offline = EconomyCalc.OfflineCoins(seconds, 12, income);
            Assert.AreEqual(Math.Floor(online * 0.5), offline, 1);
        }

        [Test]
        public void Offline_ValuesAreFloored()
        {
            Assert.AreEqual(2, EconomyCalc.OfflineCoins(10, 1, 0.55)); // 2,75 → 2
            Assert.AreEqual(1, EconomyCalc.OfflineFish(5, 1, 0.5));    // 1,25 → 1
        }

        [Test]
        public void Bait_Level30_RareAbout1Point9x_AndTotalIs100Percent()
        {
            var p0 = LootTable.RarityProbabilities(Weights, 0);
            var p30 = LootTable.RarityProbabilities(Weights, 30);
            Assert.AreEqual(1.9, p30[2] / p0[2], 1e-9);
            Assert.AreEqual(1.0, Sum(p0), 1e-9);
            Assert.AreEqual(1.0, Sum(p30), 1e-9);
            Assert.AreEqual(p0[1], p30[1], 1e-9, "Uncommon không được mồi buff");
            Assert.Less(p30[0], p0[0], "Common giảm để bù");
        }

        [Test]
        public void Bait_HugeLevel_StillSumsTo100Percent_NoNegative()
        {
            var p = LootTable.RarityProbabilities(Weights, 10000);
            Assert.AreEqual(1.0, Sum(p), 1e-9);
            foreach (var x in p) Assert.GreaterOrEqual(x, 0);
        }

        [Test]
        public void Rarity_UnavailableTiersMasked_AndRenormalized()
        {
            var p = LootTable.RarityProbabilities(Weights, 0, new[] { true, true, false, false, false });
            Assert.AreEqual(0, p[2]);
            Assert.AreEqual(1.0, Sum(p), 1e-9);
        }

        [Test]
        public void PickRarity_FollowsCumulativeRanges()
        {
            var p = new[] { 0.5, 0.3, 0.2, 0.0, 0.0 };
            Assert.AreEqual(0, LootTable.PickRarity(p, 0.0));
            Assert.AreEqual(1, LootTable.PickRarity(p, 0.6));
            Assert.AreEqual(2, LootTable.PickRarity(p, 0.95));
            Assert.AreEqual(2, LootTable.PickRarity(p, 0.9999999999), "Không bao giờ chọn độ hiếm 0%");
        }

        [Test]
        public void Rods_Level1Is1_MaxIs10()
        {
            Assert.AreEqual(1, EconomyCalc.ActiveRods(1));
            Assert.AreEqual(10, EconomyCalc.ActiveRods(10));
            Assert.AreEqual(10, EconomyCalc.ActiveRods(50));
        }

        [Test]
        public void UpgradeCost_GrowsBy15Percent()
        {
            Assert.AreEqual(10, EconomyCalc.UpgradeCost(10, 0), 1e-9);
            Assert.AreEqual(11.5, EconomyCalc.UpgradeCost(10, 1), 1e-9);
            Assert.AreEqual(10 * Math.Pow(1.15, 20), EconomyCalc.UpgradeCost(10, 20), 1e-6);
        }

        [Test]
        public void UpgradeCost_CustomGrowth()
        {
            Assert.AreEqual(200, EconomyCalc.UpgradeCost(200, 0, 2.8), 1e-9);
            Assert.AreEqual(560, EconomyCalc.UpgradeCost(200, 1, 2.8), 1e-9);
            Assert.AreEqual(200, EconomyCalc.UpgradeCost(200, 3, 0.5), 1e-9, "Hệ số < 1 coi như 1");
        }

        [Test]
        public void FishValueMultiplier_IsLinearPerLevel()
        {
            Assert.AreEqual(1.0, EconomyCalc.FishValueMultiplier(0, 0.1), 1e-9);
            Assert.AreEqual(2.0, EconomyCalc.FishValueMultiplier(10, 0.1), 1e-9);
            Assert.AreEqual(1.0, EconomyCalc.FishValueMultiplier(-3, 0.1), 1e-9);
        }

        [Test]
        public void CastSeconds_DecreasesWithLevel_NotBelowMin()
        {
            Assert.AreEqual(5, EconomyCalc.CastSeconds(5, 0.97, 1, 0), 1e-9);
            Assert.Less(EconomyCalc.CastSeconds(5, 0.97, 1, 10), 5);
            Assert.AreEqual(1, EconomyCalc.CastSeconds(5, 0.97, 1, 1000), 1e-9);
        }

        [Test]
        public void ExpectedValue_IncludesTrash()
        {
            var p = new[] { 1.0, 0, 0, 0, 0 };
            var price = new[] { 10.0, 0, 0, 0, 0 };
            Assert.AreEqual(0.9 * 10 + 0.1 * 1, LootTable.ExpectedValuePerCatch(p, price, 0.1, 1), 1e-9);
        }

        private static double Sum(double[] a)
        {
            double s = 0;
            foreach (var x in a) s += x;
            return s;
        }
    }
}
