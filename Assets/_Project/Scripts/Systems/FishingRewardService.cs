using System;
using System.Collections.Generic;

namespace LazyAngler.Systems
{
    /// <summary>Một kết quả câu cá chờ trao thưởng. Mỗi vé chỉ được trao đúng một lần.</summary>
    public class CatchTicket
    {
        public long Id { get; }
        public CatchResult Result { get; }
        public bool Claimed { get; internal set; }

        public CatchTicket(long id, CatchResult result)
        {
            Id = id;
            Result = result;
        }
    }

    /// <summary>
    /// Trao thưởng cho kết quả câu cá: cộng xu ngay bằng giá của con cá (không có giỏ, không cần bán)
    /// và cập nhật Fish Collection (tổng số đã bắt theo loài).
    /// </summary>
    public class FishingRewardService
    {
        private readonly Dictionary<string, int> _collection;
        private readonly Action<long> _addCoins;

        public event Action<CatchResult> Rewarded;

        public FishingRewardService(Dictionary<string, int> collection, Action<long> addCoins)
        {
            _collection = collection;
            _addCoins = addCoins;
        }

        /// <summary>Trao thưởng cho vé. Trả về false nếu vé đã được nhận hoặc không hợp lệ.</summary>
        public bool Grant(CatchTicket ticket)
        {
            if (ticket == null || ticket.Claimed) return false;
            var r = ticket.Result;
            if (string.IsNullOrEmpty(r.fishId)) return false;
            ticket.Claimed = true;

            _addCoins(r.price);
            _collection.TryGetValue(r.fishId, out int c);
            _collection[r.fishId] = c + 1;
            Rewarded?.Invoke(r);
            return true;
        }
    }
}
