namespace LazyAngler.Systems
{
    /// <summary>Nhịp câu của một cần: Câu → Đợi cá → Cá cắn → Kéo cần (hoặc Câu hụt) → Câu lại.</summary>
    public enum RodState
    {
        /// <summary>Đang quăng câu (CastSeconds).</summary>
        Casting,
        /// <summary>Đang đợi cá: hết thời gian đợi tối thiểu thì kiểm tra cá cắn mỗi BiteCheckInterval.</summary>
        Waiting,
        /// <summary>Cá thường đã cắn ("!"), chờ giật cần (BiteReactSeconds) rồi xét câu hụt.</summary>
        Biting,
        /// <summary>Đã giật dính, đang kéo cần (EffectiveDuration).</summary>
        Reeling,
        /// <summary>Giật hụt, cá thoát; thu dây (MissRecoverSeconds) rồi câu lại.</summary>
        Missed,
        /// <summary>Cá hiếm đã cắn, chờ kết quả minigame. Cần câu đứng yên.</summary>
        AwaitingMinigame,
    }

    /// <summary>
    /// Trạng thái của một cần câu trong vòng câu tự động. Chỉ giữ dữ liệu (trạng thái, đồng hồ,
    /// vé bắt cá đang xử lý); luật chơi nằm trong AutoFishingController.
    /// </summary>
    public class FishingRod
    {
        public RodState State { get; internal set; } = RodState.Casting;
        /// <summary>Thời gian đã trôi trong trạng thái hiện tại (giây).</summary>
        public float Timer { get; internal set; }
        /// <summary>Kết quả của lượt câu đang diễn ra (null khi đang quăng/đợi).</summary>
        public CatchTicket Ticket { get; internal set; }
        /// <summary>Thời gian kéo cần của lượt hiện tại (chốt lúc giật dính).</summary>
        public float ReelDuration { get; internal set; }
        /// <summary>Thời gian đợi tối thiểu của lượt này (chốt ngẫu nhiên lúc bắt đầu đợi).</summary>
        public float WaitTarget { get; internal set; }

        /// <summary>Tiến độ kéo cá 0..1 (0 khi chưa kéo), dùng cho thanh tiến độ UI.</summary>
        public float Progress => State switch
        {
            RodState.Reeling => ReelDuration > 0 ? System.Math.Min(1f, Timer / ReelDuration) : 1f,
            RodState.AwaitingMinigame => 1f,
            _ => 0f,
        };

        public FishingRod(float startOffset = 0f) => Timer = startOffset;

        internal void Enter(RodState state, float carry = 0f)
        {
            State = state;
            Timer = carry;
        }

        internal void ResetToCasting(float carry = 0f)
        {
            Ticket = null;
            Enter(RodState.Casting, carry);
        }
    }
}
