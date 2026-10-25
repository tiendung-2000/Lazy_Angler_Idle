using System;

namespace LazyAngler.Visual
{
    public enum FishermanState { FishingIdle, Casting, FishBite, HookPull, Reeling, CatchSuccess, CatchFailed }

    /// <summary>Tư thế tại một thời điểm: khung sprite thân, độ lệch góc cần (độ, + là nâng cần) và độ cong cần (độ).</summary>
    public struct FishermanPose
    {
        public int frame;
        public float rodAngleOffset;
        public float rodBend;
    }

    /// <summary>
    /// Máy trạng thái animation của ngư dân (thuần C#, có test). Chỉ nhận lệnh từ gameplay qua các hàm
    /// FishBite / HookStarted / CatchSucceeded / CatchFailed và tự quay về FishingIdle.
    /// Không quyết định kết quả câu, không trao thưởng.
    ///
    /// FishingIdle → (Casting) → FishBite → HookPull → Reeling → CatchSuccess → FishingIdle
    ///                                         └──────────→ CatchFailed  → FishingIdle
    /// </summary>
    public class FishermanPoseMachine
    {
        public const int FrameAlert = 4, FramePull = 5, FrameReel = 6, FrameSlump = 7;
        private static readonly int[] IdleCycle = { 0, 1, 0, 2, 0, 1, 0, 3 };

        public float hookPullSeconds = 0.25f;
        public float catchSeconds = 0.55f;
        public float failSeconds = 0.9f;
        public float idleFps = 2.5f;
        public float reelFps = 6f;

        public FishermanState State { get; private set; } = FishermanState.FishingIdle;
        public float StateTime { get; private set; }
        public float TotalTime { get; private set; }
        /// <summary>Độ căng dây 0..1 khi đang thu dây (cần cong theo).</summary>
        public float ReelTension { get; set; }

        public event Action<FishermanState> StateEntered;
        /// <summary>Hoạt cảnh kết thúc (CatchSuccess hoặc CatchFailed) và đã về FishingIdle. Phát đúng một lần mỗi lượt.</summary>
        public event Action<FishermanState> AnimationCompleted;

        public bool BeginCast()
        {
            if (State != FishermanState.FishingIdle) return false;
            Enter(FishermanState.Casting);
            return true;
        }

        public void EndCast()
        {
            if (State == FishermanState.Casting) Enter(FishermanState.FishingIdle);
        }

        /// <summary>Cá cắn câu: chỉ hợp lệ khi đang chờ (hoặc vừa quăng xong).</summary>
        public bool FishBite()
        {
            if (State != FishermanState.FishingIdle && State != FishermanState.Casting) return false;
            Enter(FishermanState.FishBite);
            return true;
        }

        /// <summary>Giật cần: chỉ khi cá đã cắn. Tự chuyển sang Reeling sau hookPullSeconds.</summary>
        public bool HookStarted()
        {
            if (State != FishermanState.FishBite) return false;
            Enter(FishermanState.HookPull);
            return true;
        }

        public bool CatchSucceeded()
        {
            if (!IsFighting) return false;
            Enter(FishermanState.CatchSuccess);
            return true;
        }

        public bool CatchFailed()
        {
            if (!IsFighting) return false;
            Enter(FishermanState.CatchFailed);
            return true;
        }

        /// <summary>Về tư thế chờ ngay, không phát AnimationCompleted (dùng khi bị hủy).</summary>
        public void ResetToIdle()
        {
            if (State != FishermanState.FishingIdle) Enter(FishermanState.FishingIdle);
        }

        public bool IsFighting => State == FishermanState.FishBite || State == FishermanState.HookPull
                                  || State == FishermanState.Reeling;

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            TotalTime += dt;
            StateTime += dt;
            switch (State)
            {
                case FishermanState.HookPull when StateTime >= hookPullSeconds:
                    Enter(FishermanState.Reeling);
                    break;
                case FishermanState.CatchSuccess when StateTime >= catchSeconds:
                case FishermanState.CatchFailed when StateTime >= failSeconds:
                    var finished = State;
                    Enter(FishermanState.FishingIdle);
                    AnimationCompleted?.Invoke(finished);
                    break;
            }
        }

        public FishermanPose Evaluate()
        {
            float t = StateTime;
            float u;
            switch (State)
            {
                case FishermanState.Casting:
                    return new FishermanPose { frame = t < 0.3f ? FramePull : 0 };

                case FishermanState.FishBite:
                    return new FishermanPose
                    {
                        frame = FrameAlert,
                        rodAngleOffset = (float)Math.Sin(t * 45f) * 1.5f,
                        rodBend = 9f + Math.Abs((float)Math.Sin(t * 28f)) * 6f,
                    };

                case FishermanState.HookPull:
                    u = Clamp01(t / Math.Max(0.01f, hookPullSeconds));
                    float e = 1f - (1f - u) * (1f - u);
                    return new FishermanPose { frame = FramePull, rodAngleOffset = 20f * e, rodBend = 32f * e };

                case FishermanState.Reeling:
                    return new FishermanPose
                    {
                        frame = (int)(t * reelFps) % 2 == 0 ? FramePull : FrameReel,
                        rodAngleOffset = 14f + (float)Math.Sin(t * 9f) * 2f,
                        rodBend = 14f + 26f * Clamp01(ReelTension),
                    };

                case FishermanState.CatchSuccess:
                    u = Clamp01(t / Math.Max(0.01f, catchSeconds));
                    return new FishermanPose { frame = FramePull, rodAngleOffset = 26f * (1f - u * u), rodBend = 18f * (1f - u) };

                case FishermanState.CatchFailed:
                    u = Clamp01(t / Math.Max(0.01f, failSeconds));
                    return new FishermanPose { frame = FrameSlump, rodAngleOffset = -10f * (1f - u) };

                default:
                    float tt = TotalTime;
                    return new FishermanPose
                    {
                        frame = IdleCycle[(int)(tt * idleFps) % IdleCycle.Length],
                        rodAngleOffset = (float)Math.Sin(tt * 1.3f) * 1.5f,
                        rodBend = 3f + (float)Math.Sin(tt * 1.9f) * 1.2f,
                    };
            }
        }

        private void Enter(FishermanState s)
        {
            State = s;
            StateTime = 0f;
            StateEntered?.Invoke(s);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
