using System;

namespace LazyAngler.Utils
{
    /// <summary>Định dạng số và thời gian cho UI.</summary>
    public static class Fmt
    {
        private static readonly string[] Suffixes = { "", "K", "M", "B", "T" };

        /// <summary>1234 → "1.23K", 999 → "999".</summary>
        public static string Number(double value)
        {
            if (value < 1000) return Math.Floor(value).ToString("0");
            int i = 0;
            while (value >= 1000 && i < Suffixes.Length - 1)
            {
                value /= 1000;
                i++;
            }
            return value.ToString(value < 10 ? "0.##" : value < 100 ? "0.#" : "0") + Suffixes[i];
        }

        /// <summary>3725 giây → "1g 2p".</summary>
        public static string Duration(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}g {t.Minutes}p";
            if (t.TotalMinutes >= 1) return $"{t.Minutes}p {t.Seconds}s";
            return $"{t.Seconds}s";
        }
    }
}
