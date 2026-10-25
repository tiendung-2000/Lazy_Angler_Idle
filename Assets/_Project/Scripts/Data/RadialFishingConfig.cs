using LazyAngler.Core;
using UnityEngine;

namespace LazyAngler.Data
{
    /// <summary>
    /// Preset độ khó cho minigame Radial (Dễ / Trung bình / Khó...). Gán vào FishData.radialConfig để nhiều loài
    /// dùng chung; để trống thì loài cá dùng cấu hình riêng FishData.minigame.
    /// Logic đánh giá (RadialFishingMode) chỉ đọc số liệu từ đây, không hardcode độ khó.
    /// </summary>
    [CreateAssetMenu(menuName = "LazyAngler/Radial Fishing Config", fileName = "Radial_")]
    public class RadialFishingConfig : ScriptableObject
    {
        public string difficultyName = "Trung bình";

        [Header("Kim")]
        [Tooltip("Độ/giây. Âm = quay ngược chiều kim đồng hồ.")]
        public float rotationSpeed = 180f;
        [Tooltip("Góc kim lúc bắt đầu (độ, 0 = 12 giờ, tăng theo chiều kim đồng hồ).")]
        public float startAngle = 0f;

        [Header("Vùng mục tiêu")]
        [Tooltip("Góc trung tâm vùng mục tiêu (bỏ qua khi randomizeTarget bật).")]
        public float targetAngle = 0f;
        [Range(1f, 180f)] public float targetAngleWidth = 35f;
        [Tooltip("Đặt vùng mục tiêu ngẫu nhiên (phía trước kim) lúc bắt đầu và sau mỗi lần trúng.")]
        public bool randomizeTarget = true;
        [Range(1, 4)] public int targetCount = 1;

        [Header("Thắng / thua")]
        [Min(1)] public int requiredSuccesses = 2;
        [Min(1)] public int maxAttempts = 5;

        /// <summary>Chuyển sang thông số runtime cho RadialFishingMode.</summary>
        public RareMinigameSettings ToSettings() => new RareMinigameSettings
        {
            type = MinigameType.Radial,
            rotationSpeed = rotationSpeed,
            startAngle = startAngle,
            targetAngle = targetAngle,
            targetAngleWidth = targetAngleWidth,
            randomizeTarget = randomizeTarget,
            targetCount = targetCount,
            requiredSuccesses = requiredSuccesses,
            maxAttempts = Mathf.Max(maxAttempts, requiredSuccesses),
        };
    }
}
