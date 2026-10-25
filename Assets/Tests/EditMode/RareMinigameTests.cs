using LazyAngler.Core;
using LazyAngler.Systems;
using NUnit.Framework;

namespace LazyAngler.Tests
{
    public class RareMinigameTests
    {
        // ---------- AngleMath ----------

        [Test]
        public void Normalize360_WrapsBothDirections()
        {
            Assert.AreEqual(0f, AngleMath.Normalize360(360f), 1e-4f);
            Assert.AreEqual(10f, AngleMath.Normalize360(370f), 1e-4f);
            Assert.AreEqual(350f, AngleMath.Normalize360(-10f), 1e-4f);
            Assert.AreEqual(270f, AngleMath.Normalize360(-450f), 1e-4f);
        }

        [Test]
        public void IsWithinArc_HandlesZero360Boundary()
        {
            // Tâm 350°, rộng 40° → phủ 330°..10°.
            Assert.IsTrue(AngleMath.IsWithinArc(5f, 350f, 40f));
            Assert.IsTrue(AngleMath.IsWithinArc(0f, 350f, 40f));
            Assert.IsTrue(AngleMath.IsWithinArc(335f, 350f, 40f));
            Assert.IsTrue(AngleMath.IsWithinArc(10f, 350f, 40f), "Mép vùng tính là trúng");
            Assert.IsFalse(AngleMath.IsWithinArc(15f, 350f, 40f));
            Assert.IsFalse(AngleMath.IsWithinArc(325f, 350f, 40f));
            // Tâm 5°, góc âm/vượt 360 vẫn đúng.
            Assert.IsTrue(AngleMath.IsWithinArc(-10f, 5f, 40f));
            Assert.IsTrue(AngleMath.IsWithinArc(720f, 5f, 40f));
            Assert.IsFalse(AngleMath.IsWithinArc(180f, 5f, 40f));
        }

        // ---------- Radial ----------

        private static RareMinigameSettings Radial(float target, float width = 40f, float speed = 100f,
            int required = 2, int attempts = 3, int targets = 1)
            => new RareMinigameSettings
            {
                type = MinigameType.Radial, randomizeTarget = false, targetAngle = target, targetAngleWidth = width,
                rotationSpeed = speed, requiredSuccesses = required, maxAttempts = attempts, targetCount = targets,
            };

        [Test]
        public void Radial_NeedleRotatesWithDeltaTime_AndStaysNormalized()
        {
            var m = new RadialFishingMode();
            m.StartMinigame(Radial(90f, speed: 100f));
            for (int i = 0; i < 37; i++) m.Tick(0.1f); // 370°
            Assert.AreEqual(10f, m.NeedleAngle, 1e-2f);

            var neg = new RadialFishingMode();
            neg.StartMinigame(Radial(90f, speed: -100f));
            neg.Tick(0.2f);
            Assert.AreEqual(340f, neg.NeedleAngle, 1e-3f);
        }

        [Test]
        public void Radial_HitAcrossZero_MissOutside()
        {
            var m = new RadialFishingMode();
            m.StartMinigame(Radial(350f, width: 40f, speed: 100f, required: 2, attempts: 5));
            m.Tick(0.05f); // 5° → trong vùng 330..10
            Assert.IsTrue(m.SubmitInput());
            m.Tick(0.1f);  // 15° → ngoài vùng
            Assert.IsFalse(m.SubmitInput());
            Assert.AreEqual(1, m.Successes);
            Assert.AreEqual(2, m.Attempts);
            Assert.AreEqual(MinigameStatus.Running, m.Status);
        }

        [Test]
        public void Radial_WinsAfterRequiredSuccesses()
        {
            var m = new RadialFishingMode();
            MinigameResult? result = null;
            m.OnMinigameCompleted += r => result = r;
            m.StartMinigame(Radial(0f, required: 2, attempts: 3));
            m.SubmitInput();
            m.SubmitInput();
            Assert.AreEqual(MinigameStatus.Won, m.Status);
            Assert.IsTrue(result.HasValue && result.Value.Success);
            Assert.IsFalse(m.SubmitInput(), "Hết ván thì bỏ qua input");
            Assert.AreEqual(2, m.Attempts);
        }

