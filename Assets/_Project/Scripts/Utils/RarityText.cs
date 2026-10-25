using LazyAngler.Data;

namespace LazyAngler.Utils
{
    /// <summary>Tên độ hiếm hiển thị cho người chơi.</summary>
    public static class RarityText
    {
        public static string Name(Rarity r) => r switch
        {
            Rarity.Common => "Thường",
            Rarity.Uncommon => "Khá",
            Rarity.Rare => "Hiếm",
            Rarity.Epic => "Sử thi",
            Rarity.Legendary => "Huyền thoại",
            _ => r.ToString(),
        };
    }
}
