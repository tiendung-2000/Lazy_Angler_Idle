using System;
using System.Collections.Generic;
using LazyAngler.Core;

namespace LazyAngler.Systems
{
    /// <summary>Giao diện chung cho các chế độ minigame cá hiếm (logic thuần, không UI).</summary>
    public interface IRareFishingMinigame
    {
        MinigameType Type { get; }
        MinigameStatus Status { get; }
        RareMinigameSettings Settings { get; }
        int Successes { get; }
        int Attempts { get; }
        int RequiredSuccesses { get; }
        int MaxAttempts { get; }
        /// <summary>Góc kim hiện tại (độ, 0 = 12 giờ, tăng theo chiều kim đồng hồ).</summary>
        float NeedleAngle { get; }
        /// <summary>Tâm các vùng mục tiêu (độ).</summary>
        IReadOnlyList<float> TargetAngles { get; }
        float TargetWidth { get; }

        /// <summary>Mỗi lần nhận input: true = trúng, false = trượt.</summary>
        event Action<bool> InputEvaluated;
        /// <summary>Kết thúc (thắng, thua hoặc bị dừng). Chỉ phát một lần mỗi ván.</summary>
        event Action<MinigameResult> OnMinigameCompleted;

        void StartMinigame(RareMinigameSettings settings, int? seed = null);
        /// <summary>Di chuyển kim theo thời gian thực (deltaTime, không phụ thuộc FPS).</summary>
        void Tick(float deltaTime);
        /// <summary>Xác nhận vị trí kim hiện tại. Trả về true nếu trúng. Bỏ qua khi không chạy.</summary>
        bool SubmitInput();
        void StopMinigame();
    }

    /// <summary>Phần dùng chung: đếm lượt, đánh giá thắng/thua, phát sự kiện.</summary>
    public abstract class RareFishingModeBase : IRareFishingMinigame
    {
        protected readonly List<float> Targets = new List<float>();
        protected Random Rng = new Random();

        public abstract MinigameType Type { get; }
        public MinigameStatus Status { get; private set; } = MinigameStatus.Idle;
        public RareMinigameSettings Settings { get; private set; } = new RareMinigameSettings();
        public int Successes { get; private set; }
        public int Attempts { get; private set; }
        public float NeedleAngle { get; protected set; }
        public IReadOnlyList<float> TargetAngles => Targets;

        public int RequiredSuccesses => Math.Max(1, Settings.requiredSuccesses);
        public int MaxAttempts => Math.Max(RequiredSuccesses, Settings.maxAttempts);
        public float TargetWidth => Math.Clamp(Settings.targetAngleWidth, 1f, 360f);

        public event Action<bool> InputEvaluated;
        public event Action<MinigameResult> OnMinigameCompleted;

        public void StartMinigame(RareMinigameSettings settings, int? seed = null)
        {
            Settings = settings != null ? settings.Clone() : new RareMinigameSettings();
            Rng = seed.HasValue ? new Random(seed.Value) : new Random();
            Successes = 0;
            Attempts = 0;
            Targets.Clear();
            Status = MinigameStatus.Running;
            OnStart();
            PlaceTargets();
        }

        public void Tick(float deltaTime)
        {
            if (Status != MinigameStatus.Running || deltaTime <= 0f) return;
            Advance(deltaTime);
        }

        public bool SubmitInput()
        {
            if (Status != MinigameStatus.Running) return false;

            bool hit = IsInAnyTarget(NeedleAngle);
            Attempts++;
            if (hit) Successes++;
            InputEvaluated?.Invoke(hit);

            if (Successes >= RequiredSuccesses) Finish(MinigameStatus.Won);
            else if (Successes + (MaxAttempts - Attempts) < RequiredSuccesses) Finish(MinigameStatus.Lost);
            else if (hit && Settings.randomizeTarget) PlaceTargets();
            return hit;
        }

        public void StopMinigame()
        {
            if (Status == MinigameStatus.Running) Finish(MinigameStatus.Cancelled);
        }

        public bool IsInAnyTarget(float angle)
        {
            foreach (float t in Targets)
                if (IsInTarget(angle, t)) return true;
            return false;
        }

        protected abstract void OnStart();
        protected abstract void Advance(float deltaTime);
        protected abstract void PlaceTargets();
        protected virtual bool IsInTarget(float angle, float center) => AngleMath.IsWithinArc(angle, center, TargetWidth);

        private void Finish(MinigameStatus status)
        {
            Status = status;
            OnMinigameCompleted?.Invoke(new MinigameResult { status = status, successes = Successes, attempts = Attempts });
        }
    }
}
