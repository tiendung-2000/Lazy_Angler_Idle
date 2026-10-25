using System;
using LazyAngler.Core;

namespace LazyAngler.Systems
{
    /// <summary>
    /// Kim dao động qua lại giữa minAngle và maxAngle theo hàm sin của pha tích lũy:
    /// angle = tâm + biên độ × sin(pha), pha += 2π × oscillationSpeed × dt.
    /// Mượt, không phụ thuộc FPS và không bao giờ vượt giới hạn.
    /// </summary>
    public class PendulumFishingMode : RareFishingModeBase
    {
        private const float TwoPi = (float)(Math.PI * 2.0);
        private double _phase;

        public override MinigameType Type => MinigameType.Pendulum;
        public float MinAngle => Math.Min(Settings.minAngle, Settings.maxAngle);
        public float MaxAngle => Math.Max(Settings.minAngle, Settings.maxAngle);

        protected override void OnStart()
        {
            _phase = -Math.PI / 2.0; // bắt đầu ở mép minAngle
            NeedleAngle = MinAngle;
        }

        protected override void Advance(float deltaTime)
        {
            _phase = (_phase + TwoPi * Settings.oscillationSpeed * deltaTime) % TwoPi;
            float lo = MinAngle, hi = MaxAngle;
            float center = (lo + hi) * 0.5f, amp = (hi - lo) * 0.5f;
            NeedleAngle = Math.Clamp(center + amp * (float)Math.Sin(_phase), lo, hi);
        }

        protected override void PlaceTargets()
        {
            Targets.Clear();
            float lo = MinAngle, hi = MaxAngle;
            float half = TargetWidth * 0.5f;
            float min = lo + half, max = hi - half;
            float center;
            if (min > max) center = (lo + hi) * 0.5f; // vùng mục tiêu rộng hơn đường chạy
            else if (Settings.randomizeTarget) center = min + (float)Rng.NextDouble() * (max - min);
            else center = Math.Clamp(Settings.targetAngle, min, max);
            Targets.Add(center);
        }

        // Pendulum không quay vòng nên so sánh thẳng, không qua mốc 0/360.
        protected override bool IsInTarget(float angle, float center)
            => Math.Abs(angle - center) <= TargetWidth * 0.5f + 1e-4f;
    }
}
