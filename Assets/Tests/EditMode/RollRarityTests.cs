using System;
using LazyAngler.Core;
using NUnit.Framework;

namespace LazyAngler.Tests
{
    public class RollRarityTests
    {
        private static double[] W => LootTable.DefaultRarityWeights;

        [Test]
        public void DefaultWeights_Are_60_25_10_4_1()
        {
            CollectionAssert.AreEqual(new double[] { 60, 25, 10, 4, 1 }, W);
        }

        [Test]
        public void Probabilities_MatchWeights_AndSumTo1()
        {
            var p = LootTable.RarityProbabilities(W, 0);
            double[] expected = { 0.60, 0.25, 0.10, 0.04, 0.01 };
            double sum = 0;
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(expected[i], p[i], 1e-9);
                sum += p[i];
            }
            Assert.AreEqual(1.0, sum, 1e-9);
        }

        [Test]
        public void Roll_BoundariesMapToCumulativeRanges()
        {
            // Cộng dồn: Common [0, .60), Uncommon [.60, .85), Rare [.85, .95), Epic [.95, .99), Legendary [.99, 1)
            Assert.AreEqual(0, LootTable.RollRarity(W, 0, 0.0));
            Assert.AreEqual(0, LootTable.RollRarity(W, 0, 0.5999));
            Assert.AreEqual(1, LootTable.RollRarity(W, 0, 0.6001));
            Assert.AreEqual(1, LootTable.RollRarity(W, 0, 0.8499));
            Assert.AreEqual(2, LootTable.RollRarity(W, 0, 0.8501));
            Assert.AreEqual(3, LootTable.RollRarity(W, 0, 0.9501));
            Assert.AreEqual(4, LootTable.RollRarity(W, 0, 0.9901));
            Assert.AreEqual(4, LootTable.RollRarity(W, 0, 0.999999));
        }

        [Test]
        public void Roll_OutOfRangeInput_IsClamped()
        {
            Assert.AreEqual(0, LootTable.RollRarity(W, 0, -5));
            Assert.AreEqual(4, LootTable.RollRarity(W, 0, 1.0));
            Assert.AreEqual(4, LootTable.RollRarity(W, 0, 42));
        }

        [Test]
        public void Roll_DistributionMatchesWeights()
        {
            var rng = new Random(12345);
            var counts = new int[5];
            const int n = 200000;
            for (int i = 0; i < n; i++) counts[LootTable.RollRarity(W, 0, rng.NextDouble())]++;
            double[] expected = { 0.60, 0.25, 0.10, 0.04, 0.01 };
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(expected[i], counts[i] / (double)n, 0.005, $"rarity {i}");
        }

        [Test]
        public void Roll_AlwaysReturnsValidIndex_ForAnyBaitLevel()
        {
            var rng = new Random(7);
            foreach (int bait in new[] { 0, 1, 30, 1000 })
                for (int i = 0; i < 2000; i++)
                {
                    int r = LootTable.RollRarity(W, bait, rng.NextDouble());
                    Assert.That(r, Is.InRange(0, 4));
                }
        }

        [Test]
        public void Roll_InvalidWeights_Throw()
        {
            Assert.Throws<ArgumentException>(() => LootTable.RollRarity(new double[] { 1, 2 }, 0, 0.5));
            Assert.Throws<ArgumentException>(() => LootTable.RollRarity(new double[5], 0, 0.5));
        }
    }
}