        [Test]
        public void Radial_LosesWhenWinBecomesImpossible()
        {
            var m = new RadialFishingMode();
            int completed = 0;
            m.OnMinigameCompleted += _ => completed++;
            m.StartMinigame(Radial(180f, required: 2, attempts: 3)); // kim ở 0°, luôn trượt
            m.SubmitInput();
            Assert.AreEqual(MinigameStatus.Running, m.Status);
            m.SubmitInput(); // còn 1 lượt, cần 2 → thua sớm
            Assert.AreEqual(MinigameStatus.Lost, m.Status);
            Assert.AreEqual(1, completed);
            m.StopMinigame();
            Assert.AreEqual(1, completed, "Kết quả chỉ phát một lần");
        }

        [Test]
        public void Radial_MultipleTargets_EvenlySpaced()
        {
            var m = new RadialFishingMode();
            m.StartMinigame(Radial(10f, width: 30f, targets: 3, required: 1, attempts: 3));
            Assert.AreEqual(3, m.TargetAngles.Count);
            Assert.AreEqual(10f, m.TargetAngles[0], 1e-3f);
            Assert.AreEqual(130f, m.TargetAngles[1], 1e-3f);
            Assert.AreEqual(250f, m.TargetAngles[2], 1e-3f);
            m.Tick(1.3f); // 130°
            Assert.IsTrue(m.SubmitInput());
        }

        [Test]
        public void Radial_RandomTargets_AlwaysNormalized_AndAhead()
        {
            var s = Radial(0f, width: 30f, required: 50, attempts: 60);
            s.randomizeTarget = true;
            var m = new RadialFishingMode();
            for (int seed = 0; seed < 100; seed++)
            {
                m.StartMinigame(s, seed);
                AssertTargetsValid(m);
            }

            // Sau mỗi lần trúng, vùng mục tiêu dời đi nhưng không nằm ngay dưới kim.
            m.StartMinigame(s, seed: 3);
            int hits = 0;
            for (int i = 0; i < 20000 && hits < 20; i++)
            {
                m.Tick(0.01f);
                if (!m.IsInAnyTarget(m.NeedleAngle)) continue;
                Assert.IsTrue(m.SubmitInput());
                hits++;
                AssertTargetsValid(m);
            }
            Assert.AreEqual(20, hits);
        }

        private static void AssertTargetsValid(IRareFishingMinigame m)
        {
            foreach (float t in m.TargetAngles)
            {
                Assert.That(t, Is.InRange(0f, 359.999f));
                Assert.IsFalse(AngleMath.IsWithinArc(m.NeedleAngle, t, m.TargetWidth), "Vùng mới không đặt ngay dưới kim");
            }
        }

        [Test]
        public void StopMinigame_ReportsCancelled()
        {
            var m = new RadialFishingMode();
            MinigameResult? r = null;
            m.OnMinigameCompleted += x => r = x;
            m.StartMinigame(Radial(90f));
            m.StopMinigame();
            Assert.AreEqual(MinigameStatus.Cancelled, r.Value.status);
            Assert.IsFalse(r.Value.Success);
        }

        [Test]
        public void Restart_ResetsState()
        {
            var m = new RadialFishingMode();
            m.StartMinigame(Radial(0f, required: 1, attempts: 1));
            m.SubmitInput();
            Assert.AreEqual(MinigameStatus.Won, m.Status);
            m.StartMinigame(Radial(90f, required: 2, attempts: 4));
            Assert.AreEqual(MinigameStatus.Running, m.Status);
            Assert.AreEqual(0, m.Successes);
            Assert.AreEqual(0, m.Attempts);
            Assert.AreEqual(0f, m.NeedleAngle);
        }

        // ---------- Pendulum ----------

