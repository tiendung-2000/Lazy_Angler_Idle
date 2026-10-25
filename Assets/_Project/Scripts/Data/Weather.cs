using System;

namespace LazyAngler.Data
{
    /// <summary>Điều kiện để một loài cá xuất hiện. Asset lưu theo chỉ số nên chỉ thêm vào cuối.</summary>
    public enum FishCondition { Always, Sunset, Night, Rain }

    /// <summary>Thời tiết/giờ đang diễn ra. Sunset và Night loại trừ nhau; Rain có thể xảy ra cùng lúc với bất kỳ giờ nào.</summary>
    [Flags]
    public enum WeatherFlags { None = 0, Sunset = 1, Night = 2, Rain = 4 }

    public static class WeatherUtil
    {
        public static WeatherFlags FlagOf(FishCondition c) => c switch
        {
            FishCondition.Sunset => WeatherFlags.Sunset,
            FishCondition.Night => WeatherFlags.Night,
            FishCondition.Rain => WeatherFlags.Rain,
            _ => WeatherFlags.None,
        };

        /// <summary>Cá xuất hiện được khi điều kiện của nó đang diễn ra (Always thì luôn được).</summary>
        public static bool IsActive(FishCondition c, WeatherFlags weather)
            => c == FishCondition.Always || (weather & FlagOf(c)) != 0;

        public static string Name(FishCondition c) => c switch
        {
            FishCondition.Sunset => "hoàng hôn",
            FishCondition.Night => "ban đêm",
            FishCondition.Rain => "khi trời mưa",
            _ => "",
        };

        /// <summary>Tên hiển thị của trạng thái hiện tại, vd. "Đêm trăng · Mưa" hoặc "Ban ngày".</summary>
        public static string Describe(WeatherFlags w)
        {
            string time = (w & WeatherFlags.Night) != 0 ? "Đêm trăng" : (w & WeatherFlags.Sunset) != 0 ? "Hoàng hôn" : "Ban ngày";
            return (w & WeatherFlags.Rain) != 0 ? time + " · Mưa" : time;
        }
    }
}
