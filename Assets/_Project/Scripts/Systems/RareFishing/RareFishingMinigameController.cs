using System;
using System.Collections;
using LazyAngler.Core;
using LazyAngler.Data;
using UnityEngine;

namespace LazyAngler.Systems
{
    /// <summary>
    /// Vòng đời minigame cá hiếm: chọn chế độ theo FishData, chạy kim bằng coroutine (deltaTime),
    /// nhận input (có chống lặp), báo kết quả đúng một lần. Không cho chạy hai minigame cùng lúc.
    /// Không xử lý UI: view lắng nghe Started/InputEvaluated/Completed.
    /// </summary>
    public class RareFishingMinigameController : MonoBehaviour
    {
        [Tooltip("Bỏ qua input đến sau input trước trong khoảng này (giây), chống chạm đúp.")]
        [SerializeField] private float inputCooldown = 0.15f;

        private readonly RadialFishingMode _radial = new RadialFishingMode();
        private readonly PendulumFishingMode _pendulum = new PendulumFishingMode();
        private readonly BossBarFishingMode _boss = new BossBarFishingMode();
        private IRareFishingMinigame _current;
        private Action<MinigameResult> _onComplete;
        private Coroutine _loop;
        private float _nextInputTime;
        private int _lastInputFrame = -1;
        private float _lastTickTime;

        public bool IsRunning => _current != null && _current.Status == MinigameStatus.Running;
        public IRareFishingMinigame Current => _current;
        public FishData CurrentFish { get; private set; }

        public event Action<IRareFishingMinigame, FishData> Started;
        public event Action<bool> InputEvaluated;
        public event Action<MinigameResult, FishData> Completed;

        /// <summary>
        /// Mở minigame cho cá hiếm. Trả về false (và không gọi onComplete) nếu đang có minigame khác,
        /// component bị tắt, hoặc dữ liệu cá không có minigame.
        /// </summary>
        public bool TryStart(FishData fish, Action<MinigameResult> onComplete)
        {
            if (IsRunning || !enabled || !gameObject.activeInHierarchy || fish == null || !fish.IsRareMinigameFish) return false;

            var settings = fish.EffectiveMinigame;
            _current = settings.type == MinigameType.Boss ? _boss
                : settings.type == MinigameType.Pendulum ? _pendulum
                : (IRareFishingMinigame)_radial;
            CurrentFish = fish;
            _onComplete = onComplete;
            _nextInputTime = 0f;
            _lastInputFrame = -1;

            _current.InputEvaluated += HandleInputEvaluated;
            _current.OnMinigameCompleted += HandleCompleted;
            _current.StartMinigame(settings);
            _lastTickTime = Time.time;
            _loop = StartCoroutine(RunLoop());
            Started?.Invoke(_current, fish);
            return true;
        }

        /// <summary>Input từ người chơi (chạm, click, phím). Mỗi frame và mỗi cooldown chỉ nhận một lần.</summary>
        public void SubmitInput()
        {
            if (!IsRunning) return;
            if (Time.frameCount == _lastInputFrame || Time.unscaledTime < _nextInputTime) return;
            _lastInputFrame = Time.frameCount;
            _nextInputTime = Time.unscaledTime + inputCooldown;
            // Đưa kim tới đúng thời điểm chạm rồi mới đánh giá (không dùng góc của frame trước).
            AdvanceToNow();
            _current.SubmitInput();
        }

        /// <summary>Người chơi đã đóng màn hình kết quả (hoặc hết thời gian chờ). Dùng để diễn tiếp cảnh câu.</summary>
        public void NotifyResultDismissed() => ResultDismissed?.Invoke();

        public event Action ResultDismissed;

        private void AdvanceToNow()
        {
            float now = Time.time;
            float dt = now - _lastTickTime;
            _lastTickTime = now;
            if (dt > 0f) _current.Tick(dt);
        }

        /// <summary>Dừng minigame đang chạy (đóng panel, app tạm dừng...). Kết quả Cancelled, không thưởng.</summary>
        public void Stop()
        {
            if (IsRunning) _current.StopMinigame();
        }

        private void OnDisable() => Stop();

        private IEnumerator RunLoop()
        {
            // Kim chạy theo thời gian thực tích lũy (không phụ thuộc FPS); input có thể đã tick trước trong frame.
            while (_current != null && _current.Status == MinigameStatus.Running)
            {
                AdvanceToNow();
                yield return null;
            }
        }

        private void HandleInputEvaluated(bool hit) => InputEvaluated?.Invoke(hit);

        private void HandleCompleted(MinigameResult result)
        {
            // Gỡ toàn bộ đăng ký trước khi báo kết quả, để handler có thể mở minigame mới ngay.
            var mode = _current;
            var fish = CurrentFish;
            var callback = _onComplete;
            mode.InputEvaluated -= HandleInputEvaluated;
            mode.OnMinigameCompleted -= HandleCompleted;
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            _onComplete = null;

            callback?.Invoke(result);
            Completed?.Invoke(result, fish);
        }
    }
}
