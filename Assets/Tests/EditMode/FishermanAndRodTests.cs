using System.Collections.Generic;
using LazyAngler.Data;
using LazyAngler.Systems;
using LazyAngler.Visual;
using NUnit.Framework;
using UnityEngine;

namespace LazyAngler.Tests
{
    /// <summary>Cấp cần câu (chỉ số) và máy trạng thái animation ngư dân.</summary>
    public class FishermanAndRodTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private RodTierData Tier(string name, float speed, float bite, long cost)
        {
            var t = ScriptableObject.CreateInstance<RodTierData>();
            t.displayName = name;
            t.fishingSpeedMultiplier = speed;
            t.biteChance = bite;
            t.unlockCost = cost;
            _created.Add(t);
            return t;
        }

        private GameConfig ConfigWithTiers()
        {
            var c = ScriptableObject.CreateInstance<GameConfig>();
            c.baseFishingDuration = 6f;
            c.biteChancePerLevel = 0.03f;
            c.maxBiteChance = 0.95f;
            c.rodTiers = new List<RodTierData>
            {
                Tier("Cần gỗ", 1.0f, 0.10f, 0),
                Tier("Cần tre", 1.3f, 0.15f, 60),
                Tier("Cần sợi thủy tinh", 1.7f, 0.22f, 400),
                Tier("Cần carbon", 2.3f, 0.30f, 2500),
                Tier("Cần huyền thoại", 3.0f, 0.45f, 15000),
            };
            _created.Add(c);
            return c;
        }

        // ---------- Cấp cần câu ----------

        [Test]
        public void RodTiers_DriveSpeedAndBiteChance()
        {
            var cfg = ConfigWithTiers();
            int rod = 0, bite = 0;
            var stats = new FishingStatProvider(cfg, () => rod, () => bite);

            float[] speeds = { 1.0f, 1.3f, 1.7f, 2.3f, 3.0f };
            float[] bites = { 0.10f, 0.15f, 0.22f, 0.30f, 0.45f };
            for (rod = 0; rod < 5; rod++)
            {
                Assert.AreEqual(speeds[rod], stats.FishingSpeedMultiplier, 1e-5f, $"tier {rod}");
                Assert.AreEqual(6f / speeds[rod], stats.EffectiveDuration, 1e-4f);
                Assert.AreEqual(bites[rod], stats.BiteChance, 1e-5f);
                Assert.AreSame(cfg.rodTiers[rod], stats.RodTier);
            }
        }

        [Test]
        public void BiteUpgrade_AddsOnTopOfRodTier_AndIsCapped()
        {
            var cfg = ConfigWithTiers();
            int rod = 3, bite = 5;
            var stats = new FishingStatProvider(cfg, () => rod, () => bite);
            Assert.AreEqual(0.30f + 0.15f, stats.BiteChance, 1e-5f);
            Assert.AreEqual(2.3f, stats.FishingSpeedMultiplier, 1e-5f, "Tỉ lệ cá cắn không đổi tốc độ");
            bite = 100;
            Assert.AreEqual(0.95f, stats.BiteChance, 1e-5f);
        }

        [Test]
        public void RodLevel_BeyondLastTier_UsesLastTier_NegativeUsesFirst()
        {
            var cfg = ConfigWithTiers();
            Assert.AreEqual("Cần huyền thoại", RodTierData.ForLevel(cfg.rodTiers, 99).displayName);
            Assert.AreEqual("Cần gỗ", RodTierData.ForLevel(cfg.rodTiers, -1).displayName);
            Assert.IsNull(RodTierData.ForLevel(new List<RodTierData>(), 0));
            Assert.IsNull(RodTierData.ForLevel(null, 0));
        }

        [Test]
        public void MissingTierEntry_FallsBackToLowerTier()
        {
            var cfg = ConfigWithTiers();
            Object.DestroyImmediate(cfg.rodTiers[2]);
            Assert.AreEqual("Cần tre", RodTierData.ForLevel(cfg.rodTiers, 2).displayName);
        }

