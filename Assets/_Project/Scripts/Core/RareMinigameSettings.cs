using System;

namespace LazyAngler.Core
{
    public enum MinigameType { None, Radial, Pendulum, Boss }

    /// <summary>
    /// Thông số minigame cá hiếm, gắn vào từng FishData (chỉnh trong Inspector).
    /// Góc tính bằng độ, 0 = hướng 12 giờ, tăng theo chiều kim đồng hồ.
    /// </summary>
    [Serializable]
    public class RareMinigameSettings
    {
        public MinigameType type = MinigameType.None;

        [UnityEngine.Header("Chung")]
        public int requiredSuccesses = 3;
        public int maxAttempts = 5;
        [UnityEngine.Tooltip("Độ rộng vùng mục tiêu (độ).")]
        public float targetAngleWidth = 40f;
        [UnityEngine.Tooltip("Góc trung tâm vùng mục tiêu. Bỏ qua nếu randomizeTarget bật.")]
        public float targetAngle = 0f;
        [UnityEngine.Tooltip("Đặt vùng mục tiêu ngẫu nhiên lúc bắt đầu và sau mỗi lần trúng.")]
        public bool randomizeTarget = true;

        [UnityEngine.Header("Radial")]
        [UnityEngine.Tooltip("Tốc độ quay của kim (độ/giây). Âm = ngược chiều kim đồng hồ.")]
        public float rotationSpeed = 180f;
        [UnityEngine.Min(1)] public int targetCount = 1;
        [UnityEngine.Tooltip("Góc kim lúc bắt đầu (độ).")]
        public float startAngle = 0f;

        [UnityEngine.Header("Pendulum")]
        public float minAngle = -70f;
        public float maxAngle = 70f;
        [UnityEngine.Tooltip("Số chu kỳ qua lại mỗi giây.")]
        public float oscillationSpeed = 0.6f;

        // Boss: con lăn chạy qua lại trên thanh ngang 0–100. targetAngleWidth là độ rộng vùng xanh (tính theo % thanh),
        // requiredSuccesses/maxAttempts như các chế độ khác, randomizeTarget nên bật để vùng xanh dời sau mỗi lần trúng.
        [UnityEngine.Header("Boss (thanh ngang)")]
        [UnityEngine.Tooltip("Tốc độ con lăn thấp nhất / cao nhất (% thanh mỗi giây). Mỗi lượt chạy chọn ngẫu nhiên trong khoảng này.")]
        public float bossSpeedMin = 55f;
        public float bossSpeedMax = 85f;
        [UnityEngine.Tooltip("Mỗi lượt chạy tối thiểu dài bấy nhiêu % thanh trước khi được quay đầu.")]
        public float bossMinLeg = 25f;
        [UnityEngine.Tooltip("Xác suất lượt chạy đi hết tới mép thanh (0–1). Còn lại quay đầu giữa chừng ở điểm ngẫu nhiên.")]
        [UnityEngine.Range(0f, 1f)] public float bossEndBias = 0.4f;
        [UnityEngine.Tooltip("Xác suất một lượt chạy tăng tốc đột ngột (×1,6).")]
        [UnityEngine.Range(0f, 1f)] public float bossBurstChance = 0.2f;
        [UnityEngine.Tooltip("Mỗi lần trúng, tốc độ tăng thêm tỉ lệ này (0,12 = +12%). Làm lượt sau khó hơn.")]
        public float bossSpeedGrowthPerHit = 0.12f;
        [UnityEngine.Tooltip("Mỗi lần trúng, vùng xanh hẹp đi bấy nhiêu % thanh.")]
        public float bossWidthShrinkPerHit = 1.5f;
        [UnityEngine.Tooltip("Vùng xanh không hẹp hơn mức này (% thanh).")]
        public float bossMinWidth = 5f;

        public RareMinigameSettings Clone() => (RareMinigameSettings)MemberwiseClone();
    }

    public enum MinigameStatus { Idle, Running, Won, Lost, Cancelled }

    public struct MinigameResult
    {
        public MinigameStatus status;
        public int successes;
        public int attempts;
        public bool Success => status == MinigameStatus.Won;
    }
}
