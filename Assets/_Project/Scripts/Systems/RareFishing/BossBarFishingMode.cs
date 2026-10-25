using System;
using LazyAngler.Core;

namespace LazyAngler.Systems
{
    /// <summary>
    /// Minigame cá boss: con lăn chạy trên thanh ngang, vị trí 0–100 (lưu trong NeedleAngle; "góc" ở đây là % thanh).
    /// Mỗi lượt chạy có điểm đích và tốc độ ngẫu nhiên: có thể chạy hết tới mép, hoặc quay đầu giữa chừng
    /// (chưa tới mép đã đảo chiều), thỉnh thoảng tăng tốc đột ngột. Mỗi lần trúng vùng xanh, vùng xanh dời chỗ và
    /// hẹp đi, con lăn nhanh hơn → các lượt sau khó hơn. Đủ requiredSuccesses trước khi hết maxAttempts thì thắng.
    /// </summary>
    public class BossBarFishingMode : RareFishingModeBase
    {
        public const float BarMin = 0f;
        public const float BarMax = 100f;
        private const float BurstMultiplier = 1.6f;

        private float _direction = 1f;
        private float _legTarget;
        private float _legSpeed;
        private float _baseWidth;

        public override MinigameType Type => MinigameType.Boss;

        /// <summary>Tốc độ lượt chạy hiện tại (% thanh/giây), dùng để hiển thị/kiểm tra.</summary>
        public float CurrentSpeed => _legSpeed;

        protected override void OnStart()
        {
            _baseWidth = Math.Clamp(Settings.targetAngleWidth, 1f, 100f);
            NeedleAngle = (float)(Rng.NextDouble() * (BarMax - BarMin) + BarMin);
            _direction = Rng.Next(2) == 0 ? -1f : 1f;
            NewLeg();
        }

        protected override void Advance(float deltaTime)
        {
            // Một khung hình dài có thể đi qua nhiều điểm đích: xử lý lần lượt cho đúng chiều.
            float remaining = deltaTime;
            for (int guard = 0; guard < 8 && remaining > 0f; guard++)
            {
                float distance = Math.Abs(_legTarget - NeedleAngle);
                float step = _legSpeed * remaining;
                if (step < distance)
                {
                    NeedleAngle += _direction * step;
                    return;
                }
                NeedleAngle = _legTarget;
                remaining -= _legSpeed > 0f ? distance / _legSpeed : remaining;
                _direction = -_direction;
                NewLeg();
            }
        }

        /// <summary>Chọn điểm quay đầu và tốc độ cho lượt chạy kế tiếp (theo hướng _direction).</summary>
        private void NewLeg()
        {
            float room = _direction > 0f ? BarMax - NeedleAngle : NeedleAngle - BarMin;
            float minLeg = Math.Min(Math.Max(1f, Settings.bossMinLeg), Math.Max(1f, room));
            bool toEdge = Rng.NextDouble() < Settings.bossEndBias || room <= minLeg;
            float length = toEdge ? room : minLeg + (float)Rng.NextDouble() * Math.Max(0f, room - minLeg);
            _legTarget = Math.Clamp(NeedleAngle + _direction * length, BarMin, BarMax);

            float lo = Math.Max(1f, Math.Min(Settings.bossSpeedMin, Settings.bossSpeedMax));
            float hi = Math.Max(lo, Math.Max(Settings.bossSpeedMin, Settings.bossSpeedMax));
            float speed = lo + (float)Rng.NextDouble() * (hi - lo);
            if (Rng.NextDouble() < Settings.bossBurstChance) speed *= BurstMultiplier;
            _legSpeed = speed * (1f + Math.Max(0f, Settings.bossSpeedGrowthPerHit) * Successes);
        }

        protected override void PlaceTargets()
        {
            Targets.Clear();
            // Vùng xanh hẹp dần theo số lần đã trúng (Settings là bản sao riêng của ván này nên sửa trực tiếp).
            float width = Math.Max(Math.Max(1f, Settings.bossMinWidth), _baseWidth - Math.Max(0f, Settings.bossWidthShrinkPerHit) * Successes);
            width = Math.Min(width, _baseWidth);
            Settings.targetAngleWidth = width;
            float half = width * 0.5f;
            float min = BarMin + half, max = BarMax - half;
            float center;
            if (Settings.randomizeTarget && min < max) center = min + (float)Rng.NextDouble() * (max - min);
            else center = Math.Clamp(Settings.targetAngle <= 0f ? 50f : Settings.targetAngle, min, Math.Max(min, max));
            Targets.Add(center);
        }

        // Thanh ngang không quay vòng nên so sánh thẳng.
        protected override bool IsInTarget(float position, float center)
            => Math.Abs(position - center) <= TargetWidth * 0.5f + 1e-4f;
    }
}