        private static RareMinigameSettings Pendulum(float min = -60f, float max = 60f, float speed = 0.5f,
            float target = 0f, float width = 20f)
            => new RareMinigameSettings
            {
                type = MinigameType.Pendulum, minAngle = min, maxAngle = max, oscillationSpeed = speed,
                randomizeTarget = false, targetAngle = target, targetAngleWidth = width,
                requiredSuccesses = 3, maxAttempts = 5,
            };

        [Test]
        public void Pendulum_NeverExceedsLimits_AnyDeltaTime()
        {
            var m = new PendulumFishingMode();
            m.StartMinigame(Pendulum(-60f, 60f, 1.7f));
            var rng = new System.Random(1);
            float seenMin = float.MaxValue, seenMax = float.MinValue;
            for (int i = 0; i < 20000; i++)
            {
                float dt = i % 500 == 0 ? 10f : (float)rng.NextDouble() * 0.1f;
                m.Tick(dt);
                Assert.That(m.NeedleAngle, Is.InRange(-60f, 60f));
                seenMin = System.Math.Min(seenMin, m.NeedleAngle);
                seenMax = System.Math.Max(seenMax, m.NeedleAngle);
            }
            Assert.Less(seenMin, -55f, "Kim chạy hết biên độ");
            Assert.Greater(seenMax, 55f);
        }

        [Test]
        public void Pendulum_SwappedLimits_StillBounded()
        {
            var m = new PendulumFishingMode();
            m.StartMinigame(Pendulum(50f, -30f, 1f));
            for (int i = 0; i < 1000; i++)
            {
                m.Tick(0.013f);
                Assert.That(m.NeedleAngle, Is.InRange(-30f, 50f));
            }
        }

        [Test]
        public void Pendulum_IsFrameRateIndependent()
        {
            var slow = new PendulumFishingMode();
            var fast = new PendulumFishingMode();
            slow.StartMinigame(Pendulum(speed: 0.8f));
            fast.StartMinigame(Pendulum(speed: 0.8f));
            for (int i = 0; i < 30; i++) slow.Tick(1f / 30f);
            for (int i = 0; i < 144; i++) fast.Tick(1f / 144f);
            Assert.AreEqual(slow.NeedleAngle, fast.NeedleAngle, 0.05f);
        }

        [Test]
        public void Pendulum_StartsAtMin_ReachesMaxAfterHalfCycle()
        {
            var m = new PendulumFishingMode();
            m.StartMinigame(Pendulum(-60f, 60f, 0.5f)); // chu kỳ 2s
            Assert.AreEqual(-60f, m.NeedleAngle, 1e-3f);
            m.Tick(1f);
            Assert.AreEqual(60f, m.NeedleAngle, 0.01f);
            m.Tick(0.5f);
            Assert.AreEqual(0f, m.NeedleAngle, 0.01f);
        }

        [Test]
        public void Pendulum_EvaluatesInputAgainstTarget()
        {
            var m = new PendulumFishingMode();
            m.StartMinigame(Pendulum(-60f, 60f, 0.5f, target: 0f, width: 20f));
            Assert.IsFalse(m.SubmitInput(), "-60° ngoài vùng 0±10");
            m.Tick(0.5f); // 0°
            Assert.IsTrue(m.SubmitInput());
            Assert.AreEqual(1, m.Successes);
            Assert.AreEqual(2, m.Attempts);
        }

        [Test]
        public void Pendulum_TargetKeptInsideTrack()
        {
            var m = new PendulumFishingMode();
            m.StartMinigame(Pendulum(-60f, 60f, target: 200f, width: 20f));
            Assert.AreEqual(50f, m.TargetAngles[0], 1e-3f, "Vùng mục tiêu bị kẹp vào trong đường chạy");

            var s = Pendulum(-60f, 60f, width: 30f);
            s.randomizeTarget = true;
            var r = new PendulumFishingMode();
            for (int seed = 0; seed < 100; seed++)
            {
                r.StartMinigame(s, seed);
                Assert.That(r.TargetAngles[0], Is.InRange(-45f, 45f));
            }
        }
    }
}
