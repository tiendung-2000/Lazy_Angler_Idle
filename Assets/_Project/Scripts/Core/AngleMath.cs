using System;

namespace LazyAngler.Core
{
    /// <summary>Phép tính góc (độ) dùng cho minigame, xử lý đúng mốc 0/360.</summary>
    public static class AngleMath
    {
        /// <summary>Chuẩn hóa về [0, 360).</summary>
        public static float Normalize360(float deg)
        {
            float r = deg % 360f;
            if (r < 0f) r += 360f;
            return r >= 360f ? 0f : r;
        }

        /// <summary>Hiệu góc ngắn nhất từ a tới b, trong (-180, 180].</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = Normalize360(b - a);
            return d > 180f ? d - 360f : d;
        }

        /// <summary>
        /// Góc nằm trong cung có tâm center, độ rộng width (gồm cả hai mép).
        /// Đúng cả khi cung đi qua mốc 0/360 (vd. tâm 350°, rộng 40° phủ 330°..10°).
        /// </summary>
        public static bool IsWithinArc(float angle, float center, float width)
        {
            if (width >= 360f) return true;
            if (width <= 0f) return false;
            return Math.Abs(DeltaAngle(center, angle)) <= width * 0.5f + 1e-4f;
        }
    }
}