        [Test]
        public void NoTiers_KeepsLegacyFormula()
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            _created.Add(cfg);
            cfg.baseFishingDuration = 5f;
            cfg.castReductionPerLevel = 0.5f;
            cfg.minCastSeconds = 0.1f;
            cfg.baseBiteChance = 0.5f;
            var stats = new FishingStatProvider(cfg, () => 1, () => 0);
            Assert.IsNull(stats.RodTier);
            Assert.AreEqual(2f, stats.FishingSpeedMultiplier, 1e-4f);
            Assert.AreEqual(0.5f, stats.BiteChance, 1e-5f);
        }

        // ---------- Animation ngư dân ----------

        [Test]
        public void Idle_Loops_WithoutLeavingIdle()
        {
            var m = new FishermanPoseMachine();
            var frames = new HashSet<int>();
            for (int i = 0; i < 600; i++)
            {
                m.Tick(1f / 60f);
                frames.Add(m.Evaluate().frame);
                Assert.AreEqual(FishermanState.FishingIdle, m.State);
            }
            Assert.That(frames, Is.SubsetOf(new[] { 0, 1, 2, 3 }));
            Assert.Greater(frames.Count, 1, "Ngồi chờ có chuyển động nhẹ");
        }

        [Test]
        public void HookPull_OnlyAfterBite()
        {
            var m = new FishermanPoseMachine();
            Assert.IsFalse(m.HookStarted(), "Không giật cần khi cá chưa cắn");
            Assert.IsFalse(m.CatchSucceeded());
            Assert.IsFalse(m.CatchFailed());
            Assert.AreEqual(FishermanState.FishingIdle, m.State);

            Assert.IsTrue(m.FishBite());
            Assert.IsFalse(m.FishBite(), "Không cắn chồng khi đang cắn");
            Assert.IsTrue(m.HookStarted());
            Assert.AreEqual(FishermanState.HookPull, m.State);
        }

        [Test]
        public void SuccessFlow_ReturnsToIdle_CompletesOnce()
        {
            var m = new FishermanPoseMachine();
            var completed = new List<FishermanState>();
            m.AnimationCompleted += completed.Add;

            m.FishBite();
            m.HookStarted();
            Tick(m, m.hookPullSeconds + 0.01f);
            Assert.AreEqual(FishermanState.Reeling, m.State, "Giật cần xong tự chuyển sang thu dây");
            Assert.IsTrue(m.CatchSucceeded());
            Assert.IsFalse(m.CatchSucceeded(), "Không thu cá hai lần");
            Tick(m, m.catchSeconds + 0.5f);
            Assert.AreEqual(FishermanState.FishingIdle, m.State);
            CollectionAssert.AreEqual(new[] { FishermanState.CatchSuccess }, completed);
        }

        [Test]
        public void FailFlow_ReturnsToIdle_CompletesOnce()
        {
            var m = new FishermanPoseMachine();
            int done = 0;
            m.AnimationCompleted += _ => done++;
            m.FishBite();
            m.HookStarted();
            Assert.IsTrue(m.CatchFailed());
            Assert.AreEqual(FishermanPoseMachine.FrameSlump, m.Evaluate().frame);
            Tick(m, 5f);
            Assert.AreEqual(FishermanState.FishingIdle, m.State);
            Assert.AreEqual(1, done);
        }

        [Test]
        public void ResetToIdle_DoesNotRaiseCompleted()
        {
            var m = new FishermanPoseMachine();
            int done = 0;
            m.AnimationCompleted += _ => done++;
            m.FishBite();
            m.ResetToIdle();
            Tick(m, 3f);
            Assert.AreEqual(0, done);
            Assert.AreEqual(FishermanState.FishingIdle, m.State);
        }

        [Test]
        public void Cast_ThenBite_IsAllowed_AndCastEndsInIdle()
        {
            var m = new FishermanPoseMachine();
            Assert.IsTrue(m.BeginCast());
            Assert.IsFalse(m.BeginCast());
            m.EndCast();
            Assert.AreEqual(FishermanState.FishingIdle, m.State);
            m.BeginCast();
            Assert.IsTrue(m.FishBite(), "Cá có thể cắn ngay khi vừa quăng xong");
        }

        [Test]
        public void Poses_AreBoundedAndValidFrames()
        {
            var m = new FishermanPoseMachine();
            void Check()
            {
                var p = m.Evaluate();
                Assert.That(p.frame, Is.InRange(0, FishingPixelArt.FrameCount - 1));
                Assert.That(p.rodAngleOffset, Is.InRange(-15f, 30f));
                Assert.That(p.rodBend, Is.InRange(0f, 45f));
            }
            m.FishBite(); for (int i = 0; i < 30; i++) { m.Tick(0.02f); Check(); }
            m.HookStarted(); for (int i = 0; i < 30; i++) { m.Tick(0.02f); Check(); }
            m.ReelTension = 1f; for (int i = 0; i < 30; i++) { m.Tick(0.02f); Check(); }
            m.CatchSucceeded(); for (int i = 0; i < 60; i++) { m.Tick(0.02f); Check(); }
        }

        // ---------- Pixel art ----------

        [Test]
        public void PixelArt_SheetsHaveExpectedCells_AndTransparentBackground()
        {
            var fisher = FishingPixelArt.PaintFisherman();
            Assert.AreEqual(FishingPixelArt.FrameCount, fisher.Cells.Count);
            foreach (var c in fisher.Cells)
            {
                Assert.AreEqual(FishingPixelArt.FrameWidth, c.rect.width);
                Assert.AreEqual(FishingPixelArt.FrameHeight, c.rect.height);
            }
            Assert.AreEqual(0, fisher.Pixels[0].a, "Góc khung trong suốt");

            var rods = FishingPixelArt.PaintRods();
            Assert.AreEqual(FishingPixelArt.RodTierCount * 3, rods.Cells.Count);

            var props = FishingPixelArt.PaintProps();
            var names = new HashSet<string>();
            foreach (var c in props.Cells)
            {
                Assert.IsTrue(names.Add(c.name), "Tên ô trùng: " + c.name);
                Assert.That(c.rect.xMax, Is.LessThanOrEqualTo(props.Width));
                Assert.That(c.rect.yMax, Is.LessThanOrEqualTo(props.Height));
            }
            // Các ô không chồng lên nhau.
            for (int i = 0; i < props.Cells.Count; i++)
                for (int j = i + 1; j < props.Cells.Count; j++)
                    Assert.IsFalse(props.Cells[i].rect.Overlaps(props.Cells[j].rect),
                        props.Cells[i].name + " / " + props.Cells[j].name);
        }

        [Test]
        public void RuntimeArtSet_IsComplete()
        {
            var set = FishingArtSet.CreateRuntime(out var sprites);
            _created.Add(set);
            Assert.IsTrue(set.IsComplete);
            Assert.AreEqual(FilterMode.Point, set.fishermanFrames[0].texture.filterMode);
            Assert.AreEqual(FishingPixelArt.PixelsPerUnit, set.fishermanFrames[0].pixelsPerUnit, 1e-4f);
            for (int t = 0; t < FishingPixelArt.RodTierCount; t++)
                for (int s = 0; s < 3; s++)
                    Assert.IsTrue(sprites.ContainsKey(FishingPixelArt.RodCellName(t, s)));
            foreach (var s in sprites.Values) { _created.Add(s.texture); _created.Add(s); }
        }

        private static void Tick(FishermanPoseMachine m, float seconds)
        {
            for (float t = 0; t < seconds; t += 0.02f) m.Tick(0.02f);
        }
    }
}
