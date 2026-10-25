using UnityEngine;

namespace LazyAngler.Data
{
    /// <summary>
    /// Mồi dùng một lần, riêng cho từng map. Mua bằng xu, dùng thì có hiệu lực trong durationSeconds giây
    /// (chỉ đếm khi đang câu ở đúng map của mồi). Không cộng dồn: mỗi lúc chỉ một mồi đang dùng.
    /// Khác với nâng cấp "Mồi câu" (cấp vĩnh viễn): đây là vật phẩm tiêu hao.
    /// </summary>
    [CreateAssetMenu(menuName = "LazyAngler/Bait", fileName = "Bait_")]
    public class BaitData : ScriptableObject
    {
        [Tooltip("Mã lưu trong save. Không đổi sau khi phát hành.")]
        public string baitId;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Chỉ số map (GameConfig.zones) mà mồi này thuộc về và có tác dụng.")]
        [Min(0)] public int zoneIndex;
        [Min(1)] public long price = 100;
        [Tooltip("Thời gian hiệu lực (giây câu thật ở đúng map).")]
        [Min(10f)] public float durationSeconds = 300f;

        [Header("Hiệu quả")]
        [Tooltip("Cộng thêm vào tỉ lệ cá hiếm (0,1 = +10 điểm %), tính cả sau trần May mắn.")]
        [Range(0f, 1f)] public float rareBonus;
        [Tooltip("Cộng thêm vào xác suất cá biến thể (0,05 = +5 điểm %).")]
        [Range(0f, 1f)] public float variantBonus;
    }
}
