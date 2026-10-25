using System;
using LazyAngler.Core;

namespace LazyAngler.Systems
{
    /// <summary>
    /// Kim quay đều quanh tâm với rotationSpeed (độ/giây). Một hoặc nhiều vùng mục tiêu cách đều trên vòng tròn.
    /// Góc luôn chuẩn hóa 0..360; kiểm tra vùng mục tiêu đúng cả khi đi qua mốc 0/360.
    /// </summary>
    public class RadialFishingMode : RareFishingModeBase
    {
        public override MinigameType Type => MinigameType.Radial;

        protected override void OnStart() => NeedleAngle = AngleMath.Normalize360(Settings.startAngle);

        protected override void Advance(float deltaTime)
            => NeedleAngle = AngleMath.Normalize360(NeedleAngle + Settings.rotationSpeed * deltaTime);

        protected override void PlaceTargets()
        {
            Targets.Clear();
            int maxFit = Math.Max(1, (int)(360f / TargetWidth));
            int count = Math.Clamp(Settings.targetCount, 1, maxFit);

            float first;
            if (Settings.randomizeTarget)
            {
                // Đặt phía trước kim từ 90° tới 270° để người chơi có thời gian phản ứng.
                float dir = Settings.rotationSpeed >= 0f ? 1f : -1f;
                first = NeedleAngle + dir * (90f + (float)Rng.NextDouble() * 180f);
            }
            else
            {
                first = Settings.targetAngle;
            }

            for (int i = 0; i < count; i++)
                Targets.Add(AngleMath.Normalize360(first + i * 360f / count));
        }
    }
}
