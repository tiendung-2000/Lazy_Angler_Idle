using System;
using System.Collections.Generic;

namespace LazyAngler.Systems
{
    /// <summary>
    /// Vòng câu tự động (không UI, không MonoBehaviour, được GameManager tick):
    /// Casting (quăng) → Waiting (đợi tối thiểu theo Bite Speed, rồi kiểm tra theo Bite Chance)
    /// → Biting (cá cắn) → giật cần: Reeling (kéo theo Fishing Speed → trao thưởng) hoặc Missed (câu hụt)
    /// → Casting.
    /// Cá hiếm: cần câu chuyển sang AwaitingMinigame (không xét câu hụt, minigame quyết định),
    /// toàn bộ vòng câu tạm dừng tới khi CompleteRare/CancelRare được gọi.
    /// </summary>
    public class AutoFishingController
    {
        /// <summary>Giới hạn số bước xử lý mỗi tick (chống treo khi dt rất lớn).</summary>
        public const int MaxStepsPerTick = 64;

        private readonly FishingStatProvider _stats;
        private FishSelectionService _selection;
        private readonly FishingRewardService _rewards;
        private readonly Func<int> _luckLevel;
        private readonly Func<bool> _rareAllowed;
        private readonly Func<bool> _rewardNormalOnRareFail;
        private readonly Random _rng;
        private readonly List<FishingRod> _rods = new List<FishingRod>();
        private FishingRod _pendingRod;
        private long _ticketSeq;

        /// <summary>Cá hiếm đã cắn câu, cần mở minigame. Handler có thể gọi CancelRare ngay nếu không mở được.</summary>
        public event Action<CatchTicket> RareFishHooked;
        /// <summary>Giật cần hụt: cá thoát, không thưởng.</summary>
        public event Action<CatchTicket> FishMissed;

        public IReadOnlyList<FishingRod> Rods => _rods;
        public CatchTicket PendingRare { get; private set; }
        public bool IsPaused => PendingRare != null;

        public AutoFishingController(FishingStatProvider stats, FishSelectionService selection,
            FishingRewardService rewards, Func<int> luckLevel, Func<bool> rareAllowed,
            Func<bool> rewardNormalOnRareFail = null, int? seed = null)
        {
            _stats = stats;
            _selection = selection;
            _rewards = rewards;
            _luckLevel = luckLevel;
            _rareAllowed = rareAllowed;
            _rewardNormalOnRareFail = rewardNormalOnRareFail ?? (() => false);
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>Đổi bể cá (đổi bản đồ). Lượt đang dở giữ kết quả đã chọn; gọi SetRodCount để bắt đầu lại.</summary>
        public void SetSelection(FishSelectionService selection) => _selection = selection;

        /// <summary>
        /// Thêm cần cho đủ số lượng (nâng cấp Thuyền) mà không đụng tới các cần đang câu dở hay cá hiếm đang chờ.
        /// Cần mới lệch pha ngẫu nhiên để không quăng cùng lúc với cần cũ.
        /// </summary>
        public void EnsureRodCount(int count)
        {
            float interval = _stats.BiteCheckInterval;
            while (_rods.Count < count) _rods.Add(new FishingRod((float)_rng.NextDouble() * interval));
        }

        /// <summary>Đặt số cần câu song song. Hủy cá hiếm đang chờ (nếu có) để tránh vé mồ côi.</summary>
        public void SetRodCount(int count)
        {
            CancelRare();
            _rods.Clear();
            float interval = _stats.BiteCheckInterval;
            for (int i = 0; i < count; i++)
            {
                // Lệch pha để các cần không quăng cùng lúc.
                _rods.Add(new FishingRod(interval * i / Math.Max(1, count)));
            }
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || IsPaused) return;

            foreach (var rod in _rods)
            {
                rod.Timer += dt;
                for (int step = 0; step < MaxStepsPerTick; step++)
                {
                    if (!Advance(rod)) break;
                    if (IsPaused) return; // cá hiếm: dừng toàn bộ vòng câu
                }
            }
        }

        /// <summary>Chuyển trạng thái nếu đủ thời gian. Trả về false khi cần câu phải đợi thêm.</summary>
        private bool Advance(FishingRod rod)
        {
            switch (rod.State)
            {
                case RodState.Casting:
                {
                    float cast = _stats.CastSeconds;
                    if (rod.Timer < cast) return false;
                    StartWaiting(rod, rod.Timer - cast);
                    return true;
                }

                case RodState.Waiting:
                {
                    // Đợi tối thiểu (Bite Speed), sau đó mỗi interval kiểm tra một lần (Bite Chance).
                    float interval = _stats.BiteCheckInterval;
                    float due = rod.WaitTarget + interval;
                    if (rod.Timer < due) return false;
                    rod.Timer -= interval;
                    if (_rng.NextDouble() >= _stats.BiteChance) return true; // chưa cắn, đợi lần sau
                    OnBite(rod, rod.Timer - rod.WaitTarget);
                    return true;
                }

                case RodState.Biting:
                {
                    // Giật cần: dính thì kéo, hụt thì cá thoát.
                    float react = _stats.BiteReactSeconds;
                    if (rod.Timer < react) return false;
                    float carry = rod.Timer - react;
                    if (_rng.NextDouble() < _stats.MissChance)
                    {
                        rod.Enter(RodState.Missed, carry);
                        FishMissed?.Invoke(rod.Ticket);
                    }
                    else
                    {
                        rod.ReelDuration = _stats.EffectiveDuration;
                        rod.Enter(RodState.Reeling, carry);
                    }
                    return true;
                }

                case RodState.Reeling:
                {
                    if (rod.Timer < rod.ReelDuration) return false;
                    var ticket = rod.Ticket;
                    rod.ResetToCasting(rod.Timer - rod.ReelDuration);
                    _rewards.Grant(ticket);
                    return true;
                }

                case RodState.Missed:
                {
                    float recover = _stats.MissRecoverSeconds;
                    if (rod.Timer < recover) return false;
                    rod.ResetToCasting(rod.Timer - recover);
                    return true;
                }

                default:
                    return false;
            }
        }

        private void StartWaiting(FishingRod rod, float carry)
        {
            float r = _stats.BiteWaitRandomness;
            float jitter = 1f + (float)(_rng.NextDouble() * 2.0 - 1.0) * r;
            rod.WaitTarget = Math.Max(0f, _stats.MinBiteWaitSeconds * jitter);
            rod.Enter(RodState.Waiting, carry);
        }

        /// <summary>Kết thúc minigame: thắng thì trao cá hiếm, thua thì (tùy cấu hình) trao cá thường. Luôn mở lại vòng câu.</summary>
        public bool CompleteRare(bool success)
        {
            var ticket = PendingRare;
            if (ticket == null) return false;
            Release();

            if (success)
            {
                _rewards.Grant(ticket);
            }
            else if (_rewardNormalOnRareFail())
            {
                var consolation = new CatchTicket(++_ticketSeq, _selection.Select(_luckLevel(), allowRare: false));
                _rewards.Grant(consolation);
            }
            return true;
        }

        /// <summary>Hủy cá hiếm đang chờ, không thưởng (đóng app, thiếu UI...). An toàn khi gọi nhiều lần.</summary>
        public void CancelRare()
        {
            if (PendingRare != null) Release();
        }

        private void OnBite(FishingRod rod, float carry)
        {
            var ticket = NewTicket();
            if (ticket.Result.IsRare)
            {
                BeginRare(rod, ticket);
                return;
            }
            rod.Ticket = ticket;
            rod.Enter(RodState.Biting, Math.Max(0f, carry));
        }

        private CatchTicket NewTicket()
            => new CatchTicket(++_ticketSeq, _selection.Select(_luckLevel(), _rareAllowed()));

        private void BeginRare(FishingRod rod, CatchTicket ticket)
        {
            if (rod != null)
            {
                rod.Ticket = ticket;
                rod.Enter(RodState.AwaitingMinigame);
            }
            _pendingRod = rod;
            PendingRare = ticket;
            RareFishHooked?.Invoke(ticket);
        }

        private void Release()
        {
            _pendingRod?.ResetToCasting();
            _pendingRod = null;
            PendingRare = null;
        }
    }
}
